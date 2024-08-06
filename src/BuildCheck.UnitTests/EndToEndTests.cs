// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
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
    public void SampleCheckIntegrationTest_CheckOnBuild(bool buildInOutOfProcessNode, bool checkRequested)
    {
        PrepareSampleProjectsAndConfig(buildInOutOfProcessNode, out TransientTestFile projectFile);

        string output = RunnerUtilities.ExecBootstrapedMSBuild(
            $"{Path.GetFileName(projectFile.Path)} /m:1 -nr:False -restore" +
            (checkRequested ? " -analyze" : string.Empty), out bool success, false, _env.Output, timeoutMilliseconds: 120_000);
        _env.Output.WriteLine(output);

        success.ShouldBeTrue();

        // The check warnings should appear - but only if check was requested.
        if (checkRequested)
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
    public void SampleCheckIntegrationTest_ReplayBinaryLogOfCheckedBuild(bool buildInOutOfProcessNode, bool checkRequested, string BC0101Severity)
    {
        PrepareSampleProjectsAndConfig(buildInOutOfProcessNode, out TransientTestFile projectFile, BC0101Severity);

        var projectDirectory = Path.GetDirectoryName(projectFile.Path);
        string logFile = _env.ExpectFile(".binlog").Path;

        _ = RunnerUtilities.ExecBootstrapedMSBuild(
            $"{Path.GetFileName(projectFile.Path)} /m:1 -nr:False -restore {(checkRequested ? "-analyze" : string.Empty)} -bl:{logFile}",
            out bool success, false, _env.Output, timeoutMilliseconds: 120_000);

        success.ShouldBeTrue();

        string output = RunnerUtilities.ExecBootstrapedMSBuild(
         $"{logFile} -flp:logfile={Path.Combine(projectDirectory!, "logFile.log")};verbosity=diagnostic",
         out success, false, _env.Output, timeoutMilliseconds: 120_000);

        _env.Output.WriteLine(output);

        success.ShouldBeTrue();

        // The conflicting outputs warning appears - but only if check was requested
        if (checkRequested)
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
        PrepareSampleProjectsAndConfig(true, out TransientTestFile projectFile, BC0101Severity);

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
    public void SampleCheckIntegrationTest_CheckOnBinaryLogReplay(bool buildInOutOfProcessNode, bool checkRequested)
    {
        PrepareSampleProjectsAndConfig(buildInOutOfProcessNode, out TransientTestFile projectFile);

        string? projectDirectory = Path.GetDirectoryName(projectFile.Path);
        string logFile = _env.ExpectFile(".binlog").Path;

        _ = RunnerUtilities.ExecBootstrapedMSBuild(
            $"{Path.GetFileName(projectFile.Path)} /m:1 -nr:False -restore -bl:{logFile}",
            out bool success, false, _env.Output, timeoutMilliseconds: 120_000);

        success.ShouldBeTrue();

        string output = RunnerUtilities.ExecBootstrapedMSBuild(
         $"{logFile} -flp:logfile={Path.Combine(projectDirectory!, "logFile.log")};verbosity=diagnostic {(checkRequested ? "-analyze" : string.Empty)}",
         out success, false, _env.Output, timeoutMilliseconds: 120_000);

        _env.Output.WriteLine(output);

        success.ShouldBeTrue();

        // The conflicting outputs warning appears - but only if check was requested
        if (checkRequested)
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
    [InlineData("CheckCandidate", new[] { "CustomRule1", "CustomRule2" })]
    [InlineData("CheckCandidateWithMultipleChecksInjected", new[] { "CustomRule1", "CustomRule2", "CustomRule3" }, true)]
    public void CustomCheckTest(string checkCandidate, string[] expectedRegisteredRules, bool expectedRejectedChecks = false)
    {
        using (var env = TestEnvironment.Create())
        {
            var checkCandidatePath = Path.Combine(TestAssetsRootPath, checkCandidate);
            AddCustomDataSourceToNugetConfig(checkCandidatePath);

            string projectCheckBuildLog = RunnerUtilities.ExecBootstrapedMSBuild(
                $"{Path.Combine(checkCandidatePath, $"{checkCandidate}.csproj")} /m:1 -nr:False -restore /p:OutputPath={env.CreateFolder().Path} -analyze -verbosity:n",
                out bool successBuild);
            successBuild.ShouldBeTrue(projectCheckBuildLog);

            foreach (string registeredRule in expectedRegisteredRules)
            {
                projectCheckBuildLog.ShouldContain(ResourceUtilities.FormatResourceStringStripCodeAndKeyword("CustomCheckSuccessfulAcquisition", registeredRule));
            }

            if (expectedRejectedChecks)
            {
                projectCheckBuildLog.ShouldContain(ResourceUtilities.FormatResourceStringStripCodeAndKeyword("CustomCheckBaseTypeNotAssignable", "InvalidCheck", "InvalidCustomCheck, Version=15.1.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a"));
            }
        }
    }

    private void AddCustomDataSourceToNugetConfig(string checkCandidatePath)
    {
        var nugetTemplatePath = Path.Combine(checkCandidatePath, "nugetTemplate.config");

        var doc = new XmlDocument();
        doc.LoadXml(File.ReadAllText(nugetTemplatePath));
        if (doc.DocumentElement != null)
        {
            XmlNode? packageSourcesNode = doc.SelectSingleNode("//packageSources");

            // The test packages are generated during the test project build and saved in CustomChecks folder.
            string checksPackagesPath = Path.Combine(Directory.GetParent(AssemblyLocation)?.Parent?.FullName ?? string.Empty, "CustomChecks");
            AddPackageSource(doc, packageSourcesNode, "Key", checksPackagesPath);

            doc.Save(Path.Combine(checkCandidatePath, "nuget.config"));
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
    string? BC0101Severity = null)
    {
        string testAssetsFolderName = "SampleCheckIntegrationTest";
        TransientTestFolder workFolder = _env.CreateFolder(createFolder: true);
        TransientTestFile testFile = _env.CreateFile(workFolder, "somefile");

        string contents = ReadAndAdjustProjectContent("Project1");
        string contents2 = ReadAndAdjustProjectContent("Project2");

        projectFile = _env.CreateFile(workFolder, "FooBar.csproj", contents);
        TransientTestFile projectFile2 = _env.CreateFile(workFolder, "FooBar-Copy.csproj", contents2);

        CreateEditorConfig(BC0101Severity, testAssetsFolderName, workFolder);

        // OSX links /var into /private, which makes Path.GetTempPath() return "/var..." but Directory.GetCurrentDirectory return "/private/var...".
        // This discrepancy breaks path equality checks in MSBuild checks if we pass to MSBuild full path to the initial project.
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

    private void CreateEditorConfig(string? BC0101Severity, string testAssetsFolderName, TransientTestFolder workFolder)
    {
        string configContent = string.IsNullOrEmpty(BC0101Severity)
            ? File.ReadAllText(Path.Combine(TestAssetsRootPath, testAssetsFolderName, ".editorconfigbasic"))
            : File.ReadAllText(Path.Combine(TestAssetsRootPath, testAssetsFolderName, ".editorconfigcustomised")).Replace("BC0101Severity", BC0101Severity);

        _ = _env.CreateFile(workFolder, ".editorconfig", configContent);
    }
}
