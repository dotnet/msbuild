// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Xml;
using Microsoft.Build.Evaluation;
using Microsoft.Build.UnitTests;
using Microsoft.Build.Utilities;
using Shouldly;
using Xunit;
#if FEATURE_WINDOWSINTEROP
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.LibraryLoader;
#endif

namespace Microsoft.Build.Tasks.UnitTests
{
    public class AddToWin32Manifest_Tests
    {
        private static string TestAssetsRootPath { get; } = Path.Combine(
            Path.GetDirectoryName(typeof(AddToWin32Manifest_Tests).Assembly.Location) ?? AppContext.BaseDirectory,
            "TestResources",
            "Manifests");

        private readonly ITestOutputHelper _testOutput;

        public AddToWin32Manifest_Tests(ITestOutputHelper testOutput) => _testOutput = testOutput;

        [Theory]
        [InlineData("testManifestWithInvalidSupportedArchs.manifest", false)]
        [InlineData("testManifestWithApplicationDefined.manifest", true)]
        [InlineData("testManifestSavesTheCurrentNodesPositions.manifest", true)]
        [InlineData("testManifestNoPrefixes.manifest", true)]
        [InlineData(null, true)]
        public void ManifestPopulationCheck(string? manifestName, bool expectedResult)
        {
            AddToWin32Manifest task = new AddToWin32Manifest()
            {
                BuildEngine = new MockEngine(_testOutput),
            };

            using (TestEnvironment env = TestEnvironment.Create())
            {
                var tempOutput = env.CreateFolder().Path;
                task.OutputDirectory = tempOutput;
                task.SupportedArchitectures = "amd64 arm64";
                if (!string.IsNullOrEmpty(manifestName))
                {
                    task.ApplicationManifest = new TaskItem(Path.Combine(TestAssetsRootPath, manifestName));
                }

                var result = task.Execute();

                result.ShouldBe(expectedResult);

                if (result)
                {
                    string generatedManifest = task.ManifestPath;
                    string expectedManifest = Path.Combine(TestAssetsRootPath, $"{manifestName ?? "default.win32manifest"}_expected");

                    XmlDocument expectedDoc = new XmlDocument();
                    XmlDocument actualDoc = new XmlDocument();

                    using (var reader = XmlReader.Create(expectedManifest))
                    {
                        expectedDoc.Load(reader);
                    }

                    using (var reader = XmlReader.Create(generatedManifest))
                    {
                        actualDoc.Load(reader);
                    }

                    expectedDoc.OuterXml.ShouldBe(actualDoc.OuterXml);
                    expectedDoc.InnerXml.ShouldBe(actualDoc.InnerXml);
                }
            }
        }

        [SupportedOSPlatform("windows6.1")]
        [WindowsOnlyTheory]
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

                    using (var reader = XmlReader.Create(expectedManifest))
                    {
                        expectedDoc.Load(reader);
                    }

                    using (MemoryStream stream = new MemoryStream(actualManifestBytes))
                    using (var reader = XmlReader.Create(stream))
                    {
                        actualDoc.Load(reader);
                    }

                    NormalizeLineEndings(expectedDoc.OuterXml).ShouldBe(NormalizeLineEndings(actualDoc.OuterXml));
                    NormalizeLineEndings(expectedDoc.InnerText).ShouldBe(NormalizeLineEndings(actualDoc.InnerText));
                }
            }

            static string NormalizeLineEndings(string input) => input.Replace("\r\n", "\n").Replace("\r", "\n");
        }

        [SupportedOSPlatform("windows6.1")]
        internal sealed class AssemblyNativeResourceManager
        {
            public static unsafe byte[]? GetResourceFromExecutable(string assembly, string lpName, string lpType)
            {
                HMODULE hModule;
                fixed (char* pAssembly = assembly)
                {
                    hModule = PInvoke.LoadLibraryEx(new PCWSTR(pAssembly), default, LOAD_LIBRARY_FLAGS.LOAD_LIBRARY_AS_DATAFILE);
                }

                try
                {
                    if (!hModule.IsNull)
                    {
                        HRSRC hResource;
                        fixed (char* pName = lpName)
                        {
                            fixed (char* pType = lpType)
                            {
                                hResource = PInvoke.FindResource(hModule, new PCWSTR(pName), new PCWSTR(pType));
                            }
                        }

                        if (!hResource.IsNull)
                        {
                            uint resSize = PInvoke.SizeofResource(hModule, hResource);
                            HGLOBAL resData = PInvoke.LoadResource(hModule, hResource);
                            if (!resData.IsNull)
                            {
                                byte[] uiBytes = new byte[resSize];
                                void* pMemorySource = PInvoke.LockResource(resData);
                                Marshal.Copy((IntPtr)pMemorySource, uiBytes, 0, (int)resSize);

                                return uiBytes;
                            }
                        }
                    }
                }
                finally
                {
                    PInvoke.FreeLibrary(hModule);
                }

                return null;
            }
        }
    }
}
