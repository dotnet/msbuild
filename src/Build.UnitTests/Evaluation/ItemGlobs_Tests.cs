// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Shared;
using Microsoft.Build.UnitTests;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests.Evaluation;

/// <summary>
/// Tests for the <c>MSBuildItemGlob</c> items synthesized from the include/exclude/remove globs of
/// selected item types when the <c>MSBuildProvideItemGlobs</c> property is set.
/// </summary>
public sealed class ItemGlobs_Tests(ITestOutputHelper output)
{
    private static Project CreateProject(string body, IDictionary<string, string>? globalProperties = null)
    {
        string projectContent = $"""
            <Project>
            {body}
            </Project>
            """;

        using ProjectRootElementFromString rootElement = new(projectContent);
        return new Project(rootElement.Project, globalProperties, toolsVersion: null);
    }

    private static List<ProjectItem> GlobItemsFor(Project project, string itemType) =>
        project.GetItems("MSBuildItemGlob").Where(i => i.EvaluatedInclude == itemType).ToList();

    private static HashSet<string> SplitList(string value) =>
        new(value.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries), StringComparer.Ordinal);

    [Fact]
    public void NotCreatedWithoutOptIn()
    {
        var project = CreateProject("""
                <ItemGroup>
                    <Compile Include="*.cs" />
                </ItemGroup>
            """);

        project.GetItems("MSBuildItemGlob").ShouldBeEmpty();
    }

    [Fact]
    public void CreatedForRequestedItemType()
    {
        var project = CreateProject("""
                <PropertyGroup>
                    <MSBuildProvideItemGlobs>Compile</MSBuildProvideItemGlobs>
                </PropertyGroup>
                <ItemGroup>
                    <Compile Include="*.cs" />
                </ItemGroup>
            """);

        var items = GlobItemsFor(project, "Compile");
        items.Count.ShouldBe(1);
        items[0].EvaluatedInclude.ShouldBe("Compile");
        items[0].GetMetadataValue("Include").ShouldBe("*.cs");
        items[0].GetMetadataValue("Exclude").ShouldBeEmpty();
        items[0].GetMetadataValue("Remove").ShouldBeEmpty();
    }

    [Fact]
    public void IncludeExcludeAndRemoveCaptured()
    {
        var project = CreateProject("""
                <PropertyGroup>
                    <MSBuildProvideItemGlobs>Compile</MSBuildProvideItemGlobs>
                </PropertyGroup>
                <ItemGroup>
                    <Compile Include="*.cs" Exclude="*.g.cs" />
                    <Compile Remove="*.designer.cs" />
                </ItemGroup>
            """);

        var items = GlobItemsFor(project, "Compile");
        items.Count.ShouldBe(1);
        items[0].GetMetadataValue("Include").ShouldBe("*.cs");
        items[0].GetMetadataValue("Exclude").ShouldBe("*.g.cs");
        items[0].GetMetadataValue("Remove").ShouldBe("*.designer.cs");
    }

    [Fact]
    public void LiteralExcludesAndRemovesArePreserved()
    {
        // Literal (non-glob) excludes and removes must be retained; only literal *includes* are dropped.
        var project = CreateProject("""
                <PropertyGroup>
                    <MSBuildProvideItemGlobs>Compile</MSBuildProvideItemGlobs>
                </PropertyGroup>
                <ItemGroup>
                    <Compile Include="*.cs" Exclude="Special.cs" />
                    <Compile Remove="Legacy.cs" />
                </ItemGroup>
            """);

        var items = GlobItemsFor(project, "Compile");
        items.Count.ShouldBe(1);
        items[0].GetMetadataValue("Exclude").ShouldBe("Special.cs");
        items[0].GetMetadataValue("Remove").ShouldBe("Legacy.cs");
    }

    [Fact]
    public void LiteralOnlyIncludeProducesNoItem()
    {
        // An include with no wildcards contributes no glob, matching Project.GetAllGlobs.
        var project = CreateProject("""
                <PropertyGroup>
                    <MSBuildProvideItemGlobs>Compile</MSBuildProvideItemGlobs>
                </PropertyGroup>
                <ItemGroup>
                    <Compile Include="Program.cs" />
                </ItemGroup>
            """);

        GlobItemsFor(project, "Compile").ShouldBeEmpty();
    }

    [Fact]
    public void OnlyRequestedItemTypesAreExposed()
    {
        var project = CreateProject("""
                <PropertyGroup>
                    <MSBuildProvideItemGlobs>Compile</MSBuildProvideItemGlobs>
                </PropertyGroup>
                <ItemGroup>
                    <Compile Include="*.cs" />
                    <Content Include="*.txt" />
                </ItemGroup>
            """);

        GlobItemsFor(project, "Compile").Count.ShouldBe(1);
        GlobItemsFor(project, "Content").ShouldBeEmpty();
    }

    [Fact]
    public void MultipleItemTypesAreExposed()
    {
        var project = CreateProject("""
                <PropertyGroup>
                    <MSBuildProvideItemGlobs>Compile;Content</MSBuildProvideItemGlobs>
                </PropertyGroup>
                <ItemGroup>
                    <Compile Include="*.cs" />
                    <Content Include="*.txt" />
                </ItemGroup>
            """);

        GlobItemsFor(project, "Compile").Single().GetMetadataValue("Include").ShouldBe("*.cs");
        GlobItemsFor(project, "Content").Single().GetMetadataValue("Include").ShouldBe("*.txt");
    }

    [Fact]
    public void PropertyReferencesAreExpandedButWildcardsPreserved()
    {
        var project = CreateProject("""
                <PropertyGroup>
                    <MSBuildProvideItemGlobs>Compile</MSBuildProvideItemGlobs>
                    <Prefix>generated</Prefix>
                </PropertyGroup>
                <ItemGroup>
                    <Compile Include="$(Prefix)_*.cs" />
                </ItemGroup>
            """);

        GlobItemsFor(project, "Compile").Single().GetMetadataValue("Include").ShouldBe("generated_*.cs");
    }

    [Fact]
    public void DocumentOrderIsPreserved()
    {
        var project = CreateProject("""
                <PropertyGroup>
                    <MSBuildProvideItemGlobs>Compile</MSBuildProvideItemGlobs>
                </PropertyGroup>
                <ItemGroup>
                    <Compile Include="a*.cs" />
                    <Compile Include="b*.cs" />
                    <Compile Include="c*.cs" />
                </ItemGroup>
            """);

        var includes = GlobItemsFor(project, "Compile").Select(i => i.GetMetadataValue("Include")).ToList();
        includes.ShouldBe(new[] { "a*.cs", "b*.cs", "c*.cs" });
    }

    [Fact]
    public void RemoveIsAttributedOnlyToPrecedingIncludes()
    {
        // <Compile Include="*.cs"/> then Remove then a re-including glob. The remove applies only to the
        // first include; the later "re-add" glob must not be attributed the earlier remove.
        var project = CreateProject("""
                <PropertyGroup>
                    <MSBuildProvideItemGlobs>Compile</MSBuildProvideItemGlobs>
                </PropertyGroup>
                <ItemGroup>
                    <Compile Include="*.cs" />
                    <Compile Remove="Generated.cs" />
                    <Compile Include="Generated*.cs" />
                </ItemGroup>
            """);

        var items = GlobItemsFor(project, "Compile");
        items.Count.ShouldBe(2);

        // Document order: first the "*.cs" include (which the remove applies to)...
        items[0].GetMetadataValue("Include").ShouldBe("*.cs");
        items[0].GetMetadataValue("Remove").ShouldBe("Generated.cs");

        // ...then the re-adding glob, which must NOT carry the earlier remove.
        items[1].GetMetadataValue("Include").ShouldBe("Generated*.cs");
        items[1].GetMetadataValue("Remove").ShouldBeEmpty();
    }

    [Fact]
    public void ConditionedOutElementsAreNotExposed()
    {
        var project = CreateProject("""
                <PropertyGroup>
                    <MSBuildProvideItemGlobs>Compile</MSBuildProvideItemGlobs>
                </PropertyGroup>
                <ItemGroup>
                    <Compile Include="kept*.cs" />
                    <Compile Include="dropped*.cs" Condition="false" />
                </ItemGroup>
            """);

        var includes = GlobItemsFor(project, "Compile").Select(i => i.GetMetadataValue("Include")).ToList();
        includes.ShouldBe(new[] { "kept*.cs" });
    }

    [Fact]
    public void CreatedWhenSetViaGlobalProperty()
    {
        var globalProperties = new Dictionary<string, string>
        {
            { "MSBuildProvideItemGlobs", "Compile" },
        };

        var project = CreateProject("""
                <ItemGroup>
                    <Compile Include="*.cs" />
                </ItemGroup>
            """,
            globalProperties);

        GlobItemsFor(project, "Compile").Single().GetMetadataValue("Include").ShouldBe("*.cs");
    }

    [Fact]
    public void ItemsAreIdenticalOnProjectAndProjectInstance()
    {
        // rainersigwald's requirement from #13681: items must not differ between Project and ProjectInstance.
        var project = CreateProject("""
                <PropertyGroup>
                    <MSBuildProvideItemGlobs>Compile</MSBuildProvideItemGlobs>
                </PropertyGroup>
                <ItemGroup>
                    <Compile Include="*.cs" Exclude="*.g.cs" />
                </ItemGroup>
            """);

        var projectItem = GlobItemsFor(project, "Compile").Single();

        ProjectInstance instance = project.CreateProjectInstance();
        var instanceItems = instance.GetItems("MSBuildItemGlob").Where(i => i.EvaluatedInclude == "Compile").ToList();
        instanceItems.Count.ShouldBe(1);

        instanceItems[0].GetMetadataValue("Include").ShouldBe(projectItem.GetMetadataValue("Include"));
        instanceItems[0].GetMetadataValue("Exclude").ShouldBe(projectItem.GetMetadataValue("Exclude"));
    }

    [Fact]
    public void MatchesGetAllGlobs()
    {
        // The synthesized items must carry exactly the include/exclude/remove data that
        // Project.GetAllGlobs returns for the item type.
        var project = CreateProject("""
                <PropertyGroup>
                    <MSBuildProvideItemGlobs>Compile</MSBuildProvideItemGlobs>
                </PropertyGroup>
                <ItemGroup>
                    <Compile Include="*.cs" Exclude="*.g.cs" />
                    <Compile Remove="*.designer.cs" />
                    <Compile Include="extra_*.cs" />
                </ItemGroup>
            """);

        List<GlobResult> globResults = project.GetAllGlobs("Compile");
        var items = GlobItemsFor(project, "Compile");

        items.Count.ShouldBe(globResults.Count);

        var expected = globResults
            .Select(g => (
                Include: string.Join(";", g.IncludeGlobs),
                Exclude: SplitList(string.Join(";", g.Excludes)),
                Remove: SplitList(string.Join(";", g.Removes))))
            .OrderBy(t => t.Include, StringComparer.Ordinal)
            .ToList();

        var actual = items
            .Select(i => (
                Include: i.GetMetadataValue("Include"),
                Exclude: SplitList(i.GetMetadataValue("Exclude")),
                Remove: SplitList(i.GetMetadataValue("Remove"))))
            .OrderBy(t => t.Include, StringComparer.Ordinal)
            .ToList();

        actual.Select(t => t.Include).ShouldBe(expected.Select(t => t.Include));
        for (int i = 0; i < expected.Count; i++)
        {
            actual[i].Exclude.ShouldBe(expected[i].Exclude);
            actual[i].Remove.ShouldBe(expected[i].Remove);
        }
    }

    [Fact]
    public void PatternsWithPercentEncodingRoundTripAndMatchGetAllGlobs()
    {
        // A '%' in a path is authored as %25 and, after evaluation, the glob contains %NN which must not be
        // re-interpreted as an escape sequence when read back from metadata. The synthesized values must
        // round-trip to exactly what GetAllGlobs reports (this is the escaping edge case).
        var project = CreateProject("""
                <PropertyGroup>
                    <MSBuildProvideItemGlobs>Compile</MSBuildProvideItemGlobs>
                </PropertyGroup>
                <ItemGroup>
                    <Compile Include="a%2512b_*.cs" Exclude="c%2512d_*.cs" />
                </ItemGroup>
            """);

        List<GlobResult> globResults = project.GetAllGlobs("Compile");
        globResults.Count.ShouldBe(1);

        var item = GlobItemsFor(project, "Compile").Single();
        item.GetMetadataValue("Include").ShouldBe(string.Join(";", globResults[0].IncludeGlobs));
        SplitList(item.GetMetadataValue("Exclude")).ShouldBe(SplitList(string.Join(";", globResults[0].Excludes)));

        // The '%12' must survive as-is, not be decoded to control char 0x12.
        item.GetMetadataValue("Include").ShouldContain("%12");
    }

    [Fact]
    public void PatternContainingSemicolonIsRecoverableFromEscapedMetadata()
    {
        // A single glob pattern that itself contains a literal ';' (here a directory named "a;b",
        // authored as %3B) is stored escaped, so it round-trips losslessly only via the escaped
        // metadata value: split on ';' and unescape each element, the way MSBuild treats item lists.
        var project = CreateProject("""
                <PropertyGroup>
                    <MSBuildProvideItemGlobs>Compile</MSBuildProvideItemGlobs>
                </PropertyGroup>
                <ItemGroup>
                    <Compile Include="a%3Bb/**/*.cs;c/**/*.cs" />
                </ItemGroup>
            """);

        // The include authors two patterns; the first contains a literal ';'.
        List<GlobResult> globResults = project.GetAllGlobs("Compile");
        List<string> expectedIncludes = globResults.SelectMany(g => g.IncludeGlobs).ToList();
        expectedIncludes.ShouldBe(new[] { "a;b/**/*.cs", "c/**/*.cs" });

        ProjectItem item = GlobItemsFor(project, "Compile").Single();

        // Lossless recovery: split the ESCAPED value on ';', then unescape each element.
        string escaped = item.GetMetadata("Include").EvaluatedValueEscaped;
        List<string> recovered = escaped.Split(';').Select(s => EscapingUtilities.UnescapeAll(s)!).ToList();
        recovered.ShouldBe(expectedIncludes);

        // By contrast, naively splitting the fully-unescaped value is ambiguous: the pattern's own
        // ';' is indistinguishable from the list separator, so it yields more elements than patterns.
        item.GetMetadataValue("Include").Split(';').Length.ShouldBeGreaterThan(expectedIncludes.Count);
    }

    [Fact]
    public void AvailableToTargets()
    {
        var project = CreateProject("""
                <PropertyGroup>
                    <MSBuildProvideItemGlobs>Compile</MSBuildProvideItemGlobs>
                </PropertyGroup>
                <ItemGroup>
                    <Compile Include="*.cs" />
                </ItemGroup>
                <Target Name="ShowGlobs">
                    <Message Text="Glob: %(MSBuildItemGlob.Identity) include=%(MSBuildItemGlob.Include)" Importance="High" />
                </Target>
            """);

        ProjectInstance instance = project.CreateProjectInstance();
        var mockLogger = new MockLogger(output);
        instance.Build(new[] { "ShowGlobs" }, new[] { mockLogger }).ShouldBeTrue();
        mockLogger.AssertLogContains("Glob: Compile include=*.cs");
    }
}
