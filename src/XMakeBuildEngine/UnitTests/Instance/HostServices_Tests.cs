// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Tests for TaskItem internal members</summary>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Build.UnitTests.OM.Instance
{
    /// <summary>
    /// Tests for the HostServices object.
    /// </summary>
    [TestClass]
    public class HostServices_Tests
    {
        /// <summary>
        /// Setup
        /// </summary>
        [TestInitialize]
        public void Setup()
        {
            ProjectCollection.GlobalProjectCollection.UnloadAllProjects();
        }

        /// <summary>
        /// Test allowed host object registrations
        /// </summary>
        [TestMethod]
        public void TestValidHostObjectRegistration()
        {
            HostServices hostServices = new HostServices();
            TestHostObject hostObject = new TestHostObject();
            TestHostObject hostObject2 = new TestHostObject();
            TestHostObject hostObject3 = new TestHostObject();
            hostServices.RegisterHostObject("foo.proj", "target", "task", hostObject);
            hostServices.RegisterHostObject("foo.proj", "target2", "task", hostObject2);
            hostServices.RegisterHostObject("foo.proj", "target", "task2", hostObject3);

            Assert.AreSame(hostObject, hostServices.GetHostObject("foo.proj", "target", "task"));
            Assert.AreSame(hostObject2, hostServices.GetHostObject("foo.proj", "target2", "task"));
            Assert.AreSame(hostObject3, hostServices.GetHostObject("foo.proj", "target", "task2"));
        }

        /// <summary>
        /// Test ensuring a null project for host object registration throws.
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void TestInvalidHostObjectRegistration_NullProject()
        {
            HostServices hostServices = new HostServices();
            TestHostObject hostObject = new TestHostObject();
            hostServices.RegisterHostObject(null, "target", "task", hostObject);
        }

        /// <summary>
        /// Test ensuring a null target for host object registration throws.
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void TestInvalidHostObjectRegistration_NullTarget()
        {
            HostServices hostServices = new HostServices();
            TestHostObject hostObject = new TestHostObject();
            hostServices.RegisterHostObject("project", null, "task", hostObject);
        }

        /// <summary>
        /// Test ensuring a null task for host object registration throws.
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void TestInvalidHostObjectRegistration_NullTask()
        {
            HostServices hostServices = new HostServices();
            TestHostObject hostObject = new TestHostObject();
            hostServices.RegisterHostObject("project", "target", null, hostObject);
        }

        /// <summary>
        /// Test which verifies host object unregistration.
        /// </summary>
        [TestMethod]
        public void TestUnregisterHostObject()
        {
            HostServices hostServices = new HostServices();
            TestHostObject hostObject = new TestHostObject();
            hostServices.RegisterHostObject("project", "target", "task", hostObject);
            Assert.AreSame(hostObject, hostServices.GetHostObject("project", "target", "task"));

            hostServices.RegisterHostObject("project", "target", "task", null);
            Assert.IsNull(hostServices.GetHostObject("project", "target", "task"));
        }

        /// <summary>
        /// Test which shows that affinity defaults to Any.
        /// </summary>
        [TestMethod]
        public void TestAffinityDefaultsToAny()
        {
            HostServices hostServices = new HostServices();
            Assert.AreEqual(NodeAffinity.Any, hostServices.GetNodeAffinity("project"));
        }

        /// <summary>
        /// Test which shows that setting a host object causes the affinity to become InProc.
        /// </summary>
        [TestMethod]
        public void TestHostObjectCausesInProcAffinity()
        {
            HostServices hostServices = new HostServices();
            TestHostObject hostObject = new TestHostObject();
            hostServices.RegisterHostObject("project", "target", "task", hostObject);
            Assert.AreEqual(NodeAffinity.InProc, hostServices.GetNodeAffinity("project"));
        }

        /// <summary>
        /// Test of the ability to set and change specific project affinities.
        /// </summary>
        [TestMethod]
        public void TestSpecificAffinityRegistration()
        {
            HostServices hostServices = new HostServices();
            hostServices.SetNodeAffinity("project", NodeAffinity.InProc);
            Assert.AreEqual(NodeAffinity.InProc, hostServices.GetNodeAffinity("project"));
            hostServices.SetNodeAffinity("project", NodeAffinity.OutOfProc);
            Assert.AreEqual(NodeAffinity.OutOfProc, hostServices.GetNodeAffinity("project"));
            hostServices.SetNodeAffinity("project", NodeAffinity.Any);
            Assert.AreEqual(NodeAffinity.Any, hostServices.GetNodeAffinity("project"));
        }

        /// <summary>
        /// Make sure we get the default affinity when the affinity map exists, but the specific 
        /// project we're requesting is not set. 
        /// </summary>
        [TestMethod]
        public void TestDefaultAffinityWhenProjectNotRegistered()
        {
            HostServices hostServices = new HostServices();
            hostServices.SetNodeAffinity("project1", NodeAffinity.InProc);
            Assert.AreEqual(NodeAffinity.Any, hostServices.GetNodeAffinity("project2"));
        }

        /// <summary>
        /// Test of setting the default affinity.
        /// </summary>
        [TestMethod]
        public void TestGeneralAffinityRegistration()
        {
            HostServices hostServices = new HostServices();

            hostServices.SetNodeAffinity(String.Empty, NodeAffinity.InProc);
            Assert.AreEqual(NodeAffinity.InProc, hostServices.GetNodeAffinity("project"));
            Assert.AreEqual(NodeAffinity.InProc, hostServices.GetNodeAffinity("project2"));

            hostServices.SetNodeAffinity(String.Empty, NodeAffinity.OutOfProc);
            Assert.AreEqual(NodeAffinity.OutOfProc, hostServices.GetNodeAffinity("project"));
            Assert.AreEqual(NodeAffinity.OutOfProc, hostServices.GetNodeAffinity("project2"));

            hostServices.SetNodeAffinity(String.Empty, NodeAffinity.Any);
            Assert.AreEqual(NodeAffinity.Any, hostServices.GetNodeAffinity("project"));
            Assert.AreEqual(NodeAffinity.Any, hostServices.GetNodeAffinity("project2"));
        }

        /// <summary>
        /// Test which ensures specific project affinities override general affinity.
        /// </summary>
        [TestMethod]
        public void TestOverrideGeneralAffinityRegistration()
        {
            HostServices hostServices = new HostServices();

            hostServices.SetNodeAffinity(String.Empty, NodeAffinity.InProc);
            hostServices.SetNodeAffinity("project", NodeAffinity.OutOfProc);
            Assert.AreEqual(NodeAffinity.OutOfProc, hostServices.GetNodeAffinity("project"));
            Assert.AreEqual(NodeAffinity.InProc, hostServices.GetNodeAffinity("project2"));
        }

        /// <summary>
        /// Test of clearing the affinity settings for all projects.
        /// </summary>
        [TestMethod]
        public void TestClearingAffinities()
        {
            HostServices hostServices = new HostServices();

            hostServices.SetNodeAffinity("project", NodeAffinity.OutOfProc);
            Assert.AreEqual(NodeAffinity.OutOfProc, hostServices.GetNodeAffinity("project"));
            hostServices.SetNodeAffinity(null, NodeAffinity.OutOfProc);
            Assert.AreEqual(NodeAffinity.Any, hostServices.GetNodeAffinity("project"));

            hostServices.SetNodeAffinity(String.Empty, NodeAffinity.OutOfProc);
            Assert.AreEqual(NodeAffinity.OutOfProc, hostServices.GetNodeAffinity("project"));
            hostServices.SetNodeAffinity(null, NodeAffinity.OutOfProc);
            Assert.AreEqual(NodeAffinity.Any, hostServices.GetNodeAffinity("project"));
        }

        /// <summary>
        /// Test which ensures that setting an OutOfProc affinity for a project with a host object throws.
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void TestContradictoryAffinityCausesException_OutOfProc()
        {
            HostServices hostServices = new HostServices();
            TestHostObject hostObject = new TestHostObject();
            hostServices.RegisterHostObject("project", "target", "task", hostObject);
            Assert.AreEqual(NodeAffinity.InProc, hostServices.GetNodeAffinity("project"));
            hostServices.SetNodeAffinity("project", NodeAffinity.OutOfProc);
        }

        /// <summary>
        /// Test which ensures that setting an Any affinity for a project with a host object throws.
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void TestContradictoryAffinityCausesException_Any()
        {
            HostServices hostServices = new HostServices();
            TestHostObject hostObject = new TestHostObject();
            hostServices.RegisterHostObject("project", "target", "task", hostObject);
            Assert.AreEqual(NodeAffinity.InProc, hostServices.GetNodeAffinity("project"));
            hostServices.SetNodeAffinity("project", NodeAffinity.Any);
        }

        /// <summary>
        /// Test which ensures that setting the InProc affinity for a project with a host object is allowed.
        /// </summary>
        [TestMethod]
        public void TestNonContradictoryAffinityAllowed()
        {
            HostServices hostServices = new HostServices();
            TestHostObject hostObject = new TestHostObject();
            hostServices.RegisterHostObject("project", "target", "task", hostObject);
            Assert.AreEqual(NodeAffinity.InProc, hostServices.GetNodeAffinity("project"));
            hostServices.SetNodeAffinity("project", NodeAffinity.InProc);
            Assert.AreEqual(NodeAffinity.InProc, hostServices.GetNodeAffinity("project"));
        }

        /// <summary>
        /// Test which ensures that setting a host object for a project with an out-of-proc affinity throws.
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void TestContraditcoryHostObjectCausesException_OutOfProc()
        {
            HostServices hostServices = new HostServices();
            TestHostObject hostObject = new TestHostObject();
            hostServices.SetNodeAffinity("project", NodeAffinity.OutOfProc);
            hostServices.RegisterHostObject("project", "target", "task", hostObject);
        }

        /// <summary>
        /// Test which ensures the host object can be set for a project which has the Any affinity specifically set.
        /// </summary>
        [TestMethod]
        public void TestNonContraditcoryHostObjectAllowed_Any()
        {
            HostServices hostServices = new HostServices();
            TestHostObject hostObject = new TestHostObject();
            hostServices.SetNodeAffinity("project", NodeAffinity.Any);
            hostServices.RegisterHostObject("project", "target", "task", hostObject);
            Assert.AreEqual(NodeAffinity.InProc, hostServices.GetNodeAffinity("project"));
        }

        /// <summary>
        /// Test which ensures the host object can be set for a project which has an out-of-proc affinity only because that affinity
        /// is implied by being set generally for all project, not for that specific project.
        /// </summary>
        [TestMethod]
        public void TestNonContraditcoryHostObjectAllowed_ImplicitOutOfProc()
        {
            HostServices hostServices = new HostServices();
            TestHostObject hostObject = new TestHostObject();
            hostServices.SetNodeAffinity(String.Empty, NodeAffinity.InProc);
            hostServices.RegisterHostObject("project", "target", "task", hostObject);
        }

        /// <summary>
        /// Test which ensures the host object can be set for a project which has the InProc affinity specifically set.
        /// </summary>
        [TestMethod]
        public void TestNonContraditcoryHostObjectAllowed_InProc()
        {
            HostServices hostServices = new HostServices();
            TestHostObject hostObject = new TestHostObject();
            hostServices.SetNodeAffinity("project", NodeAffinity.InProc);
            hostServices.RegisterHostObject("project", "target", "task", hostObject);
        }

        /// <summary>
        /// Test which ensures the affinity for a project can be changed once the host object is cleared.
        /// </summary>
        [TestMethod]
        public void TestAffinityChangeAfterClearingHostObject()
        {
            HostServices hostServices = new HostServices();
            TestHostObject hostObject = new TestHostObject();
            hostServices.RegisterHostObject("project", "target", "task", hostObject);
            Assert.AreEqual(NodeAffinity.InProc, hostServices.GetNodeAffinity("project"));
            hostServices.RegisterHostObject("project", "target", "task", null);
            Assert.AreEqual(NodeAffinity.Any, hostServices.GetNodeAffinity("project"));
            hostServices.SetNodeAffinity("project", NodeAffinity.OutOfProc);
            Assert.AreEqual(NodeAffinity.OutOfProc, hostServices.GetNodeAffinity("project"));
        }

        /// <summary>
        /// Test which ensures that setting then clearing the host object restores a previously specifically set non-conflicting affinity.
        /// </summary>
        [TestMethod]
        public void TestUnregisteringNonConflictingHostObjectRestoresOriginalAffinity()
        {
            HostServices hostServices = new HostServices();
            TestHostObject hostObject = new TestHostObject();
            hostServices.SetNodeAffinity(String.Empty, NodeAffinity.OutOfProc);
            hostServices.SetNodeAffinity("project", NodeAffinity.Any);
            Assert.AreEqual(NodeAffinity.OutOfProc, hostServices.GetNodeAffinity("project2"));
            Assert.AreEqual(NodeAffinity.Any, hostServices.GetNodeAffinity("project"));

            hostServices.RegisterHostObject("project", "target", "task", hostObject);
            Assert.AreEqual(NodeAffinity.InProc, hostServices.GetNodeAffinity("project"));
            hostServices.RegisterHostObject("project", "target", "task", null);
            Assert.AreEqual(NodeAffinity.Any, hostServices.GetNodeAffinity("project"));
            Assert.AreEqual(NodeAffinity.OutOfProc, hostServices.GetNodeAffinity("project2"));
        }

        /// <summary>
        /// Tests that creating a BuildRequestData with a non-conflicting HostServices and ProjectInstance works.
        /// </summary>
        [TestMethod]
        public void TestProjectInstanceWithNonConflictingHostServices()
        {
            HostServices hostServices = new HostServices();
            ProjectInstance project = CreateDummyProject("foo.proj");

            BuildRequestData data = new BuildRequestData(project, new string[] { }, hostServices);

            hostServices.SetNodeAffinity(project.FullPath, NodeAffinity.InProc);
            BuildRequestData data2 = new BuildRequestData(project, new string[] { }, hostServices);
        }

        /// <summary>
        /// Tests that unloading all projects from the project collection
        /// discards the host services
        /// </summary>
        [TestMethod]
        public void UnloadedProjectDiscardsHostServicesAllProjects()
        {
            HostServices hostServices = new HostServices();
            TestHostObject th = new TestHostObject();
            ProjectCollection.GlobalProjectCollection.HostServices = hostServices;
            Project project = LoadDummyProject("foo.proj");

            hostServices.RegisterHostObject(project.FullPath, "test", "Message", th);

            ProjectCollection.GlobalProjectCollection.UnloadAllProjects();

            Assert.IsFalse(hostServices.HasHostObject(project.FullPath));
        }

        /// <summary>
        /// Tests that unloading the last project from the project collection
        /// discards the host services for that project
        /// </summary>
        [TestMethod]
        public void UnloadedProjectDiscardsHostServices()
        {
            HostServices hostServices = new HostServices();
            TestHostObject th = new TestHostObject();
            ProjectCollection.GlobalProjectCollection.HostServices = hostServices;
            Project project1 = LoadDummyProject("foo.proj");
            Project project2 = LoadDummyProject("foo.proj");

            hostServices.RegisterHostObject(project1.FullPath, "test", "Message", th);

            ProjectCollection.GlobalProjectCollection.UnloadProject(project1);

            Assert.IsTrue(hostServices.HasHostObject(project2.FullPath));

            ProjectCollection.GlobalProjectCollection.UnloadProject(project2);

            Assert.IsFalse(hostServices.HasHostObject(project2.FullPath));
        }

        /// <summary>
        /// Creates a dummy project instance.
        /// </summary>
        public ProjectInstance CreateDummyProject(string fileName)
        {
            string contents = ObjectModelHelpers.CleanupFileContents(@"
<Project xmlns='msbuildnamespace' ToolsVersion='msbuilddefaulttoolsversion'>
 <Target Name='test'>
 </Target>
</Project>
");

            Project project = new Project(new XmlTextReader(new StringReader(contents)), new Dictionary<string, string>(), "4.0");
            project.FullPath = fileName;
            ProjectInstance instance = project.CreateProjectInstance();

            return instance;
        }

        /// <summary>
        /// Loads a dummy project instance.
        /// </summary>
        public Project LoadDummyProject(string fileName)
        {
            string contents = ObjectModelHelpers.CleanupFileContents(@"
<Project xmlns='msbuildnamespace' ToolsVersion='msbuilddefaulttoolsversion'>
 <Target Name='test'>
    <Message text='hello' />
 </Target>
</Project>
");
            Dictionary<string, string> globals = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            globals["UniqueDummy"] = Guid.NewGuid().ToString();

            Project project = ProjectCollection.GlobalProjectCollection.LoadProject(new XmlTextReader(new StringReader(contents)), globals, "4.0");
            project.FullPath = fileName;

            return project;
        }

        /// <summary>
        /// A dummy host object class.
        /// </summary>
        private class TestHostObject : ITaskHost
        {
            /// <summary>
            /// Constructor.
            /// </summary>
            public TestHostObject()
            {
            }
        }
    }
}
