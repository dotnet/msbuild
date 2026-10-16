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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MatchedItemArraysPreserveFilteringOrderAndStorage(bool includeMatchingItems)
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
        var evaluator = CreateEvaluator(project, default);
        evaluator.ProcessItemGroups([.. project.Xml.ItemGroups], project.Xml.DirectoryPath);
        IItemProvider<ProjectItem> provider = evaluator;

        ImmutableArray<ProjectItem> firstRead = provider.GetItems("Source").ShouldBeOfType<ImmutableArray<ProjectItem>>();
        ImmutableArray<ProjectItem> capturedRead = provider.GetItems("Captured").ShouldBeOfType<ImmutableArray<ProjectItem>>();
        provider.GetItems("Other").ShouldHaveSingleItem().EvaluatedInclude.ShouldBe("other.cs");
        ImmutableArray<ProjectItem> secondRead = provider.GetItems("Source").ShouldBeOfType<ImmutableArray<ProjectItem>>();
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
        var evaluator = CreateEvaluator(project, default);
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
        var evaluator = CreateEvaluator(project, default);
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

    [Theory]
    [InlineData("a.cs;*.cs", false, "initial-update")]
    [InlineData("a.cs;*.cs", true, "initial-pending-update")]
    [InlineData("a.cs;a.cs", false, "initial")]
    [InlineData("a.cs;a.cs", true, "initial-pending")]
    [InlineData("a.cs;./a.cs", false, "initial")]
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

        var evaluator = CreateEvaluator(project, default);
        evaluator.ProcessItemGroups([.. project.Xml.ItemGroups], project.Xml.DirectoryPath);

        ImmutableArray<ProjectItem> items = CollectItems(evaluator);
        AssertIncludes(items, ["a.cs", "b.cs"]);
        items[0].GetMetadataValue("Trace").ShouldBe("initial-update");
        items[1].GetMetadataValue("Trace").ShouldBe(expectedBTrace);
    }

    [Theory]
    [InlineData("missing.cs", "true")]
    [InlineData("a.cs", "false")]
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
        var evaluator = CreateEvaluator(project, default);
        evaluator.ProcessItemGroups([.. project.Xml.ItemGroups], project.Xml.DirectoryPath);
        IItemProvider<ProjectItem> provider = evaluator;
        using EventSourceTestHelper listener = new();

        for (int read = 0; read < 2; read++)
        {
            ProjectItem[] items = [.. provider.GetItems(itemType)];
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
        var evaluator = CreateEvaluator(project, default);
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
        var evaluator = CreateEvaluator(project, default);
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
            List<string> includes = [];
            foreach (ProjectItem item in provider.GetItems(itemType))
            {
                includes.Add(item.EvaluatedInclude);
            }

            string[] expectedIncludes = phase < 2 ? ["a.cs"] : [];
            includes.ShouldBe(expectedIncludes);
            CountEvents(listener, nameof(MSBuildEventSource.ApplyLazyItemOperationsStart), itemType).ShouldBe(expectedApplications[phase]);
        }

        // The earlier reference needs the unpruned Include, not either nonempty later Remove glob patterns.
        List<string> copiedIncludes = [];
        foreach (ProjectItem item in provider.GetItems("CacheEqualityCopy"))
        {
            copiedIncludes.Add(item.EvaluatedInclude);
        }

        string[] expectedCopies = ["a.cs", "b.tmp"];
        copiedIncludes.ShouldBe(expectedCopies);
        CountEvents(listener, nameof(MSBuildEventSource.ApplyLazyItemOperationsStart), itemType).ShouldBe(1);
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
        EvaluatedItemElementRecorder recorder)
        => new(
            project.TestOnlyGetPrivateData,
            new ProjectItem.ProjectItemFactory(project),
            TestLoggingContext.CreateTestContext(new BuildEventContext(1, 2, 3, 4)),
            new EvaluationProfiler(shouldTrackElements: false),
            EvaluationContext.Create(EvaluationContext.SharingPolicy.Isolated),
            recorder);
}
