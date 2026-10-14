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
    public void EvaluationStateDoesNotLeakAfterEvaluationThrows()
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
    public async Task SharedExpressionCanFinishAfterCacheEviction()
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

    [Fact]
    public void NestedEvaluationKeepsOuterStatePrivate()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string outerPath = Path.Combine(env.DefaultTestDirectory.Path, "outer.file");
        string innerPath = Path.Combine(env.DefaultTestDirectory.Path, "inner.file");
        string condition = "'$(Value)' != 'none' And Exists('$(File)')";
        ParsedCondition parsed = new Parser().ParseForEvaluation(
            condition, ParserOptions.AllowProperties, ElementLocation.EmptyLocation);
        MSBuildFileSystemBase fileSystem = A.Fake<MSBuildFileSystemBase>();
        ConditionEvaluator.ConditionEvaluationState<ProjectPropertyInstance, ProjectItemInstance> outerState = new(
            condition, CreateExpander(fileSystem, ("Value", "outer"), ("File", outerPath)),
            ExpanderOptions.ExpandProperties, null, env.DefaultTestDirectory.Path,
            ElementLocation.EmptyLocation, fileSystem);
        ConditionEvaluator.ConditionEvaluationState<ProjectPropertyInstance, ProjectItemInstance> innerState = new(
            condition, CreateExpander(fileSystem, ("Value", "inner"), ("File", innerPath)),
            ExpanderOptions.ExpandProperties, null, env.DefaultTestDirectory.Path,
            ElementLocation.EmptyLocation, fileSystem);

        A.CallTo(() => fileSystem.FileOrDirectoryExists(innerPath)).Returns(true);
        A.CallTo(() => fileSystem.FileOrDirectoryExists(outerPath)).ReturnsLazily(() =>
        {
            ref StringExpressionNode.EvaluationState outer = ref outerState.NodeStates.GetString(0);
            outer.ExpandedValue.ShouldBe("outer");
            parsed.Evaluate(innerState).ShouldBeTrue();
            outer.ExpandedValue.ShouldBe("outer");
            return true;
        });

        parsed.Evaluate(outerState).ShouldBeTrue();
        parsed.Evaluate(outerState).ShouldBeTrue();
    }

    [Fact]
    public void FailedParsingUsesTheCurrentCallersLocation()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        CachedCondition cached = new();
        ElementLocation first = ElementLocation.Create(
            Path.Combine(env.DefaultTestDirectory.Path, "first.proj"), 7, 2);
        ElementLocation second = ElementLocation.Create(
            Path.Combine(env.DefaultTestDirectory.Path, "second.proj"), 19, 4);
        const string condition = "true And (";

        InvalidProjectFileException firstError = Should.Throw<InvalidProjectFileException>(() =>
            cached.GetOrParse(condition, ParserOptions.AllowProperties, first, null));
        firstError.ProjectFile.ShouldBe(first.File);
        firstError.LineNumber.ShouldBe(first.Line);
        firstError.ColumnNumber.ShouldBe(first.Column);

        InvalidProjectFileException secondError = Should.Throw<InvalidProjectFileException>(() =>
            cached.GetOrParse(condition, ParserOptions.AllowProperties, second, null));
        secondError.ProjectFile.ShouldBe(second.File);
        secondError.LineNumber.ShouldBe(second.Line);
        secondError.ColumnNumber.ShouldBe(second.Column);
    }

    [Theory]
    [InlineData("'' == ''", 0)]
    [InlineData("'fixed' == 'fixed'", 0)]
    [InlineData("'$(Value)' == 'value'", 1)]
    [InlineData("'fixed' != '$(Value)' And '$(Other)' == 'other'", 2)]
    [InlineData("'%61' == 'a'", 1)]
    [InlineData("'$(Value)' == '%76alue'", 2)]
    public void StringStateSlotsOnlyIncludeExpandableNodes(string condition, int expectedSlots)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        ParsedCondition parsed = new Parser().ParseForEvaluation(
            condition, ParserOptions.AllowProperties, ElementLocation.EmptyLocation);
        parsed.StateLayout.StringSlotCount.ShouldBe(expectedSlots);
        ConditionEvaluator.ConditionEvaluationState<ProjectPropertyInstance, ProjectItemInstance> state = new(
            condition, CreateExpander(FileSystems.Default, ("Value", "value"), ("Other", "other")),
            ExpanderOptions.ExpandProperties, null, env.DefaultTestDirectory.Path,
            ElementLocation.EmptyLocation, FileSystems.Default);

        parsed.Evaluate(state).ShouldBeTrue();
    }

    [Theory]
    [InlineData("", false, false, false)]
    [InlineData("true", true, false, false)]
    [InlineData("17.0", false, true, true)]
    [InlineData("Current", false, false, false)]
    [InlineData("literal", false, false, false)]
    public void LiteralNodeEvaluationDoesNotAccessState(
        string value, bool convertsToBool, bool convertsToNumber, bool convertsToVersion)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string condition = $"'{value}'";
        ParsedCondition parsed = new Parser().ParseForEvaluation(
            condition, ParserOptions.AllowProperties, ElementLocation.EmptyLocation);
        StringExpressionNode node = parsed.Root.ShouldBeOfType<StringExpressionNode>();
        ConditionEvaluator.ConditionEvaluationState<ProjectPropertyInstance, ProjectItemInstance> state = new(
            condition, CreateExpander(FileSystems.Default), ExpanderOptions.ExpandProperties,
            null, env.DefaultTestDirectory.Path, ElementLocation.EmptyLocation, FileSystems.Default);

        node.StateSlot.ShouldBe(-1);
        node.GetExpandedValue(state).ShouldBe(value);
        node.EvaluatesToEmpty(state).ShouldBe(value.Length == 0);
        node.TryBoolEvaluate(state, out _).ShouldBe(convertsToBool);
        node.TryNumericEvaluate(state, out _).ShouldBe(convertsToNumber);
        node.TryVersionEvaluate(state, out _).ShouldBe(convertsToVersion);
    }

    [Fact]
    public void NodeStateAccessorsValidateSlotsAndClearReusedBuffers()
    {
        NodeStateStore state = new();
        Should.Throw<InvalidOperationException>(() => state.GetString(0));
        Should.Throw<InvalidOperationException>(() => state.GetComparison(0));
        state.Initialize(new NodeStateLayout(1, 1));
        try
        {
            Should.Throw<ArgumentOutOfRangeException>(() => state.GetString(-1));
            Should.Throw<ArgumentOutOfRangeException>(() => state.GetString(1));
            Should.Throw<ArgumentOutOfRangeException>(() => state.GetComparison(-1));
            Should.Throw<ArgumentOutOfRangeException>(() => state.GetComparison(1));
            state.GetString(0).ExpandedValue = "first";
            state.GetComparison(0).ConditionedPropertiesUpdated = true;
            state.GetString(0).ExpandedValue.ShouldBe("first");
            state.GetComparison(0).ConditionedPropertiesUpdated.ShouldBeTrue();
            state.Release();
            Should.Throw<InvalidOperationException>(() => state.GetString(0));
            Should.Throw<InvalidOperationException>(() => state.GetComparison(0));

            state.Initialize(new NodeStateLayout(1, 1));
            state.GetString(0).ExpandedValue.ShouldBeNull();
            state.GetComparison(0).ConditionedPropertiesUpdated.ShouldBeFalse();
        }
        finally
        {
            state.Release();
        }
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
        params (string Name, string Value)[] properties) =>
        ConditionEvaluator.EvaluateCondition(
            condition, ParserOptions.AllowProperties, CreateExpander(fileSystem, properties),
            ExpanderOptions.ExpandProperties, directory, ElementLocation.EmptyLocation, fileSystem, loggingContext);

    private static Expander<ProjectPropertyInstance, ProjectItemInstance> CreateExpander(
        IFileSystem fileSystem,
        params (string Name, string Value)[] properties)
    {
        PropertyDictionary<ProjectPropertyInstance> propertyBag = new();
        foreach ((string name, string value) in properties)
        {
            propertyBag.Set(ProjectPropertyInstance.Create(name, value));
        }

        return new Expander<ProjectPropertyInstance, ProjectItemInstance>(propertyBag, fileSystem);
    }
}
