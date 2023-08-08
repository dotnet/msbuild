// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using System.Linq;
using System.Xml.Linq;
using FluentAssertions;
using Microsoft.NET.Build.Tasks;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Xunit.Abstractions;

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToPackACppCliProject : SdkTest
    {
        public GivenThatWeWantToPackACppCliProject(ITestOutputHelper log) : base(log)
        {
        }

        [FullMSBuildOnlyFact]
        public void It_cannot_pack_the_cppcliproject()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("NetCoreCsharpAppReferenceCppCliLib")
                .WithSource()
                .WithProjectChanges((projectPath, project) => AddPackageReference(projectPath, project, "NewtonSoft.Json", ToolsetInfo.GetNewtonsoftJsonPackageVersion()))
                .WithProjectChanges((projectPath, project) => AddBuildProperty(projectPath, project, "EnableManagedpackageReferenceSupport", "true"));

            new PackCommand(Log, Path.Combine(testAsset.TestRoot, "NETCoreCppCliTest", "NETCoreCppCliTest.vcxproj"))
                .Execute("-p:Platform=x64", "-p:EnableManagedpackageReferenceSupport=true")
                .Should()
                .Fail()
                .And
                .HaveStdOutContaining("MSB4057"); // We don't get NETSDK1118 when enabling PackageReference support but can't resolve the apphost without it
        }
        private void AddPackageReference(string projectPath, XDocument project, string package, string version)
        {
            if (Path.GetExtension(projectPath) == ".vcxproj")
            {
                XNamespace ns = project.Root.Name.Namespace;
                XElement itemGroup = project.Root.Descendants(ns + "ItemGroup").First();
                itemGroup.Add(new XElement(ns + "PackageReference", new XAttribute("Include", package),
                                                    new XAttribute("Version", version)));

            }
        }
        private void AddBuildProperty(string projectPath, XDocument project, string property, string value)
        {
            if (Path.GetExtension(projectPath) == ".vcxproj")
            {
                XNamespace ns = project.Root.Name.Namespace;
                XElement propertyGroup = project.Root.Descendants(ns + "PropertyGroup").First();
                propertyGroup.Add(new XElement(ns + $"{property}", value));
            }
        }
    }
}
