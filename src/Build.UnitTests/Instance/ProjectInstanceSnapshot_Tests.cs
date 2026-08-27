// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Definition;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.UnitTests;
using Shouldly;
using Xunit;
using static Microsoft.Build.Engine.UnitTests.TestComparers.ProjectInstanceModelTestComparers;

#nullable enable

namespace Microsoft.Build.UnitTests.OM.Instance;

public sealed class ProjectInstanceSnapshot_Tests
{
    private const string ProjectContents = """
        <Project DefaultTargets="Build" TreatAsLocalProperty="LocalGlobal">
          <UsingTask TaskName="SnapshotCustomTask" AssemblyFile="$(MSBuildToolsPath)\Microsoft.Build.Tasks.Core.dll" />
          <PropertyGroup>
            <Configuration>Debug</Configuration>
          </PropertyGroup>
          <ItemGroup>
            <Compile Include="Program.cs">
              <Visible>true</Visible>
            </Compile>
            <Reference Include="System.Runtime" />
          </ItemGroup>
          <ItemDefinitionGroup>
            <Reference>
              <Private>true</Private>
            </Reference>
          </ItemDefinitionGroup>
          <Target Name="BeforeBuild" BeforeTargets="Build" />
          <Target Name="Build">
            <Message Text="snapshot" Importance="Low" />
            <CreateProperty Value="task-output">
              <Output TaskParameter="Value" PropertyName="TaskOutput" />
            </CreateProperty>
            <PropertyGroup>
              <Built>true</Built>
            </PropertyGroup>
            <ItemGroup>
              <Generated Include="Output.txt">
                <Marker>value</Marker>
              </Generated>
            </ItemGroup>
            <OnError ExecuteTargets="AfterBuild" Condition="'false' == 'true'" />
          </Target>
          <Target Name="AfterBuild" AfterTargets="Build" />
        </Project>
        """;

    [Fact]
    public void Materialize_ReconstructsCompleteBuildState()
    {
        using var collection = new ProjectCollection();
        using var projectFromString = new ProjectRootElementFromString(ProjectContents, collection);
        var source = new ProjectInstance(projectFromString.Project);
        source.TranslateEntireState = true;

        ProjectInstanceSnapshot snapshot = ProjectInstanceSnapshot.Create(source);
        ProjectInstance materialized =
            Materialize(snapshot, collection);

        snapshot.EstimatedRetainedSizeBytes.ShouldBeGreaterThan(0);
        materialized.IsImmutable.ShouldBeFalse();
        new ProjectInstanceComparer().Equals(source, materialized).ShouldBeTrue();
        ((IEvaluatorData<ProjectPropertyInstance, ProjectItemInstance, ProjectMetadataInstance, ProjectItemDefinitionInstance>)materialized)
            .GlobalPropertiesToTreatAsLocal.ShouldContain("LocalGlobal");
    }

    [Fact]
    public void Create_DoesNotChangeSourceTranslationMode()
    {
        using var collection = new ProjectCollection();
        using var projectFromString = new ProjectRootElementFromString(ProjectContents, collection);
        var source = new ProjectInstance(projectFromString.Project);

        source.TranslateEntireState.ShouldBeFalse();
        ProjectInstanceSnapshot minimalSnapshot = ProjectInstanceSnapshot.Create(source);
        source.TranslateEntireState.ShouldBeFalse();
        minimalSnapshot
            .Materialize(new BuildParameters(collection), evaluationId: 1)
            .TranslateEntireState.ShouldBeFalse();

        source.TranslateEntireState = true;
        ProjectInstanceSnapshot fullSnapshot = ProjectInstanceSnapshot.Create(source);
        source.TranslateEntireState.ShouldBeTrue();
        fullSnapshot
            .Materialize(new BuildParameters(collection), evaluationId: 2)
            .TranslateEntireState.ShouldBeTrue();
    }

    [Fact]
    public void Create_RejectsPartialEvaluation()
    {
        using var collection = new ProjectCollection();
        using var projectFromString = new ProjectRootElementFromString(ProjectContents, collection);
        ProjectInstance partial = ProjectInstance.FromProjectRootElement(
            projectFromString.Project,
            new ProjectOptions
            {
                EvaluationStage = ProjectEvaluationStage.Properties,
                ProjectCollection = collection,
            });

        Should.Throw<InvalidOperationException>(() => ProjectInstanceSnapshot.Create(partial));
    }

    [Fact]
    public void Create_IgnoresPartialTranslationEscapeHatch()
    {
        const string VariableName = "MSBUILD_PROJECTINSTANCE_TRANSLATION_MODE";
        string? originalValue = Environment.GetEnvironmentVariable(VariableName);
        Environment.SetEnvironmentVariable(VariableName, "partial");
        Traits.UpdateFromEnvironment();

        try
        {
            using var collection = new ProjectCollection();
            using var projectFromString = new ProjectRootElementFromString(ProjectContents, collection);
            var source = new ProjectInstance(projectFromString.Project);

            ProjectInstanceSnapshot snapshot = ProjectInstanceSnapshot.Create(source);
            ProjectInstance materialized =
                Materialize(snapshot, collection);

            materialized.Targets.Count.ShouldBe(3);
            materialized.ItemDefinitions.Count.ShouldBe(1);
            materialized.TranslateEntireState.ShouldBeFalse();
        }
        finally
        {
            Environment.SetEnvironmentVariable(VariableName, originalValue);
            Traits.UpdateFromEnvironment();
        }
    }

    [Fact]
    public void Create_RejectsImmutableSource()
    {
        using var collection = new ProjectCollection();
        using var projectFromString = new ProjectRootElementFromString(ProjectContents, collection);
        ProjectInstance immutable = new ProjectInstance(projectFromString.Project).DeepCopy(isImmutable: true);

        Should.Throw<InvalidOperationException>(() => ProjectInstanceSnapshot.Create(immutable));
    }

    [Fact]
    public void Materialize_ReturnsIndependentMutableInstances()
    {
        using var collection = new ProjectCollection();
        using var projectFromString = new ProjectRootElementFromString(ProjectContents, collection);
        var source = new ProjectInstance(projectFromString.Project);
        ProjectInstanceSnapshot snapshot = ProjectInstanceSnapshot.Create(source);

        ProjectInstance first =
            Materialize(snapshot, collection, evaluationId: 1);
        ProjectInstance second =
            Materialize(snapshot, collection, evaluationId: 2);

        first.SetProperty("Configuration", "Release");
        first.AddItem("Compile", "Generated.cs");
        first.AddTarget(
            "DynamicTarget",
            condition: string.Empty,
            inputs: string.Empty,
            outputs: string.Empty,
            returns: string.Empty,
            keepDuplicateOutputs: string.Empty,
            dependsOnTargets: string.Empty,
            beforeTargets: string.Empty,
            afterTargets: string.Empty,
            parentProjectSupportsReturnsAttribute: false);
        first.Build("Build", []).ShouldBeTrue();

        first.GetPropertyValue("Built").ShouldBe("true");
        first.GetItems("Generated").Count.ShouldBe(1);
        second.GetPropertyValue("Configuration").ShouldBe("Debug");
        second.GetPropertyValue("Built").ShouldBeEmpty();
        second.GetItems("Compile").Count.ShouldBe(1);
        second.GetItems("Generated").ShouldBeEmpty();
        second.Targets.ShouldNotContainKey("DynamicTarget");

        ProjectInstance third =
            Materialize(snapshot, collection, evaluationId: 3);
        third.GetPropertyValue("Configuration").ShouldBe("Debug");
        third.GetPropertyValue("Built").ShouldBeEmpty();
        third.GetItems("Compile").Count.ShouldBe(1);
        third.GetItems("Generated").ShouldBeEmpty();
        third.Targets.ShouldNotContainKey("DynamicTarget");
    }

    [Fact]
    public void Create_IsolatesTemplateFromLaterSourceMutation()
    {
        using var collection = new ProjectCollection();
        using var projectFromString = new ProjectRootElementFromString(ProjectContents, collection);
        var source = new ProjectInstance(projectFromString.Project);
        ProjectInstanceSnapshot snapshot = ProjectInstanceSnapshot.Create(source);
        ProjectInstance template = GetTemplate(snapshot);

        source.SetProperty("Configuration", "Release");
        source.AddItem("Compile", "SourceMutation.cs");

        ProjectInstance materialized = Materialize(snapshot, collection);

        template.Toolset.ShouldBeNull();
        materialized.GetPropertyValue("Configuration").ShouldBe("Debug");
        materialized.GetItems("Compile").Select(item => item.EvaluatedInclude)
            .ShouldBe(["Program.cs"]);
        materialized.Targets.ShouldNotBeSameAs(source.Targets);
        materialized.TaskRegistry.ShouldNotBeSameAs(source.TaskRegistry);
        materialized.ProjectRootElementCache.ShouldBeSameAs(collection.ProjectRootElementCache);
        materialized.TaskRegistry.RootElementCache.ShouldBeSameAs(collection.ProjectRootElementCache);
        materialized.Toolset.ToolsVersion.ShouldBe(source.Toolset.ToolsVersion);
    }

    [Fact]
    public void Materialize_RebindsCurrentBuildState()
    {
        using var collection = new ProjectCollection();
        using var projectFromString = new ProjectRootElementFromString(ProjectContents, collection);
        var source = new ProjectInstance(projectFromString.Project);
        ProjectInstanceSnapshot snapshot = ProjectInstanceSnapshot.Create(source);
        var buildParameters = new BuildParameters(collection);
        buildParameters.EnvironmentPropertiesInternal["SNAPSHOT_CURRENT_ENV"] =
            ProjectPropertyInstance.Create("SNAPSHOT_CURRENT_ENV", "current-value");

        ProjectInstance materialized =
            snapshot.Materialize(buildParameters, evaluationId: 1234);
        var evaluatorData =
            (IEvaluatorData<ProjectPropertyInstance, ProjectItemInstance, ProjectMetadataInstance, ProjectItemDefinitionInstance>)materialized;

        materialized.EvaluationId.ShouldBe(1234);
        evaluatorData.EnvironmentVariablePropertiesDictionary["SNAPSHOT_CURRENT_ENV"]
            .EvaluatedValue.ShouldBe("current-value");
        materialized.ProjectRootElementCache.ShouldBeSameAs(buildParameters.ProjectRootElementCache);
        materialized.TaskRegistry.RootElementCache.ShouldBeSameAs(buildParameters.ProjectRootElementCache);
    }

    [Fact]
    public void Materialize_OwnsIndependentCompleteObjectGraph()
    {
        using var collection = new ProjectCollection();
        using var projectFromString = new ProjectRootElementFromString(ProjectContents, collection);
        var source = new ProjectInstance(projectFromString.Project);
        ProjectInstanceSnapshot snapshot = ProjectInstanceSnapshot.Create(source);

        ProjectInstance first = Materialize(snapshot, collection, evaluationId: 1);
        ProjectInstance second = Materialize(snapshot, collection, evaluationId: 2);

        first.Targets.ShouldNotBeSameAs(second.Targets);
        ProjectTargetInstance firstTarget = first.Targets["Build"];
        ProjectTargetInstance secondTarget = second.Targets["Build"];
        firstTarget.ShouldNotBeSameAs(secondTarget);
        firstTarget.Children.Count.ShouldBe(secondTarget.Children.Count);
        for (int index = 0; index < firstTarget.Children.Count; index++)
        {
            firstTarget.Children[index].ShouldNotBeSameAs(secondTarget.Children[index]);
        }

        ProjectTaskInstance firstTask =
            firstTarget.Children.OfType<ProjectTaskInstance>().Single(
                task => task.Name == "CreateProperty");
        ProjectTaskInstance secondTask =
            secondTarget.Children.OfType<ProjectTaskInstance>().Single(
                task => task.Name == "CreateProperty");
        firstTask.ParametersForBuild.ShouldNotBeSameAs(secondTask.ParametersForBuild);
        firstTask.Outputs.Single().ShouldNotBeSameAs(secondTask.Outputs.Single());

        firstTarget.OnErrorChildren.Single()
            .ShouldNotBeSameAs(secondTarget.OnErrorChildren.Single());

        ProjectPropertyGroupTaskInstance firstPropertyGroup =
            firstTarget.Children.OfType<ProjectPropertyGroupTaskInstance>().Single();
        ProjectPropertyGroupTaskInstance secondPropertyGroup =
            secondTarget.Children.OfType<ProjectPropertyGroupTaskInstance>().Single();
        firstPropertyGroup.Properties.Single()
            .ShouldNotBeSameAs(secondPropertyGroup.Properties.Single());

        ProjectItemGroupTaskInstance firstItemGroup =
            firstTarget.Children.OfType<ProjectItemGroupTaskInstance>().Single();
        ProjectItemGroupTaskInstance secondItemGroup =
            secondTarget.Children.OfType<ProjectItemGroupTaskInstance>().Single();
        firstItemGroup.Items.Single()
            .ShouldNotBeSameAs(secondItemGroup.Items.Single());
        firstItemGroup.Items.Single().Metadata.Single()
            .ShouldNotBeSameAs(secondItemGroup.Items.Single().Metadata.Single());

        first.ItemDefinitions.ShouldNotBeSameAs(second.ItemDefinitions);
        first.ItemDefinitions["Reference"]
            .ShouldNotBeSameAs(second.ItemDefinitions["Reference"]);
        GetItemDefinitions(first.GetItems("Reference").Single())
            .ShouldContain(first.ItemDefinitions["Reference"]);
        GetItemDefinitions(first.GetItems("Reference").Single())
            .ShouldNotContain(second.ItemDefinitions["Reference"]);

        first.Toolset.ShouldBeSameAs(second.Toolset);
        first.Toolset.ShouldBeSameAs(collection.GetToolset(first.ToolsVersion));

        first.TaskRegistry.ShouldNotBeSameAs(second.TaskRegistry);
        first.TaskRegistry.Toolset.ShouldBeSameAs(first.Toolset);
        second.TaskRegistry.Toolset.ShouldBeSameAs(second.Toolset);
        first.TaskRegistry.TaskRegistrations.ShouldNotBeSameAs(
            second.TaskRegistry.TaskRegistrations);
        KeyValuePair<TaskRegistry.RegisteredTaskIdentity, List<TaskRegistry.RegisteredTaskRecord>>
            firstRegistration = first.TaskRegistry.TaskRegistrations.Single(
                pair => pair.Key.Name == "SnapshotCustomTask");
        KeyValuePair<TaskRegistry.RegisteredTaskIdentity, List<TaskRegistry.RegisteredTaskRecord>>
            secondRegistration = second.TaskRegistry.TaskRegistrations.Single(
                pair => pair.Key.Name == "SnapshotCustomTask");
        firstRegistration.Key.ShouldNotBeSameAs(secondRegistration.Key);
        firstRegistration.Value.ShouldNotBeSameAs(secondRegistration.Value);
        firstRegistration.Value.Single()
            .ShouldNotBeSameAs(secondRegistration.Value.Single());

        first.ImportPaths.ShouldNotBeSameAs(second.ImportPaths);
        first.ImportPathsIncludingDuplicates.ShouldNotBeSameAs(
            second.ImportPathsIncludingDuplicates);

        var firstEvaluatorData =
            (IEvaluatorData<ProjectPropertyInstance, ProjectItemInstance, ProjectMetadataInstance, ProjectItemDefinitionInstance>)first;
        var secondEvaluatorData =
            (IEvaluatorData<ProjectPropertyInstance, ProjectItemInstance, ProjectMetadataInstance, ProjectItemDefinitionInstance>)second;
        List<TargetSpecification> firstBeforeTargets =
            firstEvaluatorData.BeforeTargets["Build"];
        List<TargetSpecification> secondBeforeTargets =
            secondEvaluatorData.BeforeTargets["Build"];
        firstBeforeTargets.ShouldNotBeSameAs(secondBeforeTargets);
        firstBeforeTargets.Single().ShouldNotBeSameAs(secondBeforeTargets.Single());
    }

    [Fact]
    public void Materialize_MissingToolsetFailsNonCritically()
    {
        const string toolsVersion = "SnapshotOnly";
        using var sourceCollection = new ProjectCollection();
        Toolset defaultToolset = sourceCollection.GetToolset(sourceCollection.DefaultToolsVersion);
        sourceCollection.AddToolset(
            new Toolset(
                toolsVersion,
                defaultToolset.ToolsPath,
                defaultToolset.Properties.ToDictionary(
                    property => property.Key,
                    property => property.Value.EvaluatedValue),
                sourceCollection,
                msbuildOverrideTasksPath: null));
        using TestEnvironment env = TestEnvironment.Create();
        TransientTestFile project = env.CreateFile(
            "project.proj",
            "<Project><Target Name=\"Build\" /></Project>");
        var source = new ProjectInstance(
            project.Path,
            globalProperties: null,
            toolsVersion,
            sourceCollection);
        source.ToolsVersion.ShouldBe(toolsVersion);
        ProjectInstanceSnapshot snapshot = ProjectInstanceSnapshot.Create(source);

        using var targetCollection = new ProjectCollection();

        Should.Throw<InvalidOperationException>(
            () => snapshot.Materialize(
                new BuildParameters(targetCollection),
                evaluationId: 1));
    }

    [Fact]
    public void Materialize_PreservesSdkResolvedEnvironmentVariables()
    {
        using var collection = new ProjectCollection();
        using var projectFromString = new ProjectRootElementFromString(ProjectContents, collection);
        var source = new ProjectInstance(projectFromString.Project);
        source.AddSdkResolvedEnvironmentVariable("SNAPSHOT_SDK_ENV", "sdk-value");
        ProjectInstanceSnapshot snapshot = ProjectInstanceSnapshot.Create(source);

        ProjectInstance materialized =
            Materialize(snapshot, collection);
        var evaluatorData =
            (IEvaluatorData<ProjectPropertyInstance, ProjectItemInstance, ProjectMetadataInstance, ProjectItemDefinitionInstance>)materialized;
        ProjectPropertyInstance property =
            evaluatorData.SdkResolvedEnvironmentVariablePropertiesDictionary["SNAPSHOT_SDK_ENV"];

        property.EvaluatedValue.ShouldBe("sdk-value");
        property.ShouldBeOfType<ProjectPropertyInstance.SdkResolvedEnvironmentVariablePropertyInstance>();
    }

    [Fact]
    public void Materialize_PreservesTargetReturns()
    {
        const string projectContents = """
            <Project DefaultTargets="GetOutput">
              <Target Name="GetOutput" Returns="@(ResultItem)">
                <ItemGroup>
                  <ResultItem Include="snapshot-output" />
                </ItemGroup>
              </Target>
            </Project>
            """;
        using var collection = new ProjectCollection();
        using var projectFromString = new ProjectRootElementFromString(projectContents, collection);
        var source = new ProjectInstance(projectFromString.Project);
        ProjectInstanceSnapshot snapshot = ProjectInstanceSnapshot.Create(source);
        ProjectInstance materialized =
            Materialize(snapshot, collection);

        bool succeeded = materialized.Build(
            ["GetOutput"],
            loggers: null,
            out IDictionary<string, TargetResult> targetOutputs);

        succeeded.ShouldBeTrue();
        targetOutputs["GetOutput"].Items.Single().ItemSpec.ShouldBe("snapshot-output");
    }

    [Fact]
    public async Task Materialize_IsSafeForConcurrentReaders()
    {
        using var collection = new ProjectCollection();
        using var projectFromString = new ProjectRootElementFromString(ProjectContents, collection);
        var source = new ProjectInstance(projectFromString.Project);
        ProjectInstanceSnapshot snapshot = ProjectInstanceSnapshot.Create(source);
        var buildParameters = new BuildParameters(collection);

        Task<ProjectInstance>[] tasks = Enumerable.Range(0, 16)
            .Select(index => Task.Run(
                () => snapshot.Materialize(buildParameters, evaluationId: index + 1)))
            .ToArray();

        ProjectInstance[] materialized = await Task.WhenAll(tasks);

        for (int i = 0; i < materialized.Length; i++)
        {
            for (int j = i + 1; j < materialized.Length; j++)
            {
                materialized[i].ShouldNotBeSameAs(materialized[j]);
            }

            materialized[i].GetPropertyValue("Configuration").ShouldBe("Debug");
            materialized[i].GetItems("Compile").Count.ShouldBe(1);
            materialized[i].Targets.Count.ShouldBe(3);
        }
    }

    private static ProjectInstance Materialize(
        ProjectInstanceSnapshot snapshot,
        ProjectCollection collection,
        int evaluationId = 1) =>
        snapshot.Materialize(new BuildParameters(collection), evaluationId);

    private static List<ProjectItemDefinitionInstance> GetItemDefinitions(
        ProjectItemInstance item)
    {
        FieldInfo taskItemField = typeof(ProjectItemInstance).GetField(
            "_taskItem",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        object taskItem = taskItemField.GetValue(item)!;
        FieldInfo itemDefinitionsField = taskItem.GetType().GetField(
            "_itemDefinitions",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (List<ProjectItemDefinitionInstance>)itemDefinitionsField.GetValue(taskItem)!;
    }

    private static ProjectInstance GetTemplate(ProjectInstanceSnapshot snapshot)
    {
        FieldInfo templateField = typeof(ProjectInstanceSnapshot).GetField(
            "_template",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (ProjectInstance)templateField.GetValue(snapshot)!;
    }
}
