// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using System.Reflection;
using Microsoft.Build.UnitTests;
using Microsoft.Build.UnitTests.Shared;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Build.Engine.OM.UnitTests
{
    public sealed class NugetRestoreTests
    {
        private ITestOutputHelper _output;
        public NugetRestoreTests(ITestOutputHelper output)
        {
            _output = output;
        }

        // Tests proper loading of msbuild assemblies by nuget.exe
        [WindowsFullFrameworkOnlyFact]
        public void TestOldNuget()
        {
            TestNugetRestore(string.Empty);
        }

        [WindowsFullFrameworkOnlyFact]
        public void TestOldNugetWithMsBuild64bit()
        {
            TestNugetRestore("amd64");
        }

        private void TestNugetRestore(string msbuildSubFolder)
        {
            string currentAssemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
            string bootstrapMsBuildBinaryDir = Path.Combine(RunnerUtilities.BootstrapMsBuildBinaryLocation, msbuildSubFolder);
            using TestEnvironment testEnvironment = TestEnvironment.Create();
            TransientTestFolder folder = testEnvironment.CreateFolder(createFolder: true);
            // The content of the solution isn't known to matter, but having a custom solution makes it easier to add requirements should they become evident.
            TransientTestFile sln = testEnvironment.CreateFile(folder, "test.sln",
                @"
Microsoft Visual Studio Solution File, Format Version 12.00
\# Visual Studio 15
VisualStudioVersion = 15.0.26124.0
MinimumVisualStudioVersion = 15.0.26124.0
Global
GlobalSection(SolutionConfigurationPlatforms) = preSolution
	Debug|Any CPU = Debug|Any CPU
	Debug|x64 = Debug|x64
	Debug|x86 = Debug|x86
	Release|Any CPU = Release|Any CPU
	Release|x64 = Release|x64
	Release|x86 = Release|x86
EndGlobalSection
GlobalSection(SolutionProperties) = preSolution
	HideSolutionNode = FALSE
EndGlobalSection
EndGlobal
");
            RunnerUtilities.RunProcessAndGetOutput(Path.Combine(currentAssemblyDir, "nuget", "NuGet.exe"), "restore " + sln.Path + " -MSBuildPath \"" + bootstrapMsBuildBinaryDir + "\"", out bool success, outputHelper: _output);
            success.ShouldBeTrue();
        }
    }
}
