// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml;
using Microsoft.Build.Construction;
using Microsoft.Build.Engine.UnitTests;
using Microsoft.Build.Engine.UnitTests.BackEnd;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Evaluation.Context;
using Microsoft.Build.Eventing;
using Microsoft.Build.Exceptions;
using Microsoft.Build.Execution;
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
        var evaluator = CreateEvaluator(project);
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

    [Theory]
    [InlineData("visible.cs;removed.cs;hidden.cs;hidden-removed.cs")]
    [InlineData("*")]
    public void UpdatesPreserveFalseConditionIncludeRouting(string updateSpec)
    {
        using ProjectCollection collection = new();
        Project project = CreateProject(
            $"""
            <Project>
                <ItemGroup>
                    <Source Include="visible.cs;removed.cs"><Trace>initial</Trace></Source>
                    <Source Include="hidden.cs;hidden-removed.cs" Condition="false"><Trace>hidden</Trace></Source>
                </ItemGroup>
                <ItemGroup>
                    <Before Include="@(Source)" />
                    <Source Update="{updateSpec}"><Trace>%(Trace)-updated</Trace></Source>
                    <Source Update="*" Condition="false"><Trace>incorrect</Trace></Source>
                </ItemGroup>
                <ItemGroup Condition="'@(Source->'%(Trace)')' == 'initial-updated;initial-updated'">
                    <Checks Include="updated" />
                </ItemGroup>
                <ItemGroup>
                    <After Include="@(Source)" />
                    <Source Remove="removed.cs;hidden-removed.cs" />
                    <Source Remove="hidden.cs" Condition="false" />
                </ItemGroup>
            </Project>
            """,
            collection);
        var evaluator = CreateEvaluator(project);
        IItemProvider<ProjectItem> provider = evaluator;
        ProjectItemGroupElement[] groups = [.. project.Xml.ItemGroups];
        ProjectItemElement[] sourceIncludes = [.. groups[0].Items];

        evaluator.ProcessItemGroups([groups[0]], project.Xml.DirectoryPath);
        ICollection<ProjectItem> original = provider.GetItems("Source");
        AssertIncludes(original, ["visible.cs", "removed.cs"]);

        evaluator.ProcessItemGroups([groups[1]], project.Xml.DirectoryPath);
        ICollection<ProjectItem> updated = provider.GetItems("Source");
        AssertIncludes(updated, ["visible.cs", "removed.cs"]);
        foreach (ProjectItem item in updated)
        {
            item.GetMetadataValue("Trace").ShouldBe("initial-updated");
        }

        evaluator.ProcessItemGroups([groups[2], groups[3]], project.Xml.DirectoryPath);
        ProjectItem current = provider.GetItems("Source").ShouldHaveSingleItem();
        current.EvaluatedInclude.ShouldBe("visible.cs");
        current.GetMetadataValue("Trace").ShouldBe("initial-updated");
        provider.GetItems("Checks").ShouldHaveSingleItem().EvaluatedInclude.ShouldBe("updated");

        AssertIncludes(original, ["visible.cs", "removed.cs"]);
        AssertIncludes(updated, ["visible.cs", "removed.cs"]);
        ProjectItem[] before = [.. provider.GetItems("Before")];
        ProjectItem[] after = [.. provider.GetItems("After")];
        AssertIncludes(before, ["visible.cs", "removed.cs"]);
        AssertIncludes(after, ["visible.cs", "removed.cs"]);
        foreach (ProjectItem item in original)
        {
            item.GetMetadataValue("Trace").ShouldBe("initial");
        }

        foreach (ProjectItem item in before)
        {
            item.GetMetadataValue("Trace").ShouldBe("initial");
        }

        foreach (ProjectItem item in updated)
        {
            item.GetMetadataValue("Trace").ShouldBe("initial-updated");
        }

        foreach (ProjectItem item in after)
        {
            item.GetMetadataValue("Trace").ShouldBe("initial-updated");
        }

        evaluator.AddAllItems(project.TestOnlyGetPrivateData);

        project.Items.Count.ShouldBe(6);
        project.ItemsIgnoringCondition.Count.ShouldBe(7);
        project.GetItems("Source").ShouldHaveSingleItem().ShouldBeSameAs(current);
        ProjectItem[] ignoringCondition = [.. project.GetItemsIgnoringCondition("Source")];
        AssertIncludes(ignoringCondition, ["visible.cs", "hidden.cs"]);
        ignoringCondition[0].GetMetadataValue("Trace").ShouldBe("initial-updated");
        ignoringCondition[1].GetMetadataValue("Trace").ShouldBe("hidden-updated");
        ignoringCondition[0].Xml.ShouldBeSameAs(sourceIncludes[0]);
        ignoringCondition[1].Xml.ShouldBeSameAs(sourceIncludes[1]);

        List<string> ordered = [];
        foreach (ProjectItem item in project.AllEvaluatedItems)
        {
            ordered.Add($"{item.ItemType}:{item.EvaluatedInclude}:{item.GetMetadataValue("Trace")}");
        }

        ordered.ShouldBe(
        [
            "Source:visible.cs:initial-updated",
            "Before:visible.cs:initial", "Before:removed.cs:initial",
            "Checks:updated:",
            "After:visible.cs:initial-updated", "After:removed.cs:initial-updated",
        ]);
    }

    [Theory]
    [InlineData(false, "root.cs;imported.cs")]
    [InlineData(true, "root.cs;imported.cs")]
    [InlineData(false, "*")]
    [InlineData(true, "*")]
    public void UpdatesPreserveImportedIncludesAndDefiningProjectMetadata(bool instanceModel, string updateSpec)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        var folder = env.CreateFolder();
        var importFile = env.CreateFile(
            folder,
            "items.props",
            """
            <Project>
                <ItemGroup>
                    <Other Include="import-marker" />
                    <Source Include="imported.cs"><Trace>imported</Trace></Source>
                </ItemGroup>
            </Project>
            """);
        var projectFile = env.CreateFile(
            folder,
            "root.proj",
            $"""
            <Project>
                <ItemGroup>
                    <Source Include="root.cs"><Trace>root</Trace></Source>
                    <Other Include="before" />
                </ItemGroup>
                <Import Project="items.props" />
                <ItemGroup>
                    <Before Include="@(Source)" />
                    <Source Update="{updateSpec}">
                        <Trace>%(Trace)-updated</Trace>
                        <Origin>%(DefiningProjectFullPath)</Origin>
                    </Source>
                    <Other Include="after" />
                    <Source Include="later.cs"><Trace>later</Trace></Source>
                </ItemGroup>
            </Project>
            """);
        using ProjectCollection collection = new();
        Project project = new(projectFile.Path, globalProperties: null, toolsVersion: null, collection);
        ProjectItemElement[] rootElements = [.. project.Xml.Items];
        ProjectItemElement[] importedElements = [.. project.Imports.ShouldHaveSingleItem().ImportedProject.Items];
        ProjectInstance? instance = instanceModel
            ? new(project.Xml, globalProperties: null, toolsVersion: null, collection)
            : null;
        IEnumerable<IItem> items = instance is null ? project.AllEvaluatedItems : instance.Items;
        List<IItem> source = [];
        List<IItem> before = [];
        List<string> other = [];
        List<string> ordered = [];
        foreach (IItem item in items)
        {
            string itemType = item.Key;
            ordered.Add($"{itemType}:{item.EvaluatedInclude}");
            switch (itemType)
            {
                case "Source":
                    source.Add(item);
                    break;
                case "Before":
                    before.Add(item);
                    break;
                case "Other":
                    other.Add(item.EvaluatedInclude);
                    break;
            }
        }

        ordered.Count.ShouldBe(8);
        other.ShouldBe(["before", "import-marker", "after"]);
        source.Count.ShouldBe(3);
        string[] expectedIncludes = ["root.cs", "imported.cs", "later.cs"];
        string[] expectedTraces = ["root-updated", "imported-updated", "later"];
        string[] expectedDefiningProjects = [projectFile.Path, importFile.Path, projectFile.Path];
        ProjectItemElement[] expectedElements = [rootElements[0], importedElements[1], rootElements[5]];
        for (int index = 0; index < source.Count; index++)
        {
            IItem item = source[index];
            string definingProject = expectedDefiningProjects[index];
            item.EvaluatedInclude.ShouldBe(expectedIncludes[index]);
            item.GetMetadataValue("Trace").ShouldBe(expectedTraces[index]);
            item.GetMetadataValue("Origin").ShouldBe(index < 2 ? definingProject : string.Empty);
            item.GetMetadataValue("DefiningProjectFullPath").ShouldBe(definingProject);
            item.GetMetadataValue("DefiningProjectDirectory").ShouldBe(Path.GetDirectoryName(definingProject) + Path.DirectorySeparatorChar);
            item.GetMetadataValue("DefiningProjectName").ShouldBe(Path.GetFileNameWithoutExtension(definingProject));
            item.GetMetadataValue("DefiningProjectExtension").ShouldBe(Path.GetExtension(definingProject));
            if (item is ProjectItem projectItem)
            {
                projectItem.Xml.ShouldBeSameAs(expectedElements[index]);
            }
        }

        before.Count.ShouldBe(2);
        string[] expectedEarlierTraces = ["root", "imported"];
        for (int index = 0; index < before.Count; index++)
        {
            before[index].EvaluatedInclude.ShouldBe(expectedIncludes[index]);
            before[index].GetMetadataValue("Trace").ShouldBe(expectedEarlierTraces[index]);
            before[index].GetMetadataValue("Origin").ShouldBeEmpty();
            before[index].GetMetadataValue("DefiningProjectFullPath").ShouldBe(projectFile.Path);
        }

        if (!instanceModel)
        {
            ordered.ShouldBe(
            [
                "Source:root.cs", "Other:before", "Other:import-marker", "Source:imported.cs",
                "Before:root.cs", "Before:imported.cs", "Other:after", "Source:later.cs",
            ]);
        }
    }

    [Fact]
    public void AddAllItemsSkipsEmptyAndRemovedIncludes()
    {
        using ProjectCollection collection = new();
        Project project = CreateProject(
            """
            <Project>
                <ItemGroup>
                    <Source Include="removed.cs" />
                    <Other Include="other.txt" />
                    <Source Include="@(Missing)" />
                    <Source Include="kept.cs;last.cs" />
                    <Source Include="tail.cs" />
                    <Source Remove="removed.cs;tail.cs" />
                    <Source Include="@(Missing)" />
                    <Empty Include="@(Missing)" />
                </ItemGroup>
            </Project>
            """,
            collection);
        var evaluator = CreateEvaluator(project);
        evaluator.ProcessItemGroups([.. project.Xml.ItemGroups], project.Xml.DirectoryPath);

        for (int read = 0; read < 2; read++)
        {
            AssertIncludes(CollectItems(evaluator), ["other.txt", "kept.cs", "last.cs"]);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AddAllItemsUsesTheOperationListsItemNameComparer(bool caseSensitive)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        env.SetEnvironmentVariable("MSBUILDUSECASESENSITIVEITEMNAMES", caseSensitive ? "1" : null);
        using ProjectCollection collection = new();
        Project project = CreateProject(
            """
            <Project>
                <ItemGroup>
                    <Source Include="first.cs" />
                    <source Include="second.cs" />
                    <Other Include="other.txt" />
                    <Source Include="third.cs;fourth.cs" />
                    <source Include="last.cs" />
                </ItemGroup>
            </Project>
            """,
            collection);
        var evaluator = CreateEvaluator(project);
        evaluator.ProcessItemGroups([.. project.Xml.ItemGroups], project.Xml.DirectoryPath);

        AssertIncludes(CollectItems(evaluator), ["first.cs", "second.cs", "other.txt", "third.cs", "fourth.cs", "last.cs"]);
    }

    [Fact]
    public void AddAllItemsComputesAllSnapshotsBeforeAddingAnyItems()
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
        var evaluator = CreateEvaluator(project);
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FilteredItemsPreserveConditionFilteringOrderAndReuseStorage(bool includeMatchingItems)
    {
        using ProjectCollection collection = new();
        Project project = CreateProject(
            $"""
            <Project>
                <ItemGroup>
                    <Source Include="skipped-before.cs" Condition="false" />
                    <Source Include="first.cs;second.cs" Condition="{includeMatchingItems}" />
                    <Source Include="skipped-after.cs" Condition="false" />
                    <Captured Include="@(Source)" />
                    <Other Include="other.cs" />
                </ItemGroup>
            </Project>
            """,
            collection);
        var evaluator = CreateEvaluator(project);
        evaluator.ProcessItemGroups([.. project.Xml.ItemGroups], project.Xml.DirectoryPath);
        IItemProvider<ProjectItem> provider = evaluator;

        ICollection<ProjectItem> firstResult = provider.GetItems("Source");
        ImmutableArray<ProjectItem> firstRead = firstResult.ShouldBeOfType<ImmutableArray<ProjectItem>>();
        ICollection<ProjectItem> capturedResult = provider.GetItems("Captured");
        ImmutableArray<ProjectItem> capturedRead = capturedResult.ShouldBeOfType<ImmutableArray<ProjectItem>>();
        provider.GetItems("Other").ShouldHaveSingleItem().EvaluatedInclude.ShouldBe("other.cs");
        ICollection<ProjectItem> secondResult = provider.GetItems("Source");
        secondResult.ShouldBeSameAs(firstResult);
        provider.GetItems("Captured").ShouldBeSameAs(capturedResult);
        ImmutableArray<ProjectItem> secondRead = secondResult.ShouldBeOfType<ImmutableArray<ProjectItem>>();
        string[] expectedIncludes = includeMatchingItems ? ["first.cs", "second.cs"] : [];

        firstRead.Length.ShouldBe(expectedIncludes.Length);
        capturedRead.Length.ShouldBe(expectedIncludes.Length);
        secondRead.Length.ShouldBe(expectedIncludes.Length);
        for (int i = 0; i < expectedIncludes.Length; i++)
        {
            firstRead[i].EvaluatedInclude.ShouldBe(expectedIncludes[i]);
            capturedRead[i].EvaluatedInclude.ShouldBe(expectedIncludes[i]);
            secondRead[i].EvaluatedInclude.ShouldBe(expectedIncludes[i]);
        }
    }

    [Theory]
    [InlineData("a.cs", "initial")]
    [InlineData("*", "updated")]
    public void FilteredItemsRemainIsolatedAcrossUpdatedSnapshots(string updateSpec, string expectedBMetadata)
    {
        using ProjectCollection collection = new();
        Project project = CreateProject(
            $"""
            <Project>
                <ItemGroup>
                    <Source Include="a.cs;b.cs">
                        <Trace>initial</Trace>
                    </Source>
                    <Before Include="@(Source)" />
                </ItemGroup>
                <ItemGroup>
                    <Source Update="{updateSpec}">
                        <Trace>updated</Trace>
                    </Source>
                    <After Include="@(Source)" />
                </ItemGroup>
            </Project>
            """,
            collection);
        var evaluator = CreateEvaluator(project);
        IItemProvider<ProjectItem> provider = evaluator;
        ProjectItemGroupElement[] groups = [.. project.Xml.ItemGroups];
        evaluator.ProcessItemGroups([groups[0]], project.Xml.DirectoryPath);
        ICollection<ProjectItem> originalResult = provider.GetItems("Source");
        provider.GetItems("Source").ShouldBeSameAs(originalResult);

        evaluator.ProcessItemGroups([groups[1]], project.Xml.DirectoryPath);
        ICollection<ProjectItem> updatedResult = provider.GetItems("Source");
        updatedResult.ShouldNotBeSameAs(originalResult);
        provider.GetItems("Source").ShouldBeSameAs(updatedResult);
        ProjectItem[] updatedItems = [.. updatedResult];
        updatedItems.Length.ShouldBe(2);
        updatedItems[0].EvaluatedInclude.ShouldBe("a.cs");
        updatedItems[0].GetMetadataValue("Trace").ShouldBe("updated");
        updatedItems[1].EvaluatedInclude.ShouldBe("b.cs");
        updatedItems[1].GetMetadataValue("Trace").ShouldBe(expectedBMetadata);

        ICollection<ProjectItem> beforeResult = provider.GetItems("Before");
        provider.GetItems("Before").ShouldBeSameAs(beforeResult);
        ProjectItem[] originalItems = [.. originalResult];
        ProjectItem[] beforeItems = [.. beforeResult];
        originalItems.Length.ShouldBe(2);
        beforeItems.Length.ShouldBe(2);
        for (int i = 0; i < originalItems.Length; i++)
        {
            originalItems[i].GetMetadataValue("Trace").ShouldBe("initial");
            beforeItems[i].EvaluatedInclude.ShouldBe(originalItems[i].EvaluatedInclude);
            beforeItems[i].GetMetadataValue("Trace").ShouldBe("initial");
        }

        ICollection<ProjectItem> afterResult = provider.GetItems("After");
        provider.GetItems("After").ShouldBeSameAs(afterResult);
        ProjectItem[] afterItems = [.. afterResult];
        afterItems.Length.ShouldBe(updatedItems.Length);
        for (int i = 0; i < afterItems.Length; i++)
        {
            afterItems[i].EvaluatedInclude.ShouldBe(updatedItems[i].EvaluatedInclude);
            afterItems[i].GetMetadataValue("Trace").ShouldBe(updatedItems[i].GetMetadataValue("Trace"));
        }
    }

    [Fact]
    public void CurrentConditionReadsRetainOnlyTheLatestSnapshot()
    {
        const int phaseCount = 32;
        const string itemType = "GrowingCurrentCheckpointSource";
        using ProjectCollection collection = new();
        Project project = CreateProject("<Project />", collection, ProjectLoadSettings.DoNotEvaluateElementsWithFalseCondition);
        var evaluator = CreateEvaluator(project);
        IItemProvider<ProjectItem> provider = evaluator;
        List<ICollection<ProjectItem>> snapshots = [];
        List<string> expectedIncludes = [];
        using EventSourceTestHelper listener = new();

        evaluator.TestOnlyCachedSnapshotCount.ShouldBe(0);
        for (int phase = 1; phase <= phaseCount; phase++)
        {
            string include = $"item{phase}.cs";
            ProjectItemGroupElement group = project.Xml.AddItemGroup();
            group.AddItem(itemType, include);
            ProjectItemGroupElement probe = project.Xml.AddItemGroup();
            probe.Condition = $"'@({itemType}->Count())' != '{phase}'";
            probe.AddItem("ReadMismatch", "incorrect-count");

            evaluator.ProcessItemGroups([group, probe], project.Xml.DirectoryPath);

            ICollection<ProjectItem> result = provider.GetItems(itemType);
            expectedIncludes.Add(include);
            AssertIncludes(result, [.. expectedIncludes]);
            provider.GetItems(itemType).ShouldBeSameAs(result);
            provider.GetItems("ReadMismatch").ShouldBeEmpty();
            evaluator.TestOnlyCachedSnapshotCount.ShouldBe(1);
            CountEvents(listener, nameof(MSBuildEventSource.ApplyLazyItemOperationsStart), itemType).ShouldBe(1);
            snapshots.Add(result);
        }

        // External collections remain valid even after their cache entries have been retired.
        for (int phase = 0; phase < snapshots.Count; phase++)
        {
            List<string> includes = [];
            for (int item = 1; item <= phase + 1; item++)
            {
                includes.Add($"item{item}.cs");
            }

            AssertIncludes(snapshots[phase], [.. includes]);
        }
    }

    [Theory]
    [InlineData("a.cs")]
    [InlineData("*")]
    public void PreviousCurrentSnapshotsAreRemovedAcrossBatchedAndOrdinaryUpdates(string updateSpec)
    {
        using ProjectCollection collection = new();
        Project project = CreateProject(
            """
            <Project>
                <ItemGroup>
                    <Source Include="a.cs;b.cs"><Trace>initial</Trace></Source>
                </ItemGroup>
            </Project>
            """,
            collection);
        var evaluator = CreateEvaluator(project);
        IItemProvider<ProjectItem> provider = evaluator;
        evaluator.ProcessItemGroups([.. project.Xml.ItemGroups], project.Xml.DirectoryPath);
        List<(ICollection<ProjectItem> Items, string Trace)> snapshots = [(provider.GetItems("Source"), "initial")];
        string trace = "initial";

        for (int phase = 1; phase <= 8; phase++)
        {
            ProjectItemGroupElement group = project.Xml.AddItemGroup();
            ProjectItemElement update = project.Xml.CreateItemElement("Source");
            update.Update = updateSpec;
            group.AppendChild(update);
            update.AddMetadata("Trace", $"%(Trace)-{phase}");
            evaluator.ProcessItemGroups([group], project.Xml.DirectoryPath);

            ICollection<ProjectItem> result = provider.GetItems("Source");
            trace += $"-{phase}";
            snapshots.Add((result, trace));
            provider.GetItems("Source").ShouldBeSameAs(result);
            AssertIncludes(CollectItems(evaluator), ["a.cs", "b.cs"]);
            provider.GetItems("Source").ShouldBeSameAs(result);
            evaluator.TestOnlyCachedSnapshotCount.ShouldBe(1);
        }

        foreach (var snapshot in snapshots)
        {
            ProjectItem[] items = [.. snapshot.Items];
            items[0].GetMetadataValue("Trace").ShouldBe(snapshot.Trace);
            items[1].GetMetadataValue("Trace").ShouldBe(updateSpec == "*" ? snapshot.Trace : "initial");
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BookmarksPreserveEarlierStatesWithoutReplacingTheCurrentSnapshot(bool readBeforeCapture)
    {
        using ProjectCollection collection = new();
        Project project = CreateProject(
            """
            <Project>
                <ItemGroup>
                    <Source Include="a.cs"><Trace>initial</Trace></Source>
                </ItemGroup>
            </Project>
            """,
            collection);
        var evaluator = CreateEvaluator(project);
        IItemProvider<ProjectItem> provider = evaluator;
        evaluator.ProcessItemGroups([.. project.Xml.ItemGroups], project.Xml.DirectoryPath);
        ICollection<ProjectItem>? initial = readBeforeCapture ? provider.GetItems("Source") : null;

        ProjectItemGroupElement capture = project.Xml.AddItemGroup();
        capture.AddItem("Captured", "@(Source)");
        evaluator.ProcessItemGroups([capture], project.Xml.DirectoryPath);
        evaluator.TestOnlyCachedSnapshotCount.ShouldBe(readBeforeCapture ? 1 : 0);

        ICollection<ProjectItem>? captured = null;
        for (int phase = 1; phase <= 3; phase++)
        {
            ProjectItemGroupElement group = project.Xml.AddItemGroup();
            ProjectItemElement update = project.Xml.CreateItemElement("Source");
            update.Update = "a.cs";
            group.AppendChild(update);
            update.AddMetadata("Trace", $"phase{phase}");
            evaluator.ProcessItemGroups([group], project.Xml.DirectoryPath);

            ICollection<ProjectItem> current = provider.GetItems("Source");
            current.ShouldHaveSingleItem().GetMetadataValue("Trace").ShouldBe($"phase{phase}");
            evaluator.TestOnlyCachedSnapshotCount.ShouldBe(captured is null ? 2 : 3);

            ICollection<ProjectItem> earlier = provider.GetItems("Captured");
            earlier.ShouldHaveSingleItem().GetMetadataValue("Trace").ShouldBe("initial");
            if (captured is not null)
            {
                earlier.ShouldBeSameAs(captured);
            }

            captured = earlier;
            provider.GetItems("Source").ShouldBeSameAs(current);
            evaluator.TestOnlyCachedSnapshotCount.ShouldBe(3);
        }

        if (initial is not null)
        {
            initial.ShouldHaveSingleItem().GetMetadataValue("Trace").ShouldBe("initial");
        }
    }

    [Fact]
    public void PreviousSnapshotRemovalIsIndependentForEachItemType()
    {
        using ProjectCollection collection = new();
        Project project = CreateProject(
            """
            <Project>
                <ItemGroup>
                    <Source Include="first.cs" />
                    <Other Include="other.cs" />
                </ItemGroup>
            </Project>
            """,
            collection);
        var evaluator = CreateEvaluator(project);
        IItemProvider<ProjectItem> provider = evaluator;
        evaluator.ProcessItemGroups([.. project.Xml.ItemGroups], project.Xml.DirectoryPath);
        ICollection<ProjectItem> original = provider.GetItems("Source");
        ICollection<ProjectItem> other = provider.GetItems("Other");
        evaluator.TestOnlyCachedSnapshotCount.ShouldBe(2);

        ProjectItemGroupElement group = project.Xml.AddItemGroup();
        group.AddItem("Source", "second.cs");
        evaluator.ProcessItemGroups([group], project.Xml.DirectoryPath);

        AssertIncludes(provider.GetItems("Source"), ["first.cs", "second.cs"]);
        provider.GetItems("Other").ShouldBeSameAs(other);
        AssertIncludes(original, ["first.cs"]);
        evaluator.TestOnlyCachedSnapshotCount.ShouldBe(2);
    }

    [Fact]
    public void FailedCurrentReadPreservesThePreviousSnapshotForRetry()
    {
        const string itemType = "FailingCurrentCheckpointSource";
        using ProjectCollection collection = new();
        Project project = CreateProject(
            """
            <Project>
                <ItemGroup>
                    <FailingCurrentCheckpointSource Include="a.cs"><Trace>initial</Trace></FailingCurrentCheckpointSource>
                </ItemGroup>
                <ItemGroup>
                    <FailingCurrentCheckpointSource Update="*">
                        <Trace Condition="'%(Identity)' ==">updated</Trace>
                    </FailingCurrentCheckpointSource>
                </ItemGroup>
            </Project>
            """,
            collection);
        var evaluator = CreateEvaluator(project);
        IItemProvider<ProjectItem> provider = evaluator;
        ProjectItemGroupElement[] groups = [.. project.Xml.ItemGroups];
        evaluator.ProcessItemGroups([groups[0]], project.Xml.DirectoryPath);
        ICollection<ProjectItem> original = provider.GetItems(itemType);
        evaluator.ProcessItemGroups([groups[1]], project.Xml.DirectoryPath);
        using EventSourceTestHelper listener = new();

        for (int attempt = 0; attempt < 2; attempt++)
        {
            Should.Throw<InvalidProjectFileException>(() => provider.GetItems(itemType));

            evaluator.TestOnlyCachedSnapshotCount.ShouldBe(1);
            original.ShouldHaveSingleItem().GetMetadataValue("Trace").ShouldBe("initial");
            // Retry applies only the failing Update, not the preceding Include.
            CountEvents(listener, nameof(MSBuildEventSource.ApplyLazyItemOperationsStart), itemType).ShouldBe(1);
        }
    }

    [Fact]
    public void RemovingPreviousCurrentSnapshotsPreservesBookmarkedResultsWithLaterRemovePatterns()
    {
        using ProjectCollection collection = new();
        Project project = CreateProject(
            """
            <Project>
                <ItemGroup>
                    <Source Include="a.cs;b.tmp" />
                    <Captured Include="@(Source)" />
                    <Source Remove="*.tmp" />
                </ItemGroup>
                <ItemGroup>
                    <Source Include="c.cs" />
                </ItemGroup>
                <ItemGroup>
                    <Source Remove="*.cs" />
                </ItemGroup>
            </Project>
            """,
            collection);
        var evaluator = CreateEvaluator(project);
        IItemProvider<ProjectItem> provider = evaluator;
        ProjectItemGroupElement[] groups = [.. project.Xml.ItemGroups];

        evaluator.ProcessItemGroups([groups[0]], project.Xml.DirectoryPath);
        AssertIncludes(provider.GetItems("Source"), ["a.cs"]);
        evaluator.TestOnlyCachedSnapshotCount.ShouldBe(2);

        evaluator.ProcessItemGroups([groups[1]], project.Xml.DirectoryPath);
        AssertIncludes(provider.GetItems("Source"), ["a.cs", "c.cs"]);
        evaluator.TestOnlyCachedSnapshotCount.ShouldBe(2);

        evaluator.ProcessItemGroups([groups[2]], project.Xml.DirectoryPath);
        ICollection<ProjectItem> current = provider.GetItems("Source");
        current.ShouldBeEmpty();
        // The bookmarked Include retains both later-pattern-specific snapshots alongside the current snapshot.
        evaluator.TestOnlyCachedSnapshotCount.ShouldBe(3);

        AssertIncludes(provider.GetItems("Captured"), ["a.cs", "b.tmp"]);
        // The bookmark adds the complete earlier snapshot and the Captured type's current result.
        evaluator.TestOnlyCachedSnapshotCount.ShouldBe(5);
        provider.GetItems("Source").ShouldBeSameAs(current);
        evaluator.TestOnlyCachedSnapshotCount.ShouldBe(5);
    }

    [Fact]
    public void ReevaluatedWildcardUpdatesKeepTemporaryContextsIsolated()
    {
        const string updateCondition = "'WildcardUpdateScratch' == 'WildcardUpdateScratch'";
        using ProjectCollection collection = new();
        Project project = CreateProject(
            $"""
            <Project>
                <ItemGroup>
                    <Source Include="a.cs;b.tmp">
                        <Trace>initial</Trace>
                    </Source>
                    <Source Update="*">
                        <Trace Condition="{updateCondition}">%(Trace)-updated</Trace>
                        <EarlierTrace>@(Source->'%(Trace)')</EarlierTrace>
                    </Source>
                    <BeforeRemove Include="@(Source)" />
                    <Source Remove="*.tmp" />
                </ItemGroup>
            </Project>
            """,
            collection);
        var evaluator = CreateEvaluator(project);
        evaluator.ProcessItemGroups([.. project.Xml.ItemGroups], project.Xml.DirectoryPath);
        IItemProvider<ProjectItem> provider = evaluator;
        using EventSourceTestHelper listener = new();

        ProjectItem[] currentItems = [.. provider.GetItems("Source")];
        currentItems.Length.ShouldBe(1);
        currentItems[0].EvaluatedInclude.ShouldBe("a.cs");
        currentItems[0].GetMetadataValue("Trace").ShouldBe("initial-updated");
        currentItems[0].GetMetadataValue("EarlierTrace").ShouldBe("initial;initial");
        CountEvents(listener, nameof(MSBuildEventSource.EvaluateConditionStart), updateCondition).ShouldBe(2);

        // The bookmark re-evaluates the same Update without patterns from the later Remove.
        ProjectItem[] earlierItems = [.. provider.GetItems("BeforeRemove")];
        earlierItems.Length.ShouldBe(2);
        earlierItems[0].EvaluatedInclude.ShouldBe("a.cs");
        earlierItems[1].EvaluatedInclude.ShouldBe("b.tmp");
        foreach (ProjectItem item in earlierItems)
        {
            item.GetMetadataValue("Trace").ShouldBe("initial-updated");
            item.GetMetadataValue("EarlierTrace").ShouldBe("initial;initial");
        }

        CountEvents(listener, nameof(MSBuildEventSource.EvaluateConditionStart), updateCondition).ShouldBe(2);
        currentItems[0].GetMetadataValue("Trace").ShouldBe("initial-updated");
        provider.GetItems("Source").ShouldHaveSingleItem().ShouldBeSameAs(currentItems[0]);
    }

    [Fact]
    public void RepeatedEvaluationPreservesEarlierPieceSnapshots()
    {
        const int phaseCount = 64;
        using ProjectCollection collection = new();
        Project project = CreateProject(
            """
            <Project>
                <ItemGroup>
                    <Selected Include="a.cs" />
                    <Source Include="a.cs;b.cs">
                        <Trace>initial</Trace>
                    </Source>
                </ItemGroup>
            </Project>
            """,
            collection);
        List<ProjectItemGroupElement> groups = [.. project.Xml.ItemGroups];
        for (int phase = 1; phase <= phaseCount; phase++)
        {
            ProjectItemGroupElement group = project.Xml.AddItemGroup();
            ProjectItemElement update = project.Xml.CreateItemElement("Source");
            update.Update = phase % 2 == 0 ? "a.cs" : "@(Selected)";
            group.AppendChild(update);
            update.AddMetadata("Trace", $"%(Trace)-{phase}");
            ProjectItemElement capture = project.Xml.CreateItemElement($"Snapshot{phase}", "@(Source)");
            group.AppendChild(capture);
            groups.Add(group);
        }

        var evaluator = CreateEvaluator(project);
        IItemProvider<ProjectItem> provider = evaluator;
        evaluator.ProcessItemGroups([groups[0]], project.Xml.DirectoryPath);
        ICollection<ProjectItem> initialResult = provider.GetItems("Source");
        ProjectItem[] initialItems = [.. initialResult];
        initialItems.Length.ShouldBe(2);
        List<(ICollection<ProjectItem> Items, string Trace)> versions = [(initialResult, "initial")];
        string trace = "initial";
        for (int phase = 1; phase <= phaseCount; phase++)
        {
            evaluator.ProcessItemGroups([groups[phase]], project.Xml.DirectoryPath);
            trace += $"-{phase}";
            ICollection<ProjectItem> result = provider.GetItems("Source");
            provider.GetItems("Source").ShouldBeSameAs(result);
            versions.Add((result, trace));
        }

        for (int phase = 0; phase < versions.Count; phase++)
        {
            ProjectItem[] items = [.. versions[phase].Items];
            items.Length.ShouldBe(2);
            items[0].EvaluatedInclude.ShouldBe("a.cs");
            items[0].GetMetadataValue("Trace").ShouldBe(versions[phase].Trace);
            items[1].ShouldBeSameAs(initialItems[1]);
            items[1].GetMetadataValue("Trace").ShouldBe("initial");
            if (phase > 0)
            {
                ICollection<ProjectItem> capturedResult = provider.GetItems($"Snapshot{phase}");
                provider.GetItems($"Snapshot{phase}").ShouldBeSameAs(capturedResult);
                ProjectItem[] captured = [.. capturedResult];
                captured.Length.ShouldBe(2);
                captured[0].EvaluatedInclude.ShouldBe("a.cs");
                captured[0].GetMetadataValue("Trace").ShouldBe(versions[phase].Trace);
                captured[1].EvaluatedInclude.ShouldBe("b.cs");
                captured[1].GetMetadataValue("Trace").ShouldBe("initial");
            }
        }

        provider.GetItems("Source").ShouldBeSameAs(versions[versions.Count - 1].Items);
    }

    [Fact]
    public void QualifiedUpdatesPreserveMatchingItemsWithAnExistingPathIndex()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        env.SetEnvironmentVariable("MSBUILDDICTIONARYBASEDITEMREMOVETHRESHOLD", "0");
        env.SetEnvironmentVariable("MSBuildDoNotExpandQualifiedMetadataInUpdateOperation", null);
        using ProjectCollection collection = new();
        Project project = CreateProject(
            """
            <Project>
                <ItemGroup>
                    <Lookup Include="a.cs">
                        <Value>from-a</Value>
                    </Lookup>
                    <Lookup Include="b.cs">
                        <Value>from-b</Value>
                    </Lookup>
                    <Source Include="a.cs;a.cs;b.cs;c.cs">
                        <Trace>initial</Trace>
                    </Source>
                    <Before Include="@(Source)" />
                    <Source Remove="missing.cs" />
                    <Source Update="@(Lookup)">
                        <Trace>%(Trace)-%(Lookup.Value)</Trace>
                    </Source>
                    <After Include="@(Source)" />
                    <Lookup Update="*">
                        <Value>later</Value>
                    </Lookup>
                </ItemGroup>
            </Project>
            """,
            collection);
        var evaluator = CreateEvaluator(project);
        evaluator.ProcessItemGroups([.. project.Xml.ItemGroups], project.Xml.DirectoryPath);
        IItemProvider<ProjectItem> provider = evaluator;

        ProjectItem[] lookup = [.. provider.GetItems("Lookup")];
        lookup.Length.ShouldBe(2);
        foreach (ProjectItem item in lookup)
        {
            item.GetMetadataValue("Value").ShouldBe("later");
        }

        string[] expectedIncludes = ["a.cs", "a.cs", "b.cs", "c.cs"];
        string[] expectedTrace = ["initial-from-a", "initial-from-a", "initial-from-b", "initial"];
        string[] itemTypes = ["Source", "Before", "After"];
        foreach (string itemType in itemTypes)
        {
            ICollection<ProjectItem> result = provider.GetItems(itemType);
            provider.GetItems(itemType).ShouldBeSameAs(result);
            ProjectItem[] items = [.. result];
            items.Length.ShouldBe(expectedIncludes.Length);
            for (int i = 0; i < items.Length; i++)
            {
                items[i].EvaluatedInclude.ShouldBe(expectedIncludes[i]);
                items[i].GetMetadataValue("Trace").ShouldBe(itemType == "Before" ? "initial" : expectedTrace[i]);
            }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void QualifiedUpdatesPreserveLastSourcePerTypeAndLiteralMatches(bool instanceModel)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        env.SetEnvironmentVariable("MSBuildDoNotExpandQualifiedMetadataInUpdateOperation", null);
        using ProjectCollection collection = new();
        Project project = CreateProject(
            """
            <Project>
                <ItemGroup>
                    <Lookup Include="shared.txt"><Trace>first</Trace></Lookup>
                    <Lookup Include="shared.txt"><Trace>last</Trace></Lookup>
                    <Other Include="shared.txt"><Trace>other</Trace></Other>
                    <Source Include="shared.txt;literal.txt;unmatched.txt"><Trace>initial</Trace></Source>
                    <Before Include="@(Source)" />
                    <Source Update="@(Lookup);@(Other);literal.txt">
                        <Trace>%(Source.Trace)|%(Lookup.Trace)|%(Other.Trace)</Trace>
                    </Source>
                    <After Include="@(Source)" />
                    <Lookup Update="*"><Trace>later</Trace></Lookup>
                </ItemGroup>
            </Project>
            """,
            collection);

        EvaluateHistory(project, instanceModel, "Before", "Source", "After").ShouldBe(
        [
            "Before:shared.txt:initial", "Before:literal.txt:initial", "Before:unmatched.txt:initial",
            "Source:shared.txt:initial|last|other", "Source:literal.txt:initial||", "Source:unmatched.txt:initial",
            "After:shared.txt:initial|last|other", "After:literal.txt:initial||", "After:unmatched.txt:initial",
        ]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void QualifiedUpdateTransformsRetainWildcardMatching(bool instanceModel)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        env.SetEnvironmentVariable("MSBuildDoNotExpandQualifiedMetadataInUpdateOperation", null);
        using ProjectCollection collection = new();
        Project project = CreateProject(
            """
            <Project>
                <ItemGroup>
                    <Lookup Include="origin"><Trace>captured</Trace></Lookup>
                    <Source Include="one.txt;two.txt;other.cs" />
                    <Source Update="@(Lookup->'*.txt')"><Trace>%(Lookup.Trace)</Trace></Source>
                </ItemGroup>
            </Project>
            """,
            collection);

        EvaluateHistory(project, instanceModel, "Source").ShouldBe(
            ["Source:one.txt:captured", "Source:two.txt:captured", "Source:other.cs:"]);
    }

    [Theory]
    [InlineData(false, "literal%2A.txt", "literal*.txt")]
    [InlineData(true, "literal%2A.txt", "literal*.txt")]
    [InlineData(false, "literal%3F.txt", "literal?.txt")]
    [InlineData(true, "literal%3F.txt", "literal?.txt")]
    [InlineData(false, "literal%3Bname.txt", "literal;name.txt")]
    [InlineData(true, "literal%3Bname.txt", "literal;name.txt")]
    [InlineData(false, "literal%253Bname.txt", "literal%3Bname.txt")]
    [InlineData(true, "literal%253Bname.txt", "literal%3Bname.txt")]
    public void QualifiedUpdatesKeepEscapedIdentitiesLiteral(bool instanceModel, string escapedIdentity, string identity)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        env.SetEnvironmentVariable("MSBuildDoNotExpandQualifiedMetadataInUpdateOperation", null);
        using ProjectCollection collection = new();
        Project project = CreateProject(
            $"""
            <Project>
                <ItemGroup>
                    <Lookup Include="{escapedIdentity}"><Trace>captured</Trace></Lookup>
                    <Source Include="{escapedIdentity};literal-other.txt" />
                    <Source Update="@(Lookup)"><Trace>%(Lookup.Trace)</Trace></Source>
                </ItemGroup>
            </Project>
            """,
            collection);

        EvaluateHistory(project, instanceModel, "Source").ShouldBe(
            [$"Source:{identity}:captured", "Source:literal-other.txt:"]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void QualifiedUpdatesIndexNormalizedTransformedIdentities(bool instanceModel)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        env.SetEnvironmentVariable("MSBuildDoNotExpandQualifiedMetadataInUpdateOperation", null);
        string normalizedAlias = Path.Combine("folder", "..", "CANDIDATE.TXT");
        using ProjectCollection collection = new();
        Project project = CreateProject(
            $"""
            <Project>
                <ItemGroup>
                    <Lookup Include="first">
                        <Match>./candidate.txt</Match>
                        <Trace>first</Trace>
                    </Lookup>
                    <Lookup Include="second">
                        <Match>{normalizedAlias}</Match>
                        <Trace>last</Trace>
                    </Lookup>
                    <Source Include="candidate.txt;candidate.txt;unmatched.txt"><Trace>initial</Trace></Source>
                    <Source Update="@(Lookup->'%(Match)')">
                        <Trace>%(Lookup.Trace)|%(Source.Identity)</Trace>
                    </Source>
                </ItemGroup>
            </Project>
            """,
            collection);

        EvaluateHistory(project, instanceModel, "Source").ShouldBe(
            ["Source:candidate.txt:last|candidate.txt", "Source:candidate.txt:last|candidate.txt", "Source:unmatched.txt:initial"]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void QualifiedUpdatesPreserveWildcardAndLiteralSourceWinnerOrder(bool instanceModel)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        env.SetEnvironmentVariable("MSBuildDoNotExpandQualifiedMetadataInUpdateOperation", null);
        using ProjectCollection collection = new();
        Project project = CreateProject(
            """
            <Project>
                <ItemGroup>
                    <Lookup Include="wildcard"><Match>*.txt</Match><Trace>wildcard</Trace></Lookup>
                    <Lookup Include="literal"><Match>one.txt</Match><Trace>literal</Trace></Lookup>
                    <Source Include="one.txt;two.txt;other.cs"><Trace>initial</Trace></Source>
                    <Source Update="@(Lookup->'%(Match)')"><Trace>%(Lookup.Trace)</Trace></Source>
                    <Earlier Include="@(Source)" />
                    <Lookup Include="later"><Match>*.txt</Match><Trace>later-wildcard</Trace></Lookup>
                    <Source Update="@(Lookup->'%(Match)')"><Trace>%(Lookup.Trace)</Trace></Source>
                </ItemGroup>
            </Project>
            """,
            collection);

        EvaluateHistory(project, instanceModel, "Earlier", "Source").ShouldBe(
        [
            "Earlier:one.txt:literal", "Earlier:two.txt:wildcard", "Earlier:other.cs:initial",
            "Source:one.txt:later-wildcard", "Source:two.txt:later-wildcard", "Source:other.cs:initial",
        ]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void QualifiedUpdatesWithEmptySourcesStillMatchLiteralFragments(bool instanceModel)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        env.SetEnvironmentVariable("MSBuildDoNotExpandQualifiedMetadataInUpdateOperation", null);
        using ProjectCollection collection = new();
        Project project = CreateProject(
            """
            <Project>
                <ItemGroup>
                    <Source Include="literal.txt;unmatched.txt"><Trace>initial</Trace></Source>
                    <Source Update="@(Missing);literal.txt">
                        <Trace>%(Missing.Trace)|%(Source.Identity)</Trace>
                    </Source>
                </ItemGroup>
            </Project>
            """,
            collection);

        EvaluateHistory(project, instanceModel, "Source").ShouldBe(
            ["Source:literal.txt:|literal.txt", "Source:unmatched.txt:initial"]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void QualifiedUpdateSourcesApplyToMetadataConditions(bool instanceModel)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        env.SetEnvironmentVariable("MSBuildDoNotExpandQualifiedMetadataInUpdateOperation", null);
        using ProjectCollection collection = new();
        Project project = CreateProject(
            """
            <Project>
                <ItemGroup>
                    <Lookup Include="one.txt"><Trace>lookup</Trace></Lookup>
                    <Source Include="one.txt;two.txt"><Trace>initial</Trace></Source>
                    <Source Update="@(Lookup);two.txt">
                        <Trace Condition="'%(Lookup.Trace)' == 'lookup'">updated</Trace>
                    </Source>
                </ItemGroup>
            </Project>
            """,
            collection);

        EvaluateHistory(project, instanceModel, "Source").ShouldBe(
            ["Source:one.txt:updated", "Source:two.txt:initial"]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BareSelfUpdatesPreservePerItemQualifiedMetadata(bool instanceModel)
    {
        using ProjectCollection collection = new();
        Project project = CreateProject(
            """
            <Project>
                <ItemGroup>
                    <Source Include="a.txt"><Trace>first</Trace></Source>
                    <Source Include="b.txt"><Trace>second</Trace></Source>
                    <Before Include="@(Source)" />
                    <Source Update="@(Source)"><Trace>%(Source.Trace)-updated</Trace></Source>
                </ItemGroup>
            </Project>
            """,
            collection);

        EvaluateHistory(project, instanceModel, "Before", "Source").ShouldBe(
        [
            "Before:a.txt:first", "Before:b.txt:second",
            "Source:a.txt:first-updated", "Source:b.txt:second-updated",
        ]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void QualifiedUpdatesPreserveTheMetadataExpansionEscapeHatch(bool instanceModel)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        env.SetEnvironmentVariable("MSBuildDoNotExpandQualifiedMetadataInUpdateOperation", "1");
        using ProjectCollection collection = new();
        Project project = CreateProject(
            """
            <Project>
                <ItemGroup>
                    <Lookup Include="one.txt"><Trace>lookup</Trace></Lookup>
                    <Source Include="one.txt;two.txt"><Trace>initial</Trace></Source>
                    <Source Update="@(Lookup)"><Trace>%(Lookup.Trace)</Trace></Source>
                </ItemGroup>
            </Project>
            """,
            collection);

        EvaluateHistory(project, instanceModel, "Source").ShouldBe(
            ["Source:one.txt:", "Source:two.txt:initial"]);
    }

    [Fact]
    public void QualifiedUpdatesRootMatchingInTheFragmentDirectoryInsteadOfTheCachedItemDirectory()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        env.SetEnvironmentVariable("MSBuildDoNotExpandQualifiedMetadataInUpdateOperation", null);
        env.SetEnvironmentVariable("MSBUILDDICTIONARYBASEDITEMREMOVETHRESHOLD", "0");
        var projectFolder = env.CreateFolder();
        var matchingFolder = env.CreateFolder();
        string absoluteIdentity = Path.Combine(projectFolder.Path, "one.txt");
        using ProjectCollection collection = new();
        Project project = CreateProject(
            $"""
            <Project>
                <ItemGroup>
                    <Lookup Include="{absoluteIdentity}"><Trace>lookup</Trace></Lookup>
                    <Source Include="one.txt;{absoluteIdentity}"><Trace>initial</Trace></Source>
                    <Source Remove="missing.txt" />
                    <Source Update="@(Lookup)"><Trace>%(Lookup.Trace)</Trace></Source>
                </ItemGroup>
            </Project>
            """,
            collection);
        project.Xml.FullPath = Path.Combine(projectFolder.Path, "test.proj");
        var evaluator = CreateEvaluator(project);
        evaluator.ProcessItemGroups([.. project.Xml.ItemGroups], matchingFolder.Path);
        IItemProvider<ProjectItem> provider = evaluator;

        ProjectItem[] items = [.. provider.GetItems("Source")];
        items.Length.ShouldBe(2);
        items[0].EvaluatedInclude.ShouldBe("one.txt");
        items[0].GetMetadataValue("Trace").ShouldBe("initial");
        items[1].EvaluatedInclude.ShouldBe(absoluteIdentity);
        items[1].GetMetadataValue("Trace").ShouldBe("lookup");
    }

    [Fact]
    public void ReevaluatedQualifiedUpdatesReuseMatchingItemsWithoutMutatingEarlierItems()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        env.SetEnvironmentVariable("MSBuildDoNotExpandQualifiedMetadataInUpdateOperation", null);
        using ProjectCollection collection = new();
        Project project = CreateProject(
            """
            <Project>
                <ItemGroup>
                    <Lookup Include="a.cs"><Trace>from-a</Trace></Lookup>
                    <Lookup Include="b.tmp"><Trace>from-b</Trace></Lookup>
                    <Source Include="a.cs;a.cs;b.tmp"><Trace>initial</Trace></Source>
                    <Source Update="@(Lookup)">
                        <Trace>%(Source.Trace)-%(Lookup.Trace)</Trace>
                    </Source>
                    <BeforeRemove Include="@(Source)" />
                    <Source Remove="*.tmp" />
                    <Lookup Update="*"><Trace>later</Trace></Lookup>
                </ItemGroup>
            </Project>
            """,
            collection);
        var evaluator = CreateEvaluator(project);
        evaluator.ProcessItemGroups([.. project.Xml.ItemGroups], project.Xml.DirectoryPath);
        IItemProvider<ProjectItem> provider = evaluator;

        foreach (ProjectItem item in provider.GetItems("Lookup"))
        {
            item.GetMetadataValue("Trace").ShouldBe("later");
        }

        ICollection<ProjectItem> current = provider.GetItems("Source");
        ProjectItem[] currentItems = [.. current];
        currentItems.Length.ShouldBe(2);
        foreach (ProjectItem item in currentItems)
        {
            item.EvaluatedInclude.ShouldBe("a.cs");
            item.GetMetadataValue("Trace").ShouldBe("initial-from-a");
        }

        ProjectItem[] earlierItems = [.. provider.GetItems("BeforeRemove")];
        AssertIncludes(earlierItems, ["a.cs", "a.cs", "b.tmp"]);
        earlierItems[0].GetMetadataValue("Trace").ShouldBe("initial-from-a");
        earlierItems[1].GetMetadataValue("Trace").ShouldBe("initial-from-a");
        earlierItems[2].GetMetadataValue("Trace").ShouldBe("initial-from-b");
        provider.GetItems("Source").ShouldBeSameAs(current);
        foreach (ProjectItem item in currentItems)
        {
            item.GetMetadataValue("Trace").ShouldBe("initial-from-a");
        }
    }

    [Fact]
    public void QualifiedUpdateSourceExpansionRemainsDeferredUntilAnItemNeedsMatching()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        env.SetEnvironmentVariable("MSBuildDoNotExpandQualifiedMetadataInUpdateOperation", null);
        using ProjectCollection collection = new();
        Project project = CreateProject(
            """
            <Project>
                <ItemGroup>
                    <Lookup Include="one.txt"><Trace>lookup</Trace></Lookup>
                    <Source Update="@(Lookup->NotAnItemFunction())"><Trace>%(Lookup.Trace)</Trace></Source>
                </ItemGroup>
                <ItemGroup>
                    <Source Include="one.txt" />
                    <Source Update="@(Lookup->NotAnItemFunction())" Condition="false"><Trace>%(Lookup.Trace)</Trace></Source>
                </ItemGroup>
                <ItemGroup>
                    <Source Update="@(Lookup->NotAnItemFunction())"><Trace>%(Lookup.Trace)</Trace></Source>
                </ItemGroup>
            </Project>
            """,
            collection);
        var evaluator = CreateEvaluator(project);
        ProjectItemGroupElement[] groups = [.. project.Xml.ItemGroups];
        evaluator.ProcessItemGroups([groups[0]], project.Xml.DirectoryPath);
        IItemProvider<ProjectItem> provider = evaluator;
        provider.GetItems("Source").ShouldBeEmpty();

        evaluator.ProcessItemGroups([groups[1]], project.Xml.DirectoryPath);
        provider.GetItems("Source").ShouldHaveSingleItem().EvaluatedInclude.ShouldBe("one.txt");

        evaluator.ProcessItemGroups([groups[2]], project.Xml.DirectoryPath);
        Should.Throw<InvalidProjectFileException>(() => provider.GetItems("Source"));
    }

    [Theory]
    [InlineData("true")]
    [InlineData("false")]
    public void UpdateMatchingPreparationPreservesEmptySelectionDiagnosticTiming(string condition)
    {
        using ProjectCollection collection = new();
        Project project = CreateProject(
            $"""
            <Project>
                <ItemGroup>
                    <Source Update="*.txt" Condition="{condition}">
                        <Trace>$([System.Int32]::Parse('invalid'))</Trace>
                    </Source>
                </ItemGroup>
            </Project>
            """,
            collection);
        var evaluator = CreateEvaluator(project);
        evaluator.ProcessItemGroups([.. project.Xml.ItemGroups], project.Xml.DirectoryPath);

        if (condition == "true")
        {
            Should.Throw<InvalidProjectFileException>(() => CollectItems(evaluator));
        }
        else
        {
            CollectItems(evaluator).ShouldBeEmpty();
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CurrentReadsAndBookmarksPreserveDistinctMetadataStates(bool instanceModel)
    {
        using ProjectCollection collection = new();
        Project project = CreateProject(
            """
            <Project>
                <ItemGroup>
                    <Source Include="a.txt;b.txt"><Trace>initial</Trace></Source>
                </ItemGroup>
                <ItemGroup Condition="'@(Source->'%(Trace)')' == 'initial;initial'">
                    <Checks Include="initial" />
                </ItemGroup>
                <ItemGroup>
                    <Before Include="@(Source)" />
                    <Source Update="a.txt"><Trace>one</Trace></Source>
                </ItemGroup>
                <ItemGroup Condition="'@(Source->'%(Trace)')' == 'one;initial'">
                    <Checks Include="one" />
                </ItemGroup>
                <ItemGroup>
                    <Middle Include="@(Source)" />
                    <Source Update="b.txt"><Trace>two</Trace></Source>
                </ItemGroup>
                <ItemGroup Condition="'@(Source->'%(Trace)')' == 'one;two'">
                    <Checks Include="two" />
                </ItemGroup>
                <ItemGroup>
                    <Source Remove="a.txt" />
                </ItemGroup>
            </Project>
            """,
            collection);

        EvaluateHistory(project, instanceModel, "Source", "Before", "Middle", "Checks").ShouldBe(
        [
            "Source:b.txt:two",
            "Before:a.txt:initial", "Before:b.txt:initial",
            "Middle:a.txt:one", "Middle:b.txt:initial",
            "Checks:initial:", "Checks:one:", "Checks:two:",
        ]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GlobReadsBookmarksAndReinclusionKeepRemovalBoundariesIndependent(bool instanceModel)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        var folder = env.CreateFolder();
        env.CreateFile(folder, "a.cs", contents: string.Empty);
        env.CreateFile(folder, "b.tmp", contents: string.Empty);
        env.CreateFile(folder, "c.g.cs", contents: string.Empty);
        using ProjectCollection collection = new();
        Project project = CreateProject(
            """
            <Project>
                <ItemGroup>
                    <Source Include="*"><Trace>initial</Trace></Source>
                </ItemGroup>
                <ItemGroup Condition="'@(Source->Count())' == '3'">
                    <Checks Include="unpruned" />
                </ItemGroup>
                <ItemGroup>
                    <Before Include="@(Source)" />
                    <Source Remove="*.tmp" />
                    <Middle Include="@(Source)" />
                    <Source Remove="*.cs" Condition="false" />
                    <Source Remove="*.g.cs" />
                    <Source Include="*.tmp"><Trace>reinserted</Trace></Source>
                </ItemGroup>
            </Project>
            """,
            collection,
            ProjectLoadSettings.DoNotEvaluateElementsWithFalseCondition);
        project.Xml.FullPath = Path.Combine(folder.Path, "test.proj");

        EvaluateHistory(project, instanceModel, "Source", "Before", "Middle", "Checks").ShouldBe(
        [
            "Source:a.cs:initial", "Source:b.tmp:reinserted",
            "Before:a.cs:initial", "Before:b.tmp:initial", "Before:c.g.cs:initial",
            "Middle:a.cs:initial", "Middle:c.g.cs:initial", "Checks:unpruned:",
        ]);
    }

    [Theory]
    [InlineData(false, "$(EmptyNames)")]
    [InlineData(true, "$(EmptyNames)")]
    [InlineData(false, "@(MissingNames)")]
    [InlineData(true, "@(MissingNames)")]
    public void EmptyExpandedMetadataNamesUseSpecificationRemoval(bool instanceModel, string metadataNames)
    {
        using ProjectCollection collection = new();
        Project project = CreateProject(
            $"""
            <Project>
                <PropertyGroup>
                    <EmptyNames />
                </PropertyGroup>
                <ItemGroup>
                    <Source Include="removed;kept"><Trace>initial</Trace></Source>
                    <Source Remove="removed" MatchOnMetadata="{metadataNames}" />
                </ItemGroup>
            </Project>
            """,
            collection);

        EvaluateHistory(project, instanceModel, "Source").ShouldBe(["Source:kept:initial"]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MetadataSelfRemovalDoesNotUseTheSpecificationClearFastPath(bool instanceModel)
    {
        using ProjectCollection collection = new();
        Project project = CreateProject(
            """
            <Project>
                <ItemGroup>
                    <Source Include="empty" />
                    <Source Include="populated"><Trace>value</Trace></Source>
                    <Source Remove="@(Source)" MatchOnMetadata="Trace" />
                </ItemGroup>
            </Project>
            """,
            collection);

        EvaluateHistory(project, instanceModel, "Source").ShouldBe(["Source:empty:"]);
    }

    [Theory]
    [InlineData("literal")]
    [InlineData("*.cs")]
    public void MetadataRemovalRejectsNonVectorSpecificationsDuringCollection(string specification)
    {
        using ProjectCollection collection = new();
        Project project = CreateProject(
            $"""
            <Project>
                <ItemGroup>
                    <Source Remove="{specification}" MatchOnMetadata="Trace" Condition="false" />
                </ItemGroup>
            </Project>
            """,
            collection);
        var evaluator = CreateEvaluator(project);

        Should.Throw<InvalidProjectFileException>(
            () => evaluator.ProcessItemGroups([.. project.Xml.ItemGroups], project.Xml.DirectoryPath));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MetadataAndSpecificationRemovesPreserveBookmarkedStatesAndLaterRemovePatterns(bool instanceModel)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        var folder = env.CreateFolder();
        env.CreateFile(folder, "a.cs", contents: string.Empty);
        env.CreateFile(folder, "b.tmp", contents: string.Empty);
        env.CreateFile(folder, "c.g.cs", contents: string.Empty);
        env.CreateFile(folder, "d.skip", contents: string.Empty);
        using ProjectCollection collection = new();
        Project project = CreateProject(
            """
            <Project>
                <ItemGroup>
                    <Lookup Include="key"><Trace>drop</Trace></Lookup>
                    <Source Include="*"><Trace>keep</Trace></Source>
                    <Source Update="b.tmp"><Trace>drop</Trace></Source>
                    <Before Include="@(Source)" />
                    <Source Remove="*.skip" />
                    <Source Remove="@(Lookup)" MatchOnMetadata="Trace" />
                    <AfterMetadata Include="@(Source)" />
                    <Lookup Update="*"><Trace>keep</Trace></Lookup>
                    <Source Remove="@(Lookup)" MatchOnMetadata="Trace" Condition="false" />
                    <Source Remove="*.g.cs" />
                    <Source Include="*.tmp"><Trace>again</Trace></Source>
                </ItemGroup>
            </Project>
            """,
            collection);
        project.Xml.FullPath = Path.Combine(folder.Path, "test.proj");

        EvaluateHistory(project, instanceModel, "Source", "Before", "AfterMetadata").ShouldBe(
        [
            "Source:a.cs:keep", "Source:b.tmp:again",
            "Before:a.cs:keep", "Before:b.tmp:drop", "Before:c.g.cs:keep", "Before:d.skip:keep",
            "AfterMetadata:a.cs:keep", "AfterMetadata:c.g.cs:keep",
        ]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MetadataRemovalUsesAllNamesFromStoredItemMetadata(bool instanceModel)
    {
        using ProjectCollection collection = new();
        Project project = CreateProject(
            """
            <Project>
                <ItemGroup>
                    <Lookup Include="key"><Trace>match</Trace><Group>one</Group></Lookup>
                    <Source Include="removed"><Trace>match</Trace><Group>one</Group></Source>
                    <Source Include="different-group"><Trace>match</Trace><Group>two</Group></Source>
                    <Source Include="missing-group"><Trace>match</Trace></Source>
                    <Source Remove="@(Lookup)" MatchOnMetadata="Trace;Group" />
                    <Lookup Update="*"><Trace>later</Trace><Group>two</Group></Lookup>
                </ItemGroup>
            </Project>
            """,
            collection);

        EvaluateHistory(project, instanceModel, "Source").ShouldBe(
            ["Source:different-group:match", "Source:missing-group:match"]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MetadataRemovalKeepsValuesFromDifferentSourceTuplesSeparate(bool instanceModel)
    {
        using ProjectCollection collection = new();
        Project project = CreateProject(
            """
            <Project>
                <ItemGroup>
                    <Names Include="Trace;Group" />
                    <Lookup Include="first"><Trace>shared</Trace><Group>left</Group></Lookup>
                    <Lookup Include="second"><Trace>shared</Trace><Group>right</Group></Lookup>
                    <Lookup Include="third"><Trace>other</Trace><Group>third</Group></Lookup>
                    <Lookup Include="duplicate"><Trace>shared</Trace><Group>left</Group></Lookup>
                    <Source Include="match"><Trace>shared</Trace><Group>right</Group></Source>
                    <Source Include="cross-tuple"><Trace>other</Trace><Group>right</Group></Source>
                    <Source Include="missing-group"><Trace>shared</Trace></Source>
                    <Before Include="@(Source)" />
                    <Source Remove="@(Lookup)" MatchOnMetadata="@(Names)" />
                    <Lookup Update="*"><Trace>later</Trace></Lookup>
                </ItemGroup>
            </Project>
            """,
            collection);

        EvaluateHistory(project, instanceModel, "Source", "Before").ShouldBe(
        [
            "Source:cross-tuple:other", "Source:missing-group:shared",
            "Before:match:shared", "Before:cross-tuple:other", "Before:missing-group:shared",
        ]);
    }

    [Theory]
    [InlineData(false, "CaseSensitive", "Source:candidate:VALUE")]
    [InlineData(true, "CaseSensitive", "Source:candidate:VALUE")]
    [InlineData(false, "CaseInsensitive", null)]
    [InlineData(true, "CaseInsensitive", null)]
    public void MetadataRemovalPreservesTheSelectedComparisonMode(bool instanceModel, string options, string? expectedItem)
    {
        using ProjectCollection collection = new();
        Project project = CreateProject(
            $"""
            <Project>
                <ItemGroup>
                    <Lookup Include="key"><Trace>value</Trace><Group>one</Group></Lookup>
                    <Source Include="candidate"><Trace>VALUE</Trace><Group>ONE</Group></Source>
                    <Source Remove="@(Lookup)" MatchOnMetadata="Trace;Group" MatchOnMetadataOptions="{options}" />
                </ItemGroup>
            </Project>
            """,
            collection);

        List<string> actual = EvaluateHistory(project, instanceModel, "Source");
        if (expectedItem is null)
        {
            actual.ShouldBeEmpty();
        }
        else
        {
            actual.ShouldBe([expectedItem]);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MetadataRemovalPreservesPathLikeTupleNormalization(bool instanceModel)
    {
        string alias = Path.Combine("folder", "..", "match.txt");
        using ProjectCollection collection = new();
        Project project = CreateProject(
            $"""
            <Project>
                <ItemGroup>
                    <Lookup Include="key"><Path>{alias}</Path><Group>one</Group></Lookup>
                    <Source Include="match"><Path>match.txt</Path><Group>one</Group><Trace>removed</Trace></Source>
                    <Source Include="other-group"><Path>match.txt</Path><Group>two</Group><Trace>kept</Trace></Source>
                    <Source Remove="@(Lookup)" MatchOnMetadata="Path;Group" MatchOnMetadataOptions="PathLike" />
                </ItemGroup>
            </Project>
            """,
            collection);

        EvaluateHistory(project, instanceModel, "Source").ShouldBe(["Source:other-group:kept"]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void QualifiedMetadataRoutingPreservesOperationPrecedenceAndEscaping(bool instanceModel)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        env.SetEnvironmentVariable("MSBuildDoNotExpandQualifiedMetadataInUpdateOperation", null);
        using ProjectCollection collection = new();
        Project project = CreateProject(
            """
            <Project>
                <ItemGroup>
                    <Source Include="same.txt"><Trace>first</Trace></Source>
                    <Source Include="same.txt"><Trace>second</Trace></Source>
                    <Lookup Include="same.txt"><Value>look%3Bup</Value></Lookup>
                    <Source Update="@(Source);@(Lookup)">
                        <Trace>%(Source.Trace)|%(lookup.Value)|%(Lookup.Missing)|%(Missing.Value)</Trace>
                    </Source>
                </ItemGroup>
            </Project>
            """,
            collection);

        EvaluateHistory(project, instanceModel, "Source").ShouldBe(
            ["Source:same.txt:first|look;up||", "Source:same.txt:second|look;up||"]);
    }

    [Fact]
    public void ReevaluatedIncludesPreserveBookmarkedExcludesAndPerItemMetadata()
    {
        using ProjectCollection collection = new();
        Project project = CreateProject(
            """
            <Project>
                <ItemGroup>
                    <Excluded Include="b.tmp" />
                    <Lookup Include="a.cs;b.tmp;c.cs" />
                    <Source Include="literal.txt;@(Lookup)" Exclude="@(Excluded);c.cs">
                        <Trace Condition="'%(Extension)' == '.cs'">%(Filename)</Trace>
                        <Trace Condition="'%(Extension)' != '.cs'">literal</Trace>
                    </Source>
                    <BeforeRemove Include="@(Source)" />
                    <Source Remove="*.txt" />
                    <Excluded Include="a.cs" />
                </ItemGroup>
            </Project>
            """,
            collection);
        var evaluator = CreateEvaluator(project);
        evaluator.ProcessItemGroups([.. project.Xml.ItemGroups], project.Xml.DirectoryPath);
        IItemProvider<ProjectItem> provider = evaluator;

        ProjectItem current = provider.GetItems("Source").ShouldHaveSingleItem();
        current.EvaluatedInclude.ShouldBe("a.cs");
        current.GetMetadataValue("Trace").ShouldBe("a");
        ProjectItem[] earlier = [.. provider.GetItems("BeforeRemove")];
        AssertIncludes(earlier, ["literal.txt", "a.cs"]);
        earlier[0].GetMetadataValue("Trace").ShouldBe("literal");
        earlier[1].GetMetadataValue("Trace").ShouldBe("a");
        current.GetMetadataValue("Trace").ShouldBe("a");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MetadataCollectionPreservesOrderAndBookmarks(bool instanceModel)
    {
        using ProjectCollection collection = new();
        Project project = CreateProject(
            """
            <Project>
                <PropertyGroup>
                    <Reference>@(Lookup)</Reference>
                </PropertyGroup>
                <ItemGroup>
                    <Lookup Include="first" />
                    <Flag Include="yes" />
                    <Zero Include="zero" />
                    <Single Include="single">
                        <Trace Condition="'@(Flag)' == 'yes'">$(Reference)</Trace>
                    </Single>
                    <Multiple Include="multiple">
                        <Trace>$(Reference)</Trace>
                        <Trace>%(Trace)|second</Trace>
                        <Trace Condition="'@(Flag)' == 'yes'">%(Trace)|third</Trace>
                    </Multiple>
                    <Zero Update="zero" />
                    <Single Update="single">
                        <Trace Condition="'@(Flag)' == 'yes'">%(Trace)|$(Reference)</Trace>
                    </Single>
                    <Multiple Update="multiple">
                        <Trace>%(Trace)|$(Reference)</Trace>
                        <Trace>%(Trace)|fourth</Trace>
                        <Trace Condition="'@(Flag)' == 'yes'">%(Trace)|fifth</Trace>
                    </Multiple>
                    <Lookup Include="later" />
                    <Flag Remove="@(Flag)" />
                </ItemGroup>
            </Project>
            """,
            collection);

        EvaluateHistory(project, instanceModel, "Zero", "Single", "Multiple").ShouldBe(
        [
            "Zero:zero:",
            "Single:single:first|first",
            "Multiple:multiple:first|second|third|first|fourth|fifth",
        ]);
    }

    [Fact]
    public void IncludeSelectionPreservesMixedFragmentOrderAndExcludes()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        var folder = env.CreateFolder();
        env.CreateFile(folder, "one.cs", contents: string.Empty);
        env.CreateFile(folder, "two.cs", contents: string.Empty);
        using ProjectCollection collection = new();
        Project project = CreateProject(
            """
            <Project>
                <ItemGroup>
                    <Lookup Include="from-reference.txt;excluded.txt"><Original>copied</Original></Lookup>
                    <Source Include="semi%3Bcolon.txt;@(Lookup);*.cs" Exclude="excluded.txt;two.cs">
                        <Trace>%(Filename)</Trace>
                    </Source>
                </ItemGroup>
            </Project>
            """,
            collection);
        project.Xml.FullPath = Path.Combine(folder.Path, "test.proj");
        var evaluator = CreateEvaluator(project);
        evaluator.ProcessItemGroups([.. project.Xml.ItemGroups], project.Xml.DirectoryPath);
        IItemProvider<ProjectItem> provider = evaluator;

        ProjectItem[] items = [.. provider.GetItems("Source")];
        AssertIncludes(items, ["semi;colon.txt", "from-reference.txt", "one.cs"]);
        items[0].GetMetadataValue("Trace").ShouldBe("semi;colon");
        items[1].GetMetadataValue("Trace").ShouldBe("from-reference");
        items[1].GetMetadataValue("Original").ShouldBe("copied");
        items[2].GetMetadataValue("Trace").ShouldBe("one");
    }

    [Theory]
    [InlineData("true")]
    [InlineData("false")]
    public void IncludeAllocationChangesPreserveEmptySelectionDiagnosticTiming(string condition)
    {
        using ProjectCollection collection = new();
        Project project = CreateProject(
            $"""
            <Project>
                <ItemGroup>
                    <Source Include="@(Missing)" Condition="{condition}">
                        <Trace>$([System.Int32]::Parse('invalid'))</Trace>
                    </Source>
                </ItemGroup>
            </Project>
            """,
            collection);
        var evaluator = CreateEvaluator(project);
        evaluator.ProcessItemGroups([.. project.Xml.ItemGroups], project.Xml.DirectoryPath);

        Should.Throw<InvalidProjectFileException>(() => CollectItems(evaluator));
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
    public void EmptyGlobRequestsDoNotEnableGlobCollection(bool recordItemElements, string itemGlobRequests)
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
    public void BookmarksPreserveEarlierItemsAcrossLookupSizes(int referencedItemTypeCount, bool caseSensitive)
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
            // Shredding deduplicates these names, so the uppercase lookup must use the bookmark mapping's comparer.
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

        var evaluator = CreateEvaluator(project);
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
    [InlineData(false, 0)]
    [InlineData(false, int.MaxValue)]
    [InlineData(true, 0)]
    [InlineData(true, int.MaxValue)]
    public void BookmarkedStatesPreserveMetadataAcrossLaterUpdatesAndRemoves(bool eagerReads, int removeThreshold)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        env.SetEnvironmentVariable("MSBUILDDICTIONARYBASEDITEMREMOVETHRESHOLD", removeThreshold.ToString(CultureInfo.InvariantCulture));
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

        var evaluator = CreateEvaluator(project);
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

    [Theory]
    [InlineData("target.cs;./target.cs;missing.cs", "first.txt;second.txt;other.cs;last.txt")]
    [InlineData("TARGET.CS", "first.txt;second.txt;other.cs;last.txt")]
    [InlineData("*.cs;target.cs", "first.txt;second.txt;last.txt")]
    [InlineData("@(ToRemove)", "first.txt;second.txt;other.cs;last.txt")]
    [InlineData("missing.cs", "first.txt;target.cs;./target.cs;TARGET.CS;second.txt;other.cs;last.txt")]
    public void IndexedRemovesMatchNormalizedPathsAndPreserveSnapshots(string removeSpec, string expectedIncludes)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        env.SetEnvironmentVariable("MSBUILDDICTIONARYBASEDITEMREMOVETHRESHOLD", "0");
        using ProjectCollection collection = new();
        const string initialIncludes = "first.txt;target.cs;./target.cs;TARGET.CS;second.txt;other.cs;last.txt";
        Project project = CreateProject(
            $"""
            <Project>
                <ItemGroup>
                    <Source Include="{initialIncludes}" />
                    <ToRemove Include="target.cs;target.cs" />
                    <Before Include="@(Source)" />
                    <Source Remove="{removeSpec}" />
                </ItemGroup>
            </Project>
            """,
            collection);
        var evaluator = CreateEvaluator(project);
        evaluator.ProcessItemGroups([.. project.Xml.ItemGroups], project.Xml.DirectoryPath);
        IItemProvider<ProjectItem> provider = evaluator;

        for (int read = 0; read < 2; read++)
        {
            List<string> actual = [];
            foreach (ProjectItem item in provider.GetItems("Source"))
            {
                actual.Add(item.EvaluatedInclude);
            }

            actual.ShouldBe(expectedIncludes.Split(';'));

            List<string> before = [];
            foreach (ProjectItem item in provider.GetItems("Before"))
            {
                before.Add(item.EvaluatedInclude);
            }

            before.ShouldBe(initialIncludes.Split(';'));
        }
    }

    [Fact]
    public void IndexedRemovesTrackUpdatesAddsAndClears()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        env.SetEnvironmentVariable("MSBUILDDICTIONARYBASEDITEMREMOVETHRESHOLD", "0");
        using ProjectCollection collection = new();
        Project project = CreateProject(
            """
            <Project>
                <ItemGroup>
                    <Source Include="a.cs;./a.cs;keep.txt">
                        <Trace>initial</Trace>
                    </Source>
                    <Source Remove="missing.cs" />
                    <Source Update="a.cs">
                        <Trace>updated</Trace>
                    </Source>
                    <BeforeRemove Include="@(Source)" />
                    <Source Include="b.cs;b.cs">
                        <Trace>added</Trace>
                    </Source>
                    <Source Remove="a.cs" />
                    <Source Remove="b.cs" />
                    <AfterRemove Include="@(Source)" />
                    <Source Remove="@(Source)" />
                    <Source Include="a.cs;./a.cs;c.cs">
                        <Trace>fresh</Trace>
                    </Source>
                    <Source Remove="c.cs" />
                </ItemGroup>
            </Project>
            """,
            collection);
        var evaluator = CreateEvaluator(project);
        evaluator.ProcessItemGroups([.. project.Xml.ItemGroups], project.Xml.DirectoryPath);
        string[] expected =
        [
            "BeforeRemove:a.cs:updated",
            "BeforeRemove:./a.cs:updated",
            "BeforeRemove:keep.txt:initial",
            "AfterRemove:keep.txt:initial",
            "Source:a.cs:fresh",
            "Source:./a.cs:fresh",
        ];

        for (int read = 0; read < 2; read++)
        {
            List<string> actual = [];
            foreach (ProjectItem data in CollectItems(evaluator))
            {
                actual.Add($"{data.ItemType}:{data.EvaluatedInclude}:{data.GetMetadataValue("Trace")}");
            }

            actual.ShouldBe(expected);
        }
    }

    [Fact]
    public void IncludeConditionsAndOrderSurviveReevaluationAndLaterDeclarations()
    {
        using ProjectCollection collection = new();
        Project project = CreateProject(
            """
            <Project>
                <ItemGroup>
                    <Source Include="a.cs;b.tmp" />
                    <Source Include="c.cs;d.tmp" Condition="false" />
                </ItemGroup>
                <ItemGroup>
                    <Source Remove="*.tmp" />
                </ItemGroup>
                <ItemGroup>
                    <Source Include="e.cs" />
                </ItemGroup>
            </Project>
            """,
            collection);
        var evaluator = CreateEvaluator(project);
        ProjectItemGroupElement[] groups = [.. project.Xml.ItemGroups];
        ProjectItemElement[] elements = [.. project.Xml.Items];
        evaluator.ProcessItemGroups([groups[0]], project.Xml.DirectoryPath);
        ImmutableArray<ProjectItem> before = CollectItems(evaluator);

        AssertIncludes(before, ["a.cs", "b.tmp", "c.cs", "d.tmp"]);
        for (int i = 0; i < before.Length; i++)
        {
            before[i].Xml.ShouldBeSameAs(elements[i / 2]);
        }

        AssertIncludes(CollectItems(evaluator, ignoreConditions: false), ["a.cs", "b.tmp"]);

        evaluator.ProcessItemGroups([groups[1]], project.Xml.DirectoryPath);
        ImmutableArray<ProjectItem> after = CollectItems(evaluator);

        AssertIncludes(after, ["a.cs", "c.cs"]);
        for (int i = 0; i < after.Length; i++)
        {
            after[i].ShouldNotBeSameAs(before[i * 2]);
            after[i].Xml.ShouldBeSameAs(before[i * 2].Xml);
        }

        evaluator.ProcessItemGroups([groups[2]], project.Xml.DirectoryPath);
        ImmutableArray<ProjectItem> final = CollectItems(evaluator);
        AssertIncludes(final, ["a.cs", "c.cs", "e.cs"]);
        final[2].Xml.ShouldBeSameAs(elements[3]);
        AssertIncludes(CollectItems(evaluator, ignoreConditions: false), ["a.cs", "e.cs"]);
        AssertIncludes(before, ["a.cs", "b.tmp", "c.cs", "d.tmp"]);
    }

    [Fact]
    public void RepeatedElementProcessingStoresIndependentConditions()
    {
        using ProjectCollection collection = new();
        Project project = CreateProject(
            """
            <Project>
                <ItemGroup>
                    <Source Include="a.cs" Condition="true" />
                </ItemGroup>
            </Project>
            """,
            collection);
        var evaluator = CreateEvaluator(project);
        ProjectItemGroupElement[] groups = [.. project.Xml.ItemGroups];
        ProjectItemElement element = project.Xml.Items.ShouldHaveSingleItem();
        evaluator.ProcessItemGroups([.. groups], project.Xml.DirectoryPath);
        ProjectItem before = CollectItems(evaluator).ShouldHaveSingleItem();

        element.Condition = "false";
        evaluator.ProcessItemGroups([.. groups], project.Xml.DirectoryPath);
        ImmutableArray<ProjectItem> after = CollectItems(evaluator);

        AssertIncludes(after, ["a.cs", "a.cs"]);
        after[0].Xml.ShouldBeSameAs(element);
        after[1].Xml.ShouldBeSameAs(element);
        CollectItems(evaluator, ignoreConditions: false).ShouldHaveSingleItem().ShouldBeSameAs(before);
    }

    [Fact]
    public void BatchedUpdatesPreserveEarlierItemsAndReuseUnchangedItems()
    {
        using ProjectCollection collection = new();
        Project project = CreateProject(
            """
            <Project>
                <ItemGroup>
                    <Source Include="a.cs;b.cs">
                        <Trace>initial</Trace>
                    </Source>
                </ItemGroup>
                <ItemGroup>
                    <Source Update="a.cs">
                        <Trace>updated</Trace>
                    </Source>
                </ItemGroup>
            </Project>
            """,
            collection);
        var evaluator = CreateEvaluator(project);
        ProjectItemGroupElement[] groups = [.. project.Xml.ItemGroups];
        evaluator.ProcessItemGroups([groups[0]], project.Xml.DirectoryPath);
        ImmutableArray<ProjectItem> before = CollectItems(evaluator);

        evaluator.ProcessItemGroups([groups[1]], project.Xml.DirectoryPath);
        ImmutableArray<ProjectItem> after = CollectItems(evaluator);

        AssertIncludes(before, ["a.cs", "b.cs"]);
        AssertIncludes(after, ["a.cs", "b.cs"]);
        before[0].ShouldNotBeSameAs(after[0]);
        after[0].Xml.ShouldBeSameAs(before[0].Xml);
        before[0].GetMetadataValue("Trace").ShouldBe("initial");
        after[0].GetMetadataValue("Trace").ShouldBe("updated");
        after[1].ShouldBeSameAs(before[1]);
        after[1].GetMetadataValue("Trace").ShouldBe("initial");
    }

    [Fact]
    public void BookmarkedPositionsSurviveGrowthAcrossSegments()
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

        var evaluator = CreateEvaluator(project);
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
    public void BookmarkedGlobDoesNotInheritLaterRemovePatterns()
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

        var evaluator = CreateEvaluator(project);
        evaluator.ProcessItemGroups([.. project.Xml.ItemGroups], project.Xml.DirectoryPath);

        List<string> actual = [];
        foreach (ProjectItem data in CollectItems(evaluator))
        {
            actual.Add($"{data.ItemType}:{data.EvaluatedInclude}");
        }

        string[] expected = ["Source:a.cs", "Captured:a.cs", "Captured:b.tmp"];
        actual.ShouldBe(expected);
    }

    [Theory]
    [InlineData("a.cs;*.cs", false, "initial-update")]
    [InlineData("a.cs;*.cs", true, "initial-pending-update")]
    [InlineData("a.cs;a.cs", false, "initial")]
    [InlineData("a.cs;a.cs", true, "initial-pending")]
    [InlineData("a.cs;./a.cs", false, "initial")]
    [InlineData("a.cs;./a.cs", true, "initial-pending")]
    [InlineData("a.cs;A.CS", true, "initial-pending")]
    [InlineData("a.cs;b.cs;b.cs", true, "initial-pending-update")]
    [InlineData("./a.cs;B.CS", true, "initial-pending-update")]
    [InlineData("a.cs;a%2Ecs", true, "initial-pending")]
    [InlineData("a.cs;b.cs;*.cs", false, "initial-update")]
    [InlineData("a.cs;b.cs", true, "initial-pending-update")]
    public void RejectedUpdateBatchDoesNotLeaveTentativeEntries(string updateSpec, bool queueEarlierUpdate, string expectedBTrace)
    {
        using ProjectCollection collection = new();
        string pendingUpdate = queueEarlierUpdate
            ? """
              <Compile Update="b.cs">
                  <Trace>%(Trace)-pending</Trace>
              </Compile>
              """
            : string.Empty;
        Project project = CreateProject(
            $"""
            <Project>
                <ItemGroup>
                    <Compile Include="a.cs;b.cs">
                        <Trace>initial</Trace>
                    </Compile>
                    {pendingUpdate}
                    <Compile Update="{updateSpec}">
                        <Trace>%(Trace)-update</Trace>
                    </Compile>
                </ItemGroup>
            </Project>
            """,
            collection);

        var evaluator = CreateEvaluator(project);
        evaluator.ProcessItemGroups([.. project.Xml.ItemGroups], project.Xml.DirectoryPath);

        ImmutableArray<ProjectItem> items = CollectItems(evaluator);
        AssertIncludes(items, ["a.cs", "b.cs"]);
        items[0].GetMetadataValue("Trace").ShouldBe("initial-update");
        items[1].GetMetadataValue("Trace").ShouldBe(expectedBTrace);
    }

    [Theory]
    [InlineData("file$.cs", "file$.cs", true)]
    [InlineData("file@.cs", "file@.cs", true)]
    [InlineData("file$@.cs", "file$@.cs", true)]
    [InlineData("file(name).cs", "file(name).cs", true)]
    [InlineData("suffix$", "suffix$", true)]
    [InlineData("suffix@", "suffix@", true)]
    [InlineData("$file.cs", "$file*.cs", false)]
    [InlineData("@file1.cs", "@file?.cs", false)]
    [InlineData("$@file.cs", "$@file*.cs", false)]
    [InlineData("@$file1.cs", "@$file?.cs", false)]
    [InlineData("$file%name.cs", "$file%name.cs", false)]
    [InlineData("@file%name.cs", "@file%name.cs", false)]
    [InlineData("file%name.cs", "file%name.cs", false)]
    [InlineData("file.cs", "@(Lookup)", false)]
    public void LiteralUpdateBatchEligibilityPreservesExpansionTokenRules(string include, string updateSpec, bool canBatch)
    {
        using ProjectCollection collection = new();
        Project project = CreateProject(
            $"""
            <Project>
                <ItemGroup>
                    <Lookup Include="{include}" />
                    <Source Include="{include}"><Trace>initial</Trace></Source>
                    <Source Update="{updateSpec}"><Trace>updated</Trace></Source>
                </ItemGroup>
            </Project>
            """,
            collection);
        var evaluator = CreateEvaluator(project);
        evaluator.ProcessItemGroups([.. project.Xml.ItemGroups], project.Xml.DirectoryPath);
        IItemProvider<ProjectItem> provider = evaluator;
        using EventSourceTestHelper listener = new();

        ProjectItem item = provider.GetItems("Source").ShouldHaveSingleItem();
        item.EvaluatedInclude.ShouldBe(include);
        item.GetMetadataValue("Trace").ShouldBe("updated");

        // Batched Updates do not call Apply; non-batchable Updates run after the Source Include.
        int expectedApplications = canBatch ? 1 : 2;
        CountEvents(listener, nameof(MSBuildEventSource.ApplyLazyItemOperationsStart), "Source").ShouldBe(expectedApplications);
    }

    [Theory]
    [InlineData("a.cs;b.cs", "initial-pending-candidate", 3)]
    [InlineData("a.cs;a.cs", "initial-pending", 1)]
    [InlineData("a.cs;*.cs", "initial-pending-candidate", 3)]
    [InlineData("a.cs;a%2Ecs", "initial-pending", 1)]
    public void ReevaluatedUpdateBatchCandidatesPreserveOrderingAndMetadataCounts(
        string updateSpec, string expectedBTrace, int expectedMetadataEvaluations)
    {
        const string candidateCondition = "'ReplayedBatchCandidate' == 'ReplayedBatchCandidate'";
        using ProjectCollection collection = new();
        Project project = CreateProject(
            $"""
            <Project>
                <ItemGroup>
                    <Source Include="a.cs;b.cs;b.cs;c.tmp"><Trace>initial</Trace></Source>
                    <Source Update="b.cs"><Trace>%(Trace)-pending</Trace></Source>
                    <Source Update="{updateSpec}">
                        <Trace Condition="{candidateCondition}">%(Trace)-candidate</Trace>
                    </Source>
                    <Source Update="a.cs"><Trace>%(Trace)-later</Trace></Source>
                    <BeforeRemove Include="@(Source)" />
                    <Source Remove="*.tmp" />
                </ItemGroup>
            </Project>
            """,
            collection);
        var evaluator = CreateEvaluator(project);
        evaluator.ProcessItemGroups([.. project.Xml.ItemGroups], project.Xml.DirectoryPath);
        IItemProvider<ProjectItem> provider = evaluator;
        using EventSourceTestHelper listener = new();

        ICollection<ProjectItem> current = provider.GetItems("Source");
        ProjectItem[] currentItems = [.. current];
        AssertIncludes(currentItems, ["a.cs", "b.cs", "b.cs"]);
        currentItems[0].GetMetadataValue("Trace").ShouldBe("initial-candidate-later");
        currentItems[1].GetMetadataValue("Trace").ShouldBe(expectedBTrace);
        currentItems[2].GetMetadataValue("Trace").ShouldBe(expectedBTrace);
        CountEvents(listener, nameof(MSBuildEventSource.EvaluateConditionStart), candidateCondition).ShouldBe(expectedMetadataEvaluations);

        // The bookmark re-evaluates the same operations without patterns from the later Remove.
        ProjectItem[] earlierItems = [.. provider.GetItems("BeforeRemove")];
        AssertIncludes(earlierItems, ["a.cs", "b.cs", "b.cs", "c.tmp"]);
        earlierItems[0].GetMetadataValue("Trace").ShouldBe("initial-candidate-later");
        earlierItems[1].GetMetadataValue("Trace").ShouldBe(expectedBTrace);
        earlierItems[2].GetMetadataValue("Trace").ShouldBe(expectedBTrace);
        earlierItems[3].GetMetadataValue("Trace").ShouldBe("initial");
        CountEvents(listener, nameof(MSBuildEventSource.EvaluateConditionStart), candidateCondition).ShouldBe(expectedMetadataEvaluations);

        provider.GetItems("Source").ShouldBeSameAs(current);
        currentItems[0].GetMetadataValue("Trace").ShouldBe("initial-candidate-later");
        currentItems[1].GetMetadataValue("Trace").ShouldBe(expectedBTrace);
        currentItems[2].GetMetadataValue("Trace").ShouldBe(expectedBTrace);
        CountEvents(listener, nameof(MSBuildEventSource.EvaluateConditionStart), candidateCondition).ShouldBe(0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MultiLiteralUpdateBatchesPreserveNormalizedMatchesAndBookmarkedStates(bool instanceModel)
    {
        using ProjectCollection collection = new();
        Project project = CreateProject(
            """
            <Project>
                <ItemGroup>
                    <Source Include="a.cs;./a.cs;b.cs;c.cs"><Trace>initial</Trace></Source>
                    <Source Update="A.CS;b.cs"><Trace>%(Trace)-first</Trace></Source>
                    <Source Update="c.cs"><Trace>%(Trace)-second</Trace></Source>
                    <Before Include="@(Source)" />
                    <Source Update="./b.cs;c.cs"><Trace>%(Trace)-overlap</Trace></Source>
                </ItemGroup>
            </Project>
            """,
            collection);

        EvaluateHistory(project, instanceModel, "Before", "Source").ShouldBe(
        [
            "Before:a.cs:initial-first", "Before:./a.cs:initial-first",
            "Before:b.cs:initial-first", "Before:c.cs:initial-second",
            "Source:a.cs:initial-first", "Source:./a.cs:initial-first",
            "Source:b.cs:initial-first-overlap", "Source:c.cs:initial-second-overlap",
        ]);
    }

    [Theory]
    [InlineData("missing.cs", "true")]
    [InlineData("a.cs", "false")]
    [InlineData("$(Missing)", "true")]
    public void LiteralUpdatesWithoutEffectsStillCacheTheRequestedSnapshot(string updateSpec, string condition)
    {
        const string itemType = "UnchangedBatchCacheSource";
        using ProjectCollection collection = new();
        Project project = CreateProject(
            $"""
            <Project>
                <ItemGroup>
                    <UnchangedBatchCacheSource Include="a.cs">
                        <Trace>initial</Trace>
                    </UnchangedBatchCacheSource>
                    <UnchangedBatchCacheSource Update="{updateSpec}" Condition="{condition}">
                        <Trace>updated</Trace>
                    </UnchangedBatchCacheSource>
                </ItemGroup>
            </Project>
            """,
            collection);
        var evaluator = CreateEvaluator(project);
        evaluator.ProcessItemGroups([.. project.Xml.ItemGroups], project.Xml.DirectoryPath);
        IItemProvider<ProjectItem> provider = evaluator;
        using EventSourceTestHelper listener = new();

        for (int read = 0; read < 2; read++)
        {
            ICollection<ProjectItem> result = provider.GetItems(itemType);
            provider.GetItems(itemType).ShouldBeSameAs(result);
            ProjectItem[] items = [.. result];
            items.Length.ShouldBe(1);
            items[0].EvaluatedInclude.ShouldBe("a.cs");
            items[0].GetMetadataValue("Trace").ShouldBe("initial");
            CountEvents(listener, nameof(MSBuildEventSource.ApplyLazyItemOperationsStart), itemType).ShouldBe(read == 0 ? 1 : 0);
        }
    }

    [Fact]
    public void LiteralUpdateBatchesCacheExactBookmarkedStatesWithoutRepeatingMetadataEvaluation()
    {
        const string updateCondition = "'LiteralUpdateBatchCache' == 'LiteralUpdateBatchCache'";
        using ProjectCollection collection = new();
        Project project = CreateProject(
            $"""
            <Project>
                <ItemGroup>
                    <BatchCacheSource Include="a.cs;b.cs;c.cs">
                        <Trace>initial</Trace>
                    </BatchCacheSource>
                    <BatchCacheSource Update="a.cs">
                        <Trace Condition="{updateCondition}">%(Trace)-a</Trace>
                    </BatchCacheSource>
                    <BatchCacheSource Update="b.cs">
                        <Trace Condition="{updateCondition}">%(Trace)-b</Trace>
                    </BatchCacheSource>
                    <BatchCacheBefore Include="@(BatchCacheSource)" />
                    <BatchCacheSource Update="c.cs">
                        <Trace Condition="{updateCondition}">%(Trace)-c</Trace>
                    </BatchCacheSource>
                    <BatchCacheAfter Include="@(BatchCacheSource)" />
                </ItemGroup>
            </Project>
            """,
            collection);
        var evaluator = CreateEvaluator(project);
        evaluator.ProcessItemGroups([.. project.Xml.ItemGroups], project.Xml.DirectoryPath);
        IItemProvider<ProjectItem> provider = evaluator;
        using EventSourceTestHelper listener = new();

        for (int read = 0; read < 2; read++)
        {
            ProjectItem[] currentItems = [.. provider.GetItems("BatchCacheSource")];
            currentItems.Length.ShouldBe(3);
            currentItems[0].EvaluatedInclude.ShouldBe("a.cs");
            currentItems[0].GetMetadataValue("Trace").ShouldBe("initial-a");
            currentItems[1].EvaluatedInclude.ShouldBe("b.cs");
            currentItems[1].GetMetadataValue("Trace").ShouldBe("initial-b");
            currentItems[2].EvaluatedInclude.ShouldBe("c.cs");
            currentItems[2].GetMetadataValue("Trace").ShouldBe("initial-c");
            CountEvents(listener, nameof(MSBuildEventSource.EvaluateConditionStart), updateCondition).ShouldBe(read == 0 ? 3 : 0);

            // This bookmark ends at the b.cs Update, even though final Source evaluation has already updated c.cs.
            ProjectItem[] earlierItems = [.. provider.GetItems("BatchCacheBefore")];
            earlierItems.Length.ShouldBe(3);
            earlierItems[0].GetMetadataValue("Trace").ShouldBe("initial-a");
            earlierItems[1].GetMetadataValue("Trace").ShouldBe("initial-b");
            earlierItems[2].GetMetadataValue("Trace").ShouldBe("initial");
            CountEvents(listener, nameof(MSBuildEventSource.EvaluateConditionStart), updateCondition).ShouldBe(0);

            ProjectItem[] laterItems = [.. provider.GetItems("BatchCacheAfter")];
            laterItems.Length.ShouldBe(3);
            laterItems[0].GetMetadataValue("Trace").ShouldBe("initial-a");
            laterItems[1].GetMetadataValue("Trace").ShouldBe("initial-b");
            laterItems[2].GetMetadataValue("Trace").ShouldBe("initial-c");
            CountEvents(listener, nameof(MSBuildEventSource.EvaluateConditionStart), updateCondition).ShouldBe(0);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EquivalentRemoveGlobPatternsReuseCachedSnapshotsWithoutReapplyingOperations(bool varyPatternCase)
    {
        const string itemType = "CacheEqualitySource";
        string repeatedExclusions = varyPatternCase ? "*.G.CS;*.TMP" : "*.g.cs;*.tmp";
        using ProjectCollection collection = new();
        Project project = CreateProject(
            $"""
            <Project>
                <ItemGroup>
                    <CacheEqualitySource Include="a.cs;b.tmp" />
                    <CacheEqualityCopy Include="@(CacheEqualitySource)" />
                    <CacheEqualitySource Remove="*.tmp;*.g.cs" />
                </ItemGroup>
                <ItemGroup>
                    <CacheEqualitySource Remove="{repeatedExclusions}" />
                </ItemGroup>
                <ItemGroup>
                    <CacheEqualitySource Remove="*.cs" />
                </ItemGroup>
            </Project>
            """,
            collection);
        var evaluator = CreateEvaluator(project);
        IItemProvider<ProjectItem> provider = evaluator;
        ProjectItemGroupElement[] groups = [.. project.Xml.ItemGroups];
        using EventSourceTestHelper listener = new();

        // Reordered exclusions reuse the cached Include; casing also follows the filesystem's policy.
        // Distinct contexts require the Include to execute again before the Removes.
        int repeatedApplications = !varyPatternCase || FileUtilities.PathComparer.Equals("*.tmp", "*.TMP") ? 2 : 3;
        int[] expectedApplications = [2, repeatedApplications, 4];
        for (int phase = 0; phase < groups.Length; phase++)
        {
            evaluator.ProcessItemGroups([groups[phase]], project.Xml.DirectoryPath);
            ICollection<ProjectItem> result = provider.GetItems(itemType);
            provider.GetItems(itemType).ShouldBeSameAs(result);
            List<string> includes = [];
            foreach (ProjectItem item in result)
            {
                includes.Add(item.EvaluatedInclude);
            }

            string[] expectedIncludes = phase < 2 ? ["a.cs"] : [];
            includes.ShouldBe(expectedIncludes);
            CountEvents(listener, nameof(MSBuildEventSource.ApplyLazyItemOperationsStart), itemType).ShouldBe(expectedApplications[phase]);
        }

        // The earlier reference needs the unpruned Include, not either nonempty later Remove glob patterns.
        ICollection<ProjectItem> copiedResult = provider.GetItems("CacheEqualityCopy");
        provider.GetItems("CacheEqualityCopy").ShouldBeSameAs(copiedResult);
        List<string> copiedIncludes = [];
        foreach (ProjectItem item in copiedResult)
        {
            copiedIncludes.Add(item.EvaluatedInclude);
        }

        string[] expectedCopies = ["a.cs", "b.tmp"];
        copiedIncludes.ShouldBe(expectedCopies);
        CountEvents(listener, nameof(MSBuildEventSource.ApplyLazyItemOperationsStart), itemType).ShouldBe(1);
    }

    private static List<string> EvaluateHistory(Project project, bool instanceModel, params string[] itemTypes)
    {
        List<string> result = [];
        if (instanceModel)
        {
            ProjectInstance instance = new(project.Xml, globalProperties: null, toolsVersion: null, project.ProjectCollection);
            foreach (string itemType in itemTypes)
            {
                AddItems(itemType, instance.GetItems(itemType));
            }
        }
        else
        {
            project.ReevaluateIfNecessary();
            foreach (string itemType in itemTypes)
            {
                AddItems(itemType, project.GetItems(itemType));
            }
        }

        return result;

        void AddItems(string itemType, IEnumerable<IItem> items)
        {
            foreach (IItem item in items)
            {
                result.Add($"{itemType}:{item.EvaluatedInclude}:{item.GetMetadataValue("Trace")}");
            }
        }
    }

    private static int CountEvents(EventSourceTestHelper listener, string eventName, string firstPayload)
    {
        int count = 0;
        foreach (var eventData in listener.GetEvents())
        {
            if (eventData.EventName == eventName
                && eventData.Payload is [string payload, ..]
                && payload == firstPayload)
            {
                count++;
            }
        }

        return count;
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
        EvaluatedItemElementRecorder recorder = default)
        => new(
            project.TestOnlyGetPrivateData,
            new ProjectItem.ProjectItemFactory(project),
            TestLoggingContext.CreateTestContext(new BuildEventContext(1, 2, 3, 4)),
            new EvaluationProfiler(shouldTrackElements: false),
            EvaluationContext.Create(EvaluationContext.SharingPolicy.Isolated),
            recorder);
}
