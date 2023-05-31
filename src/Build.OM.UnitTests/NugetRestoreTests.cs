// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.UnitTests;
using Microsoft.Build.UnitTests.Shared;
using Shouldly;
using System.IO;
using Xunit;
using Xunit.Abstractions;
using Xunit.NetCore.Extensions;

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
            string msbuildExePath = Path.GetDirectoryName(RunnerUtilities.PathToCurrentlyRunningMsBuildExe)!;
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
            RunnerUtilities.RunProcessAndGetOutput(Path.Combine(msbuildExePath, "nuget", "NuGet.exe"), "restore " + sln.Path + " -MSBuildPath \"" + msbuildExePath + "\"", out bool success, outputHelper: _output);
            success.ShouldBeTrue();
        }
    }
}
