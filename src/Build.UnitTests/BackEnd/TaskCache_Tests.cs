// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging;
using Microsoft.Build.Shared;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests.BackEnd;

public sealed class TaskCache_Tests(ITestOutputHelper output)
{
    private readonly ITestOutputHelper _output = output;
    private string? _cacheDirectory;

    public static bool IsSupported => TaskCacheStore.IsSupported;

    [Fact]
    public void InputFingerprintPreservesTaskVisibleMetadataSemantics()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string path = env.CreateFile("metadata.proj", """
            <Project>
              <ItemDefinitionGroup>
                <Source><NameMeta>%(Filename)</NameMeta></Source>
              </ItemDefinitionGroup>
              <ItemGroup><Source Include="folder/hello.txt" /></ItemGroup>
            </Project>
            """).Path;
        ProjectInstance project = new(path);
        ProjectItemInstance source = project.GetItems("Source").Single();
        ITaskItem inherited = new ProjectItemInstance.TaskItem(source);
        ITaskItem literal = new ProjectItemInstance.TaskItem(source);
        literal.SetMetadata("NameMeta", "%(Filename)");

        inherited.GetMetadata("NameMeta").ShouldBe("hello");
        literal.GetMetadata("NameMeta").ShouldBe("%(Filename)");
        TaskInvocationCache.SerializeParameter(inherited).ShouldBe(TaskInvocationCache.SerializeParameter(literal));
        TaskInvocationCache.SerializeInputParameter(inherited).ShouldNotBe(TaskInvocationCache.SerializeInputParameter(literal));
        ITaskItem?[] inheritedItems = [inherited, null];
        ITaskItem?[] literalItems = [literal, null];
        TaskInvocationCache.SerializeInputParameter(inheritedItems)
            .ShouldNotBe(TaskInvocationCache.SerializeInputParameter(literalItems));

        literal.SetMetadata("NameMeta", "hello");
        inherited.GetMetadata("NameMeta").ShouldBe(literal.GetMetadata("NameMeta"));
        TaskInvocationCache.SerializeInputParameter(inherited).ShouldNotBe(TaskInvocationCache.SerializeInputParameter(literal));
        inherited.ItemSpec = literal.ItemSpec = "folder/changed.txt";
        inherited.GetMetadata("NameMeta").ShouldBe("changed");
        literal.GetMetadata("NameMeta").ShouldBe("hello");
    }

    private static string Invocation => """
        <DeclaredIOEchoTask Sources="@(Source)"
                            Destination="$(MSBuildProjectDirectory)/output.txt"
                            OptionalOutput=""
                            MSBuildTaskCacheEnabled="$(MSBuildTaskCacheEnabled)"
                            Deterministic="$(Deterministic)"
                            IssueWarning="$(IssueWarning)">
          <Output TaskParameter="Result" PropertyName="Result" />
          <Output TaskParameter="Items" ItemName="Results" />
        </DeclaredIOEchoTask>
        """;

    [Fact]
    public void StructItemArrayMetadataParticipatesInInputKeys()
    {
        TaskItem<int> first = new(42);
        TaskItem<int> changed = new(42);
        first.SetMetadata("Label", "first");
        changed.SetMetadata("Label", "changed");
        TaskItem<int>[] firstItems = [first];
        TaskItem<int>[] changedItems = [changed];
        first.ItemSpec.ShouldBe(changed.ItemSpec);
        TaskInvocationCache.SerializeInputParameter(firstItems).ShouldNotBe(TaskInvocationCache.SerializeInputParameter(changedItems));
        TaskInvocationCache.SerializeInputParameter(firstItems).ShouldBe(TaskInvocationCache.SerializeInputParameter(new ITaskItem[] { first }));
    }

    [Fact]
    public void StructItemArrayPreservesRawAndTaskVisibleMetadata()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string path = env.CreateFile("struct-metadata.proj", """
            <Project>
              <ItemDefinitionGroup><Source><Label>%(Filename)</Label></Source></ItemDefinitionGroup>
              <ItemGroup><Source Include="folder/hello.txt" /></ItemGroup>
            </Project>
            """).Path;
        ProjectItemInstance source = new ProjectInstance(path).GetItems("Source").Single();
        ITaskItem inherited = new ProjectItemInstance.TaskItem(source);
        ITaskItem literal = new ProjectItemInstance.TaskItem(source);
        literal.SetMetadata("Label", "%(Filename)");
        TaskItem<string>[] inheritedItems = [new(inherited)];
        TaskItem<string>[] literalItems = [new(literal)];
        TaskInvocationCache.SerializeParameter(inheritedItems).ShouldBe(TaskInvocationCache.SerializeParameter(literalItems));
        TaskInvocationCache.SerializeInputParameter(inheritedItems).ShouldNotBe(TaskInvocationCache.SerializeInputParameter(literalItems));

        literal.SetMetadata("Label", "hello");
        inherited.GetMetadata("Label").ShouldBe(literal.GetMetadata("Label"));
        TaskInvocationCache.SerializeInputParameter(inheritedItems).ShouldNotBe(TaskInvocationCache.SerializeInputParameter(literalItems));
    }

    [Fact]
    public void StructItemArrayOutputSerializationPreservesMetadataAndOrder()
    {
        TaskItem<int> first = new(42);
        TaskItem<int> second = new(7);
        first.SetMetadata("Label", "first;value");
        second.SetMetadata("Label", "second%value");
        TaskItem<int>[] items = [first, second];
        byte[] serialized = TaskInvocationCache.SerializeParameter(items);
        serialized.ShouldBe(TaskInvocationCache.SerializeParameter(new ITaskItem[] { first, second }));
        using MemoryStream stream = new(serialized, 0, serialized.Length, writable: false, publiclyVisible: true);
        using ITranslator translator = BinaryTranslator.GetReadTranslator(stream, InterningBinaryReader.PoolingBuffer);
        TaskParameter restored = TaskParameter.FactoryForDeserialization(translator);
        restored.ParameterType.ShouldBe(TaskParameterType.ITaskItemArray);
        ITaskItem[] outputs = restored.WrappedParameter.ShouldBeAssignableTo<ITaskItem[]>();
        outputs.Select(item => item.ItemSpec).ShouldBe(["42", "7"]);
        outputs[0].GetMetadata("Label").ShouldBe("first;value");
        outputs[1].GetMetadata("Label").ShouldBe("second%value");
    }

    [Fact]
    public void ItemArrayNormalizationPreservesEmptyAndNullElements()
    {
        TaskInvocationCache.SerializeParameter(Array.Empty<TaskItem<int>>())
            .ShouldBe(TaskInvocationCache.SerializeParameter(Array.Empty<ITaskItem>()));
        TaskInvocationCache.SerializeInputParameter(Array.Empty<TaskItem<int>>())
            .ShouldBe(TaskInvocationCache.SerializeInputParameter(Array.Empty<ITaskItem>()));
        ITaskItem?[] items = [null, new TaskItem<int>(42), null];
        byte[] serialized = TaskInvocationCache.SerializeParameter(items);
        using MemoryStream stream = new(serialized, 0, serialized.Length, writable: false, publiclyVisible: true);
        using ITranslator translator = BinaryTranslator.GetReadTranslator(stream, InterningBinaryReader.PoolingBuffer);
        ITaskItem[] outputs = TaskParameter.FactoryForDeserialization(translator).WrappedParameter.ShouldBeAssignableTo<ITaskItem[]>();
        outputs.Length.ShouldBe(3);
        outputs[0].ShouldBeNull();
        outputs[1].ItemSpec.ShouldBe("42");
        outputs[2].ShouldBeNull();
        TaskInvocationCache.SerializeInputParameter(items)
            .ShouldNotBe(TaskInvocationCache.SerializeInputParameter(new ITaskItem?[] { null, null, new TaskItem<int>(42) }));
    }

    [ConditionalTheory(typeof(TaskCache_Tests), nameof(IsSupported))]
    [InlineData(false)]
    [InlineData(true)]
    public void StructItemArrayTaskRestoresTypedOutputsAndInvalidatesMetadata(bool outOfProcess)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string project = CreateProject(env, $"""
            <Project>
              <UsingTask TaskName="{typeof(TaskCacheStructItemTask).FullName}" AssemblyFile="{typeof(TaskCacheStructItemTask).Assembly.Location}" />
              <ItemGroup><Value Include="42;7"><Label>$(Label)</Label></Value></ItemGroup>
              <Target Name="Build">
                <TaskCacheStructItemTask Values="@(Value)">
                  <Output TaskParameter="Results" ItemName="FirstResults" />
                  <Output TaskParameter="Results" ItemName="SecondResults" />
                  <Output TaskParameter="Sum" PropertyName="Sum" />
                </TaskCacheStructItemTask>
              </Target>
            </Project>
            """);
        Dictionary<string, string?> properties = new() { ["Label"] = "first" };
        Run(project, properties, outOfProcess: outOfProcess, taskCache: false).Result.ShouldHaveSucceeded();
        var cold = Run(project, properties, outOfProcess: outOfProcess);
        cold.Result.ShouldHaveSucceeded();
        cold.Logger.FullLog.ShouldContain(TaskCacheStructItemTask.ExecutionMessage);
        var warm = Run(project, properties, outOfProcess: outOfProcess);
        warm.Result.ShouldHaveSucceeded();
        warm.Logger.FullLog.ShouldContain(ResourceUtilities.FormatResourceStringIgnoreCodeAndKeyword("TaskCache.Hit", nameof(TaskCacheStructItemTask)));
        warm.Logger.FullLog.ShouldNotContain(TaskCacheStructItemTask.ExecutionMessage);
        warm.Result.ProjectStateAfterBuild!.GetPropertyValue("Sum").ShouldBe("49");
        string[] outputNames = ["FirstResults", "SecondResults"];
        foreach (string name in outputNames)
        {
            var outputs = warm.Result.ProjectStateAfterBuild.GetItems(name).ToArray();
            outputs.Select(item => item.EvaluatedInclude).ShouldBe(["42", "7"]);
            outputs[0].GetMetadataValue("Label").ShouldBe("first");
            outputs[1].GetMetadataValue("Label").ShouldBe("first");
        }
        properties["Label"] = "changed";
        var changed = Run(project, properties, outOfProcess: outOfProcess);
        changed.Result.ShouldHaveSucceeded();
        changed.Logger.FullLog.ShouldContain(TaskCacheStructItemTask.ExecutionMessage);
        changed.Result.ProjectStateAfterBuild!.GetItems("FirstResults").First().GetMetadataValue("Label").ShouldBe("changed");
    }

    private static string ProjectText => $"""
        <Project>
          <UsingTask TaskName="{typeof(DeclaredIOEchoTask).FullName}" AssemblyFile="{System.Security.SecurityElement.Escape(typeof(DeclaredIOEchoTask).Assembly.Location)}" />
          <PropertyGroup>
            <ModeAtEvaluation>$(MSBuildTaskCacheEnabled)</ModeAtEvaluation>
            <Deterministic Condition="'$(Deterministic)' == ''">true</Deterministic>
          </PropertyGroup>
          <ItemGroup>
            <Source Include="$(MSBuildProjectDirectory)/a.txt;$(MSBuildProjectDirectory)/b.txt">
              <Label>$(Label)</Label>
            </Source>
          </ItemGroup>
          <Target Name="Build" Returns="$(Result)">
            {Invocation}
          </Target>
        </Project>
        """;

    [ConditionalTheory(typeof(TaskCache_Tests), nameof(IsSupported))]
    [InlineData(false)]
    [InlineData(true)]
    public void UnconditionalTargetRestoresArtifactsAndReadOnlyRawOutputs(bool outOfProcess)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string project = CreateProject(env);
        var first = Run(project, outOfProcess: outOfProcess);
        first.Result.ShouldHaveSucceeded();
        first.Logger.FullLog.ShouldContain(DeclaredIOEchoTask.ExecutionMessage);
        first.Result.ProjectStateAfterBuild!.GetPropertyValue("ModeAtEvaluation").ShouldBe("true");
        File.Delete(Path.Combine(Path.GetDirectoryName(project)!, "output.txt"));
        var second = Run(project, outOfProcess: outOfProcess);
        second.Result.ShouldHaveSucceeded();
        AssertHit(second.Logger);
        second.Logger.TaskStartedEvents.ShouldContain(task => task.TaskName == nameof(DeclaredIOEchoTask));
        second.Result.ProjectStateAfterBuild!.GetPropertyValue("Result").ShouldBe("a|b");
        second.Result.ProjectStateAfterBuild.GetPropertyValue("MSBuildLastTaskResult").ShouldBe("true");
        var items = second.Result.ProjectStateAfterBuild.GetItems("Results");
        items.Count.ShouldBe(1);
        foreach (ProjectItemInstance item in items)
        {
            item.EvaluatedInclude.ShouldBe("a|b");
            item.GetMetadataValue("Metadata").ShouldBe("raw;metadata%value");
        }

        File.ReadAllText(Path.Combine(Path.GetDirectoryName(project)!, "output.txt")).ShouldBe("a|b");
    }

    [ConditionalTheory(typeof(TaskCache_Tests), nameof(IsSupported))]
    [InlineData(false)]
    [InlineData(true)]
    public void WorkerRestoresZeroByteOutputAndNonemptyCompanion(bool emptyFirst)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string project = CreateProject(env, $"""
            <Project>
              <UsingTask TaskName="{typeof(TaskCacheEmptyOutputTask).FullName}" AssemblyFile="{typeof(TaskCacheEmptyOutputTask).Assembly.Location}" />
              <Target Name="Build">
                <TaskCacheEmptyOutputTask Source="$(MSBuildProjectDirectory)/a.txt"
                                         First="$(MSBuildProjectDirectory)/first.out"
                                         Second="$(MSBuildProjectDirectory)/second.out"
                                         EmptyFirst="{emptyFirst}">
                  <Output TaskParameter="Content" PropertyName="Content" />
                  <Output TaskParameter="EmptyLength" PropertyName="EmptyLength" />
                  <Output TaskParameter="Produced" ItemName="Produced" />
                </TaskCacheEmptyOutputTask>
              </Target>
            </Project>
            """);
        string first = Path.Combine(Path.GetDirectoryName(project)!, "first.out");
        string second = Path.Combine(Path.GetDirectoryName(project)!, "second.out");
        string empty = emptyFirst ? first : second;
        string nonempty = emptyFirst ? second : first;
        var cold = Run(project, outOfProcess: true);
        cold.Result.ShouldHaveSucceeded();
        cold.Logger.FullLog.ShouldContain(TaskCacheEmptyOutputTask.ExecutionMessage);
        new FileInfo(empty).Length.ShouldBe(0);
        File.ReadAllText(nonempty).ShouldBe("a");
        File.GetLastWriteTimeUtc(empty).ShouldBe(TaskCacheEmptyOutputTask.OriginalTimestamp);
        File.Delete(first);
        File.Delete(second);

        DateTime started = DateTime.UtcNow;
        var warm = Run(project, outOfProcess: true);
        warm.Result.ShouldHaveSucceeded();
        warm.Logger.FullLog.ShouldContain(ResourceUtilities.FormatResourceStringIgnoreCodeAndKeyword("TaskCache.Hit", nameof(TaskCacheEmptyOutputTask)));
        warm.Logger.FullLog.ShouldNotContain(TaskCacheEmptyOutputTask.ExecutionMessage);
        new FileInfo(empty).Length.ShouldBe(0);
        File.ReadAllText(nonempty).ShouldBe("a");
        File.GetLastWriteTimeUtc(first).ShouldBeInRange(started.AddSeconds(-2), DateTime.UtcNow.AddSeconds(2));
        File.GetLastWriteTimeUtc(second).ShouldBeInRange(started.AddSeconds(-2), DateTime.UtcNow.AddSeconds(2));
        warm.Result.ProjectStateAfterBuild!.GetPropertyValue("Content").ShouldBe("a");
        warm.Result.ProjectStateAfterBuild.GetPropertyValue("EmptyLength").ShouldBe("0");
        var produced = warm.Result.ProjectStateAfterBuild.GetItems("Produced").ToArray();
        produced.Select(item => Path.GetFullPath(item.EvaluatedInclude)).ShouldBe([first, second]);
        produced.Select(item => item.GetMetadataValue("Kind")).ShouldBe(emptyFirst ? ["empty", "content"] : ["content", "empty"]);
    }

    [ConditionalFact(typeof(TaskCache_Tests), nameof(IsSupported))]
    public void ConcurrentWorkerProcessesPublishCompleteEntries()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string barrier = env.CreateFolder().Path;
        string xml = ProjectText.Replace("<DeclaredIOEchoTask Sources=", "<DeclaredIOEchoTask BarrierDirectory=\"$(BarrierDirectory)\" Sources=");
        string[] projects = [CreateProject(env, xml), CreateProject(env, xml)];
        Dictionary<string, string?> properties = new() { ["BarrierDirectory"] = barrier };
        MockLogger logger = new(_output);
        using (BuildManager manager = new())
        {
            manager.BeginBuild(new BuildParameters
            {
                TaskCache = true,
                BuildCacheDirectory = _cacheDirectory!,
                DisableInProcNode = true,
                EnableNodeReuse = false,
                MaxNodeCount = 2,
                Loggers = [logger],
            });
            List<BuildSubmission> submissions = [];
            try
            {
                foreach (string project in projects)
                {
                    File.Delete(Path.Combine(Path.GetDirectoryName(project)!, "output.txt"));
                    BuildSubmission submission = manager.PendBuildRequest(new BuildRequestData(
                        project, properties, null, ["Build"], null));
                    submissions.Add(submission);
                    submission.ExecuteAsync(null, null);
                }
            }
            finally
            {
                manager.EndBuild();
            }

            foreach (BuildSubmission submission in submissions)
            {
                submission.BuildResult.ShouldHaveSucceeded();
            }
        }

        logger.Warnings.ShouldBeEmpty();
        Directory.GetFiles(barrier).Length.ShouldBe(2);
        // Replay in the coordinator rather than starting a second pair of workers
        // while the first pair is still exiting. Fresh-worker hits are covered by
        // UnconditionalTargetRestoresArtifactsAndReadOnlyRawOutputs.
        foreach (string project in projects)
        {
            File.Delete(Path.Combine(Path.GetDirectoryName(project)!, "output.txt"));
            var restored = Run(project, properties);
            restored.Result.ShouldHaveSucceeded();
            restored.Logger.Warnings.ShouldBeEmpty();
            AssertHit(restored.Logger);
        }
    }

    [ConditionalFact(typeof(TaskCache_Tests), nameof(IsSupported))]
    public void UpToDateTargetDoesNotTouchTaskCache()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string project = CreateProject(env, ProjectText.Replace(
            "<Target Name=\"Build\"", "<Target Name=\"Build\" Inputs=\"a.txt;b.txt\" Outputs=\"output.txt\""));
        Run(project).Result.ShouldHaveSucceeded();
        File.SetLastWriteTimeUtc(Path.Combine(Path.GetDirectoryName(project)!, "output.txt"), DateTime.UtcNow.AddHours(1));
        // Environment configuration is ignored, even for timestamp-skipped targets.
        env.SetEnvironmentVariable("MSBUILD_TASK_CACHE_DIR", env.CreateFile().Path);
        var skipped = Run(project);
        skipped.Result.ShouldHaveSucceeded();
        skipped.Logger.TaskStartedEvents.ShouldBeEmpty();
        skipped.Logger.Warnings.ShouldBeEmpty();
    }

    [ConditionalTheory(typeof(TaskCache_Tests), nameof(IsSupported))]
    [InlineData(false)]
    [InlineData(true)]
    public void UnannotatedTasksExecuteWithoutCacheWarnings(bool incrementalTarget)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string project = CreateProject(env, $"""
            <Project>
              <Target Name="Build" {(incrementalTarget ? "Inputs=\"a.txt\" Outputs=\"output.txt\"" : "")}>
                <Message Text="UNANNOTATED_BUILTINS_EXECUTED" Importance="high" />
                <WriteLinesToFile File="$(MSBuildProjectDirectory)/output.txt" Lines="output" Overwrite="true" />
              </Target>
            </Project>
            """);
        env.SetEnvironmentVariable("MSBUILD_TASK_CACHE_DIR", env.CreateFile().Path);
        var result = Run(project, warningsAsErrors: true);
        result.Result.ShouldHaveSucceeded();
        result.Logger.Warnings.ShouldBeEmpty();
        result.Logger.FullLog.ShouldContain("UNANNOTATED_BUILTINS_EXECUTED");
        File.ReadAllText(Path.Combine(Path.GetDirectoryName(project)!, "output.txt")).ShouldBe("output" + Environment.NewLine);
    }

    [ConditionalFact(typeof(TaskCache_Tests), nameof(IsSupported))]
    public void UnannotatedInputsAreNotSerializedForCaching()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string project = CreateProject(env, $"""
            <Project>
              <UsingTask TaskName="{typeof(UnannotatedValueTask).FullName}" AssemblyFile="{System.Security.SecurityElement.Escape(typeof(UnannotatedValueTask).Assembly.Location)}" />
              <Target Name="Build">
                <UnannotatedValueTask Value="1.5">
                  <Output TaskParameter="Result" PropertyName="Observed" />
                </UnannotatedValueTask>
              </Target>
            </Project>
            """);
        env.SetEnvironmentVariable("MSBUILD_TASK_CACHE_DIR", env.CreateFile().Path);
        var result = Run(project, warningsAsErrors: true);
        result.Result.ShouldHaveSucceeded();
        result.Logger.Warnings.ShouldBeEmpty();
        result.Result.ProjectStateAfterBuild!.GetPropertyValue("Observed").ShouldBe("1.5");
    }

    [ConditionalFact(typeof(TaskCache_Tests), nameof(IsSupported))]
    public void DeclaredInputContentsInvalidateWithPreservedTimestamp()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string project = CreateProject(env);
        Run(project).Result.ShouldHaveSucceeded();
        AssertHit(Run(project).Logger);
        string input = Path.Combine(Path.GetDirectoryName(project)!, "b.txt");
        DateTime timestamp = File.GetLastWriteTimeUtc(input);
        File.WriteAllText(input, "c");
        File.SetLastWriteTimeUtc(input, timestamp);
        var changed = Run(project);
        changed.Result.ShouldHaveSucceeded();
        changed.Logger.FullLog.ShouldContain(DeclaredIOEchoTask.ExecutionMessage);
        File.ReadAllText(Path.Combine(Path.GetDirectoryName(project)!, "output.txt")).ShouldBe("a|c");
    }

    [ConditionalFact(typeof(TaskCache_Tests), nameof(IsSupported))]
    public void BoundItemMetadataInvalidates()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string project = CreateProject(env);
        Dictionary<string, string?> properties = new() { ["Label"] = "one" };
        Run(project, properties).Result.ShouldHaveSucceeded();
        AssertHit(Run(project, properties).Logger);
        properties["Label"] = "two";
        var changed = Run(project, properties);
        changed.Result.ShouldHaveSucceeded();
        changed.Logger.FullLog.ShouldContain(DeclaredIOEchoTask.ExecutionMessage);
        changed.Result.ProjectStateAfterBuild!.GetPropertyValue("Result").ShouldBe("a:two|b:two");
    }

    [Fact]
    public void MetadataEnumerationOrderDoesNotChangeSerialization()
    {
        OrderedMetadataItem first = new(new Dictionary<string, string> { ["A"] = "one", ["B"] = "two" });
        OrderedMetadataItem second = new(new Dictionary<string, string> { ["B"] = "two", ["A"] = "one" });
        TaskInvocationCache.SerializeParameter(first).ShouldBe(TaskInvocationCache.SerializeParameter(second));
        TaskInvocationCache.SerializeParameter(new ITaskItem[] { first }).ShouldBe(TaskInvocationCache.SerializeParameter(new ITaskItem[] { second }));
        TaskInvocationCache.SerializeInputParameter(first).ShouldBe(TaskInvocationCache.SerializeInputParameter(second));
    }

    private sealed class OrderedMetadataItem(Dictionary<string, string> metadata) : ITaskItem
    {
        public string ItemSpec { get; set; } = "input";
        public ICollection MetadataNames => metadata.Keys;
        public int MetadataCount => metadata.Count;
        public string GetMetadata(string name) => metadata.TryGetValue(name, out string? value) ? value : String.Empty;
        public void SetMetadata(string name, string value) => metadata[name] = value;
        public void RemoveMetadata(string name) => metadata.Remove(name);
        public IDictionary CloneCustomMetadata() => new Dictionary<string, string>(metadata);
        public void CopyMetadataTo(ITaskItem destinationItem)
        {
            foreach (var entry in metadata)
            {
                destinationItem.SetMetadata(entry.Key, entry.Value);
            }
        }
    }

    [ConditionalFact(typeof(TaskCache_Tests), nameof(IsSupported))]
    public void DeclaredAbsentInputInvalidatesWhenCreated()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string xml = ProjectText.Replace("<DeclaredIOEchoTask Sources=", "<DeclaredIOEchoTask AllowMissing=\"true\" Sources=")
            .Replace("/b.txt\"", "/missing.txt\"");
        string project = CreateProject(env, xml);
        Run(project).Result.ShouldHaveSucceeded();
        AssertHit(Run(project).Logger);
        File.WriteAllText(Path.Combine(Path.GetDirectoryName(project)!, "missing.txt"), "created");
        var changed = Run(project);
        changed.Result.ShouldHaveSucceeded();
        changed.Logger.FullLog.ShouldContain(DeclaredIOEchoTask.ExecutionMessage);
        changed.Result.ProjectStateAfterBuild!.GetPropertyValue("Result").ShouldBe("a|created");
    }

    [ConditionalFact(typeof(TaskCache_Tests), nameof(IsSupported))]
    public void ChangedInputsAreNotPublished()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string project = CreateProject(env, ProjectText.Replace("<DeclaredIOEchoTask Sources=", "<DeclaredIOEchoTask MutateInput=\"true\" Sources="));
        var result = Run(project);
        result.Result.ShouldHaveSucceeded();
        result.Logger.FullLog.ShouldContain("MSB4291");
        Run(project).Logger.FullLog.ShouldContain(DeclaredIOEchoTask.ExecutionMessage);
    }

    [ConditionalFact(typeof(TaskCache_Tests), nameof(IsSupported))]
    public void OutputGatheringFailuresAreNotPublished()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string project = CreateProject(env, ProjectText.Replace("<DeclaredIOEchoTask Sources=", "<DeclaredIOEchoTask FailOutputGetter=\"true\" Sources="));
        Run(project, allowTaskCrashes: true).Result.ShouldHaveFailed();
        var repeated = Run(project, allowTaskCrashes: true);
        repeated.Result.ShouldHaveFailed();
        repeated.Logger.FullLog.ShouldContain(DeclaredIOEchoTask.ExecutionMessage);
    }

    [ConditionalFact(typeof(TaskCache_Tests), nameof(IsSupported))]
    public void ContinueOnErrorDoesNotPublishFailedTask()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string project = CreateProject(env, ProjectText.Replace("<DeclaredIOEchoTask Sources=",
            "<DeclaredIOEchoTask ContinueOnError=\"true\" FailAfterWriting=\"true\" Sources="));
        Run(project).Result.ShouldHaveSucceeded();
        var repeated = Run(project);
        repeated.Result.ShouldHaveSucceeded();
        repeated.Logger.FullLog.ShouldContain("IO0003");
        repeated.Logger.FullLog.ShouldContain(DeclaredIOEchoTask.ExecutionMessage);
    }

    [ConditionalFact(typeof(TaskCache_Tests), nameof(IsSupported))]
    public void CorruptCacheFailsWithoutExecutingTask()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string project = CreateProject(env);
        Run(project).Result.ShouldHaveSucceeded();
        TaskCacheStore_Tests.CorruptContent(_cacheDirectory!, "a|b");
        var repeated = Run(project);
        repeated.Result.ShouldHaveFailed();
        repeated.Logger.FullLog.ShouldContain("MSB4289");
        repeated.Logger.FullLog.ShouldNotContain(DeclaredIOEchoTask.ExecutionMessage);
        File.ReadAllText(Path.Combine(Path.GetDirectoryName(project)!, "output.txt")).ShouldBe("a|b");
    }

    [ConditionalFact(typeof(TaskCache_Tests), nameof(IsSupported))]
    public void SeparateInvocationsReuseRawOutputsWithCurrentBindings()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string secondInvocation = Invocation.Replace("PropertyName=\"Result\"", "PropertyName=\"SecondResult\"")
            .Replace("ItemName=\"Results\"", "ItemName=\"SecondResults\"");
        string project = CreateProject(env, ProjectText.Replace(Invocation, Invocation + secondInvocation));
        var result = Run(project);
        result.Result.ShouldHaveSucceeded();
        result.Logger.FullLog.Split([DeclaredIOEchoTask.ExecutionMessage], StringSplitOptions.None).Length.ShouldBe(2);
        result.Logger.FullLog.ShouldContain(HitMessage);
        result.Result.ProjectStateAfterBuild!.GetPropertyValue("SecondResult").ShouldBe("a|b");
        result.Result.ProjectStateAfterBuild.GetItems("SecondResults").Count.ShouldBe(1);
    }

    [ConditionalFact(typeof(TaskCache_Tests), nameof(IsSupported))]
    public void DifferentOutputDestinationsAndConditionsAreEvaluatedOnHits()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string xml = ProjectText.Replace("PropertyName=\"Result\"", "PropertyName=\"$(DestinationProperty)\" Condition=\"'$(Gather)' == 'true'\"");
        string project = CreateProject(env, xml);
        Dictionary<string, string?> properties = new() { ["DestinationProperty"] = "First", ["Gather"] = "true" };
        Run(project, properties).Result.ShouldHaveSucceeded();
        properties["DestinationProperty"] = "Second";
        var result = Run(project, properties);
        AssertHit(result.Logger);
        result.Result.ProjectStateAfterBuild!.GetPropertyValue("Second").ShouldBe("a|b");
        result.Result.ProjectStateAfterBuild.GetPropertyValue("First").ShouldBeEmpty();
        properties["Gather"] = "false";
        result = Run(project, properties);
        AssertHit(result.Logger);
        result.Result.ProjectStateAfterBuild!.GetPropertyValue("Second").ShouldBeEmpty();
    }

    [ConditionalFact(typeof(TaskCache_Tests), nameof(IsSupported))]
    public void RequestedOutputCoverageIsPartOfTheKey()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string project = CreateProject(env, ProjectText.Replace(
            "<Output TaskParameter=\"Items\" ItemName=\"Results\" />", ""));
        Run(project).Result.ShouldHaveSucceeded();
        File.WriteAllText(project, ProjectText);
        var result = Run(project);
        result.Result.ShouldHaveSucceeded();
        result.Logger.FullLog.ShouldContain(DeclaredIOEchoTask.ExecutionMessage);
        result.Result.ProjectStateAfterBuild!.GetItems("Results").Count.ShouldBe(1);
        AssertHit(Run(project).Logger);
    }

    [ConditionalFact(typeof(TaskCache_Tests), nameof(IsSupported))]
    public void ConditionalOutputsAreCapturedForLaterTrueBindings()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string project = CreateProject(env, ProjectText.Replace(
            "<Output TaskParameter=\"Items\"", "<Output Condition=\"'$(Gather)' == 'true'\" TaskParameter=\"Items\""));
        Run(project).Result.ShouldHaveSucceeded();
        var repeated = Run(project);
        repeated.Result.ShouldHaveSucceeded();
        AssertHit(repeated.Logger);
        var complete = Run(project, new Dictionary<string, string?> { ["Gather"] = "true" });
        complete.Result.ShouldHaveSucceeded();
        AssertHit(complete.Logger);
        complete.Result.ProjectStateAfterBuild!.GetItems("Results").Count.ShouldBe(1);
        AssertHit(Run(project, new Dictionary<string, string?> { ["Gather"] = "true" }).Logger);
    }

    [ConditionalFact(typeof(TaskCache_Tests), nameof(IsSupported))]
    public void RepeatedBindingsReadEachOutputGetterOnce()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string project = CreateProject(env, ProjectText
            .Replace("<DeclaredIOEchoTask Sources=", "<DeclaredIOEchoTask FailRepeatedOutputReads=\"true\" Sources=")
            .Replace("<Output TaskParameter=\"Result\" PropertyName=\"Result\" />",
                "<Output TaskParameter=\"Result\" PropertyName=\"Result\" /><Output TaskParameter=\"Result\" PropertyName=\"SecondResult\" />"));
        var cold = Run(project);
        cold.Result.ShouldHaveSucceeded();
        cold.Result.ProjectStateAfterBuild!.GetPropertyValue("SecondResult").ShouldBe("a|b");
        var warm = Run(project);
        warm.Result.ShouldHaveSucceeded();
        AssertHit(warm.Logger);
        warm.Result.ProjectStateAfterBuild!.GetPropertyValue("SecondResult").ShouldBe("a|b");
    }

    [ConditionalFact(typeof(TaskCache_Tests), nameof(IsSupported))]
    public void FalseConditionGetterFailureIsFatalAndNotPublished()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string project = CreateProject(env, ProjectText.Replace(
            "<Output TaskParameter=\"Result\"",
            "<Output TaskParameter=\"Unrequested\" PropertyName=\"Unused\" Condition=\"false\" /><Output TaskParameter=\"Result\""));
        Run(project, allowTaskCrashes: true).Result.ShouldHaveFailed();
        var repeated = Run(project, allowTaskCrashes: true);
        repeated.Result.ShouldHaveFailed();
        repeated.Logger.FullLog.ShouldContain(DeclaredIOEchoTask.ExecutionMessage);
    }

    [ConditionalTheory(typeof(TaskCache_Tests), nameof(IsSupported))]
    [InlineData(false)]
    [InlineData(true)]
    public void ProjectPropertiesCannotRedirectBuildOwnedStorage(bool outOfProcess)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string forbidden = env.CreateFile("not-a-cache", "untouched").Path;
        string project = CreateProject(env, ProjectText.Replace("<Project>", """
            <Project TreatAsLocalProperty="MSBuildTaskCacheDirectory">
              <PropertyGroup><MSBuildTaskCacheDirectory>$(OtherDirectory)</MSBuildTaskCacheDirectory></PropertyGroup>
            """));
        Dictionary<string, string?> properties = new()
        {
            ["OtherDirectory"] = forbidden,
            ["MSBuildTaskCacheDirectory"] = forbidden,
        };
        env.SetEnvironmentVariable("MSBuildTaskCacheDirectory", forbidden);
        env.SetEnvironmentVariable("MSBUILD_TASK_CACHE_DIR", forbidden);
        Run(project, properties, outOfProcess: outOfProcess).Result.ShouldHaveSucceeded();
        var restored = Run(project, properties, outOfProcess: outOfProcess);
        restored.Result.ShouldHaveSucceeded();
        AssertHit(restored.Logger);
        File.ReadAllText(forbidden).ShouldBe("untouched");
    }

    [ConditionalTheory(typeof(TaskCache_Tests), nameof(IsSupported))]
    [InlineData(false)]
    [InlineData(true)]
    public void CancelledBuildDrainsAndReleasesOwner(bool outOfProcess)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string marker = Path.Combine(env.CreateFolder().Path, "started");
        string project = CreateProject(env, ProjectText.Replace("<DeclaredIOEchoTask Sources=",
            "<DeclaredIOEchoTask CancellationMarker=\"$(CancellationMarker)\" Sources="));
        MockLogger logger = new(_output);
        using (BuildManager manager = new())
        {
            manager.BeginBuild(new BuildParameters
            {
                TaskCache = true,
                BuildCacheDirectory = _cacheDirectory!,
                DisableInProcNode = outOfProcess,
                EnableNodeReuse = false,
                Loggers = [logger],
            });
            BuildSubmission submission = manager.PendBuildRequest(new BuildRequestData(project,
                new Dictionary<string, string?> { ["CancellationMarker"] = marker }, null, ["Build"], null));
            try
            {
                submission.ExecuteAsync(null, null);
                System.Threading.SpinWait.SpinUntil(() => File.Exists(marker), TimeSpan.FromSeconds(30)).ShouldBeTrue();
                manager.CancelAllSubmissions();
            }
            finally
            {
                manager.EndBuild();
            }
            submission.BuildResult.ShouldHaveFailed();
        }
        var subsequent = Run(project, outOfProcess: outOfProcess);
        subsequent.Result.ShouldHaveSucceeded();
        subsequent.Logger.FullLog.ShouldContain(DeclaredIOEchoTask.ExecutionMessage);
        AssertHit(Run(project, outOfProcess: outOfProcess).Logger);
    }

    [ConditionalFact(typeof(TaskCache_Tests), nameof(IsSupported))]
    public void TaskBatchesAreCachedIndependently()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string xml = ProjectText.Replace("Destination=\"$(MSBuildProjectDirectory)/output.txt\"",
            "Destination=\"$(MSBuildProjectDirectory)/%(Source.Filename).out\"");
        string project = CreateProject(env, xml);
        var first = Run(project);
        first.Result.ShouldHaveSucceeded();
        first.Logger.TaskStartedEvents.Count.ShouldBe(2);
        var second = Run(project);
        second.Result.ShouldHaveSucceeded();
        AssertHit(second.Logger);
        second.Logger.TaskStartedEvents.Count.ShouldBe(2);
        File.ReadAllText(Path.Combine(Path.GetDirectoryName(project)!, "a.out")).ShouldBe("a");
        File.ReadAllText(Path.Combine(Path.GetDirectoryName(project)!, "b.out")).ShouldBe("b");
    }

    [ConditionalFact(typeof(TaskCache_Tests), nameof(IsSupported))]
    public void TaskCanRejectNondeterministicModeWithoutReusingPriorSuccess()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string project = CreateProject(env);
        Run(project).Result.ShouldHaveSucceeded();
        var rejected = Run(project, new Dictionary<string, string?> { ["Deterministic"] = "false" });
        rejected.Result.ShouldHaveFailed();
        rejected.Logger.FullLog.ShouldContain("IO0002");
        rejected.Logger.FullLog.ShouldContain(DeclaredIOEchoTask.ExecutionMessage);
    }

    [ConditionalFact(typeof(TaskCache_Tests), nameof(IsSupported))]
    public void EnvironmentChangesCannotBypassTaskValidation()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        env.SetEnvironmentVariable(DeclaredIOEchoTask.UnsupportedEnvironmentVariable, null);
        string project = CreateProject(env);
        Run(project).Result.ShouldHaveSucceeded();
        AssertHit(Run(project).Logger);
        env.SetEnvironmentVariable(DeclaredIOEchoTask.UnsupportedEnvironmentVariable, "true");
        var rejected = Run(project);
        rejected.Result.ShouldHaveFailed();
        rejected.Logger.FullLog.ShouldContain("IO0004");
        rejected.Logger.FullLog.ShouldContain(DeclaredIOEchoTask.ExecutionMessage);
    }

    [ConditionalTheory(typeof(TaskCache_Tests), nameof(IsSupported))]
    [InlineData("MSBuildTaskCacheEnabled=\"false\"")]
    [InlineData("")]
    public void OptionalModeFalseOrUnboundOptsOutOfCaching(string mode)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string project = CreateProject(env, ProjectText.Replace("MSBuildTaskCacheEnabled=\"$(MSBuildTaskCacheEnabled)\"", mode));
        Run(project).Result.ShouldHaveSucceeded();
        var second = Run(project);
        second.Result.ShouldHaveSucceeded();
        second.Logger.FullLog.ShouldContain(DeclaredIOEchoTask.ExecutionMessage);
        second.Logger.FullLog.ShouldNotContain(HitMessage);
        second.Logger.FullLog.ShouldContain("MSB4287");
    }

    [ConditionalFact(typeof(TaskCache_Tests), nameof(IsSupported))]
    public void OptionalModeOptOutDoesNotOverwriteParticipatingEntry()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string project = CreateProject(env);
        Run(project).Result.ShouldHaveSucceeded();
        AssertHit(Run(project).Logger);
        File.WriteAllText(project, ProjectText.Replace("MSBuildTaskCacheEnabled=\"$(MSBuildTaskCacheEnabled)\"", "MSBuildTaskCacheEnabled=\"false\""));
        var changed = Run(project);
        changed.Result.ShouldHaveSucceeded();
        changed.Logger.FullLog.ShouldContain(DeclaredIOEchoTask.ExecutionMessage);
        Run(project).Logger.FullLog.ShouldContain(DeclaredIOEchoTask.ExecutionMessage);
        File.WriteAllText(project, ProjectText);
        AssertHit(Run(project).Logger);
    }

    [ConditionalFact(typeof(TaskCache_Tests), nameof(IsSupported))]
    public void AnnotatedTaskWithoutModePropertyIsReused()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string xml = $"""
            <Project>
              <UsingTask TaskName="{typeof(DeclaredIONoModeTask).FullName}" AssemblyFile="{System.Security.SecurityElement.Escape(typeof(DeclaredIONoModeTask).Assembly.Location)}" />
              <Target Name="Build">
                <DeclaredIONoModeTask Source="a.txt" Destination="output.txt">
                  <Output TaskParameter="Result" PropertyName="Result" />
                </DeclaredIONoModeTask>
              </Target>
            </Project>
            """;
        string project = CreateProject(env, xml);
        var first = Run(project);
        first.Result.ShouldHaveSucceeded();
        first.Logger.FullLog.ShouldContain(DeclaredIONoModeTask.ExecutionMessage);
        File.Delete(Path.Combine(Path.GetDirectoryName(project)!, "output.txt"));
        var second = Run(project);
        second.Result.ShouldHaveSucceeded();
        second.Logger.Warnings.ShouldBeEmpty();
        second.Logger.FullLog.ShouldContain(ResourceUtilities.FormatResourceStringIgnoreCodeAndKeyword(
            "TaskCache.Hit", nameof(DeclaredIONoModeTask)));
        second.Logger.FullLog.ShouldNotContain(DeclaredIONoModeTask.ExecutionMessage);
        second.Result.ProjectStateAfterBuild!.GetPropertyValue("Result").ShouldBe("a");
        File.ReadAllText(Path.Combine(Path.GetDirectoryName(project)!, "output.txt")).ShouldBe("a");
    }

    [ConditionalFact(typeof(TaskCache_Tests), nameof(IsSupported))]
    public void RelativePathsUseTaskEnvironmentRatherThanProcessDirectory()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string taskDirectory = env.CreateFolder().Path;
        string processDirectory = env.CreateFolder().Path;
        env.SetCurrentDirectory(processDirectory);
        TaskEnvironment environment = TaskEnvironment.CreateWithProjectDirectoryAndEnvironment(taskDirectory);
        TaskInvocationCache.NormalizePath("./input.txt", environment).ShouldBe(Path.Combine(taskDirectory, "input.txt"));
        TaskInvocationCache.NormalizePath("nested/../output.txt", environment).ShouldBe(Path.Combine(taskDirectory, "output.txt"));
    }

    [ConditionalFact(typeof(TaskCache_Tests), nameof(IsSupported))]
    public void MissingDeclaredBindingIsNotGuessedFromTaskDefaults()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string project = CreateProject(env, ProjectText.Replace("OptionalOutput=\"\"", ""));
        var result = Run(project);
        result.Result.ShouldHaveSucceeded();
        result.Logger.FullLog.ShouldContain("MSB4287");
        result.Logger.FullLog.ShouldContain(nameof(DeclaredIOEchoTask.OptionalOutput));
        Run(project).Logger.FullLog.ShouldContain(DeclaredIOEchoTask.ExecutionMessage);
    }

    [ConditionalFact(typeof(TaskCache_Tests), nameof(IsSupported))]
    public void DifferentParametersResolvingToTheSameFileAreRejected()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string project = CreateProject(env, ProjectText.Replace("Destination=\"$(MSBuildProjectDirectory)/output.txt\"",
            "Destination=\"$(MSBuildProjectDirectory)/./a.txt\""));
        var result = Run(project);
        result.Result.ShouldHaveSucceeded();
        result.Logger.FullLog.ShouldContain("MSB4287");
        Run(project).Logger.FullLog.ShouldContain(DeclaredIOEchoTask.ExecutionMessage);
    }

    [ConditionalTheory(typeof(TaskCache_Tests), nameof(IsSupported))]
    [InlineData(false)]
    [InlineData(true)]
    public void WarningsArePreservedAcrossWarningPolicyChanges(bool outOfProcess)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string project = CreateProject(env);
        Dictionary<string, string?> properties = new() { ["IssueWarning"] = "true" };
        Run(project, properties, warningsAsMessages: true, outOfProcess: outOfProcess).Result.ShouldHaveSucceeded();
        var rejected = Run(project, properties, warningsAsErrors: true, outOfProcess: outOfProcess);
        rejected.Result.ShouldHaveFailed();
        rejected.Logger.FullLog.ShouldContain("IO0001");
        AssertHit(rejected.Logger);
        rejected.Logger.Errors.Count(e => e.Code == "IO0001").ShouldBe(1);
        var overridden = Run(project, properties, warningsAsErrors: true, warningsNotAsErrors: true, outOfProcess: outOfProcess);
        overridden.Result.ShouldHaveSucceeded();
        AssertHit(overridden.Logger);
        overridden.Logger.Warnings.Count(e => e.Code == "IO0001").ShouldBe(1);
    }

    [ConditionalTheory(typeof(TaskCache_Tests), nameof(IsSupported))]
    [InlineData(false)]
    [InlineData(true)]
    public void SuccessfulTaskWithPromotedColdWarningCanReplayDemoted(bool outOfProcess)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string project = CreateProject(env);
        Dictionary<string, string?> properties = new() { ["IssueWarning"] = "true" };
        Run(project, properties, warningsAsErrors: true, outOfProcess: outOfProcess).Result.ShouldHaveFailed();
        var warm = Run(project, properties, warningsAsMessages: true, outOfProcess: outOfProcess);
        warm.Result.ShouldHaveSucceeded();
        AssertHit(warm.Logger);
        warm.Logger.Warnings.ShouldBeEmpty();
        warm.Logger.Errors.ShouldBeEmpty();
        warm.Logger.AllBuildEvents.Count(e => e is BuildMessageEventArgs && e.Message == "Declared I/O diagnostic.").ShouldBe(1);
    }

    [ConditionalTheory(typeof(TaskCache_Tests), nameof(IsSupported))]
    [InlineData(false)]
    [InlineData(true)]
    public void WarningReplayPreservesOrderFieldsAndCurrentBinlogContext(bool outOfProcess)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string project = CreateProject(env, ProjectText.Replace("<DeclaredIOEchoTask Sources=", "<DeclaredIOEchoTask WarningPhase=\"multiple\" Sources="));
        var cold = Run(project, outOfProcess: outOfProcess);
        cold.Result.ShouldHaveSucceeded();
        string binlog = Path.Combine(env.CreateFolder().Path, "warnings.binlog");
        var warm = Run(project, outOfProcess: outOfProcess, binlog: binlog);
        warm.Result.ShouldHaveSucceeded();
        AssertHit(warm.Logger);
        warm.Logger.Warnings.Select(w => w.Code).ShouldBe(["TCW01", "TCW02"]);
        for (int i = 0; i < 2; i++)
        {
            var warning = warm.Logger.Warnings[i];
            warning.Message.ShouldBe("warning {literal}");
            warning.File.ShouldBe("reported.cs");
            warning.Subcategory.ShouldBe("cache");
            warning.LineNumber.ShouldBe(12);
            warning.ColumnNumber.ShouldBe(3);
            warning.EndLineNumber.ShouldBe(14);
            warning.EndColumnNumber.ShouldBe(5);
            warning.HelpKeyword.ShouldBe("Task.Warning");
            warning.HelpLink.ShouldBe("https://example.invalid/task-warning");
            warning.SenderName.ShouldBe(nameof(DeclaredIOEchoTask));
            warning.BuildEventContext.ShouldBe(warm.Logger.TaskStartedEvents.Single(e => e.TaskName == nameof(DeclaredIOEchoTask)).BuildEventContext);
        }
        List<BuildWarningEventArgs> replayed = [];
        List<TaskStartedEventArgs> started = [];
        List<TaskFinishedEventArgs> finished = [];
        BinaryLogReplayEventSource replay = new();
        replay.WarningRaised += (_, e) => replayed.Add(e);
        replay.TaskStarted += (_, e) => started.Add(e);
        replay.TaskFinished += (_, e) => finished.Add(e);
        replay.Replay(binlog);
        replayed.Select(w => w.Code).ShouldBe(["TCW01", "TCW02"]);
        replayed[0].Message.ShouldBe(warm.Logger.Warnings[0].Message);
        replayed[0].BuildEventContext.ShouldBe(started.Single(e => e.TaskName == nameof(DeclaredIOEchoTask)).BuildEventContext);
        finished.Single(e => e.TaskName == nameof(DeclaredIOEchoTask)).Succeeded.ShouldBeTrue();
    }

    [ConditionalFact(typeof(TaskCache_Tests), nameof(IsSupported))]
    public void FalseConditionOutputGetterWarningIsCapturedOnceAndReplayed()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string project = CreateProject(env, ProjectText
            .Replace("<DeclaredIOEchoTask Sources=", "<DeclaredIOEchoTask WarningPhase=\"getter\" Sources=")
            .Replace("<Output TaskParameter=\"Result\" PropertyName=\"Result\" />",
                "<Output TaskParameter=\"Result\" PropertyName=\"Result\" Condition=\"'$(Gather)' == 'true'\" /><Output TaskParameter=\"Result\" PropertyName=\"Second\" Condition=\"'$(Gather)' == 'true'\" />"));
        var cold = Run(project);
        cold.Result.ShouldHaveSucceeded();
        cold.Logger.Warnings.Count.ShouldBe(1);
        var warm = Run(project, new Dictionary<string, string?> { ["Gather"] = "true" });
        warm.Result.ShouldHaveSucceeded();
        AssertHit(warm.Logger);
        warm.Logger.Warnings.Count.ShouldBe(1);
        warm.Result.ProjectStateAfterBuild!.GetPropertyValue("Second").ShouldBe("a|b");
    }

    [ConditionalTheory(typeof(TaskCache_Tests), nameof(IsSupported))]
    [InlineData("setup")]
    [InlineData("contract")]
    [InlineData("extended")]
    public void LifecycleAndUnsupportedWarningsRemainNoncacheable(string phase)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string project = CreateProject(env, ProjectText.Replace("<DeclaredIOEchoTask Sources=", $"<DeclaredIOEchoTask WarningPhase=\"{phase}\" Sources="));
        Run(project).Result.ShouldHaveSucceeded();
        var repeated = Run(project);
        repeated.Result.ShouldHaveSucceeded();
        repeated.Logger.FullLog.ShouldContain(DeclaredIOEchoTask.ExecutionMessage);
        repeated.Logger.Warnings.Count.ShouldBe(1);
    }

    [ConditionalFact(typeof(TaskCache_Tests), nameof(IsSupported))]
    public void FilteredWarningSubclassCannotBecomeAReusableReplacementWarning()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string project = CreateProject(env, ProjectText.Replace("<DeclaredIOEchoTask Sources=",
            "<DeclaredIOEchoTask WarningPhase=\"nonserializable\" Sources="));

        var first = BuildWithNodes(2, false);
        first.Result.ShouldHaveSucceeded();
        var second = BuildWithNodes(1, true);
        second.Result.ShouldHaveFailed();
        second.Logger.FullLog.ShouldContain("TCW01");
        second.Logger.FullLog.ShouldContain(DeclaredIOEchoTask.ExecutionMessage);
        second.Logger.FullLog.ShouldNotContain(HitMessage);

        (BuildResult Result, MockLogger Logger) BuildWithNodes(int nodes, bool promote)
        {
            MockLogger logger = new(_output);
            using BuildManager manager = new();
            BuildParameters parameters = new()
            {
                TaskCache = true,
                BuildCacheDirectory = _cacheDirectory!,
                MaxNodeCount = nodes,
                EnableNodeReuse = false,
                Loggers = [logger],
                WarningsAsErrors = promote ? new HashSet<string> { "TCW01" } : null,
            };
            return (manager.Build(parameters, new BuildRequestData(project, new Dictionary<string, string?>(), null, ["Build"], null)), logger);
        }
    }

#if NET
    [ConditionalFact(typeof(TaskCacheStore_Tests), nameof(TaskCacheStore_Tests.IsSupportedOnUnix))]
    public void ContractGetterWarningAvoidsInputHashingAndLookup()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string project = CreateProject(env, ProjectText.Replace("<DeclaredIOEchoTask Sources=", "<DeclaredIOEchoTask WarningPhase=\"contract\" Sources="));
        string source = Path.Combine(Path.GetDirectoryName(project)!, "a.txt");
        string actual = Path.Combine(Path.GetDirectoryName(project)!, "actual.txt");
        File.Move(source, actual);
        File.CreateSymbolicLink(source, actual);
        var result = Run(project);
        result.Result.ShouldHaveSucceeded();
        result.Logger.Warnings.Count.ShouldBe(1);
        result.Logger.FullLog.ShouldContain(DeclaredIOEchoTask.ExecutionMessage);
    }
#endif

    [ConditionalTheory(typeof(TaskCache_Tests), nameof(IsSupported))]
    [InlineData(false)]
    [InlineData(true)]
    public void InitializationWarningIsLoggedOnceAndPreventsCacheParticipation(bool outOfProcess)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string project = CreateProject(env, $"""
            <Project>
              <UsingTask TaskName="{typeof(DeclaredIOInitializationWarningTask).FullName}" AssemblyFile="{typeof(DeclaredIOInitializationWarningTask).Assembly.Location}" />
              <Target Name="Build">
                <DeclaredIOInitializationWarningTask Source="$(MSBuildProjectDirectory)/a.txt" Destination="$(MSBuildProjectDirectory)/output.txt" />
              </Target>
            </Project>
            """);
        Run(project, outOfProcess: outOfProcess).Result.ShouldHaveSucceeded();
        var repeated = Run(project, outOfProcess: outOfProcess);
        repeated.Result.ShouldHaveSucceeded();
        repeated.Logger.Warnings.Count.ShouldBe(1);
        repeated.Logger.FullLog.ShouldContain("INIT_WARNING_TASK_EXECUTED");
        repeated.Logger.FullLog.ShouldNotContain("Task-cache hit");
    }

    [ConditionalTheory(typeof(TaskCache_Tests), nameof(IsSupported))]
    [InlineData("error", false)]
    [InlineData("error", true)]
    [InlineData("false", false)]
    [InlineData("throw", false)]
    public void ContinueOnErrorDoesNotCacheRawErrorsOrFailures(string phase, bool outOfProcess)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string project = CreateProject(env, ProjectText.Replace("<DeclaredIOEchoTask Sources=",
            $"<DeclaredIOEchoTask WarningPhase=\"{phase}\" ContinueOnError=\"true\" Sources="));
        Run(project, allowTaskCrashes: true, outOfProcess: outOfProcess).Result.ShouldHaveSucceeded();
        var repeated = Run(project, allowTaskCrashes: true, outOfProcess: outOfProcess);
        repeated.Result.ShouldHaveSucceeded();
        repeated.Logger.FullLog.ShouldContain(DeclaredIOEchoTask.ExecutionMessage);
        repeated.Logger.FullLog.ShouldNotContain(HitMessage);
    }

    [ConditionalFact(typeof(TaskCache_Tests), nameof(IsSupported))]
    public void NoncacheableRawErrorsDoNotReadUnusedOutputGetters()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string project = CreateProject(env, ProjectText
            .Replace("<DeclaredIOEchoTask Sources=", "<DeclaredIOEchoTask WarningPhase=\"error\" ContinueOnError=\"true\" Sources=")
            .Replace("<Output TaskParameter=\"Result\"", "<Output TaskParameter=\"Unrequested\" PropertyName=\"Unused\" Condition=\"false\" /><Output TaskParameter=\"Result\""));
        Run(project).Result.ShouldHaveSucceeded();
        var repeated = Run(project);
        repeated.Result.ShouldHaveSucceeded();
        repeated.Logger.FullLog.ShouldContain(DeclaredIOEchoTask.ExecutionMessage);
        repeated.Logger.Errors.ShouldBeEmpty();
    }

    [ConditionalTheory(typeof(TaskCache_Tests), nameof(IsSupported))]
    [InlineData(true)]
    [InlineData(false)]
    public void ModeIsEngineOwnedOnlyWhenCachingIsEnabled(bool enabled)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string project = CreateProject(env);
        var result = Run(project, new Dictionary<string, string?> { ["MSBuildTaskCacheEnabled"] = enabled ? "false" : "true" }, taskCache: enabled);
        result.Result.ShouldHaveSucceeded();
        result.Result.ProjectStateAfterBuild!.GetPropertyValue("ModeAtEvaluation").ShouldBe("true");
    }

    private static string HitMessage => ResourceUtilities.FormatResourceStringIgnoreCodeAndKeyword("TaskCache.Hit", nameof(DeclaredIOEchoTask));

    private static void AssertHit(MockLogger logger)
    {
        logger.FullLog.ShouldContain(HitMessage);
        logger.FullLog.ShouldNotContain(DeclaredIOEchoTask.ExecutionMessage);
    }

    private string CreateProject(TestEnvironment env, string? xml = null)
    {
        var folder = env.CreateFolder();
        _cacheDirectory ??= env.CreateFolder().Path;
        env.CreateFile(folder, "a.txt", "a");
        env.CreateFile(folder, "b.txt", "b");
        return env.CreateFile(folder, "declared.proj", xml ?? ProjectText).Path;
    }

    private (BuildResult Result, MockLogger Logger) Run(
        string project,
        IDictionary<string, string?>? properties = null,
        bool outOfProcess = false,
        bool warningsAsErrors = false,
        bool warningsAsMessages = false,
        bool taskCache = true,
        bool allowTaskCrashes = false,
        bool warningsNotAsErrors = false,
        string? binlog = null)
    {
        MockLogger logger = new(_output) { AllowTaskCrashes = allowTaskCrashes };
        using BuildManager manager = new();
        BuildParameters parameters = new()
        {
            TaskCache = taskCache,
            BuildCacheDirectory = _cacheDirectory!,
            Loggers = binlog is null ? [logger] : [logger, new BinaryLogger { Parameters = binlog }],
            DisableInProcNode = outOfProcess,
            EnableNodeReuse = false,
            WarningsAsErrors = warningsAsErrors ? new HashSet<string>() : null,
            WarningsNotAsErrors = warningsNotAsErrors ? new HashSet<string> { "IO0001" } : null,
            WarningsAsMessages = warningsAsMessages ? new HashSet<string> { "IO0001" } : null
        };
        BuildRequestData request = new(project, properties ?? new Dictionary<string, string?>(), null,
            ["Build"], null, BuildRequestDataFlags.ProvideProjectStateAfterBuild);
        return (manager.Build(parameters, request), logger);
    }
}

[MSBuildDeclaredIOTask]
[MSBuildDeclaredIOInput(nameof(Source))]
[MSBuildDeclaredIOOutput(nameof(First))]
[MSBuildDeclaredIOOutput(nameof(Second))]
public sealed class TaskCacheEmptyOutputTask : ITask
{
    internal const string ExecutionMessage = "EMPTY_OUTPUT_TASK_EXECUTED";
    internal static readonly DateTime OriginalTimestamp = new(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    public IBuildEngine BuildEngine { get; set; } = null!;
    public ITaskHost HostObject { get; set; } = null!;
    public string Source { get; set; } = String.Empty;
    public string First { get; set; } = String.Empty;
    public string Second { get; set; } = String.Empty;
    public bool EmptyFirst { get; set; }
    [Output]
    public string Content { get; private set; } = String.Empty;
    [Output]
    public int EmptyLength { get; private set; }
    [Output]
    public ITaskItem[] Produced { get; private set; } = [];

    public bool Execute()
    {
        BuildEngine.LogMessageEvent(new BuildMessageEventArgs(ExecutionMessage, null, nameof(TaskCacheEmptyOutputTask), MessageImportance.High));
        Content = File.ReadAllText(Source);
        File.WriteAllText(EmptyFirst ? Second : First, Content);
        string empty = EmptyFirst ? First : Second;
        File.WriteAllBytes(empty, []);
        EmptyLength = checked((int)new FileInfo(empty).Length);
        File.SetLastWriteTimeUtc(First, OriginalTimestamp);
        File.SetLastWriteTimeUtc(Second, OriginalTimestamp);
        ITaskItem first = new Microsoft.Build.Utilities.TaskItem(First);
        ITaskItem second = new Microsoft.Build.Utilities.TaskItem(Second);
        first.SetMetadata("Kind", EmptyFirst ? "empty" : "content");
        second.SetMetadata("Kind", EmptyFirst ? "content" : "empty");
        Produced = [first, second];
        return true;
    }
}

[MSBuildDeclaredIOTask]
public sealed class TaskCacheStructItemTask : ITask
{
    internal const string ExecutionMessage = "STRUCT_ITEM_ARRAY_TASK_EXECUTED";
    public IBuildEngine BuildEngine { get; set; } = null!;
    public ITaskHost HostObject { get; set; } = null!;
    public TaskItem<int>[] Values { get; set; } = [];
    [Output]
    public TaskItem<int>[] Results { get; private set; } = [];
    [Output]
    public int Sum { get; private set; }

    public bool Execute()
    {
        BuildEngine.LogMessageEvent(new BuildMessageEventArgs(ExecutionMessage, null, nameof(TaskCacheStructItemTask), MessageImportance.High));
        Results = new TaskItem<int>[Values.Length];
        for (int i = 0; i < Values.Length; i++)
        {
            Sum += Values[i].Value;
            Results[i] = new TaskItem<int>(Values[i].Value);
            Values[i].CopyMetadataTo(Results[i]);
        }
        return true;
    }
}

[MSBuildDeclaredIOTask]
[MSBuildDeclaredIOInput(nameof(Sources))]
[MSBuildDeclaredIOOutput(nameof(Destination))]
[MSBuildDeclaredIOOutput(nameof(OptionalOutput))]
public sealed class DeclaredIOEchoTask : ICancelableTask
{
    internal const string ExecutionMessage = "DECLARED_IO_TASK_EXECUTED";
    internal const string UnsupportedEnvironmentVariable = "MSBUILD_TASK_CACHE_TEST_UNSUPPORTED";
    private string _result = String.Empty;

    public IBuildEngine BuildEngine { get; set; } = null!;
    public ITaskHost HostObject { get; set; } = null!;
    private ITaskItem[] _sources = [];
    public ITaskItem[] Sources
    {
        get
        {
            if (WarningPhase == "contract")
            {
                Warn("TCW01");
            }
            return _sources;
        }
        set => _sources = value;
    }
    public string Destination { get; set; } = String.Empty;
    public string OptionalOutput { get; set; } = String.Empty;
    public bool MSBuildTaskCacheEnabled { get; set; }
    public bool Deterministic { get; set; }
    public bool IssueWarning { get; set; }
    private string _warningPhase = String.Empty;
    public string WarningPhase
    {
        get => _warningPhase;
        set
        {
            _warningPhase = value;
            if (value == "setup")
            {
                Warn("TCW01");
            }
        }
    }
    public bool AllowMissing { get; set; }
    public bool MutateInput { get; set; }
    public bool FailOutputGetter { get; set; }
    public bool FailRepeatedOutputReads { get; set; }
    public string BarrierDirectory { get; set; } = String.Empty;
    public string CancellationMarker { get; set; } = String.Empty;
    private volatile bool _cancelled;
    private int _outputReads;
    public bool FailAfterWriting { get; set; }
    [Output]
    public string Result
    {
        get
        {
            if (WarningPhase == "getter")
            {
                Warn("TCW01");
            }
            return FailOutputGetter || (FailRepeatedOutputReads && ++_outputReads > 1)
                ? throw new InvalidOperationException("Output getter failed.") : _result;
        }
    }
    [Output]
    public ITaskItem[] Items
    {
        get
        {
            if (_result.Length == 0)
            {
                return [];
            }

            Microsoft.Build.Utilities.TaskItem item = new(_result);
            item.SetMetadata("Metadata", "raw;metadata%value");
            return [item];
        }
    }
    [Output]
    public string Unrequested => throw new InvalidOperationException("This output was not requested.");

    public bool Execute()
    {
        if (CancellationMarker.Length > 0)
        {
            File.WriteAllText(CancellationMarker, "started");
            System.Threading.SpinWait.SpinUntil(() => _cancelled, TimeSpan.FromSeconds(30));
            return false;
        }

        if (BarrierDirectory.Length > 0)
        {
            using var process = System.Diagnostics.Process.GetCurrentProcess();
            File.WriteAllText(Path.Combine(BarrierDirectory, process.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)), "executing");
            var watch = System.Diagnostics.Stopwatch.StartNew();
            while (Directory.GetFiles(BarrierDirectory).Length < 2)
            {
                if (watch.Elapsed > TimeSpan.FromSeconds(30))
                {
                    throw new TimeoutException("Two worker tasks did not execute concurrently.");
                }
                System.Threading.Thread.Sleep(10);
            }
        }

        BuildEngine.LogMessageEvent(new BuildMessageEventArgs(ExecutionMessage, null, nameof(DeclaredIOEchoTask), MessageImportance.High));
        if (MSBuildTaskCacheEnabled && Environment.GetEnvironmentVariable(UnsupportedEnvironmentVariable) is not null)
        {
            BuildEngine.LogErrorEvent(new BuildErrorEventArgs(null, "IO0004", null, 0, 0, 0, 0, "The task does not support this environment setting.", null, nameof(DeclaredIOEchoTask)));
            return false;
        }

        if (MSBuildTaskCacheEnabled && !Deterministic)
        {
            BuildEngine.LogErrorEvent(new BuildErrorEventArgs(null, "IO0002", null, 0, 0, 0, 0, "Deterministic execution is required.", null, nameof(DeclaredIOEchoTask)));
            return false;
        }

        string[] values = new string[_sources.Length];
        for (int i = 0; i < _sources.Length; i++)
        {
            values[i] = AllowMissing && !File.Exists(_sources[i].ItemSpec) ? "absent" : File.ReadAllText(_sources[i].ItemSpec);
            string label = _sources[i].GetMetadata("Label");
            if (label.Length > 0)
            {
                values[i] += ":" + label;
            }
        }

        _result = String.Join("|", values);
        File.WriteAllText(Destination, _result);
        if (OptionalOutput.Length > 0)
        {
            File.WriteAllText(OptionalOutput, _result);
        }

        if (IssueWarning)
        {
            BuildEngine.LogWarningEvent(new BuildWarningEventArgs(null, "IO0001", null, 0, 0, 0, 0, "Declared I/O diagnostic.", null, nameof(DeclaredIOEchoTask)));
        }
        if (WarningPhase is "multiple" or "false" or "throw")
        {
            Warn("TCW01");
            if (WarningPhase == "multiple")
            {
                Warn("TCW02");
            }
            if (WarningPhase == "false")
            {
                return false;
            }
            if (WarningPhase == "throw")
            {
                throw new InvalidOperationException("Task failed after warning.");
            }
        }
        if (WarningPhase == "error")
        {
            BuildEngine.LogErrorEvent(new BuildErrorEventArgs(null, "TCE01", "reported.cs", 1, 1, 1, 1, "raw error", null, nameof(DeclaredIOEchoTask)));
        }
        if (WarningPhase == "extended")
        {
            BuildEngine.LogWarningEvent(new ExtendedBuildWarningEventArgs("custom", null, "TCW01", "reported.cs",
                1, 1, 1, 1, "extended", null, nameof(DeclaredIOEchoTask), null, DateTime.UtcNow, messageArgs: null));
        }
        if (WarningPhase == "nonserializable")
        {
            BuildEngine.LogWarningEvent(new TaskCacheNonserializableWarning());
        }

        if (MutateInput)
        {
            File.AppendAllText(Sources[0].ItemSpec, "changed");
        }

        if (FailAfterWriting)
        {
            BuildEngine.LogErrorEvent(new BuildErrorEventArgs(null, "IO0003", null, 0, 0, 0, 0, "Task failed after writing outputs.", null, nameof(DeclaredIOEchoTask)));
            return false;
        }

        return true;
    }

    public void Cancel() => _cancelled = true;

    public sealed class TaskCacheNonserializableWarning() : BuildWarningEventArgs(
        null, "TCW01", "reported.cs", 1, 1, 1, 1, "custom warning", null, "cache test");

    private void Warn(string code) => BuildEngine.LogWarningEvent(new BuildWarningEventArgs("cache", code, "reported.cs",
        12, 3, 14, 5, "warning {literal}", "Task.Warning", nameof(DeclaredIOEchoTask),
        "https://example.invalid/task-warning", DateTime.UtcNow, messageArgs: null));
}

[MSBuildDeclaredIOTask]
[MSBuildDeclaredIOInput(nameof(Source))]
[MSBuildDeclaredIOOutput(nameof(Destination))]
public sealed class DeclaredIOInitializationWarningTask : ITask
{
    private IBuildEngine _buildEngine = null!;
    public IBuildEngine BuildEngine
    {
        get => _buildEngine;
        set
        {
            _buildEngine = value;
            value.LogWarningEvent(new BuildWarningEventArgs(null, "INIT01", null, 0, 0, 0, 0, "initialization warning", null, nameof(DeclaredIOInitializationWarningTask)));
        }
    }
    public ITaskHost HostObject { get; set; } = null!;
    public string Source { get; set; } = String.Empty;
    public string Destination { get; set; } = String.Empty;
    public bool Execute()
    {
        BuildEngine.LogMessageEvent(new BuildMessageEventArgs("INIT_WARNING_TASK_EXECUTED", null, nameof(DeclaredIOInitializationWarningTask), MessageImportance.High));
        File.Copy(Source, Destination, overwrite: true);
        return true;
    }
}

[MSBuildDeclaredIOTask]
[MSBuildDeclaredIOInput(nameof(Source))]
[MSBuildDeclaredIOOutput(nameof(Destination))]
public sealed class DeclaredIONoModeTask : ITask
{
    internal const string ExecutionMessage = "DECLARED_IO_NO_MODE_TASK_EXECUTED";
    private string _result = String.Empty;

    public IBuildEngine BuildEngine { get; set; } = null!;
    public ITaskHost HostObject { get; set; } = null!;
    public string Source { get; set; } = String.Empty;
    public string Destination { get; set; } = String.Empty;
    [Output]
    public string Result => _result;

    public bool Execute()
    {
        BuildEngine.LogMessageEvent(new BuildMessageEventArgs(ExecutionMessage, null, nameof(DeclaredIONoModeTask), MessageImportance.High));
        _result = File.ReadAllText(Source);
        File.WriteAllText(Destination, _result);
        return true;
    }
}
