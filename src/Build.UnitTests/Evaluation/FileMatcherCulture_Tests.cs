// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests.Evaluation;

public sealed class FileMatcherCulture_Tests : IDisposable
{
    private const string LegacyCultureEnvironmentVariable = "MSBUILDUSELEGACYCULTURESENSITIVEFILEGLOBS";

    private readonly CultureInfo _originalCulture = CultureInfo.CurrentCulture;
    private readonly TestEnvironment _environment;
    private readonly ITestOutputHelper _output;

    public FileMatcherCulture_Tests(ITestOutputHelper output)
    {
        _output = output;
        _environment = TestEnvironment.Create(output);
    }

    public void Dispose()
    {
        ChangeWaves.ResetStateForTests();
        CultureInfo.CurrentCulture = _originalCulture;
        _environment.Dispose();
    }

    [Theory]
    [InlineData("")]
    [InlineData("en-US")]
    [InlineData("tr-TR")]
    [InlineData("tr-CY")]
    [InlineData("az-Latn-AZ")]
    [InlineData("az-Cyrl-AZ")]
    public void ComplexItemGlobIsCultureInvariantByDefault(string cultureName)
    {
        string[] items = Evaluate(
            cultureName,
            "**/I/*.cs",
            "i/source.cs",
            "İ/source.cs",
            "ı/source.cs");

        items.ShouldBe([Normalize("i/source.cs")]);
    }

    [Theory]
    [InlineData("", "i/source.cs")]
    [InlineData("en-US", "i/source.cs;İ/source.cs")]
    [InlineData("tr-TR", "ı/source.cs")]
    [InlineData("tr-CY", "ı/source.cs")]
    [InlineData("az-Latn-AZ", "ı/source.cs")]
    [InlineData("az-Cyrl-AZ", "ı/source.cs")]
    public void LegacyCompatibilityOptInPreservesCultureSensitiveComplexItemGlob(
        string cultureName,
        string expectedItems)
    {
        _environment.SetEnvironmentVariable(LegacyCultureEnvironmentVariable, "1");

        string[] items = Evaluate(
            cultureName,
            "**/I/*.cs",
            "i/source.cs",
            "İ/source.cs",
            "ı/source.cs");

        items.ShouldBe(expectedItems.Split(';').Select(Normalize).OrderBy(item => item, StringComparer.Ordinal).ToArray());
    }

    [Fact]
    public void DisablingWaveRestoresCultureSensitiveComplexItemGlob()
    {
        _environment.SetEnvironmentVariable(
            "MSBUILDDISABLEFEATURESFROMVERSION",
            ChangeWaves.Wave18_11.ToString());
        ChangeWaves.ResetStateForTests();

        string[] items = Evaluate(
            "tr-TR",
            "**/I/*.cs",
            "i/source.cs",
            "ı/source.cs");

        items.ShouldBe([Normalize("ı/source.cs")]);
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("tr-TR")]
    public void RecursiveFilenameGlobIsCultureInvariant(string cultureName)
    {
        string[] items = Evaluate(
            cultureName,
            "**/I.cs",
            "i.cs",
            "ı.cs");

        items.ShouldBe([Normalize("i.cs")]);
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("tr-TR")]
    public void DirectoryPatternGlobIsCultureInvariant(string cultureName)
    {
        string[] items = Evaluate(
            cultureName,
            "**/I/**/*.cs",
            "i/source.cs",
            "ı/source.cs");

        items.ShouldBe([Normalize("i/source.cs")]);
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("tr-TR")]
    public void BuildManagerCultureDoesNotAffectComplexGlobEvaluation(string buildCultureName)
    {
        BuildProject(buildCultureName).ShouldBe([Normalize("i/source.cs")]);
    }

    [Theory]
    [InlineData("en-US", "i/source.cs")]
    [InlineData("tr-TR", "ı/source.cs")]
    public void BuildManagerCultureAffectsComplexGlobWithLegacyCompatibilityOptIn(
        string buildCultureName,
        string expectedItem)
    {
        _environment.SetEnvironmentVariable(LegacyCultureEnvironmentVariable, "1");

        BuildProject(buildCultureName).ShouldBe([Normalize(expectedItem)]);
    }

    [Theory]
    [InlineData(false, "i/source.cs")]
    [InlineData(true, "ı/source.cs")]
    public void IncludeAndRemoveUseSameCulturePolicy(bool legacyCulture, string expectedItem)
    {
        CultureInfo.CurrentCulture = new CultureInfo("tr-TR");
        _environment.SetEnvironmentVariable(
            LegacyCultureEnvironmentVariable,
            legacyCulture ? "1" : null);
        string projectContents = """
            <Project>
              <ItemGroup>
                <Compile Include="**/I/*.cs" />
                <Compile Remove="**/I/excluded.cs" />
              </ItemGroup>
            </Project>
            """.Cleanup();
        TransientTestProjectWithFiles projectFiles = _environment.CreateTestProjectWithFiles(
            projectContents,
            [
                Normalize("i/source.cs"),
                Normalize("i/excluded.cs"),
                Normalize("ı/source.cs"),
                Normalize("ı/excluded.cs"),
            ]);
        using ProjectCollection projectCollection = new();
        Project project = new(projectFiles.ProjectFile, globalProperties: null, toolsVersion: null, projectCollection);

        project.GetItems("Compile")
            .Select(item => item.EvaluatedInclude)
            .ShouldBe([Normalize(expectedItem)]);
    }

    private string[] BuildProject(string buildCultureName)
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        string projectContents = """
            <Project>
              <ItemGroup>
                <Compile Include="**/I/*.cs" />
              </ItemGroup>
              <Target Name="Build" />
            </Project>
            """.Cleanup();
        TransientTestProjectWithFiles projectFiles = _environment.CreateTestProjectWithFiles(
            projectContents,
            [Normalize("i/source.cs"), Normalize("ı/source.cs")]);
        using BuildManager buildManager = new();
        BuildParameters parameters = new()
        {
            Culture = new CultureInfo(buildCultureName),
            EnableNodeReuse = false,
            Loggers = [new MockLogger(_output)],
            MaxNodeCount = 1,
        };
        BuildRequestData request = new(
            projectFiles.ProjectFile,
            new Dictionary<string, string?>(),
            toolsVersion: null,
            targetsToBuild: ["Build"],
            hostServices: null,
            BuildRequestDataFlags.ProvideProjectStateAfterBuild);

        BuildResult result = buildManager.Build(parameters, request);

        result.OverallResult.ShouldBe(BuildResultCode.Success);
        result.ProjectStateAfterBuild.ShouldNotBeNull();
        string[] items = result.ProjectStateAfterBuild.GetItems("Compile")
            .Select(item => item.EvaluatedInclude)
            .ToArray();
        CultureInfo.CurrentCulture.ShouldBe(CultureInfo.InvariantCulture);
        return items;
    }

    private string[] Evaluate(string cultureName, string include, params string[] files)
    {
        CultureInfo.CurrentCulture = new CultureInfo(cultureName);
        string projectContents = $"""
            <Project>
              <ItemGroup>
                <Compile Include="{include}" />
              </ItemGroup>
            </Project>
            """.Cleanup();
        TransientTestProjectWithFiles projectFiles = _environment.CreateTestProjectWithFiles(
            projectContents,
            files.Select(Normalize).ToArray());

        using ProjectCollection projectCollection = new();
        Project project = new(projectFiles.ProjectFile, globalProperties: null, toolsVersion: null, projectCollection);

        CultureInfo.CurrentCulture.Name.ShouldBe(cultureName);
        return project.GetItems("Compile")
            .Select(item => item.EvaluatedInclude)
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray();
    }

    private static string Normalize(string path) => path.Replace('/', System.IO.Path.DirectorySeparatorChar);
}
