// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;
using NuGet.Packaging;

namespace Microsoft.NET.ToolPack.Tests
{
    public class GivenThatWeWantToPackAToolProjectWithComplexNugetDependency : SdkTest
    {
        public GivenThatWeWantToPackAToolProjectWithComplexNugetDependency(ITestOutputHelper log) : base(log)
        {

        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void It_has_native_and_transitive_dependencies_dll(bool multiTarget)
        {
            TestAsset helloWorldAsset = _testAssetsManager
                                        .CopyTestAsset("PortableTool")
                                        .WithSource()
                                        .WithProjectChanges(project =>
                                        {
                                            ChangeToPackageThatDependsOnOtherPackage(project);

                                            XNamespace ns = project.Root.Name.Namespace;
                                            XElement propertyGroup = project.Root.Elements(ns + "PropertyGroup").First();
                                        })
                                        .WithTargetFrameworkOrFrameworks("netcoreapp2.1", multiTarget);

            helloWorldAsset.Restore(Log);

            var packCommand = new PackCommand(Log, helloWorldAsset.TestRoot);
            packCommand.Execute();
            var nugetPackage = packCommand.GetNuGetPackage();

            using (var nupkgReader = new PackageArchiveReader(nugetPackage))
            {
                IEnumerable<NuGet.Frameworks.NuGetFramework> supportedFrameworks = nupkgReader.GetSupportedFrameworks();
                supportedFrameworks.Should().NotBeEmpty();

                var transitiveDependency = "runtimes/unix/lib/netstandard1.3/System.Data.SqlClient.dll";
                var nativeDependency = "runtimes/win7-x86/native/sni.dll";

                foreach (var dependency in new string[] { transitiveDependency, nativeDependency })
                {
                    foreach (NuGet.Frameworks.NuGetFramework framework in supportedFrameworks)
                    {
                        var allItems = nupkgReader.GetToolItems().SelectMany(i => i.Items).ToList();
                        allItems.Should().Contain($"tools/{framework.GetShortFolderName()}/any/{dependency}");
                    }
                }

                nupkgReader
                    .GetPackageDependencies()
                    .Should().BeEmpty();
            }
        }

        private static void ChangeToPackageThatDependsOnOtherPackage(XDocument project)
        {
            XNamespace ns = project.Root.Name.Namespace;
            XElement itemGroup = project.Root.Elements(ns + "ItemGroup").First();
            itemGroup.Add(new XElement(ns + "PackageReference", new XAttribute("Include", "System.Data.SqlClient"),
                                                                new XAttribute("Version", "4.3.0")));
        }
    }
}
