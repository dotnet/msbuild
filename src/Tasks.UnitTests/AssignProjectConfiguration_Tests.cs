// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Text;
using System.Xml;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.Build.Tasks;
using Xunit;

namespace Microsoft.Build.UnitTests
{
    /// <summary>
    /// Most of the common ResolveNonMSBuildOutput/AssignProjectConfiguration functionality is tested
    /// in ResolveNonMSBuildProjectOutput_Tests.
    /// Here, only test the AssignProjectConfiguration specific code
    /// </summary>
    sealed public class AssignProjectConfiguration_Tests
    {
        private void TestResolveHelper(string itemSpec, string projectGuid, string package, string name,
            Hashtable pregenConfigurations, bool expectedResult,
            string expectedFullConfiguration, string expectedConfiguration, string expectedPlatform)
        {
            ITaskItem reference = ResolveNonMSBuildProjectOutput_Tests.CreateReferenceItem(itemSpec, projectGuid, package, name);
            // Use the XML string generation method from our sister class - XML element names will be different,
            // but they are ignored anyway, and the rest is identical
            string xmlString = ResolveNonMSBuildProjectOutput_Tests.CreatePregeneratedPathDoc(pregenConfigurations);
            ITaskItem resolvedProjectWithConfiguration;

            AssignProjectConfiguration rpc = new AssignProjectConfiguration();
            rpc.SolutionConfigurationContents = xmlString;
            rpc.CacheProjectElementsFromXml(xmlString);
            bool result = rpc.ResolveProject(reference, out resolvedProjectWithConfiguration);

            string message = string.Format("Reference \"{0}\" [project \"{1}\", package \"{2}\", name \"{3}\"] Pregen Xml string : \"{4}\"" +
                "expected result \"{5}\", actual result \"{6}\", expected configuration \"{7}\", actual configuration \"{8}\".",
                itemSpec, projectGuid, package, name, xmlString, expectedResult, result, expectedFullConfiguration,
                (resolvedProjectWithConfiguration == null) ? string.Empty : resolvedProjectWithConfiguration.GetMetadata("FullConfiguration"));

            Assert.Equal(expectedResult, result);
            if (result == true)
            {
                Assert.Equal(expectedFullConfiguration, resolvedProjectWithConfiguration.GetMetadata("FullConfiguration"));
                Assert.Equal(expectedConfiguration, resolvedProjectWithConfiguration.GetMetadata("Configuration"));
                Assert.Equal(expectedPlatform, resolvedProjectWithConfiguration.GetMetadata("Platform"));
                Assert.Equal("Configuration=" + expectedConfiguration, resolvedProjectWithConfiguration.GetMetadata("SetConfiguration"));
                Assert.Equal("Platform=" + expectedPlatform, resolvedProjectWithConfiguration.GetMetadata("SetPlatform"));
                Assert.Equal(reference.ItemSpec, resolvedProjectWithConfiguration.ItemSpec);
            }
            else
            {
                Assert.Equal(null, resolvedProjectWithConfiguration);
            }
        }

        [Fact]
        public void TestResolve()
        {
            // empty pre-generated string
            Hashtable projectOutputs = new Hashtable();
            TestResolveHelper("MCDep1.vcproj", "{2F6BBCC3-7111-4116-A68B-34CFC76F37C5}", "{8BC9CEB8-8B4A-11D0-8D11-00A0C91BC942}", "MCDep1",
                projectOutputs, false, null, null, null);

            // non matching project in string
            projectOutputs = new Hashtable();
            projectOutputs.Add("{11111111-1111-1111-1111-111111111111}", @"Debug|Win32");
            TestResolveHelper("MCDep1.vcproj", "{2F6BBCC3-7111-4116-A68B-34CFC76F37C5}", "{8BC9CEB8-8B4A-11D0-8D11-00A0C91BC942}", "MCDep1",
                projectOutputs, false, null, null, null);

            // matching projects in string
            projectOutputs = new Hashtable();
            projectOutputs.Add("{2F6BBCC3-7111-4116-A68B-34CFC76F37C5}", @"Debug|Win32");
            projectOutputs.Add("{2F6BBCC3-7111-4116-A68B-666666666666}", @"Debug");
            TestResolveHelper("MCDep1.vcproj", "{2F6BBCC3-7111-4116-A68B-34CFC76F37C5}", "{8BC9CEB8-8B4A-11D0-8D11-00A0C91BC942}", "MCDep1",
                projectOutputs, true, @"Debug|Win32", "Debug", "Win32");
            TestResolveHelper("MCDep2.vcproj", "{2F6BBCC3-7111-4116-A68B-666666666666}", "{8BC9CEB8-8B4A-11D0-8D11-00A0C91BC942}", "MCDep1",
                projectOutputs, true, @"Debug", "Debug", string.Empty);

            // multiple non matching projects in string
            projectOutputs = new Hashtable();
            projectOutputs.Add("{11111111-1111-1111-1111-111111111111}", @"Config1|Win32");
            projectOutputs.Add("{11111111-1111-1111-1111-111111111112}", @"Config2|AnyCPU");
            projectOutputs.Add("{11111111-1111-1111-1111-111111111113}", @"Config3|AnyCPU");
            TestResolveHelper("MCDep1.vcproj", "{2F6BBCC3-7111-4116-A68B-34CFC76F37C5}", "{8BC9CEB8-8B4A-11D0-8D11-00A0C91BC942}", "MCDep1",
                projectOutputs, false, null, null, null);

            // multiple non matching projects in string, two matching
            projectOutputs = new Hashtable();
            projectOutputs.Add("{11111111-1111-1111-1111-111111111111}", @"Config1|Win32");
            projectOutputs.Add("{11111111-1111-1111-1111-111111111112}", @"Config2|AnyCPU");
            projectOutputs.Add("{11111111-1111-1111-1111-111111111113}", @"Config3|AnyCPU");
            projectOutputs.Add("{2F6BBCC3-7111-4116-A68B-34CFC76F37C5}", @"CorrectProjectConfig|Platform");
            projectOutputs.Add("{2F6BBCC3-7111-4116-A68B-666666666666}", @"JustConfig");
            TestResolveHelper("MCDep1.vcproj", "{2F6BBCC3-7111-4116-A68B-34CFC76F37C5}", "{8BC9CEB8-8B4A-11D0-8D11-00A0C91BC942}", "MCDep1",
                projectOutputs, true, @"CorrectProjectConfig|Platform", "CorrectProjectConfig", "Platform");
            TestResolveHelper("MCDep2.vcproj", "{2F6BBCC3-7111-4116-A68B-666666666666}", "{8BC9CEB8-8B4A-11D0-8D11-00A0C91BC942}", "MCDep1",
                projectOutputs, true, @"JustConfig", "JustConfig", string.Empty);
        }


        /// <summary>
        /// Test the case where the project reference does not have either of the metadata set on it.
        ///
        /// We would expect the following case:
        ///
        /// 1) The xml element does not have the BuildProjectInSolution attribute set
        ///     Expect none of the metadata to be set
        /// </summary>
        [Fact]
        public void TestReferenceWithNoMetadataBadBuildInProjectAttribute()
        {
            // Test the case where the metadata is missing and we are not supposed to build the reference
            ITaskItem referenceItem = new TaskItem("TestItem");
            XmlDocument doc = new XmlDocument();
            XmlElement element = doc.CreateElement("TestElement");
            element.SetAttribute("BuildProjectInSolution", "IAmReallyABadOne");
            AssignProjectConfiguration.SetBuildInProjectAndReferenceOutputAssemblyMetadata(true, referenceItem, element);
            Assert.Equal(0, referenceItem.GetMetadata("BuildReference").Length);
            Assert.Equal(0, referenceItem.GetMetadata("ReferenceOutputAssembly").Length);
        }

        /// <summary>
        /// Test the case where the project reference does not have either of the metadata set on it.
        ///
        /// We would expect the following case:
        ///
        /// 1) The xml element does not have the BuildProjectInSolution attribute set
        ///     Expect none of the metadata to be set
        /// </summary>
        [Fact]
        public void TestReferenceWithNoMetadataNoBuildInProjectAttribute()
        {
            // Test the case where the metadata is missing and we are not supposed to build the reference
            ITaskItem referenceItem = new TaskItem("TestItem");
            XmlDocument doc = new XmlDocument();
            XmlElement element = doc.CreateElement("TestElement");
            AssignProjectConfiguration.SetBuildInProjectAndReferenceOutputAssemblyMetadata(true, referenceItem, element);
            Assert.Equal(0, referenceItem.GetMetadata("BuildReference").Length);
            Assert.Equal(0, referenceItem.GetMetadata("ReferenceOutputAssembly").Length);
        }

        /// <summary>
        /// Test the case where the project reference does not have either of the metadata set on it.
        ///
        /// We would expect the following case:
        /// 1) The xml element has BuildProjectInSolution set to true
        ///     Expect none of the metadata to be set
        /// </summary>
        [Fact]
        public void TestReferenceWithNoMetadataBuildInProjectAttributeTrue()
        {
            // Test the case where the metadata is missing and we are not supposed to build the reference
            ITaskItem referenceItem = new TaskItem("TestItem");
            XmlDocument doc = new XmlDocument();
            XmlElement element = doc.CreateElement("TestElement");
            element.SetAttribute("BuildProjectInSolution", "true");
            AssignProjectConfiguration.SetBuildInProjectAndReferenceOutputAssemblyMetadata(true, referenceItem, element);
            Assert.Equal(0, referenceItem.GetMetadata("BuildReference").Length);
            Assert.Equal(0, referenceItem.GetMetadata("ReferenceOutputAssembly").Length);
        }


        /// <summary>
        /// Test the case where the project reference does not have either of the metadata set on it.
        ///
        /// We would expect the following case:
        /// ReferenceAndBuildProjectsDisabledInProjectConfiguration is set to true meaning we want to build disabled projects.
        ///
        /// 1) The xml element has BuildProjectInSolution set to false
        ///     Expect no pieces of metadata to be set on the reference item
        /// </summary>
        [Fact]
        public void TestReferenceWithNoMetadataBuildInProjectAttributeFalseReferenceAndBuildProjectsDisabledInProjectConfiguration()
        {
            // Test the case where the metadata is missing and we are not supposed to build the reference
            ITaskItem referenceItem = new TaskItem("TestItem");
            XmlDocument doc = new XmlDocument();
            XmlElement element = doc.CreateElement("TestElement");
            element.SetAttribute("BuildProjectInSolution", "false");
            AssignProjectConfiguration.SetBuildInProjectAndReferenceOutputAssemblyMetadata(false, referenceItem, element);
            Assert.Equal(0, referenceItem.GetMetadata("BuildReference").Length);
            Assert.Equal(0, referenceItem.GetMetadata("ReferenceOutputAssembly").Length);
        }

        /// <summary>
        /// Test the case where the project reference does not have either of the metadata set on it.
        ///
        /// We would expect the following case:
        /// 1) The xml element has BuildProjectInSolution set to false
        ///     Expect two pieces of metadata to be put on the item and be set to false (BuildReference, and ReferenceOutputAssembly)
        /// </summary>
        [Fact]
        public void TestReferenceWithNoMetadataBuildInProjectAttributeFalse()
        {
            // Test the case where the metadata is missing and we are not supposed to build the reference
            ITaskItem referenceItem = new TaskItem("TestItem");
            XmlDocument doc = new XmlDocument();
            XmlElement element = doc.CreateElement("TestElement");
            element.SetAttribute("BuildProjectInSolution", "false");
            AssignProjectConfiguration.SetBuildInProjectAndReferenceOutputAssemblyMetadata(true, referenceItem, element);
            Assert.True(referenceItem.GetMetadata("BuildReference").Equals("false", StringComparison.OrdinalIgnoreCase));
            Assert.True(referenceItem.GetMetadata("ReferenceOutputAssembly").Equals("false", StringComparison.OrdinalIgnoreCase));
        }


        /// <summary>
        /// Test the case where the project reference does has one or more of the metadata set on it.
        ///
        /// We would expect the following case:
        /// 1) The xml element has BuildProjectInSolution set to false
        ///     Expect two pieces of metadata to be put on the item and be set to true since they were already set (BuildReference, and ReferenceOutputAssembly)
        /// </summary>
        [Fact]
        public void TestReferenceWithMetadataAlreadySetBuildInProjectAttributeFalse()
        {
            // Test the case where the metadata is missing and we are not supposed to build the reference
            ITaskItem referenceItem = new TaskItem("TestItem");
            referenceItem.SetMetadata("BuildReference", "true");
            referenceItem.SetMetadata("ReferenceOutputAssembly", "true");

            XmlDocument doc = new XmlDocument();
            XmlElement element = doc.CreateElement("TestElement");
            element.SetAttribute("BuildProjectInSolution", "false");
            AssignProjectConfiguration.SetBuildInProjectAndReferenceOutputAssemblyMetadata(true, referenceItem, element);
            Assert.True(referenceItem.GetMetadata("BuildReference").Equals("true", StringComparison.OrdinalIgnoreCase));
            Assert.True(referenceItem.GetMetadata("ReferenceOutputAssembly").Equals("true", StringComparison.OrdinalIgnoreCase));

            // Test the case where only ReferenceOutputAssembly is not set
            referenceItem = new TaskItem("TestItem");
            referenceItem.SetMetadata("BuildReference", "true");
            doc = new XmlDocument();
            element = doc.CreateElement("TestElement");
            element.SetAttribute("BuildProjectInSolution", "false");
            AssignProjectConfiguration.SetBuildInProjectAndReferenceOutputAssemblyMetadata(true, referenceItem, element);
            Assert.True(referenceItem.GetMetadata("BuildReference").Equals("true", StringComparison.OrdinalIgnoreCase));
            Assert.True(referenceItem.GetMetadata("ReferenceOutputAssembly").Equals("false", StringComparison.OrdinalIgnoreCase));

            // Test the case where only BuildReference is not set
            referenceItem = new TaskItem("TestItem");
            referenceItem.SetMetadata("ReferenceOutputAssembly", "true");
            doc = new XmlDocument();
            element = doc.CreateElement("TestElement");
            element.SetAttribute("BuildProjectInSolution", "false");
            AssignProjectConfiguration.SetBuildInProjectAndReferenceOutputAssemblyMetadata(true, referenceItem, element);
            Assert.True(referenceItem.GetMetadata("BuildReference").Equals("false", StringComparison.OrdinalIgnoreCase));
            Assert.True(referenceItem.GetMetadata("ReferenceOutputAssembly").Equals("true", StringComparison.OrdinalIgnoreCase));
        }


        private void TestUnresolvedReferencesHelper(ArrayList projectRefs, Hashtable pregenConfigurations,
            out Hashtable unresolvedProjects, out Hashtable resolvedProjects)
        {
            // Use the XML string generation method from our sister class - XML element names will be different,
            // but they are ignored anyway, and the rest is identical
            string xmlString = ResolveNonMSBuildProjectOutput_Tests.CreatePregeneratedPathDoc(pregenConfigurations);

            MockEngine engine = new MockEngine();
            AssignProjectConfiguration rpc = new AssignProjectConfiguration();
            rpc.BuildEngine = engine;
            rpc.SolutionConfigurationContents = xmlString;
            rpc.ProjectReferences = (ITaskItem[])projectRefs.ToArray(typeof(ITaskItem));

            bool result = rpc.Execute();
            unresolvedProjects = new Hashtable();

            for (int i = 0; i < rpc.UnassignedProjects.Length; i++)
            {
                unresolvedProjects[rpc.UnassignedProjects[i].ItemSpec] = rpc.UnassignedProjects[i];
            }

            resolvedProjects = new Hashtable();
            for (int i = 0; i < rpc.AssignedProjects.Length; i++)
            {
                resolvedProjects[rpc.AssignedProjects[i].GetMetadata("FullConfiguration")] = rpc.AssignedProjects[i];
            }
        }

        /// <summary>
        /// Verifies that the UnresolvedProjectReferences output parameter is populated correctly.
        /// </summary>
        [Fact]
        public void TestUnresolvedReferences()
        {
            Hashtable unresolvedProjects = null;
            Hashtable resolvedProjects = null;
            Hashtable projectConfigurations = null;
            ArrayList projectRefs = null;

            projectRefs = new ArrayList();
            projectRefs.Add(ResolveNonMSBuildProjectOutput_Tests.CreateReferenceItem("MCDep1.vcproj", "{2F6BBCC3-7111-4116-A68B-000000000000}",
                "{8BC9CEB8-8B4A-11D0-8D11-00A0C91BC942}", "MCDep1"));
            projectRefs.Add(ResolveNonMSBuildProjectOutput_Tests.CreateReferenceItem("MCDep2.vcproj", "{2F6BBCC3-7111-4116-A68B-34CFC76F37C5}",
                "{8BC9CEB8-8B4A-11D0-8D11-00A0C91BC942}", "MCDep2"));

            // 1. multiple projects, none resolvable
            projectConfigurations = new Hashtable();
            projectConfigurations.Add("{11111111-1111-1111-1111-111111111111}", @"Config1|Win32");
            projectConfigurations.Add("{11111111-1111-1111-1111-111111111112}", @"Config2|AnyCPU");
            projectConfigurations.Add("{11111111-1111-1111-1111-111111111113}", @"Config3|AnyCPU");

            TestUnresolvedReferencesHelper(projectRefs, projectConfigurations, out unresolvedProjects, out resolvedProjects);

            Assert.Equal(0, resolvedProjects.Count); // "No resolved refs expected for case 1"
            Assert.Equal(2, unresolvedProjects.Count); // "Two unresolved refs expected for case 1"
            Assert.Equal(unresolvedProjects["MCDep1.vcproj"], projectRefs[0]);
            Assert.Equal(unresolvedProjects["MCDep2.vcproj"], projectRefs[1]);

            // 2. multiple projects, one resolvable
            projectConfigurations = new Hashtable();
            projectConfigurations.Add("{11111111-1111-1111-1111-111111111111}", @"Config1|Win32");
            projectConfigurations.Add("{11111111-1111-1111-1111-111111111112}", @"Config2|AnyCPU");
            projectConfigurations.Add("{11111111-1111-1111-1111-111111111113}", @"Config3|AnyCPU");
            projectConfigurations.Add("{2F6BBCC3-7111-4116-A68B-34CFC76F37C5}", @"CorrectProjectConfig|Platform");

            TestUnresolvedReferencesHelper(projectRefs, projectConfigurations, out unresolvedProjects, out resolvedProjects);

            Assert.Equal(1, resolvedProjects.Count); // "One resolved ref expected for case 2"
            Assert.True(resolvedProjects.ContainsKey(@"CorrectProjectConfig|Platform"));
            Assert.Equal(1, unresolvedProjects.Count); // "One unresolved ref expected for case 2"
            Assert.Equal(unresolvedProjects["MCDep1.vcproj"], projectRefs[0]);

            // 3. multiple projects, all resolvable
            projectConfigurations = new Hashtable();
            projectConfigurations.Add("{11111111-1111-1111-1111-111111111111}", @"Config1|Win32");
            projectConfigurations.Add("{11111111-1111-1111-1111-111111111112}", @"Config2|AnyCPU");
            projectConfigurations.Add("{11111111-1111-1111-1111-111111111113}", @"Config3|AnyCPU");
            projectConfigurations.Add("{2F6BBCC3-7111-4116-A68B-34CFC76F37C5}", @"CorrectProjectConfig|Platform");
            projectConfigurations.Add("{2F6BBCC3-7111-4116-A68B-000000000000}", @"CorrectProjectConfig2|Platform");

            TestUnresolvedReferencesHelper(projectRefs, projectConfigurations, out unresolvedProjects, out resolvedProjects);

            Assert.Equal(2, resolvedProjects.Count); // "Two resolved refs expected for case 3"
            Assert.True(resolvedProjects.ContainsKey(@"CorrectProjectConfig|Platform"));
            Assert.True(resolvedProjects.ContainsKey(@"CorrectProjectConfig2|Platform"));
            Assert.Equal(0, unresolvedProjects.Count); // "No unresolved refs expected for case 3"
        }

        #region Test Defaults
        /// <summary>
        /// Verify if no values are passed in for certain properties that their default values are used.
        /// </summary>
        [Fact]
        public void VerifyDefaultValueDefaultToVcxPlatformMappings()
        {
            string expectedDefaultToVcxPlatformMapping = "AnyCPU=Win32;X86=Win32;X64=X64;Itanium=Itanium";

            AssignProjectConfiguration assignProjectConfiguration = new AssignProjectConfiguration();

            /// Test defaults with nothing set
            string actualDefaultToVcxPlatformMapping = assignProjectConfiguration.DefaultToVcxPlatformMapping;
            Assert.True(expectedDefaultToVcxPlatformMapping.Equals(actualDefaultToVcxPlatformMapping, StringComparison.OrdinalIgnoreCase), String.Format("Expected '{0}' but found '{1}'", expectedDefaultToVcxPlatformMapping, actualDefaultToVcxPlatformMapping));

            assignProjectConfiguration.DefaultToVcxPlatformMapping = String.Empty;
            actualDefaultToVcxPlatformMapping = assignProjectConfiguration.DefaultToVcxPlatformMapping;
            Assert.True(expectedDefaultToVcxPlatformMapping.Equals(actualDefaultToVcxPlatformMapping, StringComparison.OrdinalIgnoreCase), String.Format("Expected '{0}' but found '{1}'", expectedDefaultToVcxPlatformMapping, actualDefaultToVcxPlatformMapping));

            assignProjectConfiguration.DefaultToVcxPlatformMapping = null;
            actualDefaultToVcxPlatformMapping = assignProjectConfiguration.DefaultToVcxPlatformMapping;
            Assert.True(expectedDefaultToVcxPlatformMapping.Equals(actualDefaultToVcxPlatformMapping, StringComparison.OrdinalIgnoreCase), String.Format("Expected '{0}' but found '{1}'", expectedDefaultToVcxPlatformMapping, actualDefaultToVcxPlatformMapping));
        }

        /// <summary>
        /// Verify if no values are passed in for certain properties that their default values are used.
        /// </summary>
        [Fact]
        public void VerifyDefaultValuesVcxToDefaultPlatformMappingNoOutput()
        {
            string expectedVcxToDefaultPlatformMappingNoOutput = "Win32=X86;X64=X64;Itanium=Itanium";
            AssignProjectConfiguration assignProjectConfiguration = new AssignProjectConfiguration();

            // Test the case for VcxToDefaultPlatformMapping when the outputType is not library
            string actualVcxToDefaultPlatformMappingNoOutput = assignProjectConfiguration.VcxToDefaultPlatformMapping;
            Assert.True(expectedVcxToDefaultPlatformMappingNoOutput.Equals(actualVcxToDefaultPlatformMappingNoOutput, StringComparison.OrdinalIgnoreCase), String.Format("Expected '{0}' but found '{1}'", expectedVcxToDefaultPlatformMappingNoOutput, actualVcxToDefaultPlatformMappingNoOutput));

            assignProjectConfiguration.VcxToDefaultPlatformMapping = String.Empty;
            actualVcxToDefaultPlatformMappingNoOutput = assignProjectConfiguration.VcxToDefaultPlatformMapping;
            Assert.True(expectedVcxToDefaultPlatformMappingNoOutput.Equals(actualVcxToDefaultPlatformMappingNoOutput, StringComparison.OrdinalIgnoreCase), String.Format("Expected '{0}' but found '{1}'", expectedVcxToDefaultPlatformMappingNoOutput, actualVcxToDefaultPlatformMappingNoOutput));

            assignProjectConfiguration.VcxToDefaultPlatformMapping = null;
            actualVcxToDefaultPlatformMappingNoOutput = assignProjectConfiguration.VcxToDefaultPlatformMapping;
            Assert.True(expectedVcxToDefaultPlatformMappingNoOutput.Equals(actualVcxToDefaultPlatformMappingNoOutput, StringComparison.OrdinalIgnoreCase), String.Format("Expected '{0}' but found '{1}'", expectedVcxToDefaultPlatformMappingNoOutput, actualVcxToDefaultPlatformMappingNoOutput));
        }

        /// <summary>
        /// Verify if no values are passed in for certain properties that their default values are used.
        /// </summary>
        [Fact]
        public void VerifyDefaultValuesVcxToDefaultPlatformMappingLibraryOutput()
        {
            string expectedVcxToDefaultPlatformMappingLibraryOutput = "Win32=AnyCPU;X64=X64;Itanium=Itanium";
            AssignProjectConfiguration assignProjectConfiguration = new AssignProjectConfiguration();

            // Test the case for VcxToDefaultPlatformMapping when the outputType is library
            assignProjectConfiguration.OutputType = "Library";
            string actualVcxToDefaultPlatformMappingNoOutput = assignProjectConfiguration.VcxToDefaultPlatformMapping;
            Assert.True(expectedVcxToDefaultPlatformMappingLibraryOutput.Equals(actualVcxToDefaultPlatformMappingNoOutput, StringComparison.OrdinalIgnoreCase), String.Format("Expected '{0}' but found '{1}'", expectedVcxToDefaultPlatformMappingLibraryOutput, actualVcxToDefaultPlatformMappingNoOutput));

            assignProjectConfiguration.VcxToDefaultPlatformMapping = String.Empty;
            actualVcxToDefaultPlatformMappingNoOutput = assignProjectConfiguration.VcxToDefaultPlatformMapping;
            Assert.True(expectedVcxToDefaultPlatformMappingLibraryOutput.Equals(actualVcxToDefaultPlatformMappingNoOutput, StringComparison.OrdinalIgnoreCase), String.Format("Expected '{0}' but found '{1}'", expectedVcxToDefaultPlatformMappingLibraryOutput, actualVcxToDefaultPlatformMappingNoOutput));

            assignProjectConfiguration.VcxToDefaultPlatformMapping = null;
            actualVcxToDefaultPlatformMappingNoOutput = assignProjectConfiguration.VcxToDefaultPlatformMapping;
            Assert.True(expectedVcxToDefaultPlatformMappingLibraryOutput.Equals(actualVcxToDefaultPlatformMappingNoOutput, StringComparison.OrdinalIgnoreCase), String.Format("Expected '{0}' but found '{1}'", expectedVcxToDefaultPlatformMappingLibraryOutput, actualVcxToDefaultPlatformMappingNoOutput));
        }
        #endregion
    }
}
