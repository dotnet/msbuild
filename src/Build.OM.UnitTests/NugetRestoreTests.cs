// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !DEBUG
using Microsoft.Build.UnitTests;
using Microsoft.Build.UnitTests.Shared;
using Shouldly;
using System.IO;
using Xunit;
#endif
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

        // This NuGet version cannot locate other assemblies when parsing solutions at restore time. This includes localized strings required in debug mode.
        // NuGet version 4.1.0 was somewhat arbitrarily chosen. 3.5 breaks with an unrelated error, and 4.8.2 does not fail when a new dependency is introduced. This is a safe middle point.
#if !DEBUG
        [SkipOnTargetFramework(TargetFrameworkMonikers.Netcoreapp)]
        [Fact]
        public void TestOldNuget()
        {
            string msbuildExePath = Path.GetDirectoryName(RunnerUtilities.PathToCurrentlyRunningMsBuildExe);
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
#endif
    }
}
