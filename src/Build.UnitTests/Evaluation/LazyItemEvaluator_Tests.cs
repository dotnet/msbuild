// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Xml;
using Microsoft.Build.Construction;
using Microsoft.Build.Engine.UnitTests;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Evaluation.Context;
using Microsoft.Build.Exceptions;
using Microsoft.Build.Framework;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests.Evaluation;

public sealed class LazyItemEvaluator_Tests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AddAllItemsPreservesDeclarationOrderAndConditionRouting(bool evaluateFalseConditions)
    {
        using ProjectCollection collection = new();
        Project project = CreateProject(
            """
            <Project>
                <ItemGroup>
                    <First Include="z.cs;a.cs">
                        <Trace>initial</Trace>
                    </First>
                    <Second Include="s.cs;r.cs" Condition="false" />
                    <Empty Include="gone.cs" />
                    <Third Include="t.cs" />
                    <First Update="a.cs">
                        <Trace>updated</Trace>
                    </First>
                    <Second Include="q.cs;p.cs" />
                    <First Include="d.cs;c.cs" />
                    <Third Include="v.cs;u.cs" />
                    <Empty Remove="@(Empty)" />
                </ItemGroup>
            </Project>
            """,
            collection,
            evaluateFalseConditions ? ProjectLoadSettings.Default : ProjectLoadSettings.DoNotEvaluateElementsWithFalseCondition);
        var evaluator = CreateEvaluator(project, default);
        evaluator.ProcessItemGroups([.. project.Xml.ItemGroups], project.Xml.DirectoryPath);

        evaluator.AddAllItems(project.TestOnlyGetPrivateData);

        List<string> actual = [];
        foreach (ProjectItem item in project.AllEvaluatedItems)
        {
            actual.Add($"{item.ItemType}:{item.EvaluatedInclude}");
            item.Xml.Include.ShouldNotBeEmpty();
            item.Xml.Update.ShouldBeEmpty();
            if (item.EvaluatedInclude == "a.cs")
            {
                item.GetMetadataValue("Trace").ShouldBe("updated");
            }
        }

        string[] expected =
        [
            "First:z.cs", "First:a.cs", "Third:t.cs", "Second:q.cs", "Second:p.cs",
            "First:d.cs", "First:c.cs", "Third:v.cs", "Third:u.cs",
        ];
        actual.ShouldBe(expected);
        project.Items.Count.ShouldBe(expected.Length);
        if (evaluateFalseConditions)
        {
            project.ItemsIgnoringCondition.Count.ShouldBe(expected.Length + 2);
        }

        project.GetItems("Empty").ShouldBeEmpty();

        List<string> secondItems = [];
        foreach (ProjectItem item in project.GetItemsIgnoringCondition("Second"))
        {
            secondItems.Add(item.EvaluatedInclude);
        }

        string[] expectedSecondItems = evaluateFalseConditions ? ["s.cs", "r.cs", "q.cs", "p.cs"] : ["q.cs", "p.cs"];
        secondItems.ShouldBe(expectedSecondItems);
    }

    [Fact]
    public void AddAllItemsComputesAllSnapshotsBeforePublishingAnyItems()
    {
        using ProjectCollection collection = new();
        Project project = CreateProject(
            """
            <Project>
                <ItemGroup>
                    <First Include="first.cs" />
                    <Second Include="second.cs">
                        <M Condition="'%(Identity)' ==">invalid-condition</M>
                    </Second>
                </ItemGroup>
            </Project>
            """,
            collection);
        var evaluator = CreateEvaluator(project, default);
        evaluator.ProcessItemGroups([.. project.Xml.ItemGroups], project.Xml.DirectoryPath);

        Should.Throw<InvalidProjectFileException>(() => evaluator.AddAllItems(project.TestOnlyGetPrivateData));

        project.Items.ShouldBeEmpty();
        project.AllEvaluatedItems.ShouldBeEmpty();
        project.ItemsIgnoringCondition.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void RecordsRequestedElementsInDocumentOrder(bool recordItemElements, bool recordItemGlobs)
    {
        using ProjectCollection collection = new();
        Project project = CreateProject(
            """
            <Project>
                <ItemGroup>
                    <Compile Include="first.cs" />
                    <Content Include="readme.txt" />
                </ItemGroup>
                <ItemGroup>
                    <Compile Update="first.cs">
                        <M>updated</M>
                    </Compile>
                    <Compile Remove="missing.cs" />
                    <Content Include="second.txt" />
                </ItemGroup>
            </Project>
            """,
            collection);

        List<ProjectItemElement> recordedElements = [];
        EvaluatedItemElementRecorder recorder = (recordItemElements, recordItemGlobs) switch
        {
            (true, true) => EvaluatedItemElementRecorder.Create(recordedElements, " ; compile; ; COMPILE ; "),
            (true, false) => EvaluatedItemElementRecorder.Create(recordedElements),
            (false, true) => EvaluatedItemElementRecorder.Create(" ; compile; ; COMPILE ; "),
            (false, false) => default,
        };

        var evaluator = CreateEvaluator(project, recorder);

        evaluator.ProcessItemGroups([.. project.Xml.ItemGroups], project.Xml.DirectoryPath);

        ProjectItemElement[] elements = [.. project.Xml.Items];
        if (recordItemElements)
        {
            recordedElements.ShouldBe(elements);
        }
        else
        {
            recordedElements.ShouldBeEmpty();
        }

        if (recordItemGlobs)
        {
            ProjectItemElement[] expectedGlobElements = [elements[0], elements[2], elements[3]];
            recorder.ItemGlobElements.ShouldBe(expectedGlobElements);
        }
        else
        {
            recorder.ItemGlobElements.ShouldBeNull();
        }

        ImmutableArray<ProjectItem> items = CollectItems(evaluator);
        AssertIncludes(items, ["first.cs", "readme.txt", "second.txt"]);
        items[0].GetMetadataValue("M").ShouldBe("updated");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RecordsOnlyElementsWithTrueGroupAndItemConditions(bool evaluateFalseConditions)
    {
        using ProjectCollection collection = new();
        Project project = CreateProject(
            """
            <Project>
                <ItemGroup Condition="false">
                    <Compile Include="false-group.cs" />
                    <Compile Include="false-group-and-item.cs" Condition="false" />
                </ItemGroup>
                <ItemGroup>
                    <Compile Include="false-item.cs" Condition="false" />
                    <Compile Include="first.cs" Condition="true" />
                    <Compile Include="second.cs" />
                </ItemGroup>
            </Project>
            """,
            collection,
            evaluateFalseConditions ? ProjectLoadSettings.Default : ProjectLoadSettings.DoNotEvaluateElementsWithFalseCondition);

        List<ProjectItemElement> recordedElements = [];
        EvaluatedItemElementRecorder recorder = EvaluatedItemElementRecorder.Create(recordedElements, "Compile");
        var evaluator = CreateEvaluator(project, recorder);

        evaluator.ProcessItemGroups([.. project.Xml.ItemGroups], project.Xml.DirectoryPath);

        ProjectItemElement[] elements = [.. project.Xml.Items];
        ProjectItemElement[] expectedRecordedElements = [elements[3], elements[4]];
        recordedElements.ShouldBe(expectedRecordedElements);
        recorder.ItemGlobElements.ShouldBe(expectedRecordedElements);

        ImmutableArray<ProjectItem> items = CollectItems(evaluator);
        string[] expectedIncludes = evaluateFalseConditions
            ? ["false-group.cs", "false-group-and-item.cs", "false-item.cs", "first.cs", "second.cs"]
            : ["first.cs", "second.cs"];
        AssertIncludes(items, expectedIncludes);
        AssertIncludes(CollectItems(evaluator, ignoreConditions: false), ["first.cs", "second.cs"]);
    }

    [Fact]
    public void ConditionsUseItemsVisibleAtEachElement()
    {
        using ProjectCollection collection = new();
        Project project = CreateProject(
            """
            <Project>
                <ItemGroup>
                    <Compile Include="first.cs" />
                    <Compile Include="second.cs" Condition="'@(Compile)' == 'first.cs'" />
                    <Compile Include="skipped-item.cs" Condition="'@(Compile)' == 'first.cs'" />
                </ItemGroup>
                <ItemGroup Condition="'@(Compile)' == 'first.cs;second.cs'">
                    <Content Include="readme.txt" />
                </ItemGroup>
                <ItemGroup Condition="'@(Compile)' == 'first.cs'">
                    <Compile Include="skipped-group.cs" />
                </ItemGroup>
            </Project>
            """,
            collection,
            ProjectLoadSettings.DoNotEvaluateElementsWithFalseCondition);

        List<ProjectItemElement> recordedElements = [];
        EvaluatedItemElementRecorder recorder = EvaluatedItemElementRecorder.Create(recordedElements, "Compile");
        var evaluator = CreateEvaluator(project, recorder);

        evaluator.ProcessItemGroups([.. project.Xml.ItemGroups], project.Xml.DirectoryPath);

        ProjectItemElement[] elements = [.. project.Xml.Items];
        ProjectItemElement[] expectedRecordedElements = [elements[0], elements[1], elements[3]];
        ProjectItemElement[] expectedGlobElements = [elements[0], elements[1]];
        recordedElements.ShouldBe(expectedRecordedElements);
        recorder.ItemGlobElements.ShouldBe(expectedGlobElements);
        ImmutableArray<ProjectItem> items = CollectItems(evaluator);
        AssertIncludes(items, ["first.cs", "second.cs", "readme.txt"]);
    }

    [Theory]
    [InlineData(false, "")]
    [InlineData(false, " \t ")]
    [InlineData(false, " ; \t; ")]
    [InlineData(true, "")]
    [InlineData(true, " \t ")]
    [InlineData(true, " ; \t; ")]
    public void EmptyGlobRequestsDoNotEnableGlobRecording(bool recordItemElements, string itemGlobRequests)
    {
        using ProjectCollection collection = new();
        Project project = CreateProject(
            """
            <Project>
                <ItemGroup>
                    <Compile Include="first.cs" />
                </ItemGroup>
            </Project>
            """,
            collection);

        List<ProjectItemElement> recordedElements = [];
        EvaluatedItemElementRecorder recorder = recordItemElements
            ? EvaluatedItemElementRecorder.Create(recordedElements, itemGlobRequests)
            : EvaluatedItemElementRecorder.Create(itemGlobRequests);

        var evaluator = CreateEvaluator(project, recorder);

        evaluator.ProcessItemGroups([.. project.Xml.ItemGroups], project.Xml.DirectoryPath);

        recorder.ItemGlobElements.ShouldBeNull();
        if (recordItemElements)
        {
            recordedElements.ShouldBe(project.Xml.Items);
        }
        else
        {
            recordedElements.ShouldBeEmpty();
        }

        ImmutableArray<ProjectItem> items = CollectItems(evaluator);
        AssertIncludes(items, ["first.cs"]);
    }

    private static ImmutableArray<ProjectItem> CollectItems(
        LazyItemEvaluator<ProjectProperty, ProjectItem, ProjectMetadata, ProjectItemDefinition> evaluator,
        bool ignoreConditions = true)
    {
        ImmutableArrayItemCollector<ProjectItem> collector = new(ignoreConditions);
        evaluator.AddAllItems(collector);
        return collector.ToImmutable();
    }

    private static void AssertIncludes(IEnumerable<ProjectItem> items, string[] expectedIncludes)
    {
        List<string> actual = [];
        foreach (ProjectItem item in items)
        {
            actual.Add(item.EvaluatedInclude);
        }

        actual.ShouldBe(expectedIncludes);
    }

    private static Project CreateProject(
        string projectXml,
        ProjectCollection collection,
        ProjectLoadSettings loadSettings = ProjectLoadSettings.Default)
    {
        // Project construction evaluates its XML. Start empty to initialize real evaluator data without
        // processing the test declarations. ReloadFrom replaces the XML and marks the project dirty, but
        // does not reevaluate it; the LazyItemEvaluator under test processes those declarations first.
        using ProjectFromString project = new("<Project />", globalProperties: null, toolsVersion: null, collection, loadSettings);
        using StringReader stringReader = new(projectXml);
        using XmlReader reader = XmlReader.Create(stringReader);
        project.Project.Xml.ReloadFrom(reader, throwIfUnsavedChanges: false);
        return project.Project;
    }

    private static LazyItemEvaluator<ProjectProperty, ProjectItem, ProjectMetadata, ProjectItemDefinition> CreateEvaluator(
        Project project,
        EvaluatedItemElementRecorder recorder)
        => new(
            project.TestOnlyGetPrivateData,
            new ProjectItem.ProjectItemFactory(project),
            TestLoggingContext.CreateTestContext(new BuildEventContext(1, 2, 3, 4)),
            new EvaluationProfiler(shouldTrackElements: false),
            EvaluationContext.Create(EvaluationContext.SharingPolicy.Isolated),
            recorder);
}
