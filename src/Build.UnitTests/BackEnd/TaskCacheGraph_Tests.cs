// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Execution;
using Microsoft.Build.Graph;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests.BackEnd;

public sealed class TaskCacheGraph_Tests(ITestOutputHelper output)
{
    private readonly ITestOutputHelper _output = output;
    private const string CacheMode = "MSBuildTaskCacheEnabled";
    public static bool IsSupported => TaskCacheStore.IsSupported;

    private const string ProjectText = """
        <Project>
          <PropertyGroup><ModeAtEvaluation>$(MSBuildTaskCacheEnabled)</ModeAtEvaluation></PropertyGroup>
          <Target Name="Build" Returns="@(Observed)">
            <ItemGroup>
              <Observed Include="mode=$(ModeAtEvaluation)">
                <LiveMode>$(MSBuildTaskCacheEnabled)</LiveMode>
                <Label>$(Label)</Label>
              </Observed>
            </ItemGroup>
          </Target>
        </Project>
        """;

    [ConditionalTheory(typeof(TaskCacheGraph_Tests), nameof(IsSupported))]
    [InlineData(true, null, "true")]
    [InlineData(true, "false", "true")]
    [InlineData(true, "", "true")]
    [InlineData(false, null, "")]
    [InlineData(false, "caller", "caller")]
    public void FileBasedGraphModeIsAvailableDuringEvaluationWithoutMutatingGlobals(bool enabled, string? suppliedMode, string expectedMode)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        env.SetEnvironmentVariable(CacheMode, null);
        string project = env.CreateFile("mode.proj", ProjectText).Path;
        Dictionary<string, string> globals = new();
        if (suppliedMode is not null)
        {
            globals["msbuildtaskcacheenabled"] = suppliedMode;
        }
        GraphBuildRequestData request = new(project, globals, ["Build"], null);
        GraphBuildResult result = Build(env, request, enabled);
        result.ShouldHaveSucceeded();
        var nodeResult = result.ResultsByNode.Single();
        nodeResult.Value["Build"].Items.Single().ItemSpec.ShouldBe("mode=" + expectedMode);
        nodeResult.Value["Build"].Items.Single().GetMetadata("LiveMode").ShouldBe(expectedMode);
        nodeResult.Key.ProjectInstance.GetPropertyValue(CacheMode).ShouldBe(expectedMode);
        globals.Count.ShouldBe(suppliedMode is null ? 0 : 1);
        if (suppliedMode is not null)
        {
            globals["msbuildtaskcacheenabled"].ShouldBe(suppliedMode);
        }
    }

    [ConditionalFact(typeof(TaskCacheGraph_Tests), nameof(IsSupported))]
    public void EveryEntryPointReceivesModeIncludingNullAndReadOnlyGlobals()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        env.SetEnvironmentVariable(CacheMode, null);
        Dictionary<string, string> original = new()
        {
            [CacheMode] = "false",
            ["Label"] = "caller",
        };
        ReadOnlyDictionary<string, string> globals = new(original);
        ProjectGraphEntryPoint[] entries =
        [
            new(env.CreateFile("first.proj", ProjectText).Path, globals),
            new(env.CreateFile("second.proj", ProjectText).Path, globals),
            new(env.CreateFile("third.proj", ProjectText).Path),
        ];
        GraphBuildResult result = Build(env, new GraphBuildRequestData(entries, ["Build"]), enabled: true);
        result.ShouldHaveSucceeded();
        result.ResultsByNode.Count.ShouldBe(3);
        foreach (var nodeResult in result.ResultsByNode)
        {
            nodeResult.Value["Build"].Items.Single().ItemSpec.ShouldBe("mode=true");
            nodeResult.Key.ProjectInstance.GlobalProperties[CacheMode].ShouldBe("true");
            string expectedLabel = Path.GetFileName(nodeResult.Key.ProjectInstance.FullPath) == "third.proj" ? "" : "caller";
            nodeResult.Value["Build"].Items.Single().GetMetadata("Label").ShouldBe(expectedLabel);
        }
        original.Count.ShouldBe(2);
        original[CacheMode].ShouldBe("false");
        entries[0].GlobalProperties.ShouldBeSameAs(globals);
        entries[1].GlobalProperties.ShouldBeSameAs(globals);
        entries[2].GlobalProperties.ShouldBeNull();
    }

    [ConditionalFact(typeof(TaskCacheGraph_Tests), nameof(IsSupported))]
    public void ReferencedGraphProjectsInheritTheEntryPointMode()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        env.SetEnvironmentVariable(CacheMode, null);
        var folder = env.CreateFolder();
        env.CreateFile(folder, "child.proj", ProjectText);
        string parent = env.CreateFile(folder, "parent.proj", ProjectText.Replace("<Project>", """
            <Project>
              <ItemGroup>
                <ProjectReference Include="child.proj" />
                <ProjectReferenceTargets Include="Build" Targets="Build" />
              </ItemGroup>
            """)).Path;
        GraphBuildResult result = Build(env, new GraphBuildRequestData(parent, new Dictionary<string, string>(), ["Build"], null), enabled: true);
        result.ShouldHaveSucceeded();
        result.ResultsByNode.Count.ShouldBe(2);
        foreach (BuildResult nodeResult in result.ResultsByNode.Values)
        {
            nodeResult["Build"].Items.Single().ItemSpec.ShouldBe("mode=true");
        }
    }

    [ConditionalFact(typeof(TaskCacheGraph_Tests), nameof(IsSupported))]
    public void SuppliedGraphRetainsItsCallerEvaluationWithoutReevaluation()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        env.SetEnvironmentVariable(CacheMode, null);
        string project = env.CreateFile("supplied.proj", ProjectText).Path;
        Dictionary<string, string> globals = new() { [CacheMode] = "caller" };
        ProjectGraph graph = new(project, globals);
        ProjectGraphNode node = graph.EntryPointNodes.Single();
        File.WriteAllText(project, """<Project><Target Name="Build"><Error Text="The supplied graph was reevaluated." /></Target></Project>""");
        GraphBuildResult result = Build(env, new GraphBuildRequestData(graph, ["Build"]), enabled: true);
        result.ShouldHaveSucceeded();
        result.ResultsByNode.Keys.Single().ShouldBeSameAs(node);
        result[node]["Build"].Items.Single().ItemSpec.ShouldBe("mode=caller");
        node.ProjectInstance.GetPropertyValue(CacheMode).ShouldBe("caller");
        globals[CacheMode].ShouldBe("caller");
    }

    private GraphBuildResult Build(TestEnvironment env, GraphBuildRequestData request, bool enabled)
    {
        using BuildManager manager = new();
        BuildParameters parameters = new()
        {
            TaskCache = enabled,
            BuildCacheDirectory = enabled ? env.CreateFolder().Path : env.CreateFile("not-a-cache", "untouched").Path,
            EnableNodeReuse = false,
            Loggers = [new MockLogger(_output)],
        };
        return manager.Build(parameters, request);
    }
}
