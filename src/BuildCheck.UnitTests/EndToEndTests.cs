// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Build.Evaluation;
using Microsoft.Build.UnitTests;
using Microsoft.Build.UnitTests.Shared;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Build.BuildCheck.UnitTests;

public class EndToEndTests : IDisposable
{
    private readonly TestEnvironment _env;

    public EndToEndTests(ITestOutputHelper output)
    {
        _env = TestEnvironment.Create(output);

        // this is needed to ensure the binary logger does not pollute the environment
        _env.WithEnvironmentInvariant();
    }

    private static string TestAssetsRootPath { get; } = Path.Combine(AppContext.BaseDirectory, "TestAssets");

    public void Dispose() => _env.Dispose();

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public void SampleAnalyzerIntegrationTest(bool buildInOutOfProcessNode, bool analysisRequested)
    {
        string contents = $"""
            <Project Sdk="Microsoft.NET.Sdk" DefaultTargets="Hello">
                
                <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFramework>net8.0</TargetFramework>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
                </PropertyGroup>
                  
                <PropertyGroup Condition="$(Test) == true">
                <TestProperty>Test</TestProperty>
                </PropertyGroup>
                 
                <ItemGroup>
                <ProjectReference Include=".\FooBar-Copy.csproj" />
                </ItemGroup>
                  
                <Target Name="Hello">
                <Message Importance="High" Condition="$(Test2) == true" Text="XYZABC" />
                </Target>
                
            </Project>
            """;

        string contents2 = $"""
            <Project Sdk="Microsoft.NET.Sdk">
                               
                <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFramework>net8.0</TargetFramework>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
                </PropertyGroup>
                                 
                <PropertyGroup Condition="$(Test) == true">
                <TestProperty>Test</TestProperty>
                </PropertyGroup>
                                
                <ItemGroup>
                <Reference Include="bin/foo.dll" />
                </ItemGroup>
                                
                <Target Name="Hello">
                <Message Importance="High" Condition="$(Test2) == true" Text="XYZABC" />
                </Target>
                               
            </Project>
            """;
        TransientTestFolder workFolder = _env.CreateFolder(createFolder: true);
        TransientTestFile projectFile = _env.CreateFile(workFolder, "FooBar.csproj", contents);
        TransientTestFile projectFile2 = _env.CreateFile(workFolder, "FooBar-Copy.csproj", contents2);

        // var cache = new SimpleProjectRootElementCache();
        // ProjectRootElement xml = ProjectRootElement.OpenProjectOrSolution(projectFile.Path, /*unused*/null, /*unused*/null, cache, false /*Not explicitly loaded - unused*/);

        TransientTestFile config = _env.CreateFile(workFolder, "editorconfig.json",
            /*lang=json,strict*/
            """
            {
                "BC0101": {
                    "IsEnabled": true,
                    "Severity": "Error"
                },
                "COND0543": {
                    "IsEnabled": false,
                    "Severity": "Error",
                    "EvaluationAnalysisScope": "AnalyzedProjectOnly",
                    "CustomSwitch": "QWERTY"
                },
                "BLA": {
                    "IsEnabled": false
                }
            }
            """);

        // OSX links /var into /private, which makes Path.GetTempPath() return "/var..." but Directory.GetCurrentDirectory return "/private/var...".
        // This discrepancy breaks path equality checks in analyzers if we pass to MSBuild full path to the initial project.
        // See if there is a way of fixing it in the engine - tracked: https://github.com/orgs/dotnet/projects/373/views/1?pane=issue&itemId=55702688.
        _env.SetCurrentDirectory(Path.GetDirectoryName(projectFile.Path));

        _env.SetEnvironmentVariable("MSBUILDNOINPROCNODE", buildInOutOfProcessNode ? "1" : "0");
        _env.SetEnvironmentVariable("MSBUILDLOGPROPERTIESANDITEMSAFTEREVALUATION", "1");
        string output = RunnerUtilities.ExecBootstrapedMSBuild(
            $"{Path.GetFileName(projectFile.Path)} /m:1 -nr:False -restore" +
            (analysisRequested ? " -analyze" : string.Empty), out bool success, false, _env.Output);
        _env.Output.WriteLine(output);
        success.ShouldBeTrue();
        // The conflicting outputs warning appears - but only if analysis was requested
        if (analysisRequested)
        {
            output.ShouldContain("BC0101");
        }
        else
        {
            output.ShouldNotContain("BC0101");
        }
    }

    [Theory]
    [InlineData("CustomAnalyzer", "AnalysisCandidate", new[] { "CustomRule1" })]
    public void CustomAnalyzerTest(string caName, string acName, string[] expectedRegistedRulesNames)
    {
        using (var env = TestEnvironment.Create())
        {
            var caProjectPath = Path.Combine(TestAssetsRootPath, caName, $"{caName}.csproj");
            string caBuildLog = RunnerUtilities.ExecBootstrapedMSBuild(
                 $"{caProjectPath} /m:1 -nr:False -restore /p:OutputPath={env.CreateFolder().Path}", out bool success);

            if (success)
            {
                var caNugetPackageFullPath = Regex.Match(caBuildLog, @"Successfully created package '(.*?)'").Groups[1].Value;
                var analysisCandidateSolutionPath = Path.Combine(TestAssetsRootPath, acName);
                AddCutomDataSourceToNugetConfig(analysisCandidateSolutionPath, Path.GetDirectoryName(caNugetPackageFullPath));

                string acBuildLog = RunnerUtilities.ExecBootstrapedMSBuild(
                 $"{Path.Combine(analysisCandidateSolutionPath, $"{acName}.csproj")} /m:1 -nr:False -restore /p:OutputPath={env.CreateFolder().Path} -verbosity:d", out _);

            }
        }
    }

    private void AddCutomDataSourceToNugetConfig(string filePath, string pathToCustomDataSource)
    {
        var nugetTemplatePath = Path.Combine(filePath, "nugetTemplate.config");
        string existingContent = File.ReadAllText(nugetTemplatePath);

        string modifiedContent = existingContent.Replace("LocalPackageSourcePlaceholder", pathToCustomDataSource);
        File.WriteAllText(Path.Combine(filePath, "nuget.config"), modifiedContent);
    }
}
