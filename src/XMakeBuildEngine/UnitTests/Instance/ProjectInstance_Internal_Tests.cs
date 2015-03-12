// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Tests for ProjectInstance internal members</summary>
//-----------------------------------------------------------------------

using System.Collections.Generic;
using Microsoft.Build.Execution;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Framework;
using System.Collections;
using System;
using Microsoft.Build.Construction;
using System.IO;
using System.Xml;
using System.Linq;

namespace Microsoft.Build.UnitTests.OM.Instance
{
    /// <summary>
    /// Tests for ProjectInstance internal members
    /// </summary>
    [TestClass]
    public class ProjectInstance_Internal_Tests
    {
        /// <summary>
        /// Read task registrations
        /// </summary>
        [TestMethod]
        public void GetTaskRegistrations()
        {
            try
            {
                string projectFileContent = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                        <UsingTask TaskName='t0' AssemblyFile='af0'/>
                        <UsingTask TaskName='t1' AssemblyFile='af1a'/>
                        <ItemGroup>
                            <i Include='i0'/>
                        </ItemGroup>
                        <Import Project='{0}'/>
                    </Project>";

                string importContent = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                        <UsingTask TaskName='t1' AssemblyName='an1' Condition=""'$(p)'=='v'""/>
                        <UsingTask TaskName='t2' AssemblyName='an2' Condition=""'@(i)'=='i0'""/>
                        <UsingTask TaskName='t3' AssemblyFile='af' Condition='false'/>
                        <PropertyGroup>
                            <p>v</p>
                        </PropertyGroup>
                    </Project>";

                string importPath = ObjectModelHelpers.CreateFileInTempProjectDirectory("import.targets", importContent);
                projectFileContent = String.Format(projectFileContent, importPath);

                ProjectInstance project = new Project(ProjectRootElement.Create(XmlReader.Create(new StringReader(projectFileContent)))).CreateProjectInstance();

                Assert.AreEqual(3, project.TaskRegistry.TaskRegistrations.Count);
                Assert.AreEqual(Path.Combine(Environment.CurrentDirectory, "af0"), project.TaskRegistry.TaskRegistrations[new TaskRegistry.RegisteredTaskIdentity("t0", null)][0].TaskFactoryAssemblyLoadInfo.AssemblyFile);
                Assert.AreEqual(Path.Combine(Environment.CurrentDirectory, "af1a"), project.TaskRegistry.TaskRegistrations[new TaskRegistry.RegisteredTaskIdentity("t1", null)][0].TaskFactoryAssemblyLoadInfo.AssemblyFile);
                Assert.AreEqual("an1", project.TaskRegistry.TaskRegistrations[new TaskRegistry.RegisteredTaskIdentity("t1", null)][1].TaskFactoryAssemblyLoadInfo.AssemblyName);
                Assert.AreEqual("an2", project.TaskRegistry.TaskRegistrations[new TaskRegistry.RegisteredTaskIdentity("t2", null)][0].TaskFactoryAssemblyLoadInfo.AssemblyName);
            }
            finally
            {
                ObjectModelHelpers.DeleteTempProjectDirectory();
            }
        }

        /// <summary>
        /// InitialTargets and DefaultTargets with imported projects.
        /// DefaultTargets are not read from imported projects.
        /// InitialTargets are gathered from imports depth-first.
        /// </summary>
        [TestMethod]
        public void InitialTargetsDefaultTargets()
        {
            try
            {
                string projectFileContent = @"
                    <Project DefaultTargets='d0a;d0b' InitialTargets='i0a;i0b' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                        <Import Project='{0}'/>
                        <Import Project='{1}'/>
                    </Project>";

                string import1Content = @"
                    <Project DefaultTargets='d1a;d1b' InitialTargets='i1a;i1b' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                        <Import Project='{0}'/>
                    </Project>";

                string import2Content = @"<Project DefaultTargets='d2a;2db' InitialTargets='i2a;i2b' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'/>";

                string import3Content = @"<Project DefaultTargets='d3a;d3b' InitialTargets='i3a;i3b' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'/>";

                string import2Path = ObjectModelHelpers.CreateFileInTempProjectDirectory("import2.targets", import2Content);
                string import3Path = ObjectModelHelpers.CreateFileInTempProjectDirectory("import3.targets", import3Content);

                import1Content = String.Format(import1Content, import3Path);
                string import1Path = ObjectModelHelpers.CreateFileInTempProjectDirectory("import1.targets", import1Content);

                projectFileContent = String.Format(projectFileContent, import1Path, import2Path);

                ProjectInstance project = new Project(ProjectRootElement.Create(XmlReader.Create(new StringReader(projectFileContent)))).CreateProjectInstance();

                Helpers.AssertListsValueEqual(new string[] { "d0a", "d0b" }, project.DefaultTargets);
                Helpers.AssertListsValueEqual(new string[] { "i0a", "i0b", "i1a", "i1b", "i3a", "i3b", "i2a", "i2b" }, project.InitialTargets);
            }
            finally
            {
                ObjectModelHelpers.DeleteTempProjectDirectory();
            }
        }

        /// <summary>
        /// InitialTargets and DefaultTargets with imported projects.
        /// DefaultTargets are not read from imported projects.
        /// InitialTargets are gathered from imports depth-first.
        /// </summary>
        [TestMethod]
        public void InitialTargetsDefaultTargetsEscaped()
        {
            try
            {
                string projectFileContent = @"
                    <Project DefaultTargets='d0a%3bd0b' InitialTargets='i0a%3bi0b' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                    </Project>";

                ProjectInstance project = new Project(ProjectRootElement.Create(XmlReader.Create(new StringReader(projectFileContent)))).CreateProjectInstance();

                Helpers.AssertListsValueEqual(new string[] { "d0a;d0b" }, project.DefaultTargets);
                Helpers.AssertListsValueEqual(new string[] { "i0a;i0b" }, project.InitialTargets);
            }
            finally
            {
                ObjectModelHelpers.DeleteTempProjectDirectory();
            }
        }

        /// <summary>
        /// Read property group under target
        /// </summary>
        [TestMethod]
        public void GetPropertyGroupUnderTarget()
        {
            string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                        <Target Name='t'>
                            <PropertyGroup Condition='c1'>
                                <p1 Condition='c2'>v1</p1>
                                <p2/>
                            </PropertyGroup>
                        </Target>
                    </Project>
                ";

            ProjectInstance p = GetProjectInstance(content);
            ProjectPropertyGroupTaskInstance propertyGroup = (ProjectPropertyGroupTaskInstance)(p.Targets["t"].Children[0]);

            Assert.AreEqual("c1", propertyGroup.Condition);

            List<ProjectPropertyGroupTaskPropertyInstance> properties = Helpers.MakeList(propertyGroup.Properties);
            Assert.AreEqual(2, properties.Count);

            Assert.AreEqual("c2", properties[0].Condition);
            Assert.AreEqual("v1", properties[0].Value);

            Assert.AreEqual(String.Empty, properties[1].Condition);
            Assert.AreEqual(String.Empty, properties[1].Value);
        }

        /// <summary>
        /// Read item group under target
        /// </summary>
        [TestMethod]
        public void GetItemGroupUnderTarget()
        {
            string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                        <Target Name='t'>
                            <ItemGroup Condition='c1'>
                                <i Include='i1' Exclude='e1' Condition='c2'>
                                    <m Condition='c3'>m1</m>    
                                    <n>n1</n>                        
                                </i>
                                <j Remove='r1'/>
                                <k>
                                    <o>o1</o>
                                </k>
                            </ItemGroup>
                        </Target>
                    </Project>
                ";

            ProjectInstance p = GetProjectInstance(content);
            ProjectItemGroupTaskInstance itemGroup = (ProjectItemGroupTaskInstance)(p.Targets["t"].Children[0]);

            Assert.AreEqual("c1", itemGroup.Condition);

            List<ProjectItemGroupTaskItemInstance> items = Helpers.MakeList(itemGroup.Items);
            Assert.AreEqual(3, items.Count);

            Assert.AreEqual("i1", items[0].Include);
            Assert.AreEqual("e1", items[0].Exclude);
            Assert.AreEqual(String.Empty, items[0].Remove);
            Assert.AreEqual("c2", items[0].Condition);

            Assert.AreEqual(String.Empty, items[1].Include);
            Assert.AreEqual(String.Empty, items[1].Exclude);
            Assert.AreEqual("r1", items[1].Remove);
            Assert.AreEqual(String.Empty, items[1].Condition);

            Assert.AreEqual(String.Empty, items[2].Include);
            Assert.AreEqual(String.Empty, items[2].Exclude);
            Assert.AreEqual(String.Empty, items[2].Remove);
            Assert.AreEqual(String.Empty, items[2].Condition);

            List<ProjectItemGroupTaskMetadataInstance> metadata1 = Helpers.MakeList(items[0].Metadata);
            List<ProjectItemGroupTaskMetadataInstance> metadata2 = Helpers.MakeList(items[1].Metadata);
            List<ProjectItemGroupTaskMetadataInstance> metadata3 = Helpers.MakeList(items[2].Metadata);

            Assert.AreEqual(2, metadata1.Count);
            Assert.AreEqual(0, metadata2.Count);
            Assert.AreEqual(1, metadata3.Count);

            Assert.AreEqual("c3", metadata1[0].Condition);
            Assert.AreEqual("m1", metadata1[0].Value);
            Assert.AreEqual(String.Empty, metadata1[1].Condition);
            Assert.AreEqual("n1", metadata1[1].Value);

            Assert.AreEqual(String.Empty, metadata3[0].Condition);
            Assert.AreEqual("o1", metadata3[0].Value);
        }

        /// <summary>
        /// Task registry accessor
        /// </summary>
        [TestMethod]
        public void GetTaskRegistry()
        {
            ProjectInstance p = GetSampleProjectInstance();

            Assert.AreEqual(true, p.TaskRegistry != null);
        }

        /// <summary>
        /// Global properties accessor
        /// </summary>
        [TestMethod]
        public void GetGlobalProperties()
        {
            ProjectInstance p = GetSampleProjectInstance();

            Assert.AreEqual("v1", p.GlobalPropertiesDictionary["g1"].EvaluatedValue);
            Assert.AreEqual("v2", p.GlobalPropertiesDictionary["g2"].EvaluatedValue);
        }

        /// <summary>
        /// ToolsVersion accessor
        /// </summary>
        [TestMethod]
        public void GetToolsVersion()
        {
            ProjectInstance p = GetSampleProjectInstance();

            Assert.AreEqual("4.0", p.Toolset.ToolsVersion);
        }

        /// <summary>
        /// Toolset data is cloned properly
        /// </summary>
        [TestMethod]
        public void CloneToolsetData()
        {
            var projectCollection = new ProjectCollection();
            CreateMockToolsetIfNotExists("TESTTV", projectCollection);
            ProjectInstance first = GetSampleProjectInstance(null, null, projectCollection, toolsVersion: "TESTTV");
            ProjectInstance second = first.DeepCopy();
            Assert.AreEqual(first.ToolsVersion, second.ToolsVersion);
            Assert.AreEqual(first.ExplicitToolsVersion, second.ExplicitToolsVersion);
            Assert.AreEqual(first.ExplicitToolsVersionSpecified, second.ExplicitToolsVersionSpecified);
        }

        /// <summary>
        /// Test ProjectInstance's surfacing of the sub-toolset version
        /// </summary>
        [TestMethod]
        [Ignore]
        // Ignore: Test requires installed toolset.
        public void GetSubToolsetVersion()
        {
            string originalVisualStudioVersion = Environment.GetEnvironmentVariable("VisualStudioVersion");

            try
            {
                Environment.SetEnvironmentVariable("VisualStudioVersion", null);

                ProjectInstance p = GetSampleProjectInstance(null, null, new ProjectCollection());

                Assert.AreEqual("4.0", p.Toolset.ToolsVersion);
                Assert.AreEqual(p.Toolset.DefaultSubToolsetVersion, p.SubToolsetVersion);
                Assert.AreEqual(p.Toolset.DefaultSubToolsetVersion, p.GetPropertyValue("VisualStudioVersion"));
            }
            finally
            {
                Environment.SetEnvironmentVariable("VisualStudioVersion", originalVisualStudioVersion);
            }
        }

        /// <summary>
        /// Test ProjectInstance's surfacing of the sub-toolset version when it is overridden by a value in the 
        /// environment 
        /// </summary>
        [TestMethod]
        public void GetSubToolsetVersion_FromEnvironment()
        {
            string originalVisualStudioVersion = Environment.GetEnvironmentVariable("VisualStudioVersion");

            try
            {
                Environment.SetEnvironmentVariable("VisualStudioVersion", "ABCD");

                ProjectInstance p = GetSampleProjectInstance(null, null, new ProjectCollection());

                Assert.AreEqual("4.0", p.Toolset.ToolsVersion);
                Assert.AreEqual("ABCD", p.SubToolsetVersion);
                Assert.AreEqual("ABCD", p.GetPropertyValue("VisualStudioVersion"));
            }
            finally
            {
                Environment.SetEnvironmentVariable("VisualStudioVersion", originalVisualStudioVersion);
            }
        }

        /// <summary>
        /// Test ProjectInstance's surfacing of the sub-toolset version when it is overridden by a global property
        /// </summary>
        [TestMethod]
        public void GetSubToolsetVersion_FromProjectGlobalProperties()
        {
            string originalVisualStudioVersion = Environment.GetEnvironmentVariable("VisualStudioVersion");

            try
            {
                Environment.SetEnvironmentVariable("VisualStudioVersion", null);

                IDictionary<string, string> globalProperties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                globalProperties.Add("VisualStudioVersion", "ABCDE");

                ProjectInstance p = GetSampleProjectInstance(null, globalProperties, new ProjectCollection());

                Assert.AreEqual("4.0", p.Toolset.ToolsVersion);
                Assert.AreEqual("ABCDE", p.SubToolsetVersion);
                Assert.AreEqual("ABCDE", p.GetPropertyValue("VisualStudioVersion"));
            }
            finally
            {
                Environment.SetEnvironmentVariable("VisualStudioVersion", originalVisualStudioVersion);
            }
        }

        /// <summary>
        /// Verify that if a sub-toolset version is passed to the constructor, it all other heuristic methods for 
        /// getting the sub-toolset version. 
        /// </summary>
        [TestMethod]
        public void GetSubToolsetVersion_FromConstructor()
        {
            string originalVisualStudioVersion = Environment.GetEnvironmentVariable("VisualStudioVersion");

            try
            {
                Environment.SetEnvironmentVariable("VisualStudioVersion", "ABC");

                string projectContent = @"<Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                        <Target Name='t'>
                            <Message Text='Hello'/>
                        </Target>
                    </Project>";

                ProjectRootElement xml = ProjectRootElement.Create(XmlReader.Create(new StringReader(projectContent)));

                IDictionary<string, string> globalProperties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                globalProperties.Add("VisualStudioVersion", "ABCD");

                IDictionary<string, string> projectCollectionGlobalProperties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                projectCollectionGlobalProperties.Add("VisualStudioVersion", "ABCDE");

                ProjectInstance p = new ProjectInstance(xml, globalProperties, "4.0", "ABCDEF", new ProjectCollection(projectCollectionGlobalProperties));

                Assert.AreEqual("4.0", p.Toolset.ToolsVersion);
                Assert.AreEqual("ABCDEF", p.SubToolsetVersion);
                Assert.AreEqual("ABCDEF", p.GetPropertyValue("VisualStudioVersion"));
            }
            finally
            {
                Environment.SetEnvironmentVariable("VisualStudioVersion", originalVisualStudioVersion);
            }
        }

        /// <summary>
        /// DefaultTargets accessor
        /// </summary>
        [TestMethod]
        public void GetDefaultTargets()
        {
            ProjectInstance p = GetSampleProjectInstance();

            Helpers.AssertListsValueEqual(new string[] { "dt" }, p.DefaultTargets);
        }

        /// <summary>
        /// InitialTargets accessor
        /// </summary>
        [TestMethod]
        public void GetInitialTargets()
        {
            ProjectInstance p = GetSampleProjectInstance();

            Helpers.AssertListsValueEqual(new string[] { "it" }, p.InitialTargets);
        }

        /// <summary>
        /// Cloning project clones targets
        /// </summary>
        [TestMethod]
        public void CloneTargets()
        {
            var hostServices = new HostServices();

            ProjectInstance first = GetSampleProjectInstance(hostServices);
            ProjectInstance second = first.DeepCopy();

            // Targets, tasks are immutable so we can expect the same objects
            Assert.IsTrue(Object.ReferenceEquals(first.Targets, second.Targets));
            Assert.IsTrue(Object.ReferenceEquals(first.Targets["t"], second.Targets["t"]));

            var firstTasks = first.Targets["t"];
            var secondTasks = second.Targets["t"];

            Assert.IsTrue(Object.ReferenceEquals(firstTasks.Children[0], secondTasks.Children[0]));
        }

        /// <summary>
        /// Cloning project copies task registry
        /// </summary>
        [TestMethod]
        public void CloneTaskRegistry()
        {
            ProjectInstance first = GetSampleProjectInstance();
            ProjectInstance second = first.DeepCopy();

            // Task registry object should be immutable
            Assert.ReferenceEquals(first.TaskRegistry, second.TaskRegistry);
        }

        /// <summary>
        /// Cloning project copies global properties
        /// </summary>
        [TestMethod]
        public void CloneGlobalProperties()
        {
            ProjectInstance first = GetSampleProjectInstance();
            ProjectInstance second = first.DeepCopy();

            Assert.AreEqual("v1", second.GlobalPropertiesDictionary["g1"].EvaluatedValue);
            Assert.AreEqual("v2", second.GlobalPropertiesDictionary["g2"].EvaluatedValue);
        }

        /// <summary>
        /// Cloning project copies default targets
        /// </summary>
        [TestMethod]
        public void CloneDefaultTargets()
        {
            ProjectInstance first = GetSampleProjectInstance();
            ProjectInstance second = first.DeepCopy();

            Helpers.AssertListsValueEqual(new string[] { "dt" }, second.DefaultTargets);
        }

        /// <summary>
        /// Cloning project copies initial targets
        /// </summary>
        [TestMethod]
        public void CloneInitialTargets()
        {
            ProjectInstance first = GetSampleProjectInstance();
            ProjectInstance second = first.DeepCopy();

            Helpers.AssertListsValueEqual(new string[] { "it" }, second.InitialTargets);
        }

        /// <summary>
        /// Cloning project copies toolsversion
        /// </summary>
        [TestMethod]
        public void CloneToolsVersion()
        {
            ProjectInstance first = GetSampleProjectInstance();
            ProjectInstance second = first.DeepCopy();

            Assert.AreEqual(first.Toolset, second.Toolset);
        }

        /// <summary>
        /// Tests building a simple project and verifying the log looks as expected.
        /// </summary>
        [TestMethod]
        public void Build()
        {
            string projectFileContent = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                        <UsingTask TaskName='Microsoft.Build.Tasks.Message' AssemblyFile='Microsoft.Build.Tasks.Core.dll'/>
                        <ItemGroup>
                            <i Include='i0'/>
                        </ItemGroup>
                        <Target Name='Build'>
                            <Message Text='Building...'/>
                            <Message Text='Completed!'/>
                        </Target>
                    </Project>";

            ProjectInstance projectInstance = GetProjectInstance(projectFileContent);
            List<ILogger> loggers = new List<ILogger>();
            MockLogger mockLogger = new MockLogger();
            loggers.Add(mockLogger);
            bool success = projectInstance.Build("Build", loggers);

            Assert.IsTrue(success);
            mockLogger.AssertLogContains(new string[] { "Building...", "Completed!" });
        }

        /// <summary>
        /// Create a ProjectInstance from provided project content
        /// </summary>
        private static ProjectInstance GetProjectInstance(string content)
        {
            return GetProjectInstance(content, null);
        }

        /// <summary>
        /// Create a ProjectInstance from provided project content and host services object
        /// </summary>
        private static ProjectInstance GetProjectInstance(string content, HostServices hostServices)
        {
            return GetProjectInstance(content, hostServices, null, null);
        }

        /// <summary>
        /// Create a ProjectInstance from provided project content and host services object
        /// </summary>
        private static ProjectInstance GetProjectInstance(string content, HostServices hostServices, IDictionary<string, string> globalProperties, ProjectCollection projectCollection, string toolsVersion = null)
        {
            XmlReader reader = XmlReader.Create(new StringReader(content));

            if (globalProperties == null)
            {
                // choose some interesting defaults if we weren't explicitly asked to use a set. 
                globalProperties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                globalProperties.Add("g1", "v1");
                globalProperties.Add("g2", "v2");
            }

            Project project = new Project(reader, globalProperties, toolsVersion ?? "4.0", projectCollection ?? ProjectCollection.GlobalProjectCollection);

            ProjectInstance instance = project.CreateProjectInstance();

            return instance;
        }

        /// <summary>
        /// Create a ProjectInstance with some items and properties and targets
        /// </summary>
        private static ProjectInstance GetSampleProjectInstance()
        {
            return GetSampleProjectInstance(null);
        }

        /// <summary>
        /// Create a ProjectInstance with some items and properties and targets
        /// </summary>
        private static ProjectInstance GetSampleProjectInstance(HostServices hostServices)
        {
            return GetSampleProjectInstance(hostServices, null, null);
        }

        /// <summary>
        /// Create a ProjectInstance with some items and properties and targets
        /// </summary>
        private static ProjectInstance GetSampleProjectInstance(HostServices hostServices, IDictionary<string, string> globalProperties, ProjectCollection projectCollection, string toolsVersion = null)
        {
            string toolsVersionSubstring = toolsVersion != null ? "ToolsVersion=\"" + toolsVersion + "\" " : String.Empty;
            string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' InitialTargets='it' DefaultTargets='dt' " + toolsVersionSubstring + @">
                        <PropertyGroup>
                            <p1>v1</p1>
                            <p2>v2</p2>
                            <p2>$(p2)X$(p)</p2>
                        </PropertyGroup>
                        <ItemGroup>
                            <i Include='i0'/>
                            <i Include='i1'>
                                <m>m1</m>
                            </i>
                            <i Include='$(p1)'/>
                        </ItemGroup>
                        <Target Name='t'>
                            <t1 a='a1' b='b1' ContinueOnError='coe' Condition='c'/>
                            <t2/>
                        </Target>
                        <Target Name='tt'/>
                    </Project>
                ";

            ProjectInstance p = GetProjectInstance(content, hostServices, globalProperties, projectCollection, toolsVersion);

            return p;
        }

        /// <summary>
        /// Creates a toolset with the given tools version if one does not already exist.
        /// </summary>
        private static void CreateMockToolsetIfNotExists(string toolsVersion, ProjectCollection projectCollection)
        {
            ProjectCollection pc = projectCollection;
            if (!pc.Toolsets.Any(t => String.Equals(t.ToolsVersion, toolsVersion, StringComparison.OrdinalIgnoreCase)))
            {
                Toolset template = pc.Toolsets.First(t => String.Equals(t.ToolsVersion, pc.DefaultToolsVersion, StringComparison.OrdinalIgnoreCase));
                var toolset = new Toolset(
                    toolsVersion,
                    template.ToolsPath,
                    template.Properties.ToDictionary(p => p.Key, p => p.Value.EvaluatedValue),
                    pc,
                    null);
                pc.AddToolset(toolset);
            }
        }
    }
}
