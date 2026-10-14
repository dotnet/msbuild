// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Text;
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

public sealed class LazyItemEvaluator_Tests(ITestOutputHelper output)
{
    private readonly ITestOutputHelper _output = output;

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

    [Theory]
    [InlineData(0, false)]
    [InlineData(0, true)]
    [InlineData(1, false)]
    [InlineData(1, true)]
    [InlineData(2, false)]
    [InlineData(2, true)]
    [InlineData(4, false)]
    [InlineData(4, true)]
    [InlineData(5, false)]
    [InlineData(5, true)]
    public void CapturedReferencesPreserveEarlierItemsAcrossLookupSizes(int referencedItemTypeCount, bool caseSensitive)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        env.SetEnvironmentVariable("MSBUILDUSECASESENSITIVEITEMNAMES", caseSensitive ? "1" : null);
        using ProjectCollection collection = new();
        Project project = CreateProject("<Project />", collection);
        ProjectItemGroupElement sources = project.Xml.AddItemGroup();
        StringBuilder include = new("@(Missing);marker");
        List<string> expectedIncludes = ["marker"];

        for (int i = 0; i < referencedItemTypeCount; i++)
        {
            sources.AddItem($"Source{i}", $"before{i}");
            include.Append($";@(Source{i});@(Source{i})");
            expectedIncludes.Add($"before{i}");
            expectedIncludes.Add($"before{i}");
        }

        ProjectItemElement captured = project.Xml.AddItemGroup().AddItem("Captured", include.ToString());
        for (int i = 0; i < referencedItemTypeCount; i++)
        {
            // Shredding deduplicates these names, so the uppercase lookup must use the captured mapping's comparer.
            captured.AddMetadata($"M{i}", $"@(Source{i})|@(SOURCE{i})");
        }

        ProjectItemGroupElement changes = project.Xml.AddItemGroup();
        for (int i = 0; i < referencedItemTypeCount; i++)
        {
            ProjectItemElement remove = project.Xml.CreateItemElement($"Source{i}");
            remove.Remove = $"before{i}";
            changes.AppendChild(remove);
            changes.AddItem($"Source{i}", $"after{i}");
        }

        var evaluator = CreateEvaluator(project, default);
        evaluator.ProcessItemGroups([.. project.Xml.ItemGroups], project.Xml.DirectoryPath);

        List<string> capturedIncludes = [];
        List<string> sourceIncludes = [];
        foreach (ProjectItem itemData in CollectItems(evaluator))
        {
            if (itemData.ItemType == "Captured")
            {
                capturedIncludes.Add(itemData.EvaluatedInclude);
                for (int i = 0; i < referencedItemTypeCount; i++)
                {
                    string expectedUppercaseValue = caseSensitive ? string.Empty : $"before{i}";
                    itemData.GetMetadataValue($"M{i}").ShouldBe($"before{i}|{expectedUppercaseValue}");
                }
            }
            else
            {
                sourceIncludes.Add(itemData.EvaluatedInclude);
            }
        }

        capturedIncludes.ShouldBe(expectedIncludes);
        sourceIncludes.Count.ShouldBe(referencedItemTypeCount);
        for (int i = 0; i < referencedItemTypeCount; i++)
        {
            sourceIncludes[i].ShouldBe($"after{i}");
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CapturedPrefixesPreserveMetadataAcrossLaterUpdatesAndRemoves(bool eagerReads)
    {
        using ProjectCollection collection = new();
        Project project = CreateProject(
            """
            <Project>
                <ItemGroup>
                    <Source Include="a;b">
                        <M>original</M>
                    </Source>
                </ItemGroup>
                <ItemGroup>
                    <Before Include="@(Source)" />
                </ItemGroup>
                <ItemGroup>
                    <Source Update="*">
                        <M>updated</M>
                    </Source>
                </ItemGroup>
                <ItemGroup>
                    <Updated Include="@(Source)" />
                </ItemGroup>
                <ItemGroup>
                    <Source Remove="b" />
                </ItemGroup>
                <ItemGroup>
                    <Removed Include="@(Source)" />
                </ItemGroup>
                <ItemGroup>
                    <Source Include="c">
                        <M>new</M>
                    </Source>
                </ItemGroup>
                <ItemGroup>
                    <Final Include="@(Source)" />
                </ItemGroup>
            </Project>
            """,
            collection);

        if (eagerReads)
        {
            bool firstGroup = true;
            foreach (ProjectItemGroupElement group in project.Xml.ItemGroups)
            {
                if (!firstGroup)
                {
                    group.Condition = "'@(Source->Count())' >= '0'";
                }

                firstGroup = false;
            }
        }

        var evaluator = CreateEvaluator(project, default);
        evaluator.ProcessItemGroups([.. project.Xml.ItemGroups], project.Xml.DirectoryPath);
        string[] expected =
        [
            "Source:a:updated",
            "Before:a:original",
            "Before:b:original",
            "Updated:a:updated",
            "Updated:b:updated",
            "Removed:a:updated",
            "Source:c:new",
            "Final:a:updated",
            "Final:c:new",
        ];

        for (int read = 0; read < 2; read++)
        {
            List<string> actual = [];
            foreach (ProjectItem data in CollectItems(evaluator))
            {
                actual.Add($"{data.ItemType}:{data.EvaluatedInclude}:{data.GetMetadataValue("M")}");
            }

            actual.ShouldBe(expected);
        }
    }

    [Fact]
    public void CapturedPositionsSurviveGrowthAcrossSegments()
    {
        using ProjectCollection collection = new();
        Project project = CreateProject("<Project />", collection);
        project.Xml.AddItemGroup().AddItem("Source", "first");
        project.Xml.AddItemGroup().AddItem("Before", "@(Source)");
        ProjectItemGroupElement operations = project.Xml.AddItemGroup();

        const int emptyOperationCount = 10000;
        for (int i = 0; i < emptyOperationCount; i++)
        {
            operations.AddItem("Source", "@(Missing)");
            if (i == emptyOperationCount / 2)
            {
                operations.AddItem("Source", "middle");
                project.Xml.AddItemGroup().AddItem("Middle", "@(Source)");
                operations = project.Xml.AddItemGroup();
            }
        }

        operations.AddItem("Source", "last");
        project.Xml.AddItemGroup().AddItem("After", "@(Source)");

        var evaluator = CreateEvaluator(project, default);
        evaluator.ProcessItemGroups([.. project.Xml.ItemGroups], project.Xml.DirectoryPath);

        List<string> actual = [];
        foreach (ProjectItem data in CollectItems(evaluator))
        {
            actual.Add($"{data.ItemType}:{data.EvaluatedInclude}");
        }

        string[] expected =
        [
            "Source:first",
            "Before:first",
            "Source:middle",
            "Middle:first",
            "Middle:middle",
            "Source:last",
            "After:first",
            "After:middle",
            "After:last",
        ];
        actual.ShouldBe(expected);
    }

    [Fact]
    public void CapturedGlobReferenceDoesNotInheritLaterRemoveExclusions()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        var folder = env.CreateFolder();
        env.CreateFile(folder, "a.cs", contents: string.Empty);
        env.CreateFile(folder, "b.tmp", contents: string.Empty);
        using ProjectCollection collection = new();
        Project project = CreateProject(
            """
            <Project>
                <ItemGroup>
                    <Source Include="*" />
                    <Captured Include="@(Source)" />
                    <Source Remove="*.tmp" />
                </ItemGroup>
            </Project>
            """,
            collection);
        project.Xml.FullPath = Path.Combine(folder.Path, "test.proj");

        var evaluator = CreateEvaluator(project, default);
        evaluator.ProcessItemGroups([.. project.Xml.ItemGroups], project.Xml.DirectoryPath);

        List<string> actual = [];
        foreach (ProjectItem data in CollectItems(evaluator))
        {
            actual.Add($"{data.ItemType}:{data.EvaluatedInclude}");
        }

        string[] expected = ["Source:a.cs", "Captured:a.cs", "Captured:b.tmp"];
        actual.ShouldBe(expected);
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
