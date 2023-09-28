// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
            TestProject testProject = new()
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
            TestProject testProject = new()
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
