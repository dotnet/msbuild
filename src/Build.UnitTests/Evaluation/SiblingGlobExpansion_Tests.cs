// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Linq;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Framework;
using Microsoft.Build.Internal;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests.Evaluation;

public sealed class SiblingGlobExpansion_Tests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly TestEnvironment _environment;

    public SiblingGlobExpansion_Tests(ITestOutputHelper output)
    {
        _output = output;
        _environment = TestEnvironment.Create(output);
    }

    public void Dispose()
    {
        _environment.Dispose();
        ChangeWaves.ResetStateForTests();
        EngineFileUtilities.CaptureLazyWildcardRegexes();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PreservesFragmentOrderDuplicatesMetadataAndRemoves(bool disableFusion)
    {
        _environment.SetEnvironmentVariable(
            "MSBUILDDISABLEFEATURESFROMVERSION",
            disableFusion ? ChangeWaves.Wave18_14.ToString() : null);
        ChangeWaves.ResetStateForTests();
        ChangeWaves.AreFeaturesEnabled(ChangeWaves.Wave18_13).ShouldBeTrue();
        ChangeWaves.AreFeaturesEnabled(ChangeWaves.Wave18_14).ShouldBe(!disableFusion);
        const string xml = """
            <Project>
              <ItemGroup>
                <Seed Include="from-reference.txt" />
                <None Include="Assets/**;literal.txt;**/*.props;@(Seed);**/*.targets;**/*.props" Exclude="obj/**">
                  <Origin>%(RecursiveDir)</Origin>
                </None>
                <None Remove="**/removed.props" />
              </ItemGroup>
            </Project>
            """;
        TransientTestProjectWithFiles files = _environment.CreateTestProjectWithFiles(
            xml,
            [
                Normalize("Assets/logo.svg"),
                Normalize("Assets/styles/theme.props"),
                Normalize("Assets/removed.props"),
                "Directory.Build.props",
                Normalize("build/Common.targets"),
                Normalize("obj/generated.props")
            ]);
        using ProjectCollection collection = new();
        collection.RegisterLogger(new MockLogger(_output));
        Project project = new(files.ProjectFile, null, null, collection);
        ProjectItem[] items = project.GetItems("None").ToArray();

        items.Select(item => item.EvaluatedInclude).ShouldBe(
        [
            Normalize("Assets/logo.svg"),
            Normalize("Assets/styles/theme.props"),
            "literal.txt",
            Normalize("Assets/styles/theme.props"),
            "Directory.Build.props",
            "from-reference.txt",
            Normalize("build/Common.targets"),
            Normalize("Assets/styles/theme.props"),
            "Directory.Build.props"
        ]);
        items.Select(item => item.GetMetadataValue("Origin")).ShouldBe(
        [
            "",
            Normalize("styles/"),
            "",
            Normalize("Assets/styles/"),
            "",
            "",
            Normalize("build/"),
            Normalize("Assets/styles/"),
            ""
        ]);
    }

    [Fact]
    public void LazyWildcardRemainsUnexpandedBetweenEagerSiblings()
    {
        _environment.SetEnvironmentVariable("MsBuildSkipEagerWildCardEvaluationRegexes", ".*lazy.*");
        EngineFileUtilities.CaptureLazyWildcardRegexes();
        const string xml = """
            <Project>
              <ItemGroup>
                <None Include="**/*.props;lazy/**/*.props;**/*.targets" />
              </ItemGroup>
            </Project>
            """;
        TransientTestProjectWithFiles files = _environment.CreateTestProjectWithFiles(
            xml, ["root.props", Normalize("build/Common.targets")]);
        using ProjectCollection collection = new();
        collection.RegisterLogger(new MockLogger(_output));
        Project project = new(files.ProjectFile, null, null, collection);

        project.GetItems("None").Select(item => item.EvaluatedInclude).ShouldBe(
            ["root.props", "lazy/**/*.props", Normalize("build/Common.targets")]);
    }

    [UnixOnlyTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void NewlineAncestorPreservesItemsWithFusionEnabledOrDisabled(bool disableFusion)
    {
        _environment.SetEnvironmentVariable(
            "MSBUILDDISABLEFEATURESFROMVERSION",
            disableFusion ? ChangeWaves.Wave18_14.ToString() : null);
        ChangeWaves.ResetStateForTests();
        const string xml = """
            <Project>
              <ItemGroup>
                <None Include="**/sub/*.cs;**/*.targets" />
                <Independent Include="**/sub/*.cs" />
              </ItemGroup>
            </Project>
            """;
        TransientTestProjectWithFiles files = _environment.CreateTestProjectWithFiles(
            xml,
            [
                Normalize("normal/sub/source.cs"),
                Normalize("line\nbreak/sub/source.cs"),
                Normalize("line\nbreak/build.targets")
            ]);
        using ProjectCollection collection = new();
        collection.RegisterLogger(new MockLogger(_output));
        Project project = new(files.ProjectFile, null, null, collection);

        project.GetItems("None").Select(item => item.EvaluatedInclude).ShouldBe(
            [Normalize("normal/sub/source.cs"), Normalize("line\nbreak/build.targets")]);
        project.GetItems("Independent").Select(item => item.EvaluatedInclude).ShouldBe(
            [Normalize("normal/sub/source.cs")]);
    }

    [Fact]
    public void FalseConditionStillSkipsFilesystemWideSibling()
    {
        const string xml = """
            <Project>
              <ItemGroup>
                <None Include="**/*.props;/**/*.props;**/*.targets" Condition="false" />
              </ItemGroup>
            </Project>
            """;
        TransientTestProjectWithFiles files = _environment.CreateTestProjectWithFiles(
            xml, ["root.props", Normalize("build/Common.targets")]);
        using ProjectCollection collection = new();
        collection.RegisterLogger(new MockLogger(_output));
        Project project = new(files.ProjectFile, null, null, collection);

        project.GetItems("None").ShouldBeEmpty();
        project.GetItemsIgnoringCondition("None").Select(item => item.EvaluatedInclude).ShouldBe(
            ["root.props", Normalize("build/Common.targets")]);
    }

    private static string Normalize(string path) => path.Replace('/', Path.DirectorySeparatorChar);
}
