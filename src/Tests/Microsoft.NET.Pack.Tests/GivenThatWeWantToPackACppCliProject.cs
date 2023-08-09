// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
