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

        //  Disable this test on full framework, as generating strong named satellite assemblies with AL.exe requires Admin permissions
        //  See https://github.com/dotnet/sdk/issues/732
        [CoreMSBuildOnlyFact]
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

            testAsset = testAsset.Restore(log);

            var buildCommand = new BuildCommand(log, testAsset.TestRoot);

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

                Command command;
                if (targetFramework == "net46")
                {
                    outputFiles.Add("AllResourcesInSatellite.exe");
                    outputFiles.Add("AllResourcesInSatellite.exe.config");
                    command = Command.Create(Path.Combine(outputDirectory.FullName, "AllResourcesInSatellite.exe"), Array.Empty<string>());
                }
                else
                {
                    outputFiles.Add("AllResourcesInSatellite.dll");
                    outputFiles.Add("AllResourcesInSatellite.deps.json");
                    outputFiles.Add("AllResourcesInSatellite.runtimeconfig.json");
                    outputFiles.Add("AllResourcesInSatellite.runtimeconfig.dev.json");
                    command = Command.Create(TestContext.Current.ToolsetUnderTest.DotNetHostPath, new[] { Path.Combine(outputDirectory.FullName, "AllResourcesInSatellite.dll") });
                }

                outputDirectory.Should().OnlyHaveFiles(outputFiles);

                command
                    .CaptureStdOut()
                    .Execute()
                    .Should()
                    .Pass()
                    .And
                    .HaveStdOutContaining("Hello World from en satellite assembly");
            }
        }
    }
}
