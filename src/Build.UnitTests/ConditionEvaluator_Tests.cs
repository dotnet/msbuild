// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using FakeItEasy;
using Microsoft.Build.BackEnd.Components.Logging;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Collections;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Exceptions;
using Microsoft.Build.Execution;
using Microsoft.Build.FileSystem;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared.FileSystem;
using Microsoft.Build.UnitTests.BackEnd;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests;

public class ConditionEvaluator_Tests
{
    private readonly ITestOutputHelper _output;

    public ConditionEvaluator_Tests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task SameConditionEvaluatesConcurrentlyWithoutRepeatingParserWarnings()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        using ManualResetEventSlim firstEntered = new();
        using ManualResetEventSlim releaseFirst = new();
        string firstPath = Path.Combine(env.DefaultTestDirectory.Path, "first.file");
        string secondPath = Path.Combine(env.DefaultTestDirectory.Path, "second.file");
        string identity = Guid.NewGuid().ToString("N");
        string condition = $"Exists('$(File)') And '$(Value)' == '{identity}' Or '{identity}' == 'never'";
        MSBuildFileSystemBase fileSystem = A.Fake<MSBuildFileSystemBase>();
        A.CallTo(() => fileSystem.FileOrDirectoryExists(A<string>._)).ReturnsLazily((string path) =>
        {
            if (path == firstPath)
            {
                firstEntered.Set();
                releaseFirst.Wait(TimeSpan.FromSeconds(30)).ShouldBeTrue();
                return true;
            }

            path.ShouldBe(secondPath);
            return false;
        });

        int warnings = 0;
        MockLoggingService loggingService = new(message =>
        {
            if (message == "ConditionMaybeEvaluatedIncorrectly")
            {
                Interlocked.Increment(ref warnings);
            }

            _output.WriteLine(message);
        });
        EvaluationLoggingContext loggingContext = new(
            loggingService, BuildEventContext.Invalid, firstPath);
        Task<bool> first = StartEvaluation(
            condition, fileSystem, env.DefaultTestDirectory.Path, loggingContext,
            ("File", firstPath), ("Value", identity));
        Task<bool>? second = null;

        try
        {
            firstEntered.Wait(TimeSpan.FromSeconds(10)).ShouldBeTrue();
            second = StartEvaluation(
                condition, fileSystem, env.DefaultTestDirectory.Path, loggingContext,
                ("File", secondPath), ("Value", identity));

            (await Task.WhenAny(second, Task.Delay(TimeSpan.FromSeconds(10)))).ShouldBeSameAs(second);
            (await second).ShouldBeFalse();
            first.IsCompleted.ShouldBeFalse();
        }
        finally
        {
            releaseFirst.Set();
            (await first).ShouldBeTrue();
            if (second is not null)
            {
                await second;
            }
        }

        warnings.ShouldBe(1);
        Evaluate(
            condition, fileSystem, env.DefaultTestDirectory.Path, loggingContext,
            ("File", secondPath), ("Value", identity)).ShouldBeFalse();
        warnings.ShouldBe(1);
    }

    [Fact]
    public void CachedExpressionIsResetAfterEvaluationThrows()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string identity = Guid.NewGuid().ToString("N");
        string condition = $"'$(Value)' > '1' And '{identity}' == '{identity}'";

        Evaluate(condition, FileSystems.Default, env.DefaultTestDirectory.Path, null,
            ("Value", "2")).ShouldBeTrue();
        Should.Throw<InvalidProjectFileException>(() =>
            Evaluate(condition, FileSystems.Default, env.DefaultTestDirectory.Path, null,
                ("Value", "not-a-number")));
        Evaluate(condition, FileSystems.Default, env.DefaultTestDirectory.Path, null,
            ("Value", "0")).ShouldBeFalse();
        Evaluate(condition, FileSystems.Default, env.DefaultTestDirectory.Path, null,
            ("Value", "3")).ShouldBeTrue();
    }

    [Fact]
    public async Task CheckedOutExpressionCanReturnAfterCacheEviction()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        using ManualResetEventSlim firstEntered = new();
        using ManualResetEventSlim releaseFirst = new();
        string identity = Guid.NewGuid().ToString("N");
        string condition = $"Exists('$(File)') And '{identity}' == '{identity}'";
        MSBuildFileSystemBase blockedFileSystem = A.Fake<MSBuildFileSystemBase>();
        A.CallTo(() => blockedFileSystem.FileOrDirectoryExists(A<string>._)).ReturnsLazily(() =>
        {
            firstEntered.Set();
            releaseFirst.Wait(TimeSpan.FromSeconds(30)).ShouldBeTrue();
            return true;
        });
        MSBuildFileSystemBase otherFileSystem = A.Fake<MSBuildFileSystemBase>();
        A.CallTo(() => otherFileSystem.FileOrDirectoryExists(A<string>._)).Returns(false);
        Task<bool> first = StartEvaluation(
            condition, blockedFileSystem, env.DefaultTestDirectory.Path, null, ("File", "file"));

        try
        {
            firstEntered.Wait(TimeSpan.FromSeconds(10)).ShouldBeTrue();
            FieldInfo cacheField = typeof(ConditionEvaluator).GetField(
                "s_cachedExpressionTrees", BindingFlags.Static | BindingFlags.NonPublic).ShouldNotBeNull();
            IDictionary cache = cacheField.GetValue(null).ShouldBeAssignableTo<IDictionary>().ShouldNotBeNull();
            cache.Remove((int)ParserOptions.AllowProperties);

            Evaluate(condition, otherFileSystem, env.DefaultTestDirectory.Path, null,
                ("File", "file")).ShouldBeFalse();
        }
        finally
        {
            releaseFirst.Set();
            (await first).ShouldBeTrue();
        }

        Evaluate(condition, otherFileSystem, env.DefaultTestDirectory.Path, null,
            ("File", "file")).ShouldBeFalse();
    }

    private static Task<bool> StartEvaluation(
        string condition,
        IFileSystem fileSystem,
        string directory,
        LoggingContext? loggingContext,
        params (string Name, string Value)[] properties) =>
        Task.Factory.StartNew(
            () => Evaluate(condition, fileSystem, directory, loggingContext, properties),
            CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);

    private static bool Evaluate(
        string condition,
        IFileSystem fileSystem,
        string directory,
        LoggingContext? loggingContext,
        params (string Name, string Value)[] properties)
    {
        PropertyDictionary<ProjectPropertyInstance> propertyBag = new();
        foreach ((string name, string value) in properties)
        {
            propertyBag.Set(ProjectPropertyInstance.Create(name, value));
        }

        Expander<ProjectPropertyInstance, ProjectItemInstance> expander = new(propertyBag, fileSystem);
        return ConditionEvaluator.EvaluateCondition(
            condition, ParserOptions.AllowProperties, expander, ExpanderOptions.ExpandProperties,
            directory, ElementLocation.EmptyLocation, fileSystem, loggingContext);
    }
}
