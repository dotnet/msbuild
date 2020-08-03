// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;

using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.UnitTests.BackEnd;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests.OM.Instance
{
    /// <summary>
    /// Tests for the HostServices object.
    /// </summary>
    public class HostServices_Tests
    {
        /// <summary>
        /// Setup
        /// </summary>
        public HostServices_Tests()
        {
            ProjectCollection.GlobalProjectCollection.UnloadAllProjects();
        }

        /// <summary>
        /// Test allowed host object registrations
        /// </summary>
        [Fact]
        public void TestValidHostObjectRegistration()
        {
            HostServices hostServices = new HostServices();
            TestHostObject hostObject = new TestHostObject();
            TestHostObject hostObject2 = new TestHostObject();
            TestHostObject hostObject3 = new TestHostObject();
            hostServices.RegisterHostObject("foo.proj", "target", "task", hostObject);
            hostServices.RegisterHostObject("foo.proj", "target2", "task", hostObject2);
            hostServices.RegisterHostObject("foo.proj", "target", "task2", hostObject3);

            Assert.Same(hostObject, hostServices.GetHostObject("foo.proj", "target", "task"));
            Assert.Same(hostObject2, hostServices.GetHostObject("foo.proj", "target2", "task"));
            Assert.Same(hostObject3, hostServices.GetHostObject("foo.proj", "target", "task2"));
        }

        /// <summary>
        /// Test ensuring a null project for host object registration throws.
        /// </summary>
        [Fact]
        public void TestInvalidHostObjectRegistration_NullProject()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                HostServices hostServices = new HostServices();
                TestHostObject hostObject = new TestHostObject();
                hostServices.RegisterHostObject(null, "target", "task", hostObject);
            }
           );
        }
        /// <summary>
        /// Test ensuring a null target for host object registration throws.
        /// </summary>
        [Fact]
        public void TestInvalidHostObjectRegistration_NullTarget()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                HostServices hostServices = new HostServices();
                TestHostObject hostObject = new TestHostObject();
                hostServices.RegisterHostObject("project", null, "task", hostObject);
            }
           );
        }
        /// <summary>
        /// Test ensuring a null task for host object registration throws.
        /// </summary>
        [Fact]
        public void TestInvalidHostObjectRegistration_NullTask()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                HostServices hostServices = new HostServices();
                TestHostObject hostObject = new TestHostObject();
                hostServices.RegisterHostObject("project", "target", null, hostObject);
            }
           );
        }
        /// <summary>
        /// Test which verifies host object unregistration.
        /// </summary>
        [Fact]
        public void TestUnregisterHostObject()
        {
            HostServices hostServices = new HostServices();
            TestHostObject hostObject = new TestHostObject();
            hostServices.RegisterHostObject("project", "target", "task", hostObject);
            Assert.Same(hostObject, hostServices.GetHostObject("project", "target", "task"));

            hostServices.RegisterHostObject("project", "target", "task", hostObject: null);
            Assert.Null(hostServices.GetHostObject("project", "target", "task"));
        }

        /// <summary>
        /// Test which shows that affinity defaults to Any.
        /// </summary>
        [Fact]
        public void TestAffinityDefaultsToAny()
        {
            HostServices hostServices = new HostServices();
            Assert.Equal(NodeAffinity.Any, hostServices.GetNodeAffinity("project"));
        }

        /// <summary>
        /// Test which shows that setting a host object causes the affinity to become InProc.
        /// </summary>
        [Fact]
        public void TestHostObjectCausesInProcAffinity()
        {
            HostServices hostServices = new HostServices();
            TestHostObject hostObject = new TestHostObject();
            hostServices.RegisterHostObject("project", "target", "task", hostObject);
            Assert.Equal(NodeAffinity.InProc, hostServices.GetNodeAffinity("project"));
        }

        /// <summary>
        /// Test of the ability to set and change specific project affinities.
        /// </summary>
        [Fact]
        public void TestSpecificAffinityRegistration()
        {
            HostServices hostServices = new HostServices();
            hostServices.SetNodeAffinity("project", NodeAffinity.InProc);
            Assert.Equal(NodeAffinity.InProc, hostServices.GetNodeAffinity("project"));
            hostServices.SetNodeAffinity("project", NodeAffinity.OutOfProc);
            Assert.Equal(NodeAffinity.OutOfProc, hostServices.GetNodeAffinity("project"));
            hostServices.SetNodeAffinity("project", NodeAffinity.Any);
            Assert.Equal(NodeAffinity.Any, hostServices.GetNodeAffinity("project"));
        }

        /// <summary>
        /// Make sure we get the default affinity when the affinity map exists, but the specific 
        /// project we're requesting is not set. 
        /// </summary>
        [Fact]
        public void TestDefaultAffinityWhenProjectNotRegistered()
        {
            HostServices hostServices = new HostServices();
            hostServices.SetNodeAffinity("project1", NodeAffinity.InProc);
            Assert.Equal(NodeAffinity.Any, hostServices.GetNodeAffinity("project2"));
        }

        /// <summary>
        /// Test of setting the default affinity.
        /// </summary>
        [Fact]
        public void TestGeneralAffinityRegistration()
        {
            HostServices hostServices = new HostServices();

            hostServices.SetNodeAffinity(String.Empty, NodeAffinity.InProc);
            Assert.Equal(NodeAffinity.InProc, hostServices.GetNodeAffinity("project"));
            Assert.Equal(NodeAffinity.InProc, hostServices.GetNodeAffinity("project2"));

            hostServices.SetNodeAffinity(String.Empty, NodeAffinity.OutOfProc);
            Assert.Equal(NodeAffinity.OutOfProc, hostServices.GetNodeAffinity("project"));
            Assert.Equal(NodeAffinity.OutOfProc, hostServices.GetNodeAffinity("project2"));

            hostServices.SetNodeAffinity(String.Empty, NodeAffinity.Any);
            Assert.Equal(NodeAffinity.Any, hostServices.GetNodeAffinity("project"));
            Assert.Equal(NodeAffinity.Any, hostServices.GetNodeAffinity("project2"));
        }

        /// <summary>
        /// Test which ensures specific project affinities override general affinity.
        /// </summary>
        [Fact]
        public void TestOverrideGeneralAffinityRegistration()
        {
            HostServices hostServices = new HostServices();

            hostServices.SetNodeAffinity(String.Empty, NodeAffinity.InProc);
            hostServices.SetNodeAffinity("project", NodeAffinity.OutOfProc);
            Assert.Equal(NodeAffinity.OutOfProc, hostServices.GetNodeAffinity("project"));
            Assert.Equal(NodeAffinity.InProc, hostServices.GetNodeAffinity("project2"));
        }

        /// <summary>
        /// Test of clearing the affinity settings for all projects.
        /// </summary>
        [Fact]
        public void TestClearingAffinities()
        {
            HostServices hostServices = new HostServices();

            hostServices.SetNodeAffinity("project", NodeAffinity.OutOfProc);
            Assert.Equal(NodeAffinity.OutOfProc, hostServices.GetNodeAffinity("project"));
            hostServices.SetNodeAffinity(null, NodeAffinity.OutOfProc);
            Assert.Equal(NodeAffinity.Any, hostServices.GetNodeAffinity("project"));

            hostServices.SetNodeAffinity(String.Empty, NodeAffinity.OutOfProc);
            Assert.Equal(NodeAffinity.OutOfProc, hostServices.GetNodeAffinity("project"));
            hostServices.SetNodeAffinity(null, NodeAffinity.OutOfProc);
            Assert.Equal(NodeAffinity.Any, hostServices.GetNodeAffinity("project"));
        }

        /// <summary>
        /// Test which ensures that setting an OutOfProc affinity for a project with a host object throws.
        /// </summary>
        [Fact]
        public void TestContradictoryAffinityCausesException_OutOfProc()
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                HostServices hostServices = new HostServices();
                TestHostObject hostObject = new TestHostObject();
                hostServices.RegisterHostObject("project", "target", "task", hostObject);
                Assert.Equal(NodeAffinity.InProc, hostServices.GetNodeAffinity("project"));
                hostServices.SetNodeAffinity("project", NodeAffinity.OutOfProc);
            }
           );
        }
        /// <summary>
        /// Test which ensures that setting an Any affinity for a project with a host object throws.
        /// </summary>
        [Fact]
        public void TestContradictoryAffinityCausesException_Any()
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                HostServices hostServices = new HostServices();
                TestHostObject hostObject = new TestHostObject();
                hostServices.RegisterHostObject("project", "target", "task", hostObject);
                Assert.Equal(NodeAffinity.InProc, hostServices.GetNodeAffinity("project"));
                hostServices.SetNodeAffinity("project", NodeAffinity.Any);
            }
           );
        }

#if FEATURE_COM_INTEROP
        /// <summary>
        /// Test which ensures that setting an Any affinity for a project with a remote host object does not throws.
        /// </summary>
        [Fact]
        [SkipOnTargetFramework(TargetFrameworkMonikers.Mono, "disable com tests on mono")]
        public void TestNoContradictoryRemoteHostObjectAffinity()
        {
            HostServices hostServices = new HostServices();
            hostServices.RegisterHostObject("project", "target", "task", "moniker");
            hostServices.SetNodeAffinity("project", NodeAffinity.Any);
        }
#endif

        /// <summary>
        /// Test which ensures that setting the InProc affinity for a project with a host object is allowed.
        /// </summary>
        [Fact]
        public void TestNonContradictoryAffinityAllowed()
        {
            HostServices hostServices = new HostServices();
            TestHostObject hostObject = new TestHostObject();
            hostServices.RegisterHostObject("project", "target", "task", hostObject);
            Assert.Equal(NodeAffinity.InProc, hostServices.GetNodeAffinity("project"));
            hostServices.SetNodeAffinity("project", NodeAffinity.InProc);
            Assert.Equal(NodeAffinity.InProc, hostServices.GetNodeAffinity("project"));
        }

        /// <summary>
        /// Test which ensures that setting a host object for a project with an out-of-proc affinity throws.
        /// </summary>
        [Fact]
        public void TestContraditcoryHostObjectCausesException_OutOfProc()
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                HostServices hostServices = new HostServices();
                TestHostObject hostObject = new TestHostObject();
                hostServices.SetNodeAffinity("project", NodeAffinity.OutOfProc);
                hostServices.RegisterHostObject("project", "target", "task", hostObject);
            }
           );
        }
        /// <summary>
        /// Test which ensures the host object can be set for a project which has the Any affinity specifically set.
        /// </summary>
        [Fact]
        public void TestNonContraditcoryHostObjectAllowed_Any()
        {
            HostServices hostServices = new HostServices();
            TestHostObject hostObject = new TestHostObject();
            hostServices.SetNodeAffinity("project", NodeAffinity.Any);
            hostServices.RegisterHostObject("project", "target", "task", hostObject);
            Assert.Equal(NodeAffinity.InProc, hostServices.GetNodeAffinity("project"));
        }

#if FEATURE_COM_INTEROP
        /// <summary>
        /// Test which ensures the remote host object cannot affect a project which has the Any affinity specifically set.
        /// </summary>
        [Fact]
        [SkipOnTargetFramework(TargetFrameworkMonikers.Mono, "disable com tests on mono")]
        public void TestRegisterRemoteHostObjectNoAffect_Any2()
        {
            HostServices hostServices = new HostServices();
            hostServices.SetNodeAffinity("project", NodeAffinity.Any);
            hostServices.RegisterHostObject("project", "target", "task", "moniker");
            hostServices.GetNodeAffinity("project").ShouldBe(NodeAffinity.Any);
        }
#endif

        /// <summary>
        /// Test which ensures the host object can be set for a project which has an out-of-proc affinity only because that affinity
        /// is implied by being set generally for all project, not for that specific project.
        /// </summary>
        [Fact]
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
        [Fact]
        public void TestNonContraditcoryHostObjectAllowed_InProc()
        {
            HostServices hostServices = new HostServices();
            TestHostObject hostObject = new TestHostObject();
            hostServices.SetNodeAffinity("project", NodeAffinity.InProc);
            hostServices.RegisterHostObject("project", "target", "task", hostObject);
        }

#if FEATURE_COM_INTEROP
        /// <summary>
        /// Test which ensures the affinity for a project can be changed once the in process host object is registered
        /// </summary>
        [Fact]
        [SkipOnTargetFramework(TargetFrameworkMonikers.Mono, "disable com tests on mono")]
        public void TestAffinityChangeAfterRegisterInprocessHostObject()
        {
            HostServices hostServices = new HostServices();
            hostServices.RegisterHostObject("project", "target", "task", "moniker");
            hostServices.GetNodeAffinity("project").ShouldBe(NodeAffinity.Any);
            TestHostObject hostObject = new TestHostObject();
            hostServices.RegisterHostObject("project", "target", "task", hostObject);
            hostServices.GetNodeAffinity("project").ShouldBe(NodeAffinity.InProc);
        }
#endif

        /// <summary>
        /// Test which ensures the affinity for a project can be changed once the host object is cleared.
        /// </summary>
        [Fact]
        public void TestAffinityChangeAfterClearingHostObject()
        {
            HostServices hostServices = new HostServices();
            TestHostObject hostObject = new TestHostObject();
            hostServices.RegisterHostObject("project", "target", "task", hostObject);
            Assert.Equal(NodeAffinity.InProc, hostServices.GetNodeAffinity("project"));
            hostServices.RegisterHostObject("project", "target", "task", hostObject: null);
            Assert.Equal(NodeAffinity.Any, hostServices.GetNodeAffinity("project"));
            hostServices.SetNodeAffinity("project", NodeAffinity.OutOfProc);
            Assert.Equal(NodeAffinity.OutOfProc, hostServices.GetNodeAffinity("project"));
        }

        /// <summary>
        /// Test which ensures that setting then clearing the host object restores a previously specifically set non-conflicting affinity.
        /// </summary>
        [Fact]
        public void TestUnregisteringNonConflictingHostObjectRestoresOriginalAffinity()
        {
            HostServices hostServices = new HostServices();
            TestHostObject hostObject = new TestHostObject();
            hostServices.SetNodeAffinity(String.Empty, NodeAffinity.OutOfProc);
            hostServices.SetNodeAffinity("project", NodeAffinity.Any);
            Assert.Equal(NodeAffinity.OutOfProc, hostServices.GetNodeAffinity("project2"));
            Assert.Equal(NodeAffinity.Any, hostServices.GetNodeAffinity("project"));

            hostServices.RegisterHostObject("project", "target", "task", hostObject);
            Assert.Equal(NodeAffinity.InProc, hostServices.GetNodeAffinity("project"));
            hostServices.RegisterHostObject("project", "target", "task", hostObject: null);
            Assert.Equal(NodeAffinity.Any, hostServices.GetNodeAffinity("project"));
            Assert.Equal(NodeAffinity.OutOfProc, hostServices.GetNodeAffinity("project2"));
        }

        /// <summary>
        /// Tests that creating a BuildRequestData with a non-conflicting HostServices and ProjectInstance works.
        /// </summary>
        [Fact]
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
        [Fact]
        public void UnloadedProjectDiscardsHostServicesAllProjects()
        {
            HostServices hostServices = new HostServices();
            TestHostObject th = new TestHostObject();
            ProjectCollection.GlobalProjectCollection.HostServices = hostServices;
            Project project = LoadDummyProject("foo.proj");

            hostServices.RegisterHostObject(project.FullPath, "test", "Message", th);

            ProjectCollection.GlobalProjectCollection.UnloadAllProjects();

            Assert.False(hostServices.HasInProcessHostObject(project.FullPath));
        }

        /// <summary>
        /// Tests that unloading the last project from the project collection
        /// discards the host services for that project
        /// </summary>
        [Fact]
        public void UnloadedProjectDiscardsHostServices()
        {
            HostServices hostServices = new HostServices();
            TestHostObject th = new TestHostObject();
            ProjectCollection.GlobalProjectCollection.HostServices = hostServices;
            Project project1 = LoadDummyProject("foo.proj");
            Project project2 = LoadDummyProject("foo.proj");

            hostServices.RegisterHostObject(project1.FullPath, "test", "Message", th);

            ProjectCollection.GlobalProjectCollection.UnloadProject(project1);

            Assert.True(hostServices.HasInProcessHostObject(project2.FullPath));

            ProjectCollection.GlobalProjectCollection.UnloadProject(project2);

            Assert.False(hostServices.HasInProcessHostObject(project2.FullPath));
        }

#if FEATURE_COM_INTEROP
        /// <summary>
        /// Tests that register overrides existing reigsted remote host object.
        /// </summary>
        [Fact]
        [SkipOnTargetFramework(TargetFrameworkMonikers.Mono, "disable com tests on mono")]
        public void TestRegisterOverrideExistingRegisted()
        {
            var hostServices = new HostServices();
            var rot = new MockRunningObjectTable();
            hostServices.SetTestRunningObjectTable(rot);

            var moniker = Guid.NewGuid().ToString();
            var remoteHost = new MockRemoteHostObject(1);
            rot.Register(moniker, remoteHost);
            var newMoniker = Guid.NewGuid().ToString();
            var newRemoteHost = new MockRemoteHostObject(2);
            rot.Register(newMoniker, newRemoteHost);
            hostServices.RegisterHostObject(
                    "WithOutOfProc.targets",
                    "DisplayMessages",
                    "ATask",
                    remoteHost);

            hostServices.RegisterHostObject("project", "test", "Message", moniker);
            hostServices.RegisterHostObject("project", "test", "Message", newMoniker);
            var resultObject = (ITestRemoteHostObject)hostServices.GetHostObject("project", "test", "Message");

            resultObject.GetState().ShouldBe(2);
        }
#endif

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

            Project project = new Project(new XmlTextReader(new StringReader(contents)), new Dictionary<string, string>(), ObjectModelHelpers.MSBuildDefaultToolsVersion);
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

            Project project =
                ProjectCollection.GlobalProjectCollection.LoadProject(
                    new XmlTextReader(new StringReader(contents)),
                    globals,
                    ObjectModelHelpers.MSBuildDefaultToolsVersion);
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
