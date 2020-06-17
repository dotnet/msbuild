// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Xunit;
using Microsoft.DotNet.Cli.Utils;
using System.Xml.Linq;
using System.Runtime.CompilerServices;
using Xunit.Abstractions;

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantAllResourcesInSatellite : SdkTest
    {
        public GivenThatWeWantAllResourcesInSatellite(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void It_retrieves_strings_successfully()
        {
            TestSatelliteResources(Log, _testAssetsManager);
        }

        internal static void TestSatelliteResources(
            ITestOutputHelper log,
            TestAssetsManager testAssetsManager, 
            Action<XDocument> projectChanges = null,
            Action<BuildCommand> setup = null, 
            [CallerMemberName] string callingMethod = null)
        {
            var testAsset = testAssetsManager
                .CopyTestAsset("AllResourcesInSatellite", callingMethod)
                .WithSource();

            if (projectChanges != null)
            {
                testAsset = testAsset.WithProjectChanges(projectChanges);
            }

            var buildCommand = new BuildCommand(testAsset);

            if (setup != null)
            {
                setup(buildCommand);
            }

            buildCommand
                .Execute()
                .Should()
                .Pass();

            foreach (var targetFramework in new[] { "net46", "netcoreapp1.1" })
            {
                if (targetFramework == "net46" && !RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    continue;
                }

                var outputDirectory = buildCommand.GetOutputDirectory(targetFramework);

                var outputFiles = new List<string>
                {
                    "AllResourcesInSatellite.pdb",
                    "en/AllResourcesInSatellite.resources.dll"
                };

                TestCommand command;
                if (targetFramework == "net46")
                {
                    outputFiles.Add("AllResourcesInSatellite.exe");
                    outputFiles.Add("AllResourcesInSatellite.exe.config");
                    command = new RunExeCommand(log, Path.Combine(outputDirectory.FullName, "AllResourcesInSatellite.exe"));
                }
                else
                {
                    outputFiles.Add("AllResourcesInSatellite.dll");
                    outputFiles.Add("AllResourcesInSatellite.deps.json");
                    outputFiles.Add("AllResourcesInSatellite.runtimeconfig.json");
                    outputFiles.Add("AllResourcesInSatellite.runtimeconfig.dev.json");
                    command = new DotnetCommand(log, Path.Combine(outputDirectory.FullName, "AllResourcesInSatellite.dll"));
                }

                outputDirectory.Should().OnlyHaveFiles(outputFiles);

                command
                    .Execute()
                    .Should()
                    .Pass()
                    .And
                    .HaveStdOutContaining("Hello World from en satellite assembly");
            }
        }
    }
}
