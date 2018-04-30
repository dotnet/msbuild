// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Xml.Linq;
using FluentAssertions;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Commands;
using NuGet.Packaging;
using Xunit;
using Xunit.Abstractions;
using Microsoft.NET.TestFramework.Assertions;

namespace Microsoft.NET.ToolPack.Tests
{
    public class GivenThatWeWantToPackAToolProjectWithGeneratePackageOnBuild : SdkTest
    {

        private const string AppName = "consoledemo";

        public GivenThatWeWantToPackAToolProjectWithGeneratePackageOnBuild(ITestOutputHelper log) : base(log)
        {}

        private TestAsset SetupAndRestoreTestAsset([CallerMemberName] string callingMethod = "")
        {
            TestAsset testAsset = _testAssetsManager
                .CopyTestAsset("PortableToolWithP2P", callingMethod)
                .WithSource()
                .WithProjectChanges((projectPath, project) =>
                {
                    if (IsAppProject(projectPath))
                    {
                        XNamespace ns = project.Root.Name.Namespace;
                        XElement propertyGroup = project.Root.Elements(ns + "PropertyGroup").First();
                        propertyGroup.Add(new XElement(ns + "GeneratePackageOnBuild", "true"));
                    }
                });

            testAsset.Restore(Log, "App");

            return testAsset;
        }

        [Fact]
        public void It_builds_successfully()
        {
            TestAsset testAsset = SetupAndRestoreTestAsset();
            var appProjectDirectory = Path.Combine(testAsset.TestRoot, "App");
            var buildCommand = new BuildCommand(Log, appProjectDirectory);

            CommandResult result = buildCommand.Execute();

            result.Should()
                  .Pass()
                  .And
                  .NotHaveStdOutContaining("There is a circular dependency");
        }

        [Fact]
        public void It_builds_and_result_contains_dependencies_dll()
        {
            TestAsset testAsset = SetupAndRestoreTestAsset();
            var appProjectDirectory = Path.Combine(testAsset.TestRoot, "App");
            var buildCommand = new BuildCommand(Log, appProjectDirectory);
            buildCommand.Execute();

            var packCommand = new PackCommand(Log, appProjectDirectory);
            // Do not run pack, just use it to get nupkg since it should be run by build.
            var nugetPackage = packCommand.GetNuGetPackage();

            using(var nupkgReader = new PackageArchiveReader(nugetPackage))
            {
                IEnumerable<NuGet.Frameworks.NuGetFramework> supportedFrameworks = nupkgReader.GetSupportedFrameworks();
                supportedFrameworks.Should().NotBeEmpty();

                foreach (NuGet.Frameworks.NuGetFramework framework in supportedFrameworks)
                {
                    var allItems = nupkgReader.GetToolItems().SelectMany(i => i.Items).ToList();
                    allItems.Should().Contain($"tools/{framework.GetShortFolderName()}/any/Library.dll");
                }
            }
        }

        private bool IsAppProject(string projectPath)
        {
            return Path.GetFileNameWithoutExtension(projectPath).Equals(AppName, StringComparison.OrdinalIgnoreCase);
        }
    }
}
