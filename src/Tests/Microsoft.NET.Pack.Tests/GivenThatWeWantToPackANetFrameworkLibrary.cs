// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Xunit;
using Microsoft.NET.TestFramework.ProjectConstruction;
using System.Xml.Linq;
using System.Linq;
using FluentAssertions;
using System.Runtime.InteropServices;
using Xunit.Abstractions;

namespace Microsoft.NET.Pack.Tests
{
    public class GivenThatWeWantToPackANetFrameworkLibrary : SdkTest
    {
        public GivenThatWeWantToPackANetFrameworkLibrary(ITestOutputHelper log) : base(log)
        {
        }

        [WindowsOnlyFact]
        public void ImplicitReferencesAreNotIncludedAsFrameworkReferences()
        {
            TestProject testProject = new TestProject()
            {
                Name = "PackImplicitReferences",
                TargetFrameworks = "net462",
                IsExe = false
            };

            var nuspecPath = PackAndGetNuspecPath(testProject);

            var nuspec = XDocument.Load(nuspecPath);
            var ns = nuspec.Root.Name.Namespace;

            var frameworkAssemblies = nuspec.Root
                .Element(ns + "metadata")
                .Element(ns + "frameworkAssemblies");

            frameworkAssemblies.Should().BeNull();
        }

        [WindowsOnlyFact]
        public void ExplicitReferencesAreIncludedAsFrameworkReferences()
        {
            TestProject testProject = new TestProject()
            {
                Name = "PackImplicitReferences",
                TargetFrameworks = "net462",
                IsExe = false
            };

            var nuspecPath = PackAndGetNuspecPath(testProject,
                "PackImplicitRefs",
                p =>
                {
                    var pns = p.Root.Name.Namespace;

                    p.Root.Add(new XElement(pns + "ItemGroup",
                        new XElement(pns + "Reference", new XAttribute("Include", "System")),
                        new XElement(pns + "Reference", new XAttribute("Include", "System.Xml.Linq"))));
                });

            var nuspec = XDocument.Load(nuspecPath);
            var ns = nuspec.Root.Name.Namespace;

            var frameworkAssemblies = nuspec.Root
                .Element(ns + "metadata")
                .Element(ns + "frameworkAssemblies")
                .Elements(ns + "frameworkAssembly");

            frameworkAssemblies.Count().Should().Be(2);
            frameworkAssemblies.Should().Contain(i => i.Attribute("assemblyName").Value == "System");
            frameworkAssemblies.Should().Contain(i => i.Attribute("assemblyName").Value == "System.Xml.Linq");
        }

        private string PackAndGetNuspecPath(TestProject testProject, string identifier = null, Action<XDocument> xmlAction = null)
        {
            var testProjectInstance = _testAssetsManager.CreateTestProject(testProject, testProject.Name, identifier);

            if (xmlAction != null)
            {
                testProjectInstance = testProjectInstance.WithProjectChanges(xmlAction);
            }

            var packCommand = new PackCommand(Log, testProjectInstance.TestRoot, testProject.Name);

            packCommand.Execute()
                .Should()
                .Pass();

            string nuspecPath = packCommand.GetIntermediateNuspecPath();
            return nuspecPath;
           
        }
    }
}
