﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Xml;
using Microsoft.Build.Shared;
using Microsoft.Build.UnitTests;
using Microsoft.Build.UnitTests.Shared;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Build.BuildCheck.UnitTests;

public class EndToEndTests : IDisposable
{
    private const string EditorConfigFileName = ".editorconfig";

    private readonly TestEnvironment _env;

    public EndToEndTests(ITestOutputHelper output)
    {
        _env = TestEnvironment.Create(output);

        // this is needed to ensure the binary logger does not pollute the environment
        _env.WithEnvironmentInvariant();
    }

    private static string AssemblyLocation { get; } = Path.Combine(Path.GetDirectoryName(typeof(EndToEndTests).Assembly.Location) ?? AppContext.BaseDirectory);

    private static string TestAssetsRootPath { get; } = Path.Combine(AssemblyLocation, "TestAssets");

    public void Dispose() => _env.Dispose();

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public void SampleAnalyzerIntegrationTest_AnalyzeOnBuild(bool buildInOutOfProcessNode, bool analysisRequested)
    {
        PrepareSampleProjectsAndConfig(buildInOutOfProcessNode, out TransientTestFile projectFile, new List<(string, string)>() { ("BC0101", "warning") });

        string output = RunnerUtilities.ExecBootstrapedMSBuild(
            $"{Path.GetFileName(projectFile.Path)} /m:1 -nr:False -restore" +
            (analysisRequested ? " -analyze" : string.Empty), out bool success, false, _env.Output, timeoutMilliseconds: 120_000);
        _env.Output.WriteLine(output);

        success.ShouldBeTrue();

        // The analyzer warnings should appear - but only if analysis was requested.
        if (analysisRequested)
        {
            output.ShouldContain("BC0101");
            output.ShouldContain("BC0102");
            output.ShouldContain("BC0103");
        }
        else
        {
            output.ShouldNotContain("BC0101");
            output.ShouldNotContain("BC0102");
            output.ShouldNotContain("BC0103");
        }
    }

    [Theory]
    [InlineData(true, true, "warning")]
    [InlineData(true, true, "error")]
    [InlineData(true, true, "suggestion")]
    [InlineData(false, true, "warning")]
    [InlineData(false, true, "error")]
    [InlineData(false, true, "suggestion")]
    [InlineData(false, false, "warning")]
    public void SampleAnalyzerIntegrationTest_ReplayBinaryLogOfAnalyzedBuild(bool buildInOutOfProcessNode, bool analysisRequested, string BC0101Severity)
    {
        PrepareSampleProjectsAndConfig(buildInOutOfProcessNode, out TransientTestFile projectFile, new List<(string, string)>() { ("BC0101", BC0101Severity) });

        var projectDirectory = Path.GetDirectoryName(projectFile.Path);
        string logFile = _env.ExpectFile(".binlog").Path;

        _ = RunnerUtilities.ExecBootstrapedMSBuild(
            $"{Path.GetFileName(projectFile.Path)} /m:1 -nr:False -restore {(analysisRequested ? "-analyze" : string.Empty)} -bl:{logFile}",
            out bool success, false, _env.Output, timeoutMilliseconds: 120_000);

        success.ShouldBeTrue();

        string output = RunnerUtilities.ExecBootstrapedMSBuild(
         $"{logFile} -flp:logfile={Path.Combine(projectDirectory!, "logFile.log")};verbosity=diagnostic",
         out success, false, _env.Output, timeoutMilliseconds: 120_000);

        _env.Output.WriteLine(output);

        success.ShouldBeTrue();

        // The conflicting outputs warning appears - but only if analysis was requested
        if (analysisRequested)
        {
            output.ShouldContain("BC0101");
            output.ShouldContain("BC0102");
            output.ShouldContain("BC0103");
        }
        else
        {
            output.ShouldNotContain("BC0101");
            output.ShouldNotContain("BC0102");
            output.ShouldNotContain("BC0103");
        }
    }

    [Theory]
    [InlineData("warning", "warning BC0101", new string[] { "error BC0101" })]
    [InlineData("error", "error BC0101", new string[] { "warning BC0101" })]
    [InlineData("suggestion", "BC0101", new string[] { "error BC0101", "warning BC0101" })]
    [InlineData("default", "warning BC0101", new string[] { "error BC0101" })]
    [InlineData("none", null, new string[] { "BC0101"})]
    public void EditorConfig_SeverityAppliedCorrectly(string BC0101Severity, string expectedOutputValues, string[] unexpectedOutputValues)
    {
        PrepareSampleProjectsAndConfig(true, out TransientTestFile projectFile, new List<(string, string)>() { ("BC0101", BC0101Severity) });

        string output = RunnerUtilities.ExecBootstrapedMSBuild(
            $"{Path.GetFileName(projectFile.Path)} /m:1 -nr:False -restore -analyze",
            out bool success, false, _env.Output, timeoutMilliseconds: 120_000);

        success.ShouldBeTrue();

        if (!string.IsNullOrEmpty(expectedOutputValues))
        {
            output.ShouldContain(expectedOutputValues);
        }

        foreach (string unexpectedOutputValue in unexpectedOutputValues)
        {
            output.ShouldNotContain(unexpectedOutputValue);
        }
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public void SampleAnalyzerIntegrationTest_AnalyzeOnBinaryLogReplay(bool buildInOutOfProcessNode, bool analysisRequested)
    {
        PrepareSampleProjectsAndConfig(buildInOutOfProcessNode, out TransientTestFile projectFile, new List<(string, string)>() { ("BC0101", "warning") });

        string? projectDirectory = Path.GetDirectoryName(projectFile.Path);
        string logFile = _env.ExpectFile(".binlog").Path;

        _ = RunnerUtilities.ExecBootstrapedMSBuild(
            $"{Path.GetFileName(projectFile.Path)} /m:1 -nr:False -restore -bl:{logFile}",
            out bool success, false, _env.Output, timeoutMilliseconds: 120_000);

        success.ShouldBeTrue();

        string output = RunnerUtilities.ExecBootstrapedMSBuild(
         $"{logFile} -flp:logfile={Path.Combine(projectDirectory!, "logFile.log")};verbosity=diagnostic {(analysisRequested ? "-analyze" : string.Empty)}",
         out success, false, _env.Output, timeoutMilliseconds: 120_000);

        _env.Output.WriteLine(output);

        success.ShouldBeTrue();

        // The conflicting outputs warning appears - but only if analysis was requested
        if (analysisRequested)
        {
            output.ShouldContain("BC0101");
            output.ShouldContain("BC0102");
            output.ShouldContain("BC0103");
        }
        else
        {
            output.ShouldNotContain("BC0101");
            output.ShouldNotContain("BC0102");
            output.ShouldNotContain("BC0103");
        }
    }

    [Theory]
    [InlineData(null, "Property is derived from environment variable: 'TEST'. Properties should be passed explicitly using the /p option.")]
    [InlineData(true, "Property is derived from environment variable: 'TEST' with value: 'FromEnvVariable'. Properties should be passed explicitly using the /p option.")]
    [InlineData(false, "Property is derived from environment variable: 'TEST'. Properties should be passed explicitly using the /p option.")]
    public void NoEnvironmentVariableProperty_Test(bool? customConfigEnabled, string expectedMessage)
    {
        List<(string RuleId, (string ConfigKey, string Value) CustomConfig)>? customConfigData = null;

        if (customConfigEnabled.HasValue)
        {
            customConfigData = new List<(string, (string, string))>()
            {
                ("BC0103", ("allow_displaying_environment_variable_value", customConfigEnabled.Value ? "true" : "false")),
            };
        }

        PrepareSampleProjectsAndConfig(
            buildInOutOfProcessNode: true,
            out TransientTestFile projectFile,
            new List<(string, string)>() { ("BC0103", "error") },
            customConfigData);

        string output = RunnerUtilities.ExecBootstrapedMSBuild(
            $"{Path.GetFileName(projectFile.Path)} /m:1 -nr:False -restore -analyze", out bool success, false, _env.Output, timeoutMilliseconds: 120_000);

        output.ShouldContain(expectedMessage);
    }

    [Theory]
    [InlineData("AnalysisCandidate", new[] { "CustomRule1", "CustomRule2" })]
    [InlineData("AnalysisCandidateWithMultipleAnalyzersInjected", new[] { "CustomRule1", "CustomRule2", "CustomRule3" }, true)]
    public void CustomAnalyzerTest_NoEditorConfig(string analysisCandidate, string[] expectedRegisteredRules, bool expectedRejectedAnalyzers = false)
    {
        using (var env = TestEnvironment.Create())
        {
            var analysisCandidatePath = Path.Combine(TestAssetsRootPath, analysisCandidate);
            AddCustomDataSourceToNugetConfig(analysisCandidatePath);

            string projectAnalysisBuildLog = RunnerUtilities.ExecBootstrapedMSBuild(
                $"{Path.Combine(analysisCandidatePath, $"{analysisCandidate}.csproj")} /m:1 -nr:False -restore -analyze -verbosity:n",
                out bool successBuild);
            successBuild.ShouldBeTrue(projectAnalysisBuildLog);

            foreach (string registeredRule in expectedRegisteredRules)
            {
                projectAnalysisBuildLog.ShouldContain(ResourceUtilities.FormatResourceStringStripCodeAndKeyword("CustomAnalyzerSuccessfulAcquisition", registeredRule));
            }

            if (expectedRejectedAnalyzers)
            {
                projectAnalysisBuildLog.ShouldContain(ResourceUtilities.FormatResourceStringStripCodeAndKeyword(
                    "CustomAnalyzerBaseTypeNotAssignable",
                    "InvalidAnalyzer",
                    "InvalidCustomAnalyzer, Version=15.1.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a"));
            }
        }
    }

    [Theory]
    [InlineData("AnalysisCandidate", "X01234", "error", "error X01234")]
    [InlineData("AnalysisCandidateWithMultipleAnalyzersInjected", "X01234", "warning", "warning X01234")]
    public void CustomAnalyzerTest_WithEditorConfig(string analysisCandidate, string ruleId, string severity, string expectedMessage)
    {
        using (var env = TestEnvironment.Create())
        {
            var analysisCandidatePath = Path.Combine(TestAssetsRootPath, analysisCandidate);
            AddCustomDataSourceToNugetConfig(analysisCandidatePath);
            File.WriteAllText(Path.Combine(analysisCandidatePath, EditorConfigFileName), ReadEditorConfig(
                new List<(string, string)>() { (ruleId, severity) },
                ruleToCustomConfig: null,
                analysisCandidatePath));

            string projectAnalysisBuildLog = RunnerUtilities.ExecBootstrapedMSBuild(
                $"{Path.Combine(analysisCandidatePath, $"{analysisCandidate}.csproj")} /m:1 -nr:False -restore -analyze -verbosity:n", out bool _, timeoutMilliseconds: 120_000);

            projectAnalysisBuildLog.ShouldContain(expectedMessage);
        }
    }

    private void AddCustomDataSourceToNugetConfig(string analysisCandidatePath)
    {
        var nugetTemplatePath = Path.Combine(analysisCandidatePath, "nugetTemplate.config");

        var doc = new XmlDocument();
        doc.LoadXml(File.ReadAllText(nugetTemplatePath));
        if (doc.DocumentElement != null)
        {
            XmlNode? packageSourcesNode = doc.SelectSingleNode("//packageSources");

            // The test packages are generated during the test project build and saved in CustomAnalyzers folder.
            string analyzersPackagesPath = Path.Combine(Directory.GetParent(AssemblyLocation)?.Parent?.FullName ?? string.Empty, "CustomAnalyzers");
            AddPackageSource(doc, packageSourcesNode, "Key", analyzersPackagesPath);

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

    private void PrepareSampleProjectsAndConfig(
        bool buildInOutOfProcessNode,
        out TransientTestFile projectFile,
        IEnumerable<(string RuleId, string Severity)>? ruleToSeverity,
        IEnumerable<(string RuleId, (string ConfigKey, string Value) CustomConfig)>? ruleToCustomConfig = null)
    {
        string testAssetsFolderName = "SampleAnalyzerIntegrationTest";
        TransientTestFolder workFolder = _env.CreateFolder(createFolder: true);
        TransientTestFile testFile = _env.CreateFile(workFolder, "somefile");

        string contents = ReadAndAdjustProjectContent("Project1");
        string contents2 = ReadAndAdjustProjectContent("Project2");

        projectFile = _env.CreateFile(workFolder, "FooBar.csproj", contents);
        TransientTestFile projectFile2 = _env.CreateFile(workFolder, "FooBar-Copy.csproj", contents2);

        _env.CreateFile(workFolder, ".editorconfig", ReadEditorConfig(ruleToSeverity, ruleToCustomConfig, testAssetsFolderName));

        // OSX links /var into /private, which makes Path.GetTempPath() return "/var..." but Directory.GetCurrentDirectory return "/private/var...".
        // This discrepancy breaks path equality checks in analyzers if we pass to MSBuild full path to the initial project.
        // See if there is a way of fixing it in the engine - tracked: https://github.com/orgs/dotnet/projects/373/views/1?pane=issue&itemId=55702688.
        _env.SetCurrentDirectory(Path.GetDirectoryName(projectFile.Path));

        _env.SetEnvironmentVariable("MSBUILDNOINPROCNODE", buildInOutOfProcessNode ? "1" : "0");
        _env.SetEnvironmentVariable("MSBUILDLOGPROPERTIESANDITEMSAFTEREVALUATION", "1");

        _env.SetEnvironmentVariable("TEST", "FromEnvVariable");

        string ReadAndAdjustProjectContent(string fileName) =>
            File.ReadAllText(Path.Combine(TestAssetsRootPath, testAssetsFolderName, fileName))
                .Replace("TestFilePath", testFile.Path)
                .Replace("WorkFolderPath", workFolder.Path);
    }

    private string ReadEditorConfig(
        IEnumerable<(string RuleId, string Severity)>? ruleToSeverity,
        IEnumerable<(string RuleId, (string ConfigKey, string Value) CustomConfig)>? ruleToCustomConfig,
        string testAssetsFolderName)
    {
        string configContent = File.ReadAllText(Path.Combine(TestAssetsRootPath, testAssetsFolderName, $"{EditorConfigFileName}test"));

        PopulateRuleToSeverity(ruleToSeverity, ref configContent);
        PopulateRuleToCustomConfig(ruleToCustomConfig, ref configContent);

        return configContent;
    }

    private void PopulateRuleToSeverity(IEnumerable<(string RuleId, string Severity)>? ruleToSeverity, ref string configContent)
    {
        if (ruleToSeverity != null && ruleToSeverity.Any())
        {
            foreach (var rule in ruleToSeverity)
            {
                configContent = configContent.Replace($"build_check.{rule.RuleId}.Severity={rule.RuleId}Severity", $"build_check.{rule.RuleId}.Severity={rule.Severity}");
            }
        }
    }

    private void PopulateRuleToCustomConfig(IEnumerable<(string RuleId, (string ConfigKey, string Value) CustomConfig)>? ruleToCustomConfig, ref string configContent)
    {
        if (ruleToCustomConfig != null && ruleToCustomConfig.Any())
        {
            foreach (var rule in ruleToCustomConfig)
            {
                configContent = configContent.Replace($"build_check.{rule.RuleId}.CustomConfig=dummy", $"build_check.{rule.RuleId}.{rule.CustomConfig.ConfigKey}={rule.CustomConfig.Value}");
            }
        }
    }
}
