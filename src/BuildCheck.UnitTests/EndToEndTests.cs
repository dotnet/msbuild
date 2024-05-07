// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using Microsoft.Build.Shared;
using Microsoft.Build.UnitTests;
using Microsoft.Build.UnitTests.Shared;
using Newtonsoft.Json.Linq;
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

    private static string TestAssetsRootPath { get; } = Path.Combine(Path.GetDirectoryName(typeof(EndToEndTests).Assembly.Location) ?? AppContext.BaseDirectory, "TestAssets");

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
    [InlineData(new[] { "CustomAnalyzer" }, "AnalysisCandidate", new[] { "CustomRule1", "CustomRule2" })]
    [InlineData(new[] { "CustomAnalyzer", "CustomAnalyzer2", "InvalidCustomAnalyzer" }, "AnalysisCandidateWithMultipleAnalyzersInjected", new[] { "CustomRule1", "CustomRule2", "CustomRule3" }, new[] { "InvalidCustomAnalyzer" })]
    public void CustomAnalyzerTest(string[] customAnalyzerNames, string analysisCandidate, string[] expectedRegisteredRules, string[]? expectedRejectedRules = null)
    {
        using (var env = TestEnvironment.Create())
        {
            var candidatesNugetFullPaths = BuildAnalyzerRules(env, customAnalyzerNames);

            candidatesNugetFullPaths.ShouldNotBeEmpty("Nuget package with custom analyzer was not generated or detected.");

            var analysisCandidatePath = Path.Combine(TestAssetsRootPath, analysisCandidate);
            AddCustomDataSourceToNugetConfig(analysisCandidatePath, candidatesNugetFullPaths);

            string projectAnalysisBuildLog = RunnerUtilities.ExecBootstrapedMSBuild(
                $"{Path.Combine(analysisCandidatePath, $"{analysisCandidate}.csproj")} /m:1 -nr:False -restore /p:OutputPath={env.CreateFolder().Path} -analyze -verbosity:d",
                out bool successBuild);
            successBuild.ShouldBeTrue();

            foreach (string registeredRule in expectedRegisteredRules)
            {
                projectAnalysisBuildLog.ShouldContain(ResourceUtilities.FormatResourceStringStripCodeAndKeyword("CustomAnalyzerSuccessfulAcquisition", registeredRule));
            }

            if (expectedRejectedRules != null)
            {
                foreach (string rejectedRule in expectedRejectedRules)
                {
                    projectAnalysisBuildLog.ShouldContain(ResourceUtilities.FormatResourceStringStripCodeAndKeyword("CustomAnalyzerBaseTypeNotAssignable", rejectedRule));
                }
            }
        }
    }

    private IList<string> BuildAnalyzerRules(TestEnvironment env, string[] customAnalyzerNames)
    {
        var candidatesNugetFullPaths = new List<string>();

        foreach (var customAnalyzerName in customAnalyzerNames)
        {
            var candidateAnalysisProjectPath = Path.Combine(TestAssetsRootPath, customAnalyzerName, $"{customAnalyzerName}.csproj");
            var nugetPackResults = RunnerUtilities.ExecBootstrapedMSBuild(
                 $"{candidateAnalysisProjectPath} /m:1 -nr:False -restore /p:OutputPath={env.CreateFolder().Path} -getTargetResult:Build", out bool success, attachProcessId: false);

            success.ShouldBeTrue();

            string? candidatesNugetPackageFullPath = (string?)(JObject.Parse(nugetPackResults)?["TargetResults"]?["Build"]?["Items"]?[0]?["RelativeDir"] ?? string.Empty);

            candidatesNugetPackageFullPath.ShouldNotBeNull();
            candidatesNugetFullPaths.Add(candidatesNugetPackageFullPath);
        }

        return candidatesNugetFullPaths;
    }

    private void AddCustomDataSourceToNugetConfig(string analysisCandidatePath, IList<string> candidatesNugetPackageFullPaths)
    {
        var nugetTemplatePath = Path.Combine(analysisCandidatePath, "nugetTemplate.config");

        var doc = new XmlDocument();
        doc.LoadXml(File.ReadAllText(nugetTemplatePath));
        if (doc.DocumentElement != null)
        {
            XmlNode? packageSourcesNode = doc.SelectSingleNode("//packageSources");
            for (int i = 0; i < candidatesNugetPackageFullPaths.Count; i++)
            {
                AddPackageSource(doc, packageSourcesNode, $"Key{i}", Path.GetDirectoryName(candidatesNugetPackageFullPaths[i]) ?? string.Empty);
            }

            doc.Save(Path.Combine(analysisCandidatePath, "nuget.config"));
        }
    }

    private void AddPackageSource(XmlDocument doc, XmlNode? packageSourcesNode, string key, string value)
    {
        if (packageSourcesNode != null)
        {
            XmlElement addNode = doc.CreateElement("add");

            PopulateXmlAttribute(doc, addNode, "key", key);
            PopulateXmlAttribute(doc, addNode, "value", value);

            packageSourcesNode.AppendChild(addNode);
        }
    }

    private void PopulateXmlAttribute(XmlDocument doc, XmlNode node, string attributeName, string attributeValue)
    {
        node.ShouldNotBeNull($"The attribute {attributeName} can not be populated with {attributeValue}. Xml node is null.");
        var attribute = doc.CreateAttribute(attributeName);
        attribute.Value = attributeValue;
        node.Attributes!.Append(attribute);
    }
}
