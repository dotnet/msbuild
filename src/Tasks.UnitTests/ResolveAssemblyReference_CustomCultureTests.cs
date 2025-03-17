// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Xml;
using Microsoft.Build.UnitTests;
using Shouldly;
using Xunit;
using static Microsoft.Build.Tasks.UnitTests.AddToWin32Manifest_Tests;

namespace Microsoft.Build.Tasks.UnitTests
{
    /// <summary>
    /// Unit tests for the ResolveAssemblyReference task.
    /// </summary>
    public class ResolveAssemblyReference_CustomCultureTests
    {
        private static string TestAssetsRootPath { get; } = Path.Combine(
            Path.GetDirectoryName(typeof(AddToWin32Manifest_Tests).Assembly.Location) ?? AppContext.BaseDirectory,
            "TestResources",
            "CustomCulture");

        [WindowsFullFrameworkOnlyTheory]
        [InlineData(null, true)]
        [InlineData("buildIn.manifest", true)]
        [InlineData("testManifestWithValidSupportedArchs.manifest", true)]
        public void E2EScenarioTests(string? manifestName, bool expectedResult)
        {
            using (TestEnvironment env = TestEnvironment.Create())
            {
                var outputPath = env.CreateFolder().Path;
                string projectContent = @$"
                <Project DefaultTargets=""Build"">
                    <Import Project=""$(MSBuildBinPath)\Microsoft.Common.props"" />

                    <PropertyGroup>
                        <Platform>AnyCPU</Platform>
                        <TargetFrameworkVersion>v4.7.2</TargetFrameworkVersion>
                        <OutputType>Library</OutputType>
                        <PreferNativeArm64>true</PreferNativeArm64>
                        <Prefer32Bit>false</Prefer32Bit>
                        {(!string.IsNullOrEmpty(manifestName) ? $"<ApplicationManifest>{manifestName}</ApplicationManifest>" : "")}
                        <IntermediateOutputPath>{outputPath}</IntermediateOutputPath>
                    </PropertyGroup>

                    <Target Name=""Build""/>
                    <Import Project=""$(MSBuildBinPath)\Microsoft.CSharp.targets"" />

                </Project>
                ";

                var projectFolder = env.CreateFolder();
                var projectFile = env.CreateFile(projectFolder, "test.csproj", projectContent).Path;

                // copy application manifest
                if (!string.IsNullOrEmpty(manifestName))
                {
                    File.Copy(Path.Combine(TestAssetsRootPath, manifestName), Path.Combine(projectFolder.Path, manifestName));
                }

                Project project = ObjectModelHelpers.LoadProjectFileInTempProjectDirectory(projectFile, touchProject: false);

                bool result = project.Build(new MockLogger(_testOutput));
                result.ShouldBe(expectedResult);

                // #2 - represents the name for native resource (Win 32 resource), #24 - the type (Manifest) 
                byte[]? actualManifestBytes = AssemblyNativeResourceManager.GetResourceFromExecutable(Path.Combine(outputPath, "test.dll"), "#2", "#24");

                // check manifest content
                if (actualManifestBytes != null)
                {
                    string expectedManifest = Path.Combine(TestAssetsRootPath, $"{manifestName ?? "default.win32manifest"}_expected");

                    XmlDocument expectedDoc = new XmlDocument();
                    XmlDocument actualDoc = new XmlDocument();

                    expectedDoc.Load(expectedManifest);
                    using (MemoryStream stream = new MemoryStream(actualManifestBytes))
                    {
                        actualDoc.Load(stream);
                    }

                    NormalizeLineEndings(expectedDoc.OuterXml).ShouldBe(NormalizeLineEndings(actualDoc.OuterXml));
                    NormalizeLineEndings(expectedDoc.InnerText).ShouldBe(NormalizeLineEndings(actualDoc.InnerText));
                }
            }

            static string NormalizeLineEndings(string input) => input.Replace("\r\n", "\n").Replace("\r", "\n");
        }

    }
}
