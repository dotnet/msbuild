// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Xml.Linq;
using Microsoft.DotNet.Tools.Test.Utilities;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Xunit;
using Xunit.Abstractions;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Microsoft.DotNet.Tests.EndToEnd
{
    public class GivenDotNetUsesMSBuild : SdkTest
    {
        public GivenDotNetUsesMSBuild(ITestOutputHelper log) : base(log)
        {
        }

        [RequiresMSBuildVersionFact("17.0.0.32901", Skip = "Unskipping tracked by https://github.com/dotnet/sdk/issues/19696")]
        public void ItCanNewRestoreBuildRunCleanMSBuildProject()
        {
            string projectDirectory = _testAssetsManager.CreateTestDirectory().Path;

            string [] newArgs = new[] { "console", "--debug:ephemeral-hive", "--no-restore" };
            new DotnetCommand(Log, "new")
                .WithWorkingDirectory(projectDirectory)
                .Execute(newArgs)
                .Should().Pass();

            new BuildCommand(Log, projectDirectory)
                .Execute()
                .Should().Pass();

            new DotnetCommand(Log, "run")
                .WithWorkingDirectory(projectDirectory)
                .Execute()
                .Should().Pass()
                        .And.HaveStdOutContaining("Hello, World!");

            var binDirectory = new DirectoryInfo(projectDirectory).Sub("bin");
            binDirectory.Should().HaveFilesMatching("*.dll", SearchOption.AllDirectories);

            new CleanCommand(Log, projectDirectory)
                .Execute()
                .Should().Pass();

            binDirectory.Should().NotHaveFilesMatching("*.dll", SearchOption.AllDirectories);
        }

        [RequiresMSBuildVersionFact("16.8.0")]
        public void ItCanRunToolsInACSProj()
        {
            var testInstance = _testAssetsManager.CopyTestAsset("MSBuildTestApp")
                                         .WithSource()
                                         .WithProjectChanges(project =>
                                         {
                                             var ns = project.Root.Name.Namespace;

                                             var itemGroup = new XElement(ns + "ItemGroup");
                                             itemGroup.Add(new XElement(ns + "DotNetCliToolReference",
                                                                new XAttribute("Include", "dotnet-portable"),
                                                                new XAttribute("Version", "1.0.0")));

                                             project.Root.Add(itemGroup);
                                         });

            NuGetConfigWriter.Write(testInstance.Path, TestContext.Current.TestPackages);

            new RestoreCommand(testInstance)
                .Execute()
                .Should()
                .Pass();

            var testProjectDirectory = testInstance.Path;

            new DotnetCommand(Log)
                .WithWorkingDirectory(testInstance.Path)
                .Execute("portable")
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining("Hello Portable World!");;
        }

        [RequiresMSBuildVersionFact("16.8.0")]
        public void ItCanRunToolsThatPrefersTheCliRuntimeEvenWhenTheToolItselfDeclaresADifferentRuntime()
        {
            var testInstance = _testAssetsManager.CopyTestAsset("MSBuildTestApp")
                                         .WithSource()
                                         .WithProjectChanges(project =>
                                         {
                                             var ns = project.Root.Name.Namespace;

                                             var itemGroup = new XElement(ns + "ItemGroup");
                                             itemGroup.Add(new XElement(ns + "DotNetCliToolReference",
                                                                new XAttribute("Include", "dotnet-prefercliruntime"),
                                                                new XAttribute("Version", "1.0.0")));

                                             project.Root.Add(itemGroup);
                                         });
            ;

            NuGetConfigWriter.Write(testInstance.Path, TestContext.Current.TestPackages);

            new RestoreCommand(testInstance)
                .Execute()
                .Should()
                .Pass();

            var testProjectDirectory = testInstance.Path;

            new DotnetCommand(Log)
                .WithWorkingDirectory(testInstance.Path)
                .Execute("prefercliruntime")
                .Should().Pass()
                .And.HaveStdOutContaining("Hello I prefer the cli runtime World!");;
        }
    }
}
