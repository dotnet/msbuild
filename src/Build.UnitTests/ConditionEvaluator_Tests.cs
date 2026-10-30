// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
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
using Microsoft.Build.Shared;
using Microsoft.Build.Shared.FileSystem;
using Microsoft.Build.UnitTests.BackEnd;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests;

public class ConditionEvaluator_Tests : IDisposable
{
    private readonly ITestOutputHelper _output;

    public ConditionEvaluator_Tests(ITestOutputHelper output)
    {
        _output = output;
    }

    public void Dispose() => ChangeWaves.ResetStateForTests();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ChangeWaveSelectsSharedOrLegacyConditionEvaluation(bool disableWave)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        SetChangeWave(disableWave, env);
        ChangeWaves.AreFeaturesEnabled(ChangeWaves.Wave18_13).ShouldBeTrue();
        string identity = Guid.NewGuid().ToString("N");
        string condition = $"Exists('$(File)') And '$(Value)' == 'expected' And '$(MSBuildToolsVersion)' > '1.0' And '{identity}' == '{identity}'";
        string path = Path.Combine(env.DefaultTestDirectory.Path, "test.file");
        GenericExpressionNode? legacyTree = null;
        MSBuildFileSystemBase fileSystem = A.Fake<MSBuildFileSystemBase>();
        A.CallTo(() => fileSystem.FileOrDirectoryExists(A<string>._)).ReturnsLazily(() =>
        {
            object entry = GetConditionCacheEntry(condition, disableWave);
            Monitor.IsEntered(entry).ShouldBe(disableWave);
            if (disableWave)
            {
                entry.ShouldBeOfType<Stack<GenericExpressionNode>>().Count.ShouldBe(0);
                if (legacyTree is not null)
                {
                    Monitor.IsEntered(legacyTree).ShouldBeTrue();
                }
            }
            else
            {
                entry.ShouldBeOfType<CachedCondition>();
            }

            return true;
        });

        Check("expected", MSBuildConstants.CurrentToolsVersion).ShouldBeTrue();
        object cached = GetConditionCacheEntry(condition, disableWave);
        if (disableWave)
        {
            Stack<GenericExpressionNode> pool = cached.ShouldBeOfType<Stack<GenericExpressionNode>>();
            pool.Count.ShouldBe(1);
            legacyTree = pool.Peek();
        }

        Check("different", MSBuildConstants.CurrentToolsVersion).ShouldBeFalse();
        Check("expected", "0.5").ShouldBeFalse();
        Check("expected", MSBuildConstants.CurrentToolsVersion).ShouldBeTrue();
        GetConditionCacheEntry(condition, disableWave).ShouldBeSameAs(cached);
        if (disableWave)
        {
            cached.ShouldBeOfType<Stack<GenericExpressionNode>>().Peek().ShouldBeSameAs(legacyTree);
        }

        bool Check(string value, string toolsVersion) =>
            Evaluate(condition, fileSystem, env.DefaultTestDirectory.Path, null,
                ("File", path), ("Value", value), ("MSBuildToolsVersion", toolsVersion));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CachedConditionResetsAfterExceptionInEitherWave(bool disableWave)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        SetChangeWave(disableWave, env);
        string identity = Guid.NewGuid().ToString("N");
        string condition = $"'$(Value)' > '1' And '{identity}' == '{identity}'";

        Should.Throw<InvalidProjectFileException>(() => Check("not-a-number"));
        Check("0").ShouldBeFalse();
        Check("3").ShouldBeTrue();

        bool Check(string value) =>
            Evaluate(condition, FileSystems.Default, env.DefaultTestDirectory.Path, null, ("Value", value));
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
        string? observedSecondPath = null;
        MSBuildFileSystemBase fileSystem = A.Fake<MSBuildFileSystemBase>();
        A.CallTo(() => fileSystem.FileOrDirectoryExists(A<string>._)).ReturnsLazily((string path) =>
        {
            if (path == firstPath)
            {
                firstEntered.Set();
                return releaseFirst.Wait(TimeSpan.FromSeconds(30));
            }

            observedSecondPath = path;
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
            observedSecondPath.ShouldBe(secondPath);
            first.IsCompleted.ShouldBeFalse();
        }
        finally
        {
            releaseFirst.Set();
            if (second is not null)
            {
                await Task.WhenAll(first, second);
            }
            else
            {
                await first;
            }
        }

        (await first).ShouldBeTrue();
        warnings.ShouldBe(1);
        Evaluate(
            condition, fileSystem, env.DefaultTestDirectory.Path, loggingContext,
            ("File", secondPath), ("Value", identity)).ShouldBeFalse();
        warnings.ShouldBe(1);
    }

    [Fact]
    public async Task SameParsedConditionDoesNotBlockConcurrentEvaluations()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        using ManualResetEventSlim firstEntered = new();
        using ManualResetEventSlim releaseFirst = new();
        const string condition = "Exists('$(File)') And '$(Value)' == 'expected'";
        CachedCondition cached = new();
        ParsedCondition parsed = cached.GetOrParse(
            condition, ParserOptions.AllowProperties, ElementLocation.Empty, null);
        string firstPath = Path.Combine(env.DefaultTestDirectory.Path, "first.file");
        string secondPath = Path.Combine(env.DefaultTestDirectory.Path, "second.file");
        MSBuildFileSystemBase fileSystem = A.Fake<MSBuildFileSystemBase>();
        int firstThreadId = 0;
        int secondThreadId = 0;
        string? observedSecondPath = null;
        A.CallTo(() => fileSystem.FileOrDirectoryExists(A<string>._)).ReturnsLazily((string path) =>
        {
            if (path == firstPath)
            {
                firstThreadId = Environment.CurrentManagedThreadId;
                firstEntered.Set();
                return releaseFirst.Wait(TimeSpan.FromSeconds(30));
            }

            observedSecondPath = path;
            secondThreadId = Environment.CurrentManagedThreadId;
            return true;
        });
        ConditionEvaluator.ConditionEvaluationState<ProjectPropertyInstance, ProjectItemInstance> firstState = new(
            condition, CreateExpander(fileSystem, ("File", firstPath), ("Value", "expected")),
            ExpanderOptions.ExpandProperties, null, env.DefaultTestDirectory.Path,
            ElementLocation.Empty, fileSystem);
        ConditionEvaluator.ConditionEvaluationState<ProjectPropertyInstance, ProjectItemInstance> secondState = new(
            condition, CreateExpander(fileSystem, ("File", secondPath), ("Value", "different")),
            ExpanderOptions.ExpandProperties, null, env.DefaultTestDirectory.Path,
            ElementLocation.Empty, fileSystem);
        Task<bool> first = Task.Factory.StartNew(
            () => parsed.Evaluate(firstState),
            CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        Task<bool>? second = null;

        try
        {
            firstEntered.Wait(TimeSpan.FromSeconds(10)).ShouldBeTrue();
            second = Task.Factory.StartNew(() =>
            {
                ParsedCondition sameCondition = cached.GetOrParse(
                    condition, ParserOptions.AllowProperties, ElementLocation.Empty, null);
                sameCondition.Root.ShouldBeSameAs(parsed.Root);
                return sameCondition.Evaluate(secondState);
            }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);

            (await Task.WhenAny(second, Task.Delay(TimeSpan.FromSeconds(10)))).ShouldBeSameAs(second);
            (await second).ShouldBeFalse();
            observedSecondPath.ShouldBe(secondPath);
            secondThreadId.ShouldNotBe(firstThreadId);
            first.IsCompleted.ShouldBeFalse();
        }
        finally
        {
            releaseFirst.Set();
            if (second is not null)
            {
                await Task.WhenAll(first, second);
            }
            else
            {
                await first;
            }
        }

        (await first).ShouldBeTrue();
    }

    [Fact]
    public async Task SameParsedConditionKeepsPopulatedStatePrivateAcrossThreads()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        using CountdownEvent bothPopulated = new(2);
        using ManualResetEventSlim releaseFirst = new();
        using ManualResetEventSlim releaseSecond = new();
        const string condition = "'$(Value)' != 'none' And Exists('$(File)') And '$(Value)' == 'first'";
        CachedCondition cached = new();
        ParsedCondition parsed = cached.GetOrParse(
            condition, ParserOptions.AllowProperties, ElementLocation.Empty, null);
        string firstPath = Path.Combine(env.DefaultTestDirectory.Path, "first.file");
        string secondPath = Path.Combine(env.DefaultTestDirectory.Path, "second.file");
        MSBuildFileSystemBase fileSystem = A.Fake<MSBuildFileSystemBase>();
        int firstThreadId = 0;
        int secondThreadId = 0;
        A.CallTo(() => fileSystem.FileOrDirectoryExists(firstPath)).ReturnsLazily(() =>
        {
            firstThreadId = Environment.CurrentManagedThreadId;
            bothPopulated.Signal();
            return releaseFirst.Wait(TimeSpan.FromSeconds(30));
        });
        A.CallTo(() => fileSystem.FileOrDirectoryExists(secondPath)).ReturnsLazily(() =>
        {
            secondThreadId = Environment.CurrentManagedThreadId;
            bothPopulated.Signal();
            return releaseSecond.Wait(TimeSpan.FromSeconds(30));
        });
        ConditionEvaluator.ConditionEvaluationState<ProjectPropertyInstance, ProjectItemInstance> firstState = new(
            condition, CreateExpander(fileSystem, ("Value", "first"), ("File", firstPath)),
            ExpanderOptions.ExpandProperties, null, env.DefaultTestDirectory.Path,
            ElementLocation.Empty, fileSystem);
        ConditionEvaluator.ConditionEvaluationState<ProjectPropertyInstance, ProjectItemInstance> secondState = new(
            condition, CreateExpander(fileSystem, ("Value", "second"), ("File", secondPath)),
            ExpanderOptions.ExpandProperties, null, env.DefaultTestDirectory.Path,
            ElementLocation.Empty, fileSystem);
        Task<bool> first = Task.Factory.StartNew(
            () => parsed.Evaluate(firstState),
            CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        Task<bool>? second = null;

        try
        {
            second = Task.Factory.StartNew(() =>
            {
                ParsedCondition sameCondition = cached.GetOrParse(
                    condition, ParserOptions.AllowProperties, ElementLocation.Empty, null);
                sameCondition.Root.ShouldBeSameAs(parsed.Root);
                return sameCondition.Evaluate(secondState);
            }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);

            bothPopulated.Wait(TimeSpan.FromSeconds(10)).ShouldBeTrue();
            secondThreadId.ShouldNotBe(firstThreadId);
            first.IsCompleted.ShouldBeFalse();
            second.IsCompleted.ShouldBeFalse();
            firstState.NodeStates.GetString(0).ExpandedValue.ShouldBe("first");
            secondState.NodeStates.GetString(0).ExpandedValue.ShouldBe("second");

            releaseSecond.Set();
            (await Task.WhenAny(second, Task.Delay(TimeSpan.FromSeconds(10)))).ShouldBeSameAs(second);
            (await second).ShouldBeFalse();
            first.IsCompleted.ShouldBeFalse();
            firstState.NodeStates.GetString(0).ExpandedValue.ShouldBe("first");
        }
        finally
        {
            releaseSecond.Set();
            releaseFirst.Set();
            if (second is not null)
            {
                await Task.WhenAll(first, second);
            }
            else
            {
                await first;
            }
        }

        (await first).ShouldBeTrue();
    }

    [Fact]
    public void EvaluationStateDoesNotLeakAfterEvaluationThrows()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        const string condition = "'$(Value)' > '1'";
        ParsedCondition parsed = new Parser().ParseForEvaluation(
            condition, ParserOptions.AllowProperties, ElementLocation.Empty);
        PropertyDictionary<ProjectPropertyInstance> properties = new();
        properties.Set(ProjectPropertyInstance.Create("Value", "not-a-number"));
        Expander<ProjectPropertyInstance, ProjectItemInstance> expander = new(properties, FileSystems.Default);
        ConditionEvaluator.ConditionEvaluationState<ProjectPropertyInstance, ProjectItemInstance> state = new(
            condition, expander, ExpanderOptions.ExpandProperties, null, env.DefaultTestDirectory.Path,
            ElementLocation.Empty, FileSystems.Default);

        Should.Throw<InvalidProjectFileException>(() => parsed.Evaluate(state));

        properties.Set(ProjectPropertyInstance.Create("Value", "0"));
        parsed.Evaluate(state).ShouldBeFalse();
        properties.Set(ProjectPropertyInstance.Create("Value", "3"));
        parsed.Evaluate(state).ShouldBeTrue();
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
            condition, ParserOptions.AllowProperties, ElementLocation.Empty);
        MSBuildFileSystemBase fileSystem = A.Fake<MSBuildFileSystemBase>();
        ConditionEvaluator.ConditionEvaluationState<ProjectPropertyInstance, ProjectItemInstance> outerState = new(
            condition, CreateExpander(fileSystem, ("Value", "outer"), ("File", outerPath)),
            ExpanderOptions.ExpandProperties, null, env.DefaultTestDirectory.Path,
            ElementLocation.Empty, fileSystem);
        ConditionEvaluator.ConditionEvaluationState<ProjectPropertyInstance, ProjectItemInstance> innerState = new(
            condition, CreateExpander(fileSystem, ("Value", "inner"), ("File", innerPath)),
            ExpanderOptions.ExpandProperties, null, env.DefaultTestDirectory.Path,
            ElementLocation.Empty, fileSystem);

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
    [InlineData("'$(MSBuildToolsVersion)' > '1.0'")]
    [InlineData("$(msbuildtoolsversion) > '1.0'")]
    [InlineData("'$(MSBuildToolsVersion)' == '$(VisualStudioVersion)'")]
    public void ToolsVersionCompatibilityUsesEachEvaluationsExpandedValue(string condition)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        ParsedCondition parsed = new Parser().ParseForEvaluation(
            condition, ParserOptions.AllowProperties, ElementLocation.Empty);
        (string Value, bool Expected)[] cases =
        [
            (MSBuildConstants.CurrentToolsVersion, true),
            ("0.5", false),
            (MSBuildConstants.CurrentToolsVersion, true)
        ];
        foreach ((string value, bool expected) in cases)
        {
            ConditionEvaluator.ConditionEvaluationState<ProjectPropertyInstance, ProjectItemInstance> state = new(
                condition, CreateExpander(FileSystems.Default,
                    ("MSBuildToolsVersion", value),
                    ("VisualStudioVersion", MSBuildConstants.CurrentVisualStudioVersion)),
                ExpanderOptions.ExpandProperties, null, env.DefaultTestDirectory.Path,
                ElementLocation.Empty, FileSystems.Default);

            parsed.Evaluate(state).ShouldBe(expected);
        }
    }

    [Theory]
    [InlineData("'Current' > '1.0'", "Current")]
    [InlineData("'$(Other)' > '1.0'", "Current")]
    [InlineData("'%24(MSBuildToolsVersion)' > '1.0'", "Current")]
    [InlineData("'$(MSBuildToolsVersion)' > '1.0'", "current")]
    public void ToolsVersionCompatibilityDoesNotApplyToOtherSyntaxOrCasing(
        string condition, string toolsVersion)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        Should.Throw<InvalidProjectFileException>(() =>
            Evaluate(condition, FileSystems.Default, env.DefaultTestDirectory.Path, null,
                ("MSBuildToolsVersion", toolsVersion), ("Other", "Current")));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CachedConditionUpdatesEachConditionedPropertiesTable(bool disableWave)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        SetChangeWave(disableWave, env);
        const string condition = "'$(Configuration)' == '$(ExpectedConfiguration)' And '$(Platform)' != '$(OtherPlatform)'";
        Dictionary<string, List<string>> firstTable = new();
        Dictionary<string, List<string>> secondTable = new();
        Check("Debug", "AnyCPU", firstTable).ShouldBeTrue();
        Check("Release", "x64", secondTable).ShouldBeTrue();
        Check("Debug", "AnyCPU", firstTable).ShouldBeTrue();

        firstTable.Count.ShouldBe(4);
        firstTable["Configuration"].ShouldBe(["Debug"]);
        firstTable["ExpectedConfiguration"].ShouldBe(["Debug"]);
        firstTable["Platform"].ShouldBe(["never"]);
        firstTable["OtherPlatform"].ShouldBe(["AnyCPU"]);
        secondTable.Count.ShouldBe(4);
        secondTable["Configuration"].ShouldBe(["Release"]);
        secondTable["ExpectedConfiguration"].ShouldBe(["Release"]);
        secondTable["Platform"].ShouldBe(["never"]);
        secondTable["OtherPlatform"].ShouldBe(["x64"]);

        bool Check(
            string configuration, string platform, Dictionary<string, List<string>> table) =>
            ConditionEvaluator.EvaluateConditionCollectingConditionedProperties(
                condition, ParserOptions.AllowProperties, CreateExpander(FileSystems.Default,
                    ("Configuration", configuration), ("ExpectedConfiguration", configuration),
                    ("Platform", platform), ("OtherPlatform", "never")),
                ExpanderOptions.ExpandProperties, table, env.DefaultTestDirectory.Path,
                ElementLocation.Empty, FileSystems.Default, null);
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
            condition, ParserOptions.AllowProperties, ElementLocation.Empty);
        parsed.StateLayout.StringSlotCount.ShouldBe(expectedSlots);
        ConditionEvaluator.ConditionEvaluationState<ProjectPropertyInstance, ProjectItemInstance> state = new(
            condition, CreateExpander(FileSystems.Default, ("Value", "value"), ("Other", "other")),
            ExpanderOptions.ExpandProperties, null, env.DefaultTestDirectory.Path,
            ElementLocation.Empty, FileSystems.Default);

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
            condition, ParserOptions.AllowProperties, ElementLocation.Empty);
        StringExpressionNode node = parsed.Root.ShouldBeOfType<StringExpressionNode>();
        ConditionEvaluator.ConditionEvaluationState<ProjectPropertyInstance, ProjectItemInstance> state = new(
            condition, CreateExpander(FileSystems.Default), ExpanderOptions.ExpandProperties,
            null, env.DefaultTestDirectory.Path, ElementLocation.Empty, FileSystems.Default);

        node.StateSlot.ShouldBe(-1);
        node.GetExpandedValue(state).ShouldBe(value);
        node.EvaluatesToEmpty(state).ShouldBe(value.Length == 0);
        node.TryBoolEvaluate(state, out _).ShouldBe(convertsToBool);
        node.TryNumericEvaluate(state, out _).ShouldBe(convertsToNumber);
        node.TryVersionEvaluate(state, out _).ShouldBe(convertsToVersion);
    }

    [Theory]
    [InlineData(256)]
    [InlineData(258)]
    public void ConditionStateBuffersAreReusedAndIsolated(int slots)
    {
        StringExpressionNode.EvaluationState[] first =
            ThreadLocalNodeStateBuffer<StringExpressionNode.EvaluationState>.Acquire(slots);
        first[0].ExpandedValue = "first";
        first[slots - 1].ExpandedValue = "last";
        ThreadLocalNodeStateBuffer<StringExpressionNode.EvaluationState>.Release(first, slots);

        StringExpressionNode.EvaluationState[] reused =
            ThreadLocalNodeStateBuffer<StringExpressionNode.EvaluationState>.Acquire(slots);
        StringExpressionNode.EvaluationState[]? nested = null;
        try
        {
            reused.ShouldBeSameAs(first);
            reused[0].ExpandedValue.ShouldBeNull();
            reused[slots - 1].ExpandedValue.ShouldBeNull();
            reused[0].ExpandedValue = "outer";
            reused[slots - 1].ExpandedValue = "outer-last";

            nested = ThreadLocalNodeStateBuffer<StringExpressionNode.EvaluationState>.Acquire(slots);
            nested.ShouldNotBeSameAs(reused);
            nested[0].ExpandedValue.ShouldBeNull();
            nested[slots - 1].ExpandedValue.ShouldBeNull();
            nested[0].ExpandedValue = "inner";
            ThreadLocalNodeStateBuffer<StringExpressionNode.EvaluationState>.Release(nested, slots);
            nested = null;

            reused[0].ExpandedValue.ShouldBe("outer");
            reused[slots - 1].ExpandedValue.ShouldBe("outer-last");
        }
        finally
        {
            if (nested is not null)
            {
                ThreadLocalNodeStateBuffer<StringExpressionNode.EvaluationState>.Release(nested, slots);
            }

            ThreadLocalNodeStateBuffer<StringExpressionNode.EvaluationState>.Release(reused, slots);
        }
    }

    [Fact]
    public void NodeStateAccessorValidatesSlotsAndClearsReusedBuffers()
    {
        NodeStateStore state = new();
        Should.Throw<InvalidOperationException>(() => state.GetString(0));
        state.Initialize(new NodeStateLayout(1));
        try
        {
            Should.Throw<ArgumentOutOfRangeException>(() => state.GetString(-1));
            Should.Throw<ArgumentOutOfRangeException>(() => state.GetString(1));
            state.GetString(0).ExpandedValue = "first";
            state.GetString(0).ExpandedValue.ShouldBe("first");
            state.Release();
            Should.Throw<InvalidOperationException>(() => state.GetString(0));

            state.Initialize(new NodeStateLayout(1));
            state.GetString(0).ExpandedValue.ShouldBeNull();
        }
        finally
        {
            state.Release();
        }
    }

    private static void SetChangeWave(bool disableWave, TestEnvironment env)
    {
        env.SetEnvironmentVariable(
            "MSBUILDDISABLEFEATURESFROMVERSION",
            disableWave ? ChangeWaves.Wave18_14.ToString() : null);
        ChangeWaves.ResetStateForTests();
    }

    private static object GetConditionCacheEntry(string condition, bool legacy)
    {
        string cacheName = legacy ? "s_cachedLegacyExpressionTrees" : "s_cachedExpressionTrees";
        FieldInfo cacheField = typeof(ConditionEvaluator).GetField(
            cacheName, BindingFlags.Static | BindingFlags.NonPublic).ShouldNotBeNull();
        IDictionary cache = cacheField.GetValue(null).ShouldBeAssignableTo<IDictionary>().ShouldNotBeNull();
        object options = cache[(int)ParserOptions.AllowProperties].ShouldNotBeNull();
        FieldInfo expressionsField = options.GetType().GetField(
            legacy ? "_conditionPools" : "_expressions",
            BindingFlags.Instance | BindingFlags.NonPublic).ShouldNotBeNull();
        IDictionary expressions = expressionsField.GetValue(options).ShouldBeAssignableTo<IDictionary>().ShouldNotBeNull();
        return expressions[condition].ShouldNotBeNull();
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
            ExpanderOptions.ExpandProperties, directory, ElementLocation.Empty, fileSystem, loggingContext);

    private static Expander<ProjectPropertyInstance, ProjectItemInstance> CreateExpander(
        IFileSystem fileSystem,
        params (string Name, string Value)[] properties)
    {
        PropertyDictionary<ProjectPropertyInstance> propertyBag = new();
        foreach ((string name, string value) in properties)
        {
            propertyBag.Set(ProjectPropertyInstance.Create(name, value, mayBeReserved: true));
        }

        return new Expander<ProjectPropertyInstance, ProjectItemInstance>(propertyBag, fileSystem);
    }
}
