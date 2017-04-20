// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Unit tests for the BuildManager object.</summary>
//-----------------------------------------------------------------------

using System;
using System.CodeDom.Compiler;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml;

using Microsoft.Build.BackEnd;
using Microsoft.Build.Collections;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging;
using Microsoft.Build.Shared;
using Microsoft.Build.Utilities;

using Xunit;

namespace Microsoft.Build.UnitTests.BackEnd
{
    /// <summary>
    /// The test fixture for the BuildManager
    /// </summary>
    public class BuildManager_Tests : IDisposable
    {
        /// <summary>
        /// The mock logger for testing.
        /// </summary>
        private MockLogger _logger;

        /// <summary>
        /// The standard build manager for each test.
        /// </summary>
        private BuildManager _buildManager;

        /// <summary>
        /// The build parameters.
        /// </summary>
        private BuildParameters _parameters;

        /// <summary>
        /// The project collection used.
        /// </summary>
        private ProjectCollection _projectCollection;

        /// <summary>
        /// SetUp
        /// </summary>
        public BuildManager_Tests()
        {
            // Ensure that any previous tests which may have been using the default BuildManager do not conflict with us.
            BuildManager.DefaultBuildManager.Dispose();

            _logger = new MockLogger();
            _parameters = new BuildParameters
            {
                ShutdownInProcNodeOnBuildFinish = true,
                Loggers = new ILogger[] { _logger },
                EnableNodeReuse = false
            };
            _buildManager = new BuildManager();
            _projectCollection = new ProjectCollection();
            Environment.SetEnvironmentVariable("MSBUILDINPROCENVCHECK", "1");
        }

        /// <summary>
        /// TearDown
        /// </summary>
        public void Dispose()
        {
            Environment.SetEnvironmentVariable("MSBUILDINPROCENVCHECK", null);
            if (_buildManager != null)
            {
                _buildManager.Dispose();
                _buildManager = null;
            }
        }

        /// <summary>
        /// Check that we behave reasonably when passed a null ProjectCollection
        /// </summary>
        [Fact]
        public void BuildParametersWithNullCollection()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                BuildParameters parameters = new BuildParameters(null);
            }
           );
        }
        /// <summary>
        /// A simple successful build.
        /// </summary>
        [Fact]
        [Trait("Category", "netcore-osx-failing")]
        [Trait("Category", "netcore-linux-failing")]
        public void SimpleBuild()
        {
            string contents = ObjectModelHelpers.CleanupFileContents(@"
<Project xmlns='msbuildnamespace' ToolsVersion='msbuilddefaulttoolsversion'>
<PropertyGroup>
       <InitialProperty1>InitialProperty1</InitialProperty1>
       <InitialProperty2>InitialProperty2</InitialProperty2>
       <InitialProperty3>InitialProperty3</InitialProperty3>
</PropertyGroup>
 <Target Name='test'>
	<Message Text='[success]'/>
 </Target>
</Project>
");
            BuildRequestData data = GetBuildRequestData(contents);
            BuildResult result = _buildManager.Build(_parameters, data);
            Assert.Equal(BuildResultCode.Success, result.OverallResult);
            _logger.AssertLogContains("[success]");
            Assert.Equal(1, _logger.ProjectStartedEvents.Count);

            ProjectStartedEventArgs projectStartedEvent = _logger.ProjectStartedEvents[0];
            Dictionary<string, string> properties = ExtractProjectStartedPropertyList(projectStartedEvent.Properties);

            string propertyValue = null;
            Assert.True(properties.TryGetValue("InitialProperty1", out propertyValue));
            Assert.True(String.Equals(propertyValue, "InitialProperty1", StringComparison.OrdinalIgnoreCase));

            propertyValue = null;
            Assert.True(properties.TryGetValue("InitialProperty2", out propertyValue));
            Assert.True(String.Equals(propertyValue, "InitialProperty2", StringComparison.OrdinalIgnoreCase));

            propertyValue = null;
            Assert.True(properties.TryGetValue("InitialProperty3", out propertyValue));
            Assert.True(String.Equals(propertyValue, "InitialProperty3", StringComparison.OrdinalIgnoreCase));
        }

#if FEATURE_CODETASKFACTORY
        /// <summary>
        /// Verify that the environment between two msbuild calls to the same project are stored
        /// so that on the next call we get access to them
        /// </summary>
        [Fact]
        public void VerifyEnvironmentSavedBetweenCalls()
        {
            string contents1 = ObjectModelHelpers.CleanupFileContents(@"
<Project xmlns='msbuildnamespace' ToolsVersion='msbuilddefaulttoolsversion'>
 <UsingTask TaskName='SetEnvv' TaskFactory='CodeTaskFactory' AssemblyFile='$(MSBuildToolsPath)\Microsoft.Build.Tasks.Core.dll' >
                            <Task>
                                <Code Language='cs'>
                                    System.Environment.SetEnvironmentVariable(""MOO"", ""When the dawn comes, tonight will be a memory too"");
                                </Code>
                           </Task>
</UsingTask>
                        <Target Name='SetEnv'>
                            <SetEnvv/>
                        </Target>        
                        <Target Name='Message1'>
                            <Exec Command='echo What does a cat say : " + (NativeMethodsShared.IsWindows ? "%MOO%" : "$MOO") + @"' />
                        </Target>       
</Project>
");

            Project project = new Project(XmlReader.Create(new StringReader(contents1)), (IDictionary<string, string>)null, null, _projectCollection);
            project.FullPath = FileUtilities.GetTemporaryFile();
            project.Save();
            try
            {
                string contents2 = ObjectModelHelpers.CleanupFileContents(@"
<Project xmlns='msbuildnamespace' ToolsVersion='msbuilddefaulttoolsversion'>
        <Target Name='Build' >
         <MSBuild Targets='SetEnv' Projects='" + project.FullPath + "'/>" +
             "<MSBuild Targets='Message1' Projects='" + project.FullPath + "'/>" +
            @"</Target>
</Project>
");

                ProjectInstance instance = CreateProjectInstance(contents2, null, _projectCollection, true);
                BuildRequestData data = new BuildRequestData(instance, new[] { "Build" }, _projectCollection.HostServices);

                BuildResult result = _buildManager.Build(_parameters, data);
                Assert.Equal(BuildResultCode.Success, result.OverallResult);
                _logger.AssertLogContains("What does a cat say : When the dawn comes, tonight will be a memory too");
            }
            finally
            {
                if (File.Exists(project.FullPath))
                {
                    File.Delete(project.FullPath);
                }
            }
        }
#endif

        /// <summary>
        /// Verify if idle nodes are shutdown when BuildManager.ShutdownAllNodes is evoked.
        /// The final number of nodes has to be less or equal the number of nodes already in 
        /// the system before this method was called.
        /// </summary>
#if RUNTIME_TYPE_NETCORE
        [Fact(Skip = "https://github.com/Microsoft/msbuild/issues/933")]
#elif MONO
        [Fact(Skip = "https://github.com/Microsoft/msbuild/issues/1240")]
#else
        [Fact]
#endif
        public void ShutdownNodesAfterParallelBuild()
        {
            ProjectCollection projectCollection = new ProjectCollection();

            // Get number of MSBuild processes currently instantiated
            int numberProcsOriginally = (new List<Process>(Process.GetProcessesByName("MSBuild"))).Count;

            // Generate a theoretically unique directory to put our dummy projects in.
            string shutdownProjectDirectory = Path.Combine(Path.GetTempPath(), String.Format(CultureInfo.InvariantCulture, "VSNodeShutdown_{0}_UnitTest", Process.GetCurrentProcess().Id));

            // Create the dummy projects we'll be "building" as our excuse to connect to and shut down 
            // all the nodes. 
            ProjectInstance rootProject = GenerateDummyProjects(shutdownProjectDirectory, numberProcsOriginally + 4, projectCollection);

            // Build the projects. 
            BuildParameters buildParameters = new BuildParameters(projectCollection);

            buildParameters.OnlyLogCriticalEvents = true;
            buildParameters.MaxNodeCount = numberProcsOriginally + 4;
            buildParameters.EnableNodeReuse = true;
            buildParameters.DisableInProcNode = true;

            // Tell the build manager to not disturb process wide state
            buildParameters.SaveOperatingEnvironment = false;

            BuildRequestData requestData = new BuildRequestData(rootProject, new[] { "Build" }, null);

            // Use a separate BuildManager for the node shutdown build, so that we don't have 
            // to worry about taking dependencies on whether or not the existing ones have already 
            // disappeared. 
            BuildManager shutdownManager = new BuildManager("IdleNodeShutdown");
            shutdownManager.Build(buildParameters, requestData);

            // Number of nodes after the build has to be greater than the original number
            int numberProcsAfterBuild = (new List<Process>(Process.GetProcessesByName("MSBuild"))).Count;
            Assert.True(numberProcsOriginally < numberProcsAfterBuild);

            // Shutdown all nodes
            shutdownManager.ShutdownAllNodes();

            // Wait until all processes shut down
            Thread.Sleep(3000);

            // Number of nodes after the shutdown has to be smaller or equal the original number
            int numberProcsAfterShutdown = (new List<Process>(Process.GetProcessesByName("MSBuild"))).Count;
            Assert.True(numberProcsAfterShutdown <= numberProcsOriginally);

            // Delete directory with the dummy project
            if (Directory.Exists(shutdownProjectDirectory))
            {
                FileUtilities.DeleteWithoutTrailingBackslash(shutdownProjectDirectory, true /* recursive delete */);
            }
        }

        /// <summary>
        /// A simple successful build, out of process only.
        /// </summary>
#if RUNTIME_TYPE_NETCORE
        [Fact(Skip = "https://github.com/Microsoft/msbuild/issues/933")]
#elif MONO
        [Fact(Skip = "https://github.com/Microsoft/msbuild/issues/1240")]
#else
        [Fact]
#endif
        public void SimpleBuildOutOfProcess()
        {
            RunOutOfProcBuild(_ => Environment.SetEnvironmentVariable("MSBUILDNOINPROCNODE", "1"));
        }

        /// <summary>
        /// A simple successful build, out of process only. Triggered by setting build parameters' DisableInProcNode to true.
        /// </summary>
#if RUNTIME_TYPE_NETCORE
        [Fact(Skip = "https://github.com/Microsoft/msbuild/issues/933")]
#elif MONO
        [Fact(Skip = "https://github.com/Microsoft/msbuild/issues/1240")]
#else
        [Fact]
#endif
        public void DisableInProcNode()
        {
            RunOutOfProcBuild(buildParameters => buildParameters.DisableInProcNode = true);
        }

        /// <summary>
        /// Runs a build and verifies it happens out of proc by checking the process ID.
        /// </summary>
        /// <param name="buildParametersModifier">Runs a test out of proc.</param>
        public void RunOutOfProcBuild(Action<BuildParameters> buildParametersModifier)
        {
            const string Contents = @"
<Project xmlns='msbuildnamespace' ToolsVersion='msbuilddefaulttoolsversion'>
<ItemGroup>
       <InitialProperty Include='InitialProperty2'/>
       <InitialProperty Include='InitialProperty3'/>
</ItemGroup>
 <Target Name='test' Returns='@(InitialProperty)'>
    <ItemGroup>
       <InitialProperty Include='$([System.Diagnostics.Process]::GetCurrentProcess().Id)'/>
    </ItemGroup>
	<Message Text='[success]'/>
 </Target>
</Project>
";

            string originalMsBuildNoInProcNode = Environment.GetEnvironmentVariable("MSBUILDNOINPROCNODE");
            string originalMsBuildEnableAllPropertyFunctions = Environment.GetEnvironmentVariable("MSBUILDENABLEALLPROPERTYFUNCTIONS");
            string projectFullPath = null;
            try
            {
                // Need to set this env variable to enable Process.GetCurrentProcess().Id in the project file.
                Environment.SetEnvironmentVariable("MSBUILDENABLEALLPROPERTYFUNCTIONS", "1");

                Project project = CreateProject(ObjectModelHelpers.CleanupFileContents(Contents), ObjectModelHelpers.MSBuildDefaultToolsVersion, _projectCollection, false);
                projectFullPath = project.FullPath;

                BuildRequestData data = new BuildRequestData(project.CreateProjectInstance(), new string[0], _projectCollection.HostServices);
                BuildParameters customparameters = new BuildParameters { EnableNodeReuse = false };
                buildParametersModifier(customparameters);

                BuildResult result = _buildManager.Build(customparameters, data);
                TargetResult targetresult = result.ResultsByTarget["test"];
                ITaskItem[] item = targetresult.Items;

                Assert.Equal(BuildResultCode.Success, result.OverallResult);
                Assert.Equal(3, item.Length);
                int processId;
                Assert.True(int.TryParse(item[2].ItemSpec, out processId), string.Format("Process ID passed from the 'test' target is not a valid integer (actual is '{0}')", item[2].ItemSpec));
                Assert.NotEqual(Process.GetCurrentProcess().Id, processId); // "Build is expected to be out-of-proc. In fact it was in-proc."
            }
            finally
            {
                if (projectFullPath != null && File.Exists(projectFullPath))
                {
                    File.Delete(projectFullPath);
                }

                Environment.SetEnvironmentVariable("MSBUILDNOINPROCNODE", originalMsBuildNoInProcNode);
                Environment.SetEnvironmentVariable("MSBUILDENABLEALLPROPERTYFUNCTIONS", originalMsBuildEnableAllPropertyFunctions);
            }
        }

        /// <summary>
        /// Make sure when we are doing an in-process build that even if the environment variable MSBUILDFORWARDPROPERTIESFROMCHILD is set that we still 
        /// get all of the initial properties.
        /// </summary>
        [Fact]
        [Trait("Category", "netcore-osx-failing")]
        [Trait("Category", "netcore-linux-failing")]
        public void InProcForwardPropertiesFromChild()
        {
            string contents = ObjectModelHelpers.CleanupFileContents(@"
<Project xmlns='msbuildnamespace' ToolsVersion='msbuilddefaulttoolsversion'>
 <PropertyGroup>
       <InitialProperty1>InitialProperty1</InitialProperty1>
       <InitialProperty2>InitialProperty2</InitialProperty2>
       <InitialProperty3>InitialProperty3</InitialProperty3>
</PropertyGroup>
<Target Name='test'>
	<Message Text='[success]'/>
 </Target>
</Project>
");
            string originalEnvironmentValue = Environment.GetEnvironmentVariable("MSBuildForwardPropertiesFromChild");

            try
            {
                Environment.SetEnvironmentVariable("MSBuildForwardPropertiesFromChild", "InitialProperty2;IAMNOTREAL");
                BuildRequestData data = GetBuildRequestData(contents);
                BuildResult result = _buildManager.Build(_parameters, data);
                Assert.Equal(BuildResultCode.Success, result.OverallResult);
                _logger.AssertLogContains("[success]");
                Assert.Equal(1, _logger.ProjectStartedEvents.Count);

                ProjectStartedEventArgs projectStartedEvent = _logger.ProjectStartedEvents[0];
                Dictionary<string, string> properties = ExtractProjectStartedPropertyList(projectStartedEvent.Properties);

                string propertyValue = null;
                Assert.True(properties.TryGetValue("InitialProperty1", out propertyValue));
                Assert.True(String.Equals(propertyValue, "InitialProperty1", StringComparison.OrdinalIgnoreCase));

                propertyValue = null;
                Assert.True(properties.TryGetValue("InitialProperty2", out propertyValue));
                Assert.True(String.Equals(propertyValue, "InitialProperty2", StringComparison.OrdinalIgnoreCase));

                propertyValue = null;
                Assert.True(properties.TryGetValue("InitialProperty3", out propertyValue));
                Assert.True(String.Equals(propertyValue, "InitialProperty3", StringComparison.OrdinalIgnoreCase));
            }
            finally
            {
                Environment.SetEnvironmentVariable("MSBuildForwardPropertiesFromChild", originalEnvironmentValue);
            }
        }

        /// <summary>
        /// Make sure when we are doing an inprocess build that even if the environment variable MsBuildForwardAllPropertiesFromChild is set that we still
        /// get all of the initial properties.
        /// </summary>
        [Fact]
        [Trait("Category", "netcore-osx-failing")]
        [Trait("Category", "netcore-linux-failing")]
        public void InProcMsBuildForwardAllPropertiesFromChild()
        {
            string contents = ObjectModelHelpers.CleanupFileContents(@"
<Project xmlns='msbuildnamespace' ToolsVersion='msbuilddefaulttoolsversion'>
 <PropertyGroup>
       <InitialProperty1>InitialProperty1</InitialProperty1>
       <InitialProperty2>InitialProperty2</InitialProperty2>
       <InitialProperty3>InitialProperty3</InitialProperty3>
</PropertyGroup>
<Target Name='test'>
	<Message Text='[success]'/>
 </Target>
</Project>
");
            string originalEnvironmentValue = Environment.GetEnvironmentVariable("MsBuildForwardAllPropertiesFromChild");

            try
            {
                Environment.SetEnvironmentVariable("MsBuildForwardAllPropertiesFromChild", "InitialProperty2;IAMNOTREAL");
                BuildRequestData data = GetBuildRequestData(contents);
                BuildResult result = _buildManager.Build(_parameters, data);
                Assert.Equal(BuildResultCode.Success, result.OverallResult);
                _logger.AssertLogContains("[success]");
                Assert.Equal(1, _logger.ProjectStartedEvents.Count);

                ProjectStartedEventArgs projectStartedEvent = _logger.ProjectStartedEvents[0];
                Dictionary<string, string> properties = ExtractProjectStartedPropertyList(projectStartedEvent.Properties);

                string propertyValue = null;
                Assert.True(properties.TryGetValue("InitialProperty1", out propertyValue));
                Assert.True(String.Equals(propertyValue, "InitialProperty1", StringComparison.OrdinalIgnoreCase));

                propertyValue = null;
                Assert.True(properties.TryGetValue("InitialProperty2", out propertyValue));
                Assert.True(String.Equals(propertyValue, "InitialProperty2", StringComparison.OrdinalIgnoreCase));

                propertyValue = null;
                Assert.True(properties.TryGetValue("InitialProperty3", out propertyValue));
                Assert.True(String.Equals(propertyValue, "InitialProperty3", StringComparison.OrdinalIgnoreCase));
            }
            finally
            {
                Environment.SetEnvironmentVariable("MsBuildForwardAllPropertiesFromChild", originalEnvironmentValue);
            }
        }

        /// <summary>
        /// Make sure when we launch a child node and set MsBuildForwardAllPropertiesFromChild that we get all of our properties. This needs to happen 
        /// even if the msbuildforwardpropertiesfromchild is set to something.
        /// </summary>
#if RUNTIME_TYPE_NETCORE
        [Fact(Skip = "https://github.com/Microsoft/msbuild/issues/933")]
#else
        [Fact]
#endif
        public void MsBuildForwardAllPropertiesFromChildLaunchChildNode()
        {
            string contents = ObjectModelHelpers.CleanupFileContents(@"
<Project xmlns='msbuildnamespace' ToolsVersion='msbuilddefaulttoolsversion'>
 <PropertyGroup>
       <InitialProperty1>InitialProperty1</InitialProperty1>
       <InitialProperty2>InitialProperty2</InitialProperty2>
       <InitialProperty3>InitialProperty3</InitialProperty3>
</PropertyGroup>
<Target Name='test'>
	<Message Text='[success]'/>
 </Target>
</Project>
");
            string originalEnvironmentValue = Environment.GetEnvironmentVariable("MsBuildForwardAllPropertiesFromChild");
            string originalForwardPropertiesFromChild = Environment.GetEnvironmentVariable("MsBuildForwardPropertiesFromChild");
            string originalMsBuildNoInProcNode = Environment.GetEnvironmentVariable("MSBUILDNOINPROCNODE");
            string tempFile = null;
            try
            {
                Environment.SetEnvironmentVariable("MsBuildForwardAllPropertiesFromChild", "InitialProperty2;IAMNOTREAL");
                Environment.SetEnvironmentVariable("MsBuildForwardPropertiesFromChild", "Something");

                Project project = CreateProject(contents, null, _projectCollection, false);
                tempFile = project.FullPath;
                BuildRequestData data = new BuildRequestData(tempFile, new Dictionary<string, string>(), ObjectModelHelpers.MSBuildDefaultToolsVersion, new string[] { }, null);
                BuildResult result = _buildManager.Build(_parameters, data);
                Assert.Equal(BuildResultCode.Success, result.OverallResult);
                _logger.AssertLogContains("[success]");
                Assert.Equal(1, _logger.ProjectStartedEvents.Count);

                ProjectStartedEventArgs projectStartedEvent = _logger.ProjectStartedEvents[0];
                Dictionary<string, string> properties = ExtractProjectStartedPropertyList(projectStartedEvent.Properties);

                string propertyValue = null;
                Assert.True(properties.TryGetValue("InitialProperty1", out propertyValue));
                Assert.True(String.Equals(propertyValue, "InitialProperty1", StringComparison.OrdinalIgnoreCase));

                propertyValue = null;
                Assert.True(properties.TryGetValue("InitialProperty2", out propertyValue));
                Assert.True(String.Equals(propertyValue, "InitialProperty2", StringComparison.OrdinalIgnoreCase));

                propertyValue = null;
                Assert.True(properties.TryGetValue("InitialProperty3", out propertyValue));
                Assert.True(String.Equals(propertyValue, "InitialProperty3", StringComparison.OrdinalIgnoreCase));
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }

                Environment.SetEnvironmentVariable("MsBuildForwardAllPropertiesFromChild", originalEnvironmentValue);
                Environment.SetEnvironmentVariable("MsBuildForwardPropertiesFromChild", originalForwardPropertiesFromChild);
                Environment.SetEnvironmentVariable("MSBUILDNOINPROCNODE", originalMsBuildNoInProcNode);
            }
        }

        /// <summary>
        /// Make sure when if the environment variable MsBuildForwardPropertiesFromChild is set to a value and
        /// we launch a child node that we get only that value.
        /// </summary>
#if RUNTIME_TYPE_NETCORE
        [Fact(Skip = "https://github.com/Microsoft/msbuild/issues/933")]
#elif MONO
        [Fact(Skip = "https://github.com/Microsoft/msbuild/issues/1240")]
#else
        [Fact]
#endif
        public void OutOfProcNodeForwardCertainproperties()
        {
            string contents = ObjectModelHelpers.CleanupFileContents(@"
<Project xmlns='msbuildnamespace' ToolsVersion='msbuilddefaulttoolsversion'>
 <PropertyGroup>
       <InitialProperty1>InitialProperty1</InitialProperty1>
       <InitialProperty2>InitialProperty2</InitialProperty2>
       <InitialProperty3>InitialProperty3</InitialProperty3>
</PropertyGroup>
<Target Name='test'>
	<Message Text='[success]'/>
 </Target>
</Project>
");
            string originalEnvironmentValue = Environment.GetEnvironmentVariable("MsBuildForwardPropertiesFromChild");
            string originalMsBuildNoInProcNode = Environment.GetEnvironmentVariable("MSBUILDNOINPROCNODE");
            string tempFile = null;
            try
            {
                Environment.SetEnvironmentVariable("MsBuildForwardPropertiesFromChild", "InitialProperty3;IAMNOTREAL");
                Environment.SetEnvironmentVariable("MSBUILDNOINPROCNODE", "1");
                Project project = CreateProject(contents, null, _projectCollection, false);
                tempFile = project.FullPath;
                BuildRequestData data = new BuildRequestData(tempFile, new Dictionary<string, string>(), ObjectModelHelpers.MSBuildDefaultToolsVersion, new string[] { }, null);

                BuildResult result = _buildManager.Build(_parameters, data);
                Assert.Equal(BuildResultCode.Success, result.OverallResult);
                _logger.AssertLogContains("[success]");
                Assert.Equal(1, _logger.ProjectStartedEvents.Count);

                ProjectStartedEventArgs projectStartedEvent = _logger.ProjectStartedEvents[0];
                Dictionary<string, string> properties = ExtractProjectStartedPropertyList(projectStartedEvent.Properties);

                Assert.Equal(1, properties.Count);

                string propertyValue = null;
                Assert.True(properties.TryGetValue("InitialProperty3", out propertyValue));
                Assert.True(String.Equals(propertyValue, "InitialProperty3", StringComparison.OrdinalIgnoreCase));
            }
            finally
            {
                if (!string.IsNullOrEmpty(tempFile) && File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }

                Environment.SetEnvironmentVariable("MsBuildForwardPropertiesFromChild", originalEnvironmentValue);
                Environment.SetEnvironmentVariable("MSBUILDNOINPROCNODE", originalMsBuildNoInProcNode);
            }
        }

        /// <summary>
        /// Make sure when if the environment variable MsBuildForwardPropertiesFromChild is set to a value and
        /// we launch a child node that we get only that value. Also, make sure that when a project is pulled from the results cache
        /// and we have a list of properties to serialize that we do not crash. This is to prevent a regression of 826594
        /// </summary>
#if RUNTIME_TYPE_NETCORE
        [Fact(Skip = "https://github.com/Microsoft/msbuild/issues/933")]
#elif MONO
        [Fact(Skip = "https://github.com/Microsoft/msbuild/issues/1240")]
#else
        [Fact]
#endif
        public void OutOfProcNodeForwardCertainpropertiesAlsoGetResultsFromCache()
        {
            string contents = ObjectModelHelpers.CleanupFileContents(@"
<Project ToolsVersion='msbuilddefaulttoolsversion' DefaultTargets='Build' xmlns='msbuildnamespace'>
  <Target Name='Build'>
       <MsBuild Projects='OutOfProcNodeForwardCertainpropertiesAlsoGetResultsFromCache.proj' Targets='BuildA'/>
       <MsBuild Projects='OutOfProcNodeForwardCertainpropertiesAlsoGetResultsFromCache.proj' Targets='BuildA'/>
  </Target>
</Project>
");

            string projectContents = ObjectModelHelpers.CleanupFileContents(@"
<Project ToolsVersion='msbuilddefaulttoolsversion' DefaultTargets='Build' xmlns='msbuildnamespace'>
 <PropertyGroup>
       <InitialProperty1>InitialProperty1</InitialProperty1>
       <InitialProperty2>InitialProperty2</InitialProperty2>
       <InitialProperty3>InitialProperty3</InitialProperty3>
</PropertyGroup>
<Target Name='BuildA'>
       <Message Text='BuildA' Importance='High'/>
       <Message Text='[success]'/>
  </Target>
</Project>
");
            string originalEnvironmentValue = Environment.GetEnvironmentVariable("MsBuildForwardPropertiesFromChild");
            string originalMsBuildNoInProcNode = Environment.GetEnvironmentVariable("MSBUILDNOINPROCNODE");
            string tempFile = null;
            string tempProject = Path.Combine(Path.GetTempPath(), "OutOfProcNodeForwardCertainpropertiesAlsoGetResultsFromCache.proj");

            try
            {
                File.WriteAllText(tempProject, projectContents);

                Environment.SetEnvironmentVariable("MsBuildForwardPropertiesFromChild", "InitialProperty3;IAMNOTREAL");
                Environment.SetEnvironmentVariable("MSBUILDNOINPROCNODE", "1");
                Project project = CreateProject(contents, null, _projectCollection, false);
                tempFile = project.FullPath;
                BuildRequestData data = new BuildRequestData(tempFile, new Dictionary<string, string>(), ObjectModelHelpers.MSBuildDefaultToolsVersion, new string[] { }, null);

                BuildResult result = _buildManager.Build(_parameters, data);
                Assert.Equal(BuildResultCode.Success, result.OverallResult);
                _logger.AssertLogContains("[success]");
                Assert.Equal(3, _logger.ProjectStartedEvents.Count);

                ProjectStartedEventArgs projectStartedEvent = _logger.ProjectStartedEvents[1];

                // After conversion to xunit, this test sometimes fails at this assertion.
                // Related to shared state that the test touches that's getting handled
                // differently in xunit?
                Assert.NotNull(projectStartedEvent.Properties);

                Dictionary<string, string> properties = ExtractProjectStartedPropertyList(projectStartedEvent.Properties);

                Assert.NotNull(properties);
                Assert.Equal(1, properties.Count);

                string propertyValue = null;
                Assert.True(properties.TryGetValue("InitialProperty3", out propertyValue));
                Assert.True(String.Equals(propertyValue, "InitialProperty3", StringComparison.OrdinalIgnoreCase));

                projectStartedEvent = _logger.ProjectStartedEvents[2];
                Assert.Null(projectStartedEvent.Properties);
            }
            finally
            {
                if (!string.IsNullOrEmpty(tempProject) && File.Exists(tempProject))
                {
                    File.Delete(tempProject);
                }

                if (!string.IsNullOrEmpty(tempFile) && File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }

                Environment.SetEnvironmentVariable("MsBuildForwardPropertiesFromChild", originalEnvironmentValue);
                Environment.SetEnvironmentVariable("MSBUILDNOINPROCNODE", originalMsBuildNoInProcNode);
            }
        }

        /// <summary>
        /// Make sure when if the environment variable MsBuildForwardPropertiesFromChild is set to empty and
        /// we launch a child node that we get no properties
        /// </summary>
#if RUNTIME_TYPE_NETCORE
        [Fact(Skip = "https://github.com/Microsoft/msbuild/issues/933")]
#elif MONO
        [Fact(Skip = "https://github.com/Microsoft/msbuild/issues/1240")]
#else
        [Fact]
#endif
        public void ForwardNoPropertiesLaunchChildNode()
        {
            string contents = ObjectModelHelpers.CleanupFileContents(@"
<Project xmlns='msbuildnamespace' ToolsVersion='msbuilddefaulttoolsversion'>
 <PropertyGroup>
       <InitialProperty1>InitialProperty1</InitialProperty1>
       <InitialProperty2>InitialProperty2</InitialProperty2>
       <InitialProperty3>InitialProperty3</InitialProperty3>
</PropertyGroup>
<Target Name='test'>
	<Message Text='[success]'/>
 </Target>
</Project>
");
            string originalEnvironmentValue = Environment.GetEnvironmentVariable("MsBuildForwardPropertiesFromChild");
            string originalMsBuildNoInProcNode = Environment.GetEnvironmentVariable("MSBUILDNOINPROCNODE");
            string tempFile = null;
            try
            {
                Environment.SetEnvironmentVariable("MsBuildForwardPropertiesFromChild", "");
                Environment.SetEnvironmentVariable("MSBUILDNOINPROCNODE", "1");
                Project project = CreateProject(contents, null, _projectCollection, false);
                tempFile = project.FullPath;
                BuildRequestData data = new BuildRequestData(tempFile, new Dictionary<string, string>(), ObjectModelHelpers.MSBuildDefaultToolsVersion, new string[] { }, null);
                BuildResult result = _buildManager.Build(_parameters, data);
                Assert.Equal(BuildResultCode.Success, result.OverallResult);

                _logger.AssertLogContains("[success]");
                Assert.Equal(1, _logger.ProjectStartedEvents.Count);

                ProjectStartedEventArgs projectStartedEvent = _logger.ProjectStartedEvents[0];
                Dictionary<string, string> properties = ExtractProjectStartedPropertyList(projectStartedEvent.Properties);
                Assert.Null(properties);
            }
            finally
            {
                if (!string.IsNullOrEmpty(tempFile) && File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }

                Environment.SetEnvironmentVariable("MsBuildForwardPropertiesFromChild", originalEnvironmentValue);
                Environment.SetEnvironmentVariable("MSBUILDNOINPROCNODE", originalMsBuildNoInProcNode);
            }
        }

        /// <summary>
        /// We want to pass the toolsets from the parent to the child nodes so that any custom toolsets 
        /// defined on the parent are also available on the child nodes for tasks which use the global project
        /// collection
        /// </summary>
#if RUNTIME_TYPE_NETCORE
        [Fact(Skip = "https://github.com/Microsoft/msbuild/issues/933")]
#elif MONO
        [Fact(Skip = "https://github.com/Microsoft/msbuild/issues/1240")]
#else
        [Fact]
#endif
        public void VerifyCustomToolSetsPropagated()
        {
            string netFrameworkDirectory = ToolLocationHelper.GetPathToDotNetFrameworkReferenceAssemblies(TargetDotNetFrameworkVersion.Version45);

            string contents = ObjectModelHelpers.CleanupFileContents(@"
<Project xmlns='msbuildnamespace' ToolsVersion='msbuilddefaulttoolsversion'>
<UsingTask TaskName='VerifyGlobalProjectCollection' TaskFactory='CodeTaskFactory' AssemblyFile='$(MSBuildToolsPath)\Microsoft.Build.Tasks.Core.dll'>
                        <Task>
                            <Using Namespace='Microsoft.Build.Evaluation'/>
                               <Reference Include='$(MSBuildToolsPath)\Microsoft.Build.dll'/>
<Code Type='Method'>
 <![CDATA[
                    
                                public override bool Execute()
                                {
                                    bool foundToolSet = false;
                                    foreach(Toolset t in ProjectCollection.GlobalProjectCollection.Toolsets)
                                    {
                                        if(t.ToolsVersion.Equals(""CustomToolSet"", StringComparison.OrdinalIgnoreCase))
                                        {
                                            foundToolSet = true;
                                            break;
                                        }
                                     }

                                    Log.LogMessage(MessageImportance.High, ""foundToolset:"" + foundToolSet.ToString());
                                    return foundToolSet;
                                }
  ]]>
                            </Code>
                         </Task>
                         </UsingTask>
                        <Target Name='Build'>
                            <VerifyGlobalProjectCollection/>
                        </Target>
                    </Project>");

            string originalMsBuildNoInProcNode = Environment.GetEnvironmentVariable("MSBUILDNOINPROCNODE");
            string tempFile = null;
            try
            {
                Environment.SetEnvironmentVariable("MSBUILDNOINPROCNODE", "1");

                ProjectCollection projectCollection = new ProjectCollection();
                Toolset newToolSet = new Toolset("CustomToolSet", "c:\\SomePath", projectCollection, null);
                projectCollection.AddToolset(newToolSet);

                Project project = CreateProject(contents, null, projectCollection, false);
                tempFile = project.FullPath;

                BuildRequestData data = new BuildRequestData(tempFile, new Dictionary<string, string>(), ObjectModelHelpers.MSBuildDefaultToolsVersion, new string[] { }, null);

                BuildParameters customParameters = new BuildParameters(projectCollection);
                customParameters.Loggers = new ILogger[] { _logger };
                BuildResult result = _buildManager.Build(customParameters, data);
                Assert.Equal(BuildResultCode.Success, result.OverallResult);
            }
            finally
            {
                if (!string.IsNullOrEmpty(tempFile) && File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }

                Environment.SetEnvironmentVariable("MSBUILDNOINPROCNODE", originalMsBuildNoInProcNode);
            }
        }

        /// <summary>
        /// When a child node is launched by default we should not send any properties.
        /// we launch a child node that we get no properties
        /// </summary>
#if RUNTIME_TYPE_NETCORE
        [Fact(Skip = "https://github.com/Microsoft/msbuild/issues/933")]
#elif MONO
        [Fact(Skip = "https://github.com/Microsoft/msbuild/issues/1240")]
#else
        [Fact]
#endif
        public void ForwardNoPropertiesLaunchChildNodeDefault()
        {
            string contents = ObjectModelHelpers.CleanupFileContents(@"
<Project xmlns='msbuildnamespace' ToolsVersion='msbuilddefaulttoolsversion'>
 <PropertyGroup>
       <InitialProperty1>InitialProperty1</InitialProperty1>
       <InitialProperty2>InitialProperty2</InitialProperty2>
       <InitialProperty3>InitialProperty3</InitialProperty3>
</PropertyGroup>
<Target Name='test'>
	<Message Text='[success]'/>
 </Target>
</Project>
");
            string originalEnvironmentValue = Environment.GetEnvironmentVariable("MsBuildForwardPropertiesFromChild");
            string originalMsBuildNoInProcNode = Environment.GetEnvironmentVariable("MSBUILDNOINPROCNODE");
            string tempFile = null;
            try
            {
                Environment.SetEnvironmentVariable("MsBuildForwardPropertiesFromChild", null);
                Environment.SetEnvironmentVariable("MSBUILDNOINPROCNODE", "1");
                Project project = CreateProject(contents, null, _projectCollection, false);
                tempFile = project.FullPath;

                BuildRequestData data = new BuildRequestData(tempFile, new Dictionary<string, string>(), ObjectModelHelpers.MSBuildDefaultToolsVersion, new string[] { }, null);
                BuildResult result = _buildManager.Build(_parameters, data);
                Assert.Equal(BuildResultCode.Success, result.OverallResult);
                _logger.AssertLogContains("[success]");
                Assert.Equal(1, _logger.ProjectStartedEvents.Count);

                ProjectStartedEventArgs projectStartedEvent = _logger.ProjectStartedEvents[0];
                Dictionary<string, string> properties = ExtractProjectStartedPropertyList(projectStartedEvent.Properties);
                Assert.Null(properties);
            }
            finally
            {
                if (!string.IsNullOrEmpty(tempFile) && File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }

                Environment.SetEnvironmentVariable("MsBuildForwardPropertiesFromChild", originalEnvironmentValue);
                Environment.SetEnvironmentVariable("MSBUILDNOINPROCNODE", originalMsBuildNoInProcNode);
            }
        }

        /// <summary>
        /// A simple failing build.
        /// </summary>
        [Fact]
        [Trait("Category", "netcore-osx-failing")]
        [Trait("Category", "netcore-linux-failing")]
        public void SimpleBuildWithFailure()
        {
            string contents = ObjectModelHelpers.CleanupFileContents(@"
<Project xmlns='msbuildnamespace' ToolsVersion='msbuilddefaulttoolsversion'>
 <Target Name='test'>
	<Error Text='[fail]'/>
 </Target>
</Project>
");
            BuildRequestData data = GetBuildRequestData(contents);
            BuildResult result = _buildManager.Build(_parameters, data);
            Assert.Equal(BuildResultCode.Failure, result.OverallResult);
            _logger.AssertLogContains("[fail]");
        }

        /// <summary>
        /// A build with a message, error and warning, verify that 
        /// we only get errors, warnings, and project started and finished when OnlyLogCriticalEvents is true
        /// </summary>
        [Fact]
        [Trait("Category", "netcore-osx-failing")]
        [Trait("Category", "netcore-linux-failing")]
        public void SimpleBuildWithFailureAndWarningOnlyLogCriticalEventsTrue()
        {
            string contents = ObjectModelHelpers.CleanupFileContents(@"
              <Project xmlns='msbuildnamespace' ToolsVersion='msbuilddefaulttoolsversion'>
                 <Target Name='test'>
                     <Message Text='[Message]' Importance='high'/>
                     <Warning Text='[warn]'/>	
                     <Error Text='[fail]'/>
                </Target>
              </Project>
            ");

            BuildRequestData data = GetBuildRequestData(contents);
            _parameters.OnlyLogCriticalEvents = true;
            BuildResult result = _buildManager.Build(_parameters, data);
            Assert.Equal(BuildResultCode.Failure, result.OverallResult);
            _logger.AssertLogContains("[fail]");
            _logger.AssertLogContains("[warn]");
            _logger.AssertLogDoesntContain("[message]");
            Assert.Equal(1, _logger.BuildStartedEvents.Count);
            Assert.Equal(1, _logger.BuildFinishedEvents.Count);
            Assert.Equal(1, _logger.ProjectStartedEvents.Count);
            Assert.Equal(1, _logger.ProjectFinishedEvents.Count);
            Assert.Equal(0, _logger.TargetStartedEvents.Count);
            Assert.Equal(0, _logger.TargetFinishedEvents.Count);
            Assert.Equal(0, _logger.TaskStartedEvents.Count);
            Assert.Equal(0, _logger.TaskFinishedEvents.Count);
        }

        /// <summary>
        /// A build with a message, error and warning, verify that 
        /// we only get errors, warnings, messages, task and target messages OnlyLogCriticalEvents is false
        /// </summary>
        [Fact]
        [Trait("Category", "netcore-osx-failing")]
        [Trait("Category", "netcore-linux-failing")]
        public void SimpleBuildWithFailureAndWarningOnlyLogCriticalEventsFalse()
        {
            string contents = ObjectModelHelpers.CleanupFileContents(@"
              <Project xmlns='msbuildnamespace' ToolsVersion='msbuilddefaulttoolsversion'>
                 <Target Name='test'>
                     <Message Text='[message]' Importance='high'/>
                     <Warning Text='[warn]'/>	
                     <Error Text='[fail]'/>
                </Target>
              </Project>
            ");

            BuildRequestData data = GetBuildRequestData(contents);
            _parameters.OnlyLogCriticalEvents = false;
            BuildResult result = _buildManager.Build(_parameters, data);
            Assert.Equal(BuildResultCode.Failure, result.OverallResult);
            _logger.AssertLogContains("[fail]");
            _logger.AssertLogContains("[warn]");
            _logger.AssertLogContains("[message]");
            Assert.Equal(1, _logger.BuildStartedEvents.Count);
            Assert.Equal(1, _logger.BuildFinishedEvents.Count);
            Assert.Equal(1, _logger.ProjectStartedEvents.Count);
            Assert.Equal(1, _logger.ProjectFinishedEvents.Count);
            Assert.Equal(1, _logger.TargetStartedEvents.Count);
            Assert.Equal(1, _logger.TargetFinishedEvents.Count);
            Assert.Equal(3, _logger.TaskStartedEvents.Count);
            Assert.Equal(3, _logger.TaskFinishedEvents.Count);
        }

        /// <summary>
        /// Submitting a synchronous build request before calling BeginBuild yields an InvalidOperationException.
        /// </summary>
        [Fact]
        public void BuildRequestWithoutBegin()
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                BuildRequestData data = new BuildRequestData("foo", new Dictionary<string, string>(), "2.0", new string[0], null);
                BuildResult result = _buildManager.BuildRequest(data);
            }
           );
        }
        /// <summary>
        /// Pending a build request before calling BeginBuild yields an InvalidOperationException.
        /// </summary>
        [Fact]
        public void PendBuildRequestWithoutBegin()
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                BuildRequestData data = new BuildRequestData("foo", new Dictionary<string, string>(), "2.0", new string[0], null);
                BuildSubmission submission = _buildManager.PendBuildRequest(data);
            }
           );
        }
        /// <summary>
        /// Calling EndBuild before BeginBuild yields an InvalidOperationException.
        /// </summary>
        [Fact]
        public void EndWithoutBegin()
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                _buildManager.EndBuild();
            }
           );
        }

        [Fact]
        public void DisposeAfterUse()
        {
            string contents = ObjectModelHelpers.CleanupFileContents(@"
<Project xmlns='msbuildnamespace' ToolsVersion='msbuilddefaulttoolsversion'>
</Project>
");

            Project project = CreateProject(contents, null, _projectCollection, false);

            var globalProperties = new Dictionary<string, string>();
            var targets = new string[0];
            var brd = new BuildRequestData(project.FullPath, globalProperties, null, targets, new HostServices());
            using (var bm = new BuildManager())
            {
                bm.Build(new BuildParameters(), brd);
            }
        }

        [Fact]
        public void DisposeWithoutUse()
        {
            var bm = new BuildManager();
            bm.Dispose();
        }

        /// <summary>
        /// Calling BeginBuild after BeginBuild has already been called yields an InvalidOperationException.
        /// </summary>
        [Fact]
        public void OverlappingBegin()
        {
            try
            {
                bool exceptionCaught = false;

                try
                {
                    _buildManager.BeginBuild(new BuildParameters());
                    _buildManager.BeginBuild(new BuildParameters());
                }
                catch (InvalidOperationException)
                {
                    exceptionCaught = true;
                }

                Assert.True(exceptionCaught);
            }
            finally
            {
                // Call EndBuild to get us back into a state that approximates reasonable
                _buildManager.EndBuild();
            }
        }

        /// <summary>
        /// Starting and ending a build without submitting any requests is valid.
        /// </summary>
        [Fact]
        public void EmptyBuild()
        {
            _buildManager.BeginBuild(_parameters);
            _buildManager.EndBuild();

            Assert.Equal(0, _logger.ErrorCount);
            Assert.Equal(0, _logger.WarningCount);
        }

        /// <summary>
        /// Calling EndBuild after it has already been called yields an InvalidOperationException.
        /// </summary>
        [Fact]
        public void ExtraEnds()
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                _buildManager.BeginBuild(new BuildParameters());
                _buildManager.EndBuild();
                _buildManager.EndBuild();
            }
           );
        }
        /// <summary>
        /// Pending a request after EndBuild has been called yields an InvalidOperationException.
        /// </summary>
        [Fact]
        public void PendBuildRequestAfterEnd()
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                BuildRequestData data = new BuildRequestData("foo", new Dictionary<string, string>(), "2.0", new string[0], null);
                _buildManager.BeginBuild(new BuildParameters());
                _buildManager.EndBuild();

                BuildSubmission submission = _buildManager.PendBuildRequest(data);
            }
           );
        }
        /// <summary>
        /// Attempting a synchronous build when a build is in progress yields an InvalidOperationException.
        /// </summary>
        [Fact]
        public void BuildDuringBuild()
        {
            try
            {
                bool exceptionCaught = false;
                try
                {
                    BuildRequestData data = new BuildRequestData("foo", new Dictionary<string, string>(), "2.0", new string[0], null);
                    _buildManager.BeginBuild(new BuildParameters());
                    _buildManager.Build(new BuildParameters(), data);
                }
                catch (InvalidOperationException)
                {
                    exceptionCaught = true;
                }

                Assert.True(exceptionCaught);
            }
            finally
            {
                // Call EndBuild to get us back into a state that approximates reasonable
                _buildManager.EndBuild();
            }
        }

        /// <summary>
        /// A sequential build.
        /// </summary>
        [Fact]
        [Trait("Category", "netcore-osx-failing")]
        [Trait("Category", "netcore-linux-failing")]
        public void EndBuildBlocks()
        {
            string contents = ObjectModelHelpers.CleanupFileContents(@"
<Project xmlns='msbuildnamespace' ToolsVersion='msbuilddefaulttoolsversion'>
 <Target Name='test'>
    <Exec Command='" + Helpers.GetSleepCommand(TimeSpan.FromSeconds(1)) + @"'/>
	<Message Text='[success 1]'/>
 </Target>
</Project>
");

            BuildRequestData data = GetBuildRequestData(contents);
            _buildManager.BeginBuild(_parameters);
            BuildSubmission submission1 = _buildManager.PendBuildRequest(data);
            submission1.ExecuteAsync(null, null);
            Assert.False(submission1.IsCompleted);
            _buildManager.EndBuild();
            Assert.True(submission1.IsCompleted);
            _logger.AssertLogContains("[success 1]");
        }

        /// <summary>
        /// Validate that EndBuild can be called during a submission completion callback.
        /// </summary>
        [Fact]
        public void EndBuildCalledWithinSubmissionCallback()
        {
            string contents = ObjectModelHelpers.CleanupFileContents(@"
<Project xmlns='msbuildnamespace' ToolsVersion='msbuilddefaulttoolsversion'>
 <Target Name='test'>
	<Message Text='[success 1]'/>
 </Target>
</Project>
");

            BuildRequestData data = GetBuildRequestData(contents);
            _buildManager.BeginBuild(_parameters);
            BuildSubmission submission1 = _buildManager.PendBuildRequest(data);
            AutoResetEvent callbackFinished = new AutoResetEvent(false);
            submission1.ExecuteAsync
                (
                delegate (BuildSubmission submission)
                {
                    _buildManager.EndBuild();
                    callbackFinished.Set();
                },
                null);

            // Wait for the build to finish
            Assert.True(callbackFinished.WaitOne(5000)); // "Build is hung."

            // EndBuild should now have been called, so invoking it again should give us an invalid operation error.
            bool invalidOperationReceived = false;
            try
            {
                _buildManager.EndBuild();
            }
            catch (InvalidOperationException)
            {
                invalidOperationReceived = true;
            }

            Assert.True(invalidOperationReceived);
        }

        /// <summary>
        /// A sequential build.
        /// </summary>
        [Fact]
        [Trait("Category", "netcore-osx-failing")]
        [Trait("Category", "netcore-linux-failing")]
        public void SequentialBuild()
        {
            string contents = ObjectModelHelpers.CleanupFileContents(@"
<Project xmlns='msbuildnamespace' ToolsVersion='msbuilddefaulttoolsversion'>
 <Target Name='test'>
	<Message Text='[success 1]'/>
 </Target>
</Project>
");

            string contents2 = ObjectModelHelpers.CleanupFileContents(@"
<Project xmlns='msbuildnamespace' ToolsVersion='msbuilddefaulttoolsversion'>
 <Target Name='test'>
	<Message Text='[success 2]'/>
 </Target>
</Project>
");

            BuildRequestData data = GetBuildRequestData(contents);
            BuildRequestData data2 = GetBuildRequestData(contents2);
            _buildManager.BeginBuild(_parameters);
            BuildResult result = _buildManager.BuildRequest(data);
            Assert.Equal(BuildResultCode.Success, result.OverallResult);

            BuildResult result2 = _buildManager.BuildRequest(data2);
            Assert.Equal(BuildResultCode.Success, result2.OverallResult);
            _buildManager.EndBuild();

            _logger.AssertLogContains("[success 1]");
            _logger.AssertLogContains("[success 2]");
        }

        /// <summary>
        /// A sequential build.
        /// </summary>
        [Fact]
        [Trait("Category", "netcore-osx-failing")]
        [Trait("Category", "netcore-linux-failing")]
        public void OverlappingBuildSubmissions()
        {
            string contents = ObjectModelHelpers.CleanupFileContents(@"
<Project xmlns='msbuildnamespace' ToolsVersion='msbuilddefaulttoolsversion'>
 <Target Name='test'>
    <Exec Command='" + Helpers.GetSleepCommand(TimeSpan.FromMilliseconds(500)) + @"'/>
	<Message Text='[success 1]'/>
 </Target>
</Project>
");

            string contents2 = ObjectModelHelpers.CleanupFileContents(@"
<Project xmlns='msbuildnamespace' ToolsVersion='msbuilddefaulttoolsversion'>
 <Target Name='test'>
	<Message Text='[success 2]'/>
 </Target>
</Project>
");

            BuildRequestData data = GetBuildRequestData(contents);
            BuildRequestData data2 = GetBuildRequestData(contents2);
            _buildManager.BeginBuild(_parameters);
            BuildSubmission submission1 = _buildManager.PendBuildRequest(data);
            submission1.ExecuteAsync(null, null);
            BuildResult result2 = _buildManager.BuildRequest(data2);
            submission1.WaitHandle.WaitOne();
            BuildResult result = submission1.BuildResult;
            _buildManager.EndBuild();

            Assert.Equal(BuildResultCode.Success, result2.OverallResult);
            Assert.Equal(BuildResultCode.Success, result.OverallResult);

            _logger.AssertLogContains("[success 1]");
            _logger.AssertLogContains("[success 2]");
        }

        /// <summary>
        /// If two overlapping submissions are executed against the same project, with at least one 
        /// target involved that skipped, make sure that the second one successfully completes 
        /// (retrieved from the cache). 
        /// </summary>
        [Fact]
        [Trait("Category", "netcore-osx-failing")]
        [Trait("Category", "netcore-linux-failing")]
        public void OverlappingIdenticalBuildSubmissions()
        {
            string contents = ObjectModelHelpers.CleanupFileContents(@"
<Project xmlns='msbuildnamespace' ToolsVersion='msbuilddefaulttoolsversion'>
 <Target Name='test' Condition='false' />
</Project>
");

            BuildRequestData data = GetBuildRequestData(contents);
            BuildRequestData data2 = new BuildRequestData(data.ProjectInstance, data.TargetNames.ToArray(), data.HostServices);

            _buildManager.BeginBuild(_parameters);
            BuildSubmission submission1 = _buildManager.PendBuildRequest(data);
            BuildSubmission submission2 = _buildManager.PendBuildRequest(data2);

            submission2.ExecuteAsync(null, null);
            submission1.ExecuteAsync(null, null);

            submission1.WaitHandle.WaitOne();
            submission2.WaitHandle.WaitOne();

            _buildManager.EndBuild();

            Assert.Equal(BuildResultCode.Success, submission1.BuildResult.OverallResult);
            Assert.Equal(BuildResultCode.Success, submission2.BuildResult.OverallResult);
        }

        /// <summary>
        /// With two overlapping submissions, the first of which skips a target and the second 
        /// of which should not, ensure that the second submission does not, in fact, skip 
        /// the target.  (E.g. despite the fact that the target results are in the cache already 
        /// as 'skipped', ensure that we retry execution in case conditions have changed.)
        /// </summary>
        [Fact]
        [Trait("Category", "netcore-osx-failing")]
        [Trait("Category", "netcore-linux-failing")]
        public void OverlappingBuildSubmissions_OnlyOneSucceeds()
        {
            string contents = ObjectModelHelpers.CleanupFileContents(@"
<Project xmlns='msbuildnamespace' ToolsVersion='msbuilddefaulttoolsversion'>
 <Target Name='A' DependsOnTargets='SetProp;MaySkip;UnsetProp' />

 <Target Name='SetProp'>
  <PropertyGroup>
   <ShouldSkip>true</ShouldSkip>
  </PropertyGroup>
 </Target>

 <Target Name='MaySkip' Condition='!$(ShouldSkip)'>
  <Error Text='[ERROR]' />
 </Target>

 <Target Name='UnsetProp'>
  <PropertyGroup>
   <ShouldSkip>false</ShouldSkip>
  </PropertyGroup>
 </Target>

</Project>
");

            BuildRequestData data = GetBuildRequestData(contents, new[] { "A" });
            BuildRequestData data2 = new BuildRequestData(data.ProjectInstance, new string[] { "MaySkip" }, data.HostServices);

            _buildManager.BeginBuild(_parameters);
            BuildSubmission submission1 = _buildManager.PendBuildRequest(data);
            BuildSubmission submission2 = _buildManager.PendBuildRequest(data2);

            submission1.ExecuteAsync(null, null);
            submission2.ExecuteAsync(null, null);

            submission1.WaitHandle.WaitOne();
            submission2.WaitHandle.WaitOne();

            _buildManager.EndBuild();

            Assert.Equal(BuildResultCode.Success, submission1.BuildResult.OverallResult);
            Assert.Equal(BuildResultCode.Failure, submission2.BuildResult.OverallResult);
        }

        /// <summary>
        /// Calling EndBuild with an unexecuted submission.
        /// </summary>
        [Fact]
        public void EndWithUnexecutedSubmission()
        {
            string contents = ObjectModelHelpers.CleanupFileContents(@"
<Project xmlns='msbuildnamespace' ToolsVersion='msbuilddefaulttoolsversion'>
 <Target Name='test'>
	<Exec Command='" + Helpers.GetSleepCommand(TimeSpan.FromSeconds(20)) + @"'/>
    <Message Text='[fail]'/>
 </Target>
</Project>
");
            BuildRequestData data = GetBuildRequestData(contents, new string[] { }, ObjectModelHelpers.MSBuildDefaultToolsVersion);
            _buildManager.BeginBuild(_parameters);
            BuildSubmission asyncResult = _buildManager.PendBuildRequest(data);
            _buildManager.EndBuild();
        }

        /// <summary>
        /// A canceled build with a submission which is not executed yet.
        /// </summary>
        [Fact]
        public void CancelledBuildWithUnexecutedSubmission()
        {
            string contents = ObjectModelHelpers.CleanupFileContents(@"
<Project xmlns='msbuildnamespace' ToolsVersion='msbuilddefaulttoolsversion'>
 <Target Name='test'>
	<Exec Command='" + Helpers.GetSleepCommand(TimeSpan.FromSeconds(20)) + @"'/>
    <Message Text='[fail]'/>
 </Target>
</Project>
");
            BuildRequestData data = GetBuildRequestData(contents, new string[] { }, ObjectModelHelpers.MSBuildDefaultToolsVersion);
            _buildManager.BeginBuild(_parameters);
            BuildSubmission asyncResult = _buildManager.PendBuildRequest(data);
            _buildManager.CancelAllSubmissions();
            _buildManager.EndBuild();
        }

        /// <summary>
        /// A cancelled build
        /// </summary>
        [Fact]
        [Trait("Category", "mono-osx-failing")]
        public void CancelledBuild()
        {
            string contents = ObjectModelHelpers.CleanupFileContents(@"
<Project xmlns='msbuildnamespace' ToolsVersion='msbuilddefaulttoolsversion'>
 <Target Name='test'>
	<Exec Command='" + Helpers.GetSleepCommand(TimeSpan.FromSeconds(60)) + @"'/>
    <Message Text='[fail]'/>
 </Target>
</Project>
");
            BuildRequestData data = GetBuildRequestData(contents, new string[] { }, ObjectModelHelpers.MSBuildDefaultToolsVersion);
            _buildManager.BeginBuild(_parameters);
            BuildSubmission asyncResult = _buildManager.PendBuildRequest(data);

            asyncResult.ExecuteAsync(null, null);
            DateTime startTime = DateTime.Now;
            _buildManager.CancelAllSubmissions();
            asyncResult.WaitHandle.WaitOne();
            BuildResult result = asyncResult.BuildResult;
            DateTime endTime = DateTime.Now;
            _buildManager.EndBuild();

            Assert.Equal(BuildResultCode.Failure, result.OverallResult); // "Build should have failed."
            _logger.AssertLogDoesntContain("[fail]");
        }

        /// <summary>
        /// A canceled build which waits for the task to get started before canceling.  Because it is a 2.0 task, we should
        /// wait until the task finishes normally (cancellation not supported.)
        /// </summary>
        [Fact]
        public void CancelledBuildWithDelay20()
        {
            if (FrameworkLocationHelper.PathToDotNetFrameworkV20 != null)
            {
                string contents = ObjectModelHelpers.CleanupFileContents(@"
<Project xmlns='msbuildnamespace' ToolsVersion='2.0'>
 <Target Name='test'>
	<Exec Command='" + Helpers.GetSleepCommand(TimeSpan.FromSeconds(5)) + @"'/>
    <Message Text='[fail]'/>
 </Target>
</Project>
");
                BuildRequestData data = GetBuildRequestData(contents);
                _buildManager.BeginBuild(_parameters);
                BuildSubmission asyncResult = _buildManager.PendBuildRequest(data);
                asyncResult.ExecuteAsync(null, null);

                Thread.Sleep(500);
                _buildManager.CancelAllSubmissions();
                asyncResult.WaitHandle.WaitOne();
                BuildResult result = asyncResult.BuildResult;
                _buildManager.EndBuild();

                Assert.Equal(BuildResultCode.Failure, result.OverallResult); // "Build should have failed."
                _logger.AssertLogDoesntContain("[fail]");
            }
        }

#if FEATURE_TASKHOST
        /// <summary>
        /// A canceled build which waits for the task to get started before canceling.  Because it is a 2.0 task, we should
        /// wait until the task finishes normally (cancellation not supported.)
        /// </summary>
        [Fact]
        [Trait("Category", "mono-osx-failing")]
        public void CancelledBuildInTaskHostWithDelay20()
        {
            if (FrameworkLocationHelper.PathToDotNetFrameworkV20 != null)
            {
                string contents = ObjectModelHelpers.CleanupFileContents(@"
<Project xmlns='msbuildnamespace' ToolsVersion='msbuilddefaulttoolsversion'>
 <UsingTask TaskName='Microsoft.Build.Tasks.Exec' AssemblyName='Microsoft.Build.Tasks.v3.5, Version=3.5.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a' TaskFactory='TaskHostFactory' />
 <Target Name='test'>
	<Exec Command='" + Helpers.GetSleepCommand(TimeSpan.FromSeconds(10)) + @"'/>
    <Message Text='[fail]'/>
 </Target>
</Project>
");
                BuildRequestData data = GetBuildRequestData(contents, new string[] { }, ObjectModelHelpers.MSBuildDefaultToolsVersion);
                _buildManager.BeginBuild(_parameters);
                BuildSubmission asyncResult = _buildManager.PendBuildRequest(data);
                asyncResult.ExecuteAsync(null, null);

                Thread.Sleep(500);
                _buildManager.CancelAllSubmissions();
                asyncResult.WaitHandle.WaitOne();
                BuildResult result = asyncResult.BuildResult;
                _buildManager.EndBuild();

                Assert.Equal(BuildResultCode.Failure, result.OverallResult); // "Build should have failed."
                _logger.AssertLogDoesntContain("[fail]");

                // Task host should not have exited prematurely
                _logger.AssertLogDoesntContain("MSB4217");
            }
        }
#endif

        /// <summary>
        /// A canceled build which waits for the task to get started before canceling.  Because it is a 12.. task, we should
        /// cancel the task and exit out after a short period wherein we wait for the task to exit cleanly. 
        /// </summary>
        [Fact]
        [Trait("Category", "mono-osx-failing")]
        public void CancelledBuildWithDelay40()
        {
            string contents = ObjectModelHelpers.CleanupFileContents(@"
<Project xmlns='msbuildnamespace' ToolsVersion='msbuilddefaulttoolsversion'>
 <Target Name='test'>
	<Exec Command='" + Helpers.GetSleepCommand(TimeSpan.FromSeconds(10)) + @"'/>
    <Message Text='[fail]'/>
 </Target>
</Project>
");
            BuildRequestData data = GetBuildRequestData(contents, new string[] { }, ObjectModelHelpers.MSBuildDefaultToolsVersion);
            _buildManager.BeginBuild(_parameters);
            BuildSubmission asyncResult = _buildManager.PendBuildRequest(data);
            asyncResult.ExecuteAsync(null, null);

            Thread.Sleep(500);
            _buildManager.CancelAllSubmissions();
            asyncResult.WaitHandle.WaitOne();
            BuildResult result = asyncResult.BuildResult;
            _buildManager.EndBuild();

            Assert.Equal(BuildResultCode.Failure, result.OverallResult); // "Build should have failed."
            _logger.AssertLogDoesntContain("[fail]");
        }

#if FEATURE_TASKHOST
        /// <summary>
        /// A canceled build which waits for the task to get started before canceling.  Because it is a 12.0 task, we should
        /// cancel the task and exit out after a short period wherein we wait for the task to exit cleanly. 
        /// </summary>
        [Fact]
        [Trait("Category", "mono-osx-failing")]
        public void CancelledBuildInTaskHostWithDelay40()
        {
            string contents = ObjectModelHelpers.CleanupFileContents(@"
<Project xmlns='msbuildnamespace' ToolsVersion='msbuilddefaulttoolsversion'>
 <UsingTask TaskName='Microsoft.Build.Tasks.Exec' AssemblyName='Microsoft.Build.Tasks.Core, Version=msbuildassemblyversion, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a' TaskFactory='TaskHostFactory' />
 <Target Name='test'>
	<Exec Command='" + Helpers.GetSleepCommand(TimeSpan.FromSeconds(10)) + @"'/>
    <Message Text='[fail]'/>
 </Target>
</Project>
");
            BuildRequestData data = GetBuildRequestData(contents, new string[] { }, ObjectModelHelpers.MSBuildDefaultToolsVersion);
            _buildManager.BeginBuild(_parameters);
            BuildSubmission asyncResult = _buildManager.PendBuildRequest(data);
            asyncResult.ExecuteAsync(null, null);

            System.Threading.Thread.Sleep(500);
            _buildManager.CancelAllSubmissions();
            asyncResult.WaitHandle.WaitOne();
            BuildResult result = asyncResult.BuildResult;
            _buildManager.EndBuild();

            Assert.Equal(BuildResultCode.Failure, result.OverallResult); // "Build should have failed."
            _logger.AssertLogDoesntContain("[fail]");

            // Task host should not have exited prematurely
            _logger.AssertLogDoesntContain("MSB4217");
        }
#endif

        /// <summary>
        /// This test verifies that builds of the same project instance in sequence are permitted.
        /// </summary>
        [Fact]
        [Trait("Category", "netcore-osx-failing")]
        [Trait("Category", "netcore-linux-failing")]
        public void SequentialBuildsOfTheSameProjectAllowed()
        {
            string contents = ObjectModelHelpers.CleanupFileContents(@"
<Project xmlns='msbuildnamespace' ToolsVersion='msbuilddefaulttoolsversion'>
 <Target Name='target1'>
    <Message Text='text'/>
 </Target>
 <Target Name='target2'>
    <Message Text='text'/>
 </Target>
</Project>
");
            Project project = CreateProject(contents, ObjectModelHelpers.MSBuildDefaultToolsVersion, _projectCollection, true);
            ProjectInstance instance = _buildManager.GetProjectInstanceForBuild(project);
            _buildManager.BeginBuild(_parameters);
            BuildResult result1 = _buildManager.BuildRequest(new BuildRequestData(instance, new string[] { "target1" }));
            BuildResult result2 = _buildManager.BuildRequest(new BuildRequestData(instance, new string[] { "target2" }));
            _buildManager.EndBuild();

            Assert.Equal(BuildResultCode.Success, result1.OverallResult);
            Assert.True(result1.HasResultsForTarget("target1")); // "Results for target1 missing"
            Assert.Equal(BuildResultCode.Success, result2.OverallResult);
            Assert.True(result2.HasResultsForTarget("target2")); // "Results for target2 missing"
        }

        /// <summary>
        /// This test verifies that overlapping builds of the same project are allowed.
        /// </summary>
        [Fact]
        [Trait("Category", "netcore-osx-failing")]
        [Trait("Category", "netcore-linux-failing")]
        public void OverlappingBuildsOfTheSameProjectDifferentTargetsAreAllowed()
        {
            string contents = ObjectModelHelpers.CleanupFileContents(@"
<Project xmlns='msbuildnamespace' ToolsVersion='msbuilddefaulttoolsversion'>
 <Target Name='target1'>
    <Exec Command='" + Helpers.GetSleepCommand(TimeSpan.FromSeconds(3)) + @"'/>
    <Message Text='text'/>
 </Target>
 <Target Name='target2'>
    <Message Text='text'/>
 </Target>
</Project>
");
            Project project = CreateProject(contents, ObjectModelHelpers.MSBuildDefaultToolsVersion, _projectCollection, true);
            ProjectInstance instance = _buildManager.GetProjectInstanceForBuild(project);
            _buildManager.BeginBuild(_parameters);
            try
            {
                BuildSubmission submission = _buildManager.PendBuildRequest(new BuildRequestData(instance, new string[] { "target1" }));
                submission.ExecuteAsync(null, null);
                BuildResult result2 = _buildManager.BuildRequest(new BuildRequestData(project.CreateProjectInstance(), new string[] { "target2" }));

                submission.WaitHandle.WaitOne();
                var result1 = submission.BuildResult;

                Assert.Equal(BuildResultCode.Success, result1.OverallResult);
                Assert.True(result1.HasResultsForTarget("target1")); // "Results for target1 missing"
                Assert.Equal(BuildResultCode.Success, result2.OverallResult);
                Assert.True(result2.HasResultsForTarget("target2")); // "Results for target2 missing"
            }
            finally
            {
                _buildManager.EndBuild();
            }
        }

        /// <summary>
        /// This test verifies that overlapping builds of the same project are allowed.
        /// </summary>
        [Fact]
        [Trait("Category", "netcore-osx-failing")]
        [Trait("Category", "netcore-linux-failing")]
        public void OverlappingBuildsOfTheSameProjectSameTargetsAreAllowed()
        {
            string contents = ObjectModelHelpers.CleanupFileContents(@"
<Project xmlns='msbuildnamespace' ToolsVersion='msbuilddefaulttoolsversion'>
 <Target Name='target1'>
    <Exec Command='" + Helpers.GetSleepCommand(TimeSpan.FromSeconds(3)) + @"'/>
    <Message Text='text'/>
 </Target>
 <Target Name='target2'>
    <Message Text='text'/>
 </Target>
</Project>
");
            Project project = CreateProject(contents, ObjectModelHelpers.MSBuildDefaultToolsVersion, _projectCollection, true);
            ProjectInstance instance = _buildManager.GetProjectInstanceForBuild(project);
            _buildManager.BeginBuild(_parameters);
            try
            {
                BuildSubmission submission = _buildManager.PendBuildRequest(new BuildRequestData(instance, new string[] { "target1" }));
                submission.ExecuteAsync(null, null);
                BuildResult result2 = _buildManager.BuildRequest(new BuildRequestData(project.CreateProjectInstance(), new string[] { "target1" }));

                submission.WaitHandle.WaitOne();
                var result1 = submission.BuildResult;

                Assert.Equal(BuildResultCode.Success, result1.OverallResult);
                Assert.True(result1.HasResultsForTarget("target1")); // "Results for target1 missing"
                Assert.Equal(BuildResultCode.Success, result2.OverallResult);
                Assert.True(result2.HasResultsForTarget("target1")); // "Results for target1 (second call) missing"
            }
            finally
            {
                _buildManager.EndBuild();
            }
        }

        /// <summary>
        /// This test verifies that the out-of-proc node won't lock the directory containing the target project.
        /// </summary>
#if RUNTIME_TYPE_NETCORE
        [Fact(Skip = "https://github.com/Microsoft/msbuild/issues/933")]
#else
        [Fact]
#endif
        public void OutOfProcNodeDoesntLockWorkingDirectory()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

            string projectFile = Path.Combine(tempDir, "foo.proj");
            string contents = ObjectModelHelpers.CleanupFileContents(@"
<Project xmlns='msbuildnamespace' ToolsVersion='msbuilddefaulttoolsversion'>
 <Target Name='test'>
    <Message Text='[success]'/>
 </Target>
</Project>
");
            Directory.CreateDirectory(tempDir);
            File.WriteAllText(projectFile, contents);

            try
            {
                Environment.SetEnvironmentVariable("MSBUILDNOINPROCNODE", "1");
                BuildRequestData data = new BuildRequestData(projectFile, new Dictionary<string, string>(), ObjectModelHelpers.MSBuildDefaultToolsVersion, new string[] { }, null);
                _buildManager.Build(_parameters, data);
            }
            finally
            {
                Environment.SetEnvironmentVariable("MSBUILDNOINPROCNODE", String.Empty);
            }

            FileUtilities.DeleteWithoutTrailingBackslash(tempDir, true);
            Assert.False(Directory.Exists(tempDir)); // "Temp directory should no longer exist."
        }

        /// <summary>
        /// Retrieving a ProjectInstance from the BuildManager stores it in the cache
        /// </summary>
        [Fact]
        public void ProjectInstanceStoredInCache()
        {
            string contents = ObjectModelHelpers.CleanupFileContents(@"
<Project xmlns='msbuildnamespace' ToolsVersion='msbuilddefaulttoolsversion'>
 <Target Name='test'>
    <Message Text='text'/>
 </Target>
</Project>
");
            Project project = CreateProject(contents, ObjectModelHelpers.MSBuildDefaultToolsVersion, _projectCollection, true);
            ProjectInstance instance = _buildManager.GetProjectInstanceForBuild(project);
            ProjectInstance instance2 = _buildManager.GetProjectInstanceForBuild(project);

            Assert.True(Object.ReferenceEquals(instance, instance2)); // "Instances don't match"
        }

        /// <summary>
        /// Retrieving a ProjectInstance from the BuildManager after a build.
        /// </summary>
        [Fact]
        [Trait("Category", "netcore-osx-failing")]
        [Trait("Category", "netcore-linux-failing")]
        public void ProjectInstanceRetrievedAfterBuildMatchesSourceProject()
        {
            string contents = ObjectModelHelpers.CleanupFileContents(@"
<Project xmlns='msbuildnamespace' ToolsVersion='msbuilddefaulttoolsversion'>
 <Target Name='test'>
    <PropertyGroup>
        <Foo>bar</Foo>
    </PropertyGroup>
	<Message Text='[success]'/>
 </Target>
</Project>
");
            IBuildComponentHost host = _buildManager as IBuildComponentHost;
            IConfigCache cache = host.GetComponent(BuildComponentType.ConfigCache) as IConfigCache;
            BuildRequestData data = GetBuildRequestData(contents);
            BuildResult result = _buildManager.Build(_parameters, data);

            Project project = _projectCollection.LoadProject(data.ProjectFullPath);
            ProjectInstance instance = _buildManager.GetProjectInstanceForBuild(project);
            Assert.Equal(instance.GetPropertyValue("Foo"), "bar");
        }

        /// <summary>
        /// Retrieving a ProjectInstance after resetting the cache clears the instances.
        /// </summary>
        [Fact]
        [Trait("Category", "netcore-osx-failing")]
        [Trait("Category", "netcore-linux-failing")]
        public void ResetCacheClearsInstances()
        {
            string contents = ObjectModelHelpers.CleanupFileContents(@"
<Project xmlns='msbuildnamespace' ToolsVersion='msbuilddefaulttoolsversion'>
 <Target Name='test'>
    <PropertyGroup>
        <Foo>bar</Foo>
    </PropertyGroup>
	<Message Text='[success]'/>
 </Target>
</Project>
");
            IBuildComponentHost host = _buildManager as IBuildComponentHost;
            IConfigCache cache = host.GetComponent(BuildComponentType.ConfigCache) as IConfigCache;
            BuildRequestData data = GetBuildRequestData(contents);
            BuildResult result = _buildManager.Build(_parameters, data);

            Project project = _projectCollection.LoadProject(data.ProjectFullPath);
            ProjectInstance instance = _buildManager.GetProjectInstanceForBuild(project);
            Assert.Equal("bar", instance.GetPropertyValue("Foo"));

            _buildManager.BeginBuild(_parameters);
            _buildManager.EndBuild();

            instance = _buildManager.GetProjectInstanceForBuild(project);
            Assert.Null(instance.GetProperty("Foo"));
        }

        /// <summary>
        /// Retrieving a ProjectInstance after another build without resetting the cache keeps the existing instance
        /// </summary>
        [Fact]
        [Trait("Category", "netcore-osx-failing")]
        [Trait("Category", "netcore-linux-failing")]
        public void DisablingCacheResetKeepsInstance()
        {
            string contents = ObjectModelHelpers.CleanupFileContents(@"
<Project xmlns='msbuildnamespace' ToolsVersion='msbuilddefaulttoolsversion'>
 <Target Name='test'>
    <PropertyGroup>
        <Foo>bar</Foo>
    </PropertyGroup>
	<Message Text='[success]'/>
 </Target>
</Project>
");
            IBuildComponentHost host = _buildManager as IBuildComponentHost;
            IConfigCache cache = host.GetComponent(BuildComponentType.ConfigCache) as IConfigCache;
            BuildRequestData data = GetBuildRequestData(contents);
            BuildResult result = _buildManager.Build(_parameters, data);

            Project project = _projectCollection.LoadProject(data.ProjectFullPath);
            ProjectInstance instance = _buildManager.GetProjectInstanceForBuild(project);
            Assert.Equal(instance.GetPropertyValue("Foo"), "bar");

            _logger.ClearLog();
            _parameters.ResetCaches = false;
            _buildManager.BeginBuild(_parameters);
            result = _buildManager.BuildRequest(data);
            _buildManager.EndBuild();

            // We should have built the same instance, with the same results, so the target will be skipped.
            string skippedMessage = ResourceUtilities.FormatResourceString("TargetAlreadyCompleteSuccess", "test");
            Assert.Equal(true, _logger.FullLog.Contains(skippedMessage));

            ProjectInstance instance2 = _buildManager.GetProjectInstanceForBuild(project);
            Assert.True(Object.ReferenceEquals(instance, instance2)); // "Instances are not the same"
        }

        /// <summary>
        /// Retrieving a ProjectInstance after another build without resetting the cache keeps the existing instance
        /// </summary>
        [Fact]
        [Trait("Category", "netcore-osx-failing")]
        [Trait("Category", "netcore-linux-failing")]
        public void GhostProjectRootElementCache()
        {
            string contents1 = ObjectModelHelpers.CleanupFileContents(@"
<Project xmlns='msbuildnamespace' ToolsVersion='msbuilddefaulttoolsversion'>
 <Target Name='test'>
    <Msbuild Projects='Project2.proj'>
      <Output TaskParameter='TargetOutputs' ItemName='P2pOutput'/>
    </Msbuild>

     <Message Text='Value:@(P2pOutput)' Importance='high'/>
 </Target>
</Project>
");

            string contents2 = ObjectModelHelpers.CleanupFileContents(@"
<Project xmlns='msbuildnamespace' ToolsVersion='msbuilddefaulttoolsversion'>
    <PropertyGroup>
        <Bar Condition=""'$(Bar)' == ''"">Baz</Bar>
    </PropertyGroup>

<Target Name='test' Returns='$(Bar)'/>
</Project>
");
            IBuildComponentHost host = _buildManager as IBuildComponentHost;
            IConfigCache cache = host.GetComponent(BuildComponentType.ConfigCache) as IConfigCache;

            // Create Project 1
            ProjectInstance projectInstance = CreateProjectInstance(contents1, null, _projectCollection, false);
            BuildRequestData data = new BuildRequestData(projectInstance, new string[0]);

            _logger.ClearLog();
            string p2pProject = Path.Combine(Path.GetDirectoryName(data.ProjectFullPath), "Project2.proj");

            try
            {
                // Write the second project to disk and load it into its own project collection
                ProjectCollection projectCollection2 = new ProjectCollection();
                File.WriteAllText(p2pProject, contents2);

                Project project2 = projectCollection2.LoadProject(p2pProject);

                _parameters.ResetCaches = false;

                // Build the first project to make sure we get the expected default values out for the p2p call.
                _parameters.ProjectRootElementCache = _projectCollection.ProjectRootElementCache;
                _buildManager.BeginBuild(_parameters);
                BuildResult result = _buildManager.BuildRequest(data);
                _buildManager.EndBuild();

                _logger.AssertLogContains("Value:Baz");
                _logger.ClearLog();

                // Modify the property in the second project and save it to disk.
                project2.SetProperty("Bar", "FOO");
                project2.Save();

                // Create a new build.
                ProjectInstance projectInstance2 = CreateProjectInstance(contents1, null, _projectCollection, false);
                BuildRequestData data2 = new BuildRequestData(projectInstance2, new string[0]);

                // Build again.
                _parameters.ResetCaches = false;
                _buildManager.BeginBuild(_parameters);
                result = _buildManager.BuildRequest(data2);
                _buildManager.EndBuild();
                _logger.AssertLogContains("Value:FOO");
            }
            finally
            {
                if (File.Exists(p2pProject))
                {
                    File.Delete(p2pProject);
                }
            }
        }

        /// <summary>
        /// Verifies that explicitly loaded projects' imports are all marked as also explicitly loaded.
        /// </summary>
        [Fact]
        public void VerifyImportedProjectRootElementsInheritExplicitLoadFlag()
        {
            string contents1 = ObjectModelHelpers.CleanupFileContents(@"
<Project xmlns='msbuildnamespace' ToolsVersion='msbuilddefaulttoolsversion'>
 <Import Project='{0}' />
 <Target Name='test' />
</Project>
");

            string contents2 = ObjectModelHelpers.CleanupFileContents(@"
<Project xmlns='msbuildnamespace' ToolsVersion='msbuilddefaulttoolsversion'>
    <PropertyGroup>
        <ImportedProperty>ImportedValue</ImportedProperty>
    </PropertyGroup>
</Project>
");

            using (TempFileCollection tfc = new TempFileCollection())
            {
                string importedProjectPath = FileUtilities.GetTemporaryFile();
                string rootProjectPath = FileUtilities.GetTemporaryFile();
                tfc.AddFile(importedProjectPath, false);
                tfc.AddFile(rootProjectPath, false);
                File.WriteAllText(importedProjectPath, contents2);
                File.WriteAllText(rootProjectPath, String.Format(CultureInfo.InvariantCulture, contents1, importedProjectPath));

                var projectCollection = new ProjectCollection();

                // Run a simple build just to prove that nothing is left in the cache.
                BuildRequestData data = new BuildRequestData(rootProjectPath, ReadOnlyEmptyDictionary<string, string>.Instance, null, new[] { "test" }, null);
                _parameters.ResetCaches = true;
                _parameters.ProjectRootElementCache = projectCollection.ProjectRootElementCache;
                _buildManager.BeginBuild(_parameters);
                BuildResult result = _buildManager.BuildRequest(data);
                _buildManager.EndBuild();
                _buildManager.ResetCaches();

                // The semantic of TryOpen is to only retrieve the PRE if it is already in the weak cache.
                Assert.Null(Microsoft.Build.Construction.ProjectRootElement.TryOpen(rootProjectPath, projectCollection)); // "The built project shouldn't be in the cache anymore."
                Assert.Null(Microsoft.Build.Construction.ProjectRootElement.TryOpen(importedProjectPath, projectCollection)); // "The built project's import shouldn't be in the cache anymore."

                Project project = projectCollection.LoadProject(rootProjectPath);
                Microsoft.Build.Construction.ProjectRootElement preRoot, preImported;
                Assert.NotNull(preRoot = Microsoft.Build.Construction.ProjectRootElement.TryOpen(rootProjectPath, projectCollection)); // "The root project file should be in the weak cache."
                Assert.NotNull(preImported = Microsoft.Build.Construction.ProjectRootElement.TryOpen(importedProjectPath, projectCollection)); // "The imported project file should be in the weak cache."
                Assert.True(preRoot.IsExplicitlyLoaded);
                Assert.True(preImported.IsExplicitlyLoaded);

                // Run a simple build just to prove that it doesn't impact what is in the cache.
                data = new BuildRequestData(rootProjectPath, ReadOnlyEmptyDictionary<string, string>.Instance, null, new[] { "test" }, null);
                _parameters.ResetCaches = true;
                _parameters.ProjectRootElementCache = projectCollection.ProjectRootElementCache;
                _buildManager.BeginBuild(_parameters);
                result = _buildManager.BuildRequest(data);
                _buildManager.EndBuild();
                _buildManager.ResetCaches();

                // Now make sure they are still in the weak cache.  Since they were loaded explictly before the build, the build shouldn't have unloaded them from the cache.
                Assert.Same(preRoot, Microsoft.Build.Construction.ProjectRootElement.TryOpen(rootProjectPath, projectCollection)); // "The root project file should be in the weak cache after a build."
                Assert.Same(preImported, Microsoft.Build.Construction.ProjectRootElement.TryOpen(importedProjectPath, projectCollection)); // "The imported project file should be in the weak cache after a build."
                Assert.True(preRoot.IsExplicitlyLoaded);
                Assert.True(preImported.IsExplicitlyLoaded);

                projectCollection.UnloadProject(project);
                projectCollection.UnloadAllProjects();
                Assert.Null(Microsoft.Build.Construction.ProjectRootElement.TryOpen(rootProjectPath, projectCollection)); // "The unloaded project shouldn't be in the cache anymore."
                Assert.Null(Microsoft.Build.Construction.ProjectRootElement.TryOpen(importedProjectPath, projectCollection)); // "The unloaded project's import shouldn't be in the cache anymore."
            }
        }

        /// <summary>
        /// Verify that using a second BuildManager doesn't cause the system to crash.
        /// </summary>
        [Fact]
        [Trait("Category", "netcore-osx-failing")]
        [Trait("Category", "netcore-linux-failing")]
        public void Regress251333()
        {
            string contents = ObjectModelHelpers.CleanupFileContents(@"
<Project xmlns='msbuildnamespace' ToolsVersion='msbuilddefaulttoolsversion'>
<PropertyGroup>
       <InitialProperty1>InitialProperty1</InitialProperty1>
       <InitialProperty2>InitialProperty2</InitialProperty2>
       <InitialProperty3>InitialProperty3</InitialProperty3>
</PropertyGroup>
 <Target Name='test'>
	<Message Text='[success]'/>
 </Target>
</Project>
");

            // First a normal build
            BuildRequestData data = GetBuildRequestData(contents);
            BuildResult result = _buildManager.Build(_parameters, data);
            Assert.Equal(result.OverallResult, BuildResultCode.Success);

            // Now a build using a different build manager.
            using (BuildManager newBuildManager = new BuildManager())
            {
                BuildRequestData data2 = GetBuildRequestData(contents);
                BuildResult result2 = newBuildManager.Build(_parameters, data);
                Assert.Equal(result2.OverallResult, BuildResultCode.Success);
            }
        }

        /// <summary>
        /// Verify that disabling the in-proc node doesn't cause projects which don't require it to fail.
        /// </summary>
#if RUNTIME_TYPE_NETCORE
        [Fact(Skip = "https://github.com/Microsoft/msbuild/issues/933")]
#elif MONO
        [Fact(Skip = "https://github.com/Microsoft/msbuild/issues/1240")]
#else
        [Fact]
#endif
        public void Regress239661()
        {
            string contents = ObjectModelHelpers.CleanupFileContents(@"
<Project xmlns='msbuildnamespace' ToolsVersion='msbuilddefaulttoolsversion'>
<PropertyGroup>
       <InitialProperty1>InitialProperty1</InitialProperty1>
       <InitialProperty2>InitialProperty2</InitialProperty2>
       <InitialProperty3>InitialProperty3</InitialProperty3>
</PropertyGroup>
 <Target Name='test'>
	<Message Text='[success]'/>
 </Target>
</Project>
");

            string fileName = Path.GetTempFileName();
            try
            {
                File.WriteAllText(fileName, contents);
                BuildRequestData data = new BuildRequestData(fileName, _projectCollection.GlobalProperties, ObjectModelHelpers.MSBuildDefaultToolsVersion, new string[0], null);
                _parameters.DisableInProcNode = true;
                BuildResult result = _buildManager.Build(_parameters, data);
                Assert.Equal(BuildResultCode.Success, result.OverallResult);
                _logger.AssertLogContains("[success]");
            }
            finally
            {
                if (File.Exists(fileName))
                {
                    File.Delete(fileName);
                }
            }
        }

        /// <summary>
        /// Verify that disabling the in-proc node when a project requires it will cause the build to fail, but not crash.
        /// </summary>
#if RUNTIME_TYPE_NETCORE
        [Fact(Skip = "https://github.com/Microsoft/msbuild/issues/933")]
#else
        [Fact]
#endif
        public void Regress239661_NodeUnavailable()
        {
            string contents = ObjectModelHelpers.CleanupFileContents(@"
<Project xmlns='msbuildnamespace' ToolsVersion='msbuilddefaulttoolsversion'>
<PropertyGroup>
       <InitialProperty1>InitialProperty1</InitialProperty1>
       <InitialProperty2>InitialProperty2</InitialProperty2>
       <InitialProperty3>InitialProperty3</InitialProperty3>
</PropertyGroup>
 <Target Name='test'>
	<Message Text='[success]'/>
 </Target>
</Project>
");
            BuildRequestData data = GetBuildRequestData(contents);
            _parameters.DisableInProcNode = true;

            // Require that this project build on the in-proc node, which will not be available.
            data.HostServices.SetNodeAffinity(data.ProjectFullPath, NodeAffinity.InProc);
            BuildResult result = _buildManager.Build(_parameters, data);
            Assert.Equal(BuildResultCode.Failure, result.OverallResult);
            _logger.AssertLogDoesntContain("[success]");
            _logger.AssertLogContains("MSB4223");
        }

        /// <summary>
        /// Ensures that properties and items are transferred to the out-of-proc node when an instance is used to start the build.
        /// </summary>
#if RUNTIME_TYPE_NETCORE
        [Fact(Skip = "https://github.com/Microsoft/msbuild/issues/933")]
#elif MONO
        [Fact(Skip = "https://github.com/Microsoft/msbuild/issues/1240")]
#else
        [Fact]
#endif
        public void ProjectInstanceTransfersToOOPNode()
        {
            string contents = ObjectModelHelpers.CleanupFileContents(@"
<Project xmlns='msbuildnamespace' ToolsVersion='msbuilddefaulttoolsversion'>
 <PropertyGroup>
   <DeleteMe>deleteme</DeleteMe>
   <Unmodified>unmodified</Unmodified>
   <VirtualProp>original</VirtualProp>
 </PropertyGroup>
<ItemGroup>
  <Foo Include='foo'/>
  <Foo2 Include='foo2'/>    
</ItemGroup>
 <Target Name='test'>   
   <Message Text='[$(DeleteMe)]'/>
   <Message Text='[$(Unmodified)]'/>
   <Message Text='[$(VirtualProp)]'/>
   <Message Text='[$(NewProp)]'/>
   <Message Text='[@(Foo)]'/>
   <Message Text='[@(Foo2)]'/>
   <Message Text='[@(Baz)]'/>
 </Target>
</Project>
");

            string fileName = Path.GetTempFileName();
            File.WriteAllText(fileName, contents);
            try
            {
                Project project = new Project(fileName);
                ProjectInstance instance = project.CreateProjectInstance();
                instance.RemoveProperty("DeleteMe");
                instance.SetProperty("VirtualProp", "overridden");
                instance.SetProperty("NewProp", "new");
                instance.AddItem("Baz", "baz");
                instance.AddItem("Foo2", "foo21");
                foreach (var item in instance.Items)
                {
                    if (item.EvaluatedInclude == "foo")
                    {
                        instance.RemoveItem(item);
                        break;
                    }
                }

                BuildRequestData data = new BuildRequestData(instance, new string[0]);

                // Force this to build out-of-proc
                _parameters.DisableInProcNode = true;
                _buildManager.Build(_parameters, data);
                _logger.AssertLogDoesntContain("[deleteme]");
                _logger.AssertLogContains("[overridden]");
                _logger.AssertLogContains("[unmodified]");
                _logger.AssertLogContains("[new]");
                _logger.AssertLogDoesntContain("[foo]");
                _logger.AssertLogContains("[foo2;foo21]");
                _logger.AssertLogContains("[baz]");
            }
            finally
            {
                if (File.Exists(fileName))
                {
                    File.Delete(fileName);
                }
            }
        }

        /// <summary>
        /// Ensures that a limited set of properties are transferred from a project instance to an OOP node.
        /// </summary>
#if RUNTIME_TYPE_NETCORE
        [Fact(Skip = "https://github.com/Microsoft/msbuild/issues/933")]
#elif MONO
        [Fact(Skip = "https://github.com/Microsoft/msbuild/issues/1240")]
#else
        [Fact]
#endif
        public void ProjectInstanceLimitedTransferToOOPNode()
        {
            string contents = ObjectModelHelpers.CleanupFileContents(@"
<Project xmlns='msbuildnamespace' ToolsVersion='msbuilddefaulttoolsversion'>
 <PropertyGroup>
   <Unmodified>unmodified</Unmodified>
   <VirtualProp>original</VirtualProp>
 </PropertyGroup>
 <Target Name='test'>   
   <Message Text='[$(Unmodified)]'/>
   <Message Text='[$(VirtualProp)]'/>
 </Target>
</Project>
");

            string fileName = Path.GetTempFileName();
            File.WriteAllText(fileName, contents);
            try
            {
                Project project = new Project(fileName);
                ProjectInstance instance = project.CreateProjectInstance();
                instance.SetProperty("VirtualProp", "overridden");
                instance.SetProperty("Unmodified", "changed");

                BuildRequestData data = new BuildRequestData(instance, new string[0], null, BuildRequestDataFlags.None, new string[] { "VirtualProp" });

                // Force this to build out-of-proc
                _parameters.DisableInProcNode = true;
                _buildManager.Build(_parameters, data);
                _logger.AssertLogContains("[overridden]");
                _logger.AssertLogContains("[unmodified]");
                _logger.AssertLogDoesntContain("[changed]");
            }
            finally
            {
                if (File.Exists(fileName))
                {
                    File.Delete(fileName);
                }
            }
        }

        /// <summary>
        /// Tests that cache files are created as expected and their lifetime is controlled appropriately.
        /// </summary>
        [Fact]
        [Trait("Category", "netcore-osx-failing")]
        [Trait("Category", "netcore-linux-failing")]
        [Trait("Category", "mono-osx-failing")]
        public void CacheLifetime()
        {
            const string ForceCaching = "MSBUILDDEBUGFORCECACHING";
            FileUtilities.ClearCacheDirectory();
            string forceCachingValue = Environment.GetEnvironmentVariable(ForceCaching);
            try
            {
                Environment.SetEnvironmentVariable(ForceCaching, "1");
                string outerBuildCacheDirectory;
                string innerBuildCacheDirectory;

                // Do a build with one build manager.
                using (var outerBuildManager = new BuildManager())
                {
                    outerBuildCacheDirectory = BuildAndCheckCache(outerBuildManager, new string[] { });

                    // Do another build with a second build manager while the first still exists.  Since both BuildManagers
                    // share a process-wide cache directory, we want to verify that they don't stomp on each other, either
                    // by accidentally sharing results, or by clearing them away.
                    using (var innerBuildManager = new BuildManager())
                    {
                        innerBuildCacheDirectory = BuildAndCheckCache(innerBuildManager, new string[] { outerBuildCacheDirectory });

                        // Force the cache for this build manager (and only this build manager) to be cleared.  It should leave 
                        // behind the results from the other one.
                        innerBuildManager.ResetCaches();
                    }

                    Assert.False(Directory.Exists(innerBuildCacheDirectory)); // "Inner build cache directory still exists after inner build manager was disposed."
                    Assert.True(Directory.Exists(outerBuildCacheDirectory)); // "Outer build cache directory doesn't exist after inner build manager was disposed."

                    // Force the cache for this build manager to be cleared.
                    outerBuildManager.ResetCaches();
                }

                Assert.False(Directory.Exists(outerBuildCacheDirectory)); // "Outer build cache directory still exists after outer build manager was disposed."
            }
            finally
            {
                Environment.SetEnvironmentVariable(ForceCaching, forceCachingValue);
            }
        }

        /// <summary>
        /// If there's a P2P that otherwise succeeds, but has an AfterTarget that errors out, the 
        /// overall build result -- and thus the return value of the MSBuild task -- should reflect
        /// that failure. 
        /// </summary>
        [Fact]
        public void FailedAfterTargetInP2PShouldCauseOverallBuildFailure()
        {
            string projA = null;
            string projB = null;

            try
            {
                projA = FileUtilities.GetTemporaryFile(".proj");
                projB = FileUtilities.GetTemporaryFile(".proj");

                string contentsA = @"
<Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
  <Target Name=`Build`>
    <MSBuild Projects=`" + projB + @"` />
 
    <Warning Text=`We shouldn't reach here.` />
  </Target>    
</Project>
";

                string contentsB = @"
<Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
  <Target Name=`Build`>
    <Message Text=`Build` />
  </Target>

  <Target Name=`Error` AfterTargets=`Build`>
    <Error Text=`Error!` />
  </Target>
</Project>
";

                File.WriteAllText(projA, ObjectModelHelpers.CleanupFileContents(contentsA));
                File.WriteAllText(projB, ObjectModelHelpers.CleanupFileContents(contentsB));

                _buildManager.BeginBuild(_parameters);
                BuildRequestData data = new BuildRequestData(projA, new Dictionary<string, string>(), null, new[] { "Build" }, new HostServices());
                BuildResult result = _buildManager.PendBuildRequest(data).Execute();

                Assert.Equal(BuildResultCode.Failure, result.OverallResult);
                _logger.AssertNoWarnings();
            }
            finally
            {
                if (projA != null)
                {
                    FileUtilities.DeleteNoThrow(projA);
                }

                if (projB != null)
                {
                    FileUtilities.DeleteNoThrow(projB);
                }

                _buildManager.EndBuild();
            }
        }

        /// <summary>
        /// If there's a P2P that otherwise succeeds, but has an AfterTarget that errors out, the 
        /// overall build result -- and thus the return value of the MSBuild task -- should reflect
        /// that failure.  Specifically tests where there are multiple entrypoint targets with 
        /// AfterTargets, only one of which fails. 
        /// </summary>
        [Fact]
        [Trait("Category", "netcore-osx-failing")]
        [Trait("Category", "netcore-linux-failing")]
        public void FailedAfterTargetInP2PShouldCauseOverallBuildFailure_MultipleEntrypoints()
        {
            string projA = null;
            string projB = null;

            try
            {
                projA = FileUtilities.GetTemporaryFile(".proj");
                projB = FileUtilities.GetTemporaryFile(".proj");

                string contentsA = @"
<Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
  <Target Name=`Build`>
    <MSBuild Projects=`" + projB + @"` Targets=`Build;Build2` />
 
    <Warning Text=`We shouldn't reach here.` />
  </Target>    
</Project>
";

                string contentsB = @"
<Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
  <Target Name=`Build`>
    <Message Text=`[Build]` />
  </Target>

  <Target Name=`Build2`>
    <Message Text=`[Build2]` />
  </Target>

  <Target Name=`AT1` AfterTargets=`Build`>
    <Message Text=`[AT1]` />
  </Target>

  <Target Name=`AT2` AfterTargets=`Build2`>
    <Message Text=`[AT2]` />
  </Target>

  <Target Name=`Error` AfterTargets=`Build2`>
    <Error Text=`Error!` />
  </Target>
</Project>
";

                File.WriteAllText(projA, ObjectModelHelpers.CleanupFileContents(contentsA));
                File.WriteAllText(projB, ObjectModelHelpers.CleanupFileContents(contentsB));

                _buildManager.BeginBuild(_parameters);
                BuildRequestData data = new BuildRequestData(projA, new Dictionary<string, string>(), null, new[] { "Build" }, new HostServices());
                BuildResult result = _buildManager.PendBuildRequest(data).Execute();

                Assert.Equal(BuildResultCode.Failure, result.OverallResult);
                _logger.AssertNoWarnings();
                _logger.AssertLogContains("[Build]");
                _logger.AssertLogContains("[Build2]");
                _logger.AssertLogContains("[AT1]");
                _logger.AssertLogContains("[AT2]");
            }
            finally
            {
                if (projA != null)
                {
                    FileUtilities.DeleteNoThrow(projA);
                }

                if (projB != null)
                {
                    FileUtilities.DeleteNoThrow(projB);
                }

                _buildManager.EndBuild();
            }
        }

        /// <summary>
        /// If there's a P2P that otherwise succeeds, but has an AfterTarget that errors out, the 
        /// overall build result -- and thus the return value of the MSBuild task -- should reflect
        /// that failure. This should also be true if the AfterTarget is an AfterTarget of the 
        /// entrypoint target.
        /// </summary>
        [Fact]
        public void FailedNestedAfterTargetInP2PShouldCauseOverallBuildFailure()
        {
            string projA = null;
            string projB = null;

            try
            {
                projA = FileUtilities.GetTemporaryFile(".proj");
                projB = FileUtilities.GetTemporaryFile(".proj");

                string contentsA = @"
<Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
  <Target Name=`Build`>
    <MSBuild Projects=`" + projB + @"` />
 
    <Warning Text=`We shouldn't reach here.` />
  </Target>    
</Project>
";

                string contentsB = @"
<Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
  <Target Name=`Build`>
    <Message Text=`Build` />
  </Target>

  <Target Name=`Target1` AfterTargets=`Build`>
    <Message Text=`Target1` />
  </Target>

  <Target Name=`Error` AfterTargets=`Target1`>
    <Error Text=`Error!` />
  </Target>
</Project>
";

                File.WriteAllText(projA, ObjectModelHelpers.CleanupFileContents(contentsA));
                File.WriteAllText(projB, ObjectModelHelpers.CleanupFileContents(contentsB));

                _buildManager.BeginBuild(_parameters);
                BuildRequestData data = new BuildRequestData(projA, new Dictionary<string, string>(), null, new[] { "Build" }, new HostServices());
                BuildResult result = _buildManager.PendBuildRequest(data).Execute();

                Assert.Equal(BuildResultCode.Failure, result.OverallResult);
                _logger.AssertNoWarnings();
            }
            finally
            {
                if (projA != null)
                {
                    FileUtilities.DeleteNoThrow(projA);
                }

                if (projB != null)
                {
                    FileUtilities.DeleteNoThrow(projB);
                }

                _buildManager.EndBuild();
            }
        }

        /// <summary>
        /// If a project is called into twice, with two different entrypoint targets that 
        /// depend on non-overlapping sets of targets, and the first fails, the second 
        /// should not inherit that failure if all the targets it calls succeed. 
        /// </summary>
        [Fact]
        [Trait("Category", "netcore-osx-failing")]
        [Trait("Category", "netcore-linux-failing")]
        public void NonOverlappingEntrypointTargetsShouldNotInfluenceEachOthersResults()
        {
            string projA = null;
            string projB = null;

            try
            {
                projA = FileUtilities.GetTemporaryFile(".proj");
                projB = FileUtilities.GetTemporaryFile(".proj");

                string contentsA = @"
<Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
  <Target Name=`Build`>
  
   <Message Text=`The next MSBuild call should FAIL, but the build will continue.` />
   <MSBuild Projects=`" + projB + @"` Targets=`Build` ContinueOnError=`true` />
   <Message Text=`The next MSBuild call should SUCCEED without error.` />
   <MSBuild Projects=`" + projB + @"` Targets=`GetTargetPath` />
  </Target>
</Project>
";

                string contentsB = @"
<Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
  <Target Name=`Build`>
    <Error Text=`Forced error in Build` />
  </Target>
  
  
  <Target Name=`GetTargetPath`>
    <Message Text=`Success` />
  </Target>
</Project>
";

                File.WriteAllText(projA, ObjectModelHelpers.CleanupFileContents(contentsA));
                File.WriteAllText(projB, ObjectModelHelpers.CleanupFileContents(contentsB));

                _buildManager.BeginBuild(_parameters);
                BuildRequestData data = new BuildRequestData(projA, new Dictionary<string, string>(), null, new[] { "Build" }, new HostServices());
                BuildResult result = _buildManager.PendBuildRequest(data).Execute();

                Assert.Equal(BuildResultCode.Success, result.OverallResult);
                Assert.Equal(1, _logger.ErrorCount);
            }
            finally
            {
                if (projA != null)
                {
                    FileUtilities.DeleteNoThrow(projA);
                }

                if (projB != null)
                {
                    FileUtilities.DeleteNoThrow(projB);
                }

                _buildManager.EndBuild();
            }
        }

        /// <summary>
        /// In a situation where we have two requests calling into the same project, with different entry point 
        /// targets, one of which depends on "A;B", the other of which depends on "B", which has a dependency of 
        /// its own on "A", that we still properly build.  
        /// </summary>
#if RUNTIME_TYPE_NETCORE
        [Fact(Skip = "https://github.com/Microsoft/msbuild/issues/933")]
#elif MONO
        [Fact(Skip = "https://github.com/Microsoft/msbuild/issues/1240")]
#else
        [Fact]
#endif
        public void Regress473114()
        {
            string projA = null;
            string projB = null;
            string projC = null;
            string projD = null;

            try
            {
                projA = FileUtilities.GetTemporaryFile(".proj");
                projB = FileUtilities.GetTemporaryFile(".proj");
                projC = FileUtilities.GetTemporaryFile(".proj");
                projD = FileUtilities.GetTemporaryFile(".proj");

                string contentsA = @"<?xml version='1.0' encoding='utf-8'?>
<Project ToolsVersion='4.0' DefaultTargets='Build' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
  <ItemGroup>
    <ProjectReference Include='" + projD + @"' /> 
    <ProjectReference Include='" + projC + @"' />
    <ProjectReference Include='" + projB + @"' />
  </ItemGroup>

  <Target Name='Build'>
    <MSBuild Projects='@(ProjectReference)' BuildInParallel='true' />
  </Target>
</Project>
";

                string contentsB = @"<?xml version='1.0' encoding='utf-8'?>
<Project ToolsVersion='4.0' DefaultTargets='CallsGenerateImpLib' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
  <Target Name='CallsGenerateImpLib'>
    <MSBuild Projects='" + projC + @"' Targets='GenerateImpLib' BuildInParallel='true' />
  </Target>

</Project>
";

                string contentsC = @"<?xml version='1.0' encoding='utf-8'?>
<Project ToolsVersion='4.0' DefaultTargets='Default' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
  <Target Name='Default' DependsOnTargets='ResolveReferences;BuildCompile'>
    <Message Text='Executed Default' />
  </Target>

  <Target Name='GenerateImpLib' DependsOnTargets='BuildCompile'>
    <Message Text='Executed GenerateImpLib' />
  </Target>

  <Target Name='ResolveReferences'>
    <MSBuild Projects='" + projD + @"' Targets='BuildSlower' BuildInParallel='true' />
  </Target>

  <Target Name='BuildCompile' DependsOnTargets='ResolveReferences'>
    <Message Text='Executed BuildCompile' />
  </Target>

</Project>
";

                string contentsD = @"<?xml version='1.0' encoding='utf-8'?>
<Project ToolsVersion='4.0' DefaultTargets='Build' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
  <Target Name='Build'>
    <Message Text='In d.proj' />
  </Target>

  <Target Name='BuildSlower'>
    <Exec Command='" + Helpers.GetSleepCommand(TimeSpan.FromMilliseconds(500)) + @"' />
  </Target>
</Project>
";

                File.WriteAllText(projA, contentsA);
                File.WriteAllText(projB, contentsB);
                File.WriteAllText(projC, contentsC);
                File.WriteAllText(projD, contentsD);

                _parameters.MaxNodeCount = 3;
                _parameters.EnableNodeReuse = false;
                _buildManager.BeginBuild(_parameters);
                BuildRequestData data = new BuildRequestData(projA, new Dictionary<string, string>(), "4.0", new[] { "Build" }, new HostServices());
                BuildResult result = _buildManager.PendBuildRequest(data).Execute();

                Assert.Equal(BuildResultCode.Success, result.OverallResult);
            }
            finally
            {
                if (projA != null)
                {
                    FileUtilities.DeleteNoThrow(projA);
                }

                if (projB != null)
                {
                    FileUtilities.DeleteNoThrow(projB);
                }

                if (projC != null)
                {
                    FileUtilities.DeleteNoThrow(projC);
                }

                if (projD != null)
                {
                    FileUtilities.DeleteNoThrow(projD);
                }

                _buildManager.EndBuild();
            }
        }

        /// <summary>
        /// If two requests are made for the same project, and they call in with 
        /// just the right timing such that: 
        /// - request 1 builds for a while, reaches a P2P, and blocks
        /// - request 2 starts building, skips for a while, reaches the above P2P, and 
        ///   blocks waiting for request 1's results 
        /// - request 1 resumes building, errors, and exits
        /// - request 2 resumes building
        /// 
        /// Then request 2 should end up exiting in the same fashion.  
        /// 
        /// This simple test verifies that if there are two error targets in a row, the 
        /// second request will bail out where the first request did, as though it had 
        /// executed the target, rather than skipping and continuing. 
        /// </summary>
#if RUNTIME_TYPE_NETCORE
        [Fact(Skip = "https://github.com/Microsoft/msbuild/issues/933")]
#elif MONO
        [Fact(Skip = "https://github.com/Microsoft/msbuild/issues/1240")]
#else
        [Fact]
#endif
        public void VerifyMultipleRequestForSameProjectWithErrors_Simple()
        {
            string projA = null;
            string projB = null;
            string projC = null;

            try
            {
                projA = FileUtilities.GetTemporaryFile(".proj");
                projB = FileUtilities.GetTemporaryFile(".proj");
                projC = FileUtilities.GetTemporaryFile(".proj");

                string contentsA = @"
<Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
  <Target Name=`Build`>
    <MSBuild Projects=`" + String.Join(";", projB, projC, projB) + @"` BuildInParallel=`true` />
  </Target>
</Project>
";

                string contentsB = @"
<Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
  <Target Name=`Build` DependsOnTargets=`CallMSBuild;Error1;Error2` />
  <Target Name=`CallMSBuild`>
    <MSBuild Projects=`" + projC + @"` Targets=`Sleep` BuildInParallel=`true` />
  </Target>
  <Target Name=`Error1`>
    <Error Text=`Error 1` />
  </Target>
  <Target Name=`Error2`>
    <Error Text=`Error 2` />
  </Target>
</Project>
";

                string contentsC = @"
<Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>

  <Target Name=`Build`>
    <Message Text=`foo` />
  </Target>

  <Target Name=`Sleep`>
    <Exec Command=`" + Helpers.GetSleepCommand(TimeSpan.FromMilliseconds(500)) + @"` />
  </Target>
</Project>
";

                File.WriteAllText(projA, ObjectModelHelpers.CleanupFileContents(contentsA));
                File.WriteAllText(projB, ObjectModelHelpers.CleanupFileContents(contentsB));
                File.WriteAllText(projC, ObjectModelHelpers.CleanupFileContents(contentsC));

                _parameters.MaxNodeCount = 2;
                _parameters.EnableNodeReuse = false;
                _buildManager.BeginBuild(_parameters);
                BuildRequestData data = new BuildRequestData(projA, new Dictionary<string, string>(), null, new[] { "Build" }, new HostServices());
                BuildResult result = _buildManager.PendBuildRequest(data).Execute();

                Assert.Equal(BuildResultCode.Failure, result.OverallResult);

                // We should never get to Error2, because it's supposed to execute after Error1, which failed.  
                _logger.AssertLogDoesntContain("Error 2");

                // We should, however, end up skipping Error1 on the second call to B. 
                string skippedMessage = ResourceUtilities.FormatResourceString("TargetAlreadyCompleteFailure", "Error1");
                _logger.AssertLogContains(skippedMessage);
            }
            finally
            {
                if (projA != null)
                {
                    FileUtilities.DeleteNoThrow(projA);
                }

                if (projB != null)
                {
                    FileUtilities.DeleteNoThrow(projB);
                }

                if (projC != null)
                {
                    FileUtilities.DeleteNoThrow(projC);
                }

                _buildManager.EndBuild();
            }
        }

        /// <summary>
        /// If two requests are made for the same project, and they call in with 
        /// just the right timing such that: 
        /// - request 1 builds for a while, reaches a P2P, and blocks
        /// - request 2 starts building, skips for a while, reaches the above P2P, and 
        ///   blocks waiting for request 1's results 
        /// - request 1 resumes building, errors, and exits
        /// - request 2 resumes building
        /// 
        /// Then request 2 should end up exiting in the same fashion.  
        /// 
        /// This simple test verifies that if there are two error targets in a row, and the 
        /// first has a chain of OnError targets, the OnError targets will all execute as 
        /// expected in the first request, but be skipped by the second (since if it's "skipping 
        /// unsuccessful", it can assume that all other OnError targets have also already been run)
        /// </summary>
#if RUNTIME_TYPE_NETCORE
        [Fact(Skip = "https://github.com/Microsoft/msbuild/issues/933")]
#elif MONO
        [Fact(Skip = "https://github.com/Microsoft/msbuild/issues/1240")]
#else
        [Fact]
#endif
        public void VerifyMultipleRequestForSameProjectWithErrors_OnErrorChain()
        {
            string projA = null;
            string projB = null;
            string projC = null;

            try
            {
                projA = FileUtilities.GetTemporaryFile(".proj");
                projB = FileUtilities.GetTemporaryFile(".proj");
                projC = FileUtilities.GetTemporaryFile(".proj");

                string contentsA = @"
<Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
  <Target Name=`Build`>
    <MSBuild Projects=`" + String.Join(";", projB, projC, projB) + @"` BuildInParallel=`true` />
  </Target>
</Project>
";

                string contentsB = @"
<Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
  <Target Name=`Build` DependsOnTargets=`CallMSBuild;Error1;Error2` />
  <Target Name=`CallMSBuild`>
    <MSBuild Projects=`" + projC + @"` Targets=`Sleep` BuildInParallel=`true` />
  </Target>
  <Target Name=`Error1`>
    <Error Text=`Error 1` />
    <OnError ExecuteTargets=`Target2;Target3` />
  </Target>
  <Target Name=`Error2`>
    <Error Text=`Error 2` />
  </Target>

  <Target Name=`Target2`>
    <Error Text=`Error in Target2` />
    <OnError ExecuteTargets=`Target4` />
  </Target>

  <Target Name=`Target3`>
    <Message Text=`Target 3` />
  </Target>

  <Target Name=`Target4`>
    <Message Text=`Target 4` />
  </Target>

</Project>
";

                string contentsC = @"
<Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>

  <Target Name=`Build`>
    <Message Text=`foo` />
  </Target>

  <Target Name=`Sleep`>
    <Exec Command=`" + Helpers.GetSleepCommand(TimeSpan.FromMilliseconds(500)) + @"` />
  </Target>
</Project>
";

                File.WriteAllText(projA, ObjectModelHelpers.CleanupFileContents(contentsA));
                File.WriteAllText(projB, ObjectModelHelpers.CleanupFileContents(contentsB));
                File.WriteAllText(projC, ObjectModelHelpers.CleanupFileContents(contentsC));

                _parameters.MaxNodeCount = 2;
                _parameters.EnableNodeReuse = false;
                _buildManager.BeginBuild(_parameters);
                BuildRequestData data = new BuildRequestData(projA, new Dictionary<string, string>(), null, new[] { "Build" }, new HostServices());
                BuildResult result = _buildManager.PendBuildRequest(data).Execute();

                Assert.Equal(BuildResultCode.Failure, result.OverallResult);

                // We should never get to Error2, because it's supposed to execute after Error1, which failed.  
                _logger.AssertLogDoesntContain("Error 2");

                // We should, however, get to Target2, Target3, and Target4, since they're part of the OnError
                // chain for Error1
                _logger.AssertLogContains("Error in Target2");
                _logger.AssertLogContains("Target 3");
                _logger.AssertLogContains("Target 4");

                // We should end up skipping Error1 on the second call to B. 
                string skippedMessage1 = ResourceUtilities.FormatResourceString("TargetAlreadyCompleteFailure", "Error1");
                _logger.AssertLogContains(skippedMessage1);

                // We shouldn't, however, see skip messages for the OnError targets
                string skippedMessage2 = ResourceUtilities.FormatResourceString("TargetAlreadyCompleteFailure", "Target2");
                _logger.AssertLogDoesntContain(skippedMessage2);

                string skippedMessage3 = ResourceUtilities.FormatResourceString("TargetAlreadyCompleteSuccess", "Target3");
                _logger.AssertLogDoesntContain(skippedMessage3);

                string skippedMessage4 = ResourceUtilities.FormatResourceString("TargetAlreadyCompleteSuccess", "Target4");
                _logger.AssertLogDoesntContain(skippedMessage4);
            }
            finally
            {
                if (projA != null)
                {
                    FileUtilities.DeleteNoThrow(projA);
                }

                if (projB != null)
                {
                    FileUtilities.DeleteNoThrow(projB);
                }

                if (projC != null)
                {
                    FileUtilities.DeleteNoThrow(projC);
                }

                _buildManager.EndBuild();
            }
        }

        /// <summary>
        /// If two requests are made for the same project, and they call in with 
        /// just the right timing such that: 
        /// - request 1 builds for a while, reaches a P2P, and blocks
        /// - request 2 starts building, skips for a while, reaches the above P2P, and 
        ///   blocks waiting for request 1's results 
        /// - request 1 resumes building, errors, and exits
        /// - request 2 resumes building
        /// 
        /// Then request 2 should end up exiting in the same fashion.  
        /// 
        /// This simple test verifies that if there are two error targets in a row, AND 
        /// they're marked as ContinueOnError=ErrorAndContinue, then we won't bail, but 
        /// will continue executing (on the first request) or skipping (on the second)
        /// </summary>
#if RUNTIME_TYPE_NETCORE
        [Fact(Skip = "https://github.com/Microsoft/msbuild/issues/933")]
#elif MONO
        [Fact(Skip = "https://github.com/Microsoft/msbuild/issues/1240")]
#else
        [Fact]
#endif
        public void VerifyMultipleRequestForSameProjectWithErrors_ErrorAndContinue()
        {
            string projA = null;
            string projB = null;
            string projC = null;

            try
            {
                projA = FileUtilities.GetTemporaryFile(".proj");
                projB = FileUtilities.GetTemporaryFile(".proj");
                projC = FileUtilities.GetTemporaryFile(".proj");

                string contentsA = @"
<Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
  <Target Name=`Build`>
    <MSBuild Projects=`" + String.Join(";", projB, projC, projB) + @"` BuildInParallel=`true` />
  </Target>
</Project>
";

                string contentsB = @"
<Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
  <Target Name=`Build` DependsOnTargets=`CallMSBuild;Error1;Error2` />
  <Target Name=`CallMSBuild`>
    <MSBuild Projects=`" + projC + @"` Targets=`Sleep` BuildInParallel=`true` />
  </Target>
  <Target Name=`Error1`>
    <Error Text=`Error 1` ContinueOnError=`ErrorAndContinue` />
  </Target>
  <Target Name=`Error2`>
    <Error Text=`Error 2` />
  </Target>
</Project>
";

                string contentsC = @"
<Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>

  <Target Name=`Build`>
    <Message Text=`foo` />
  </Target>

  <Target Name=`Sleep`>
    <Exec Command=`" + Helpers.GetSleepCommand(TimeSpan.FromMilliseconds(500)) + @"` />
  </Target>
</Project>
";

                File.WriteAllText(projA, ObjectModelHelpers.CleanupFileContents(contentsA));
                File.WriteAllText(projB, ObjectModelHelpers.CleanupFileContents(contentsB));
                File.WriteAllText(projC, ObjectModelHelpers.CleanupFileContents(contentsC));

                _parameters.MaxNodeCount = 2;
                _parameters.EnableNodeReuse = false;
                _buildManager.BeginBuild(_parameters);
                BuildRequestData data = new BuildRequestData(projA, new Dictionary<string, string>(), null, new[] { "Build" }, new HostServices());
                BuildResult result = _buildManager.PendBuildRequest(data).Execute();

                Assert.Equal(BuildResultCode.Failure, result.OverallResult);

                // We should see both Error1 and Error2
                _logger.AssertLogContains("Error 1");
                _logger.AssertLogContains("Error 2");

                // We should also end up skipping them both. 
                string skippedMessage1 = ResourceUtilities.FormatResourceString("TargetAlreadyCompleteFailure", "Error1");
                _logger.AssertLogContains(skippedMessage1);

                string skippedMessage2 = ResourceUtilities.FormatResourceString("TargetAlreadyCompleteFailure", "Error2");
                _logger.AssertLogContains(skippedMessage2);
            }
            finally
            {
                if (projA != null)
                {
                    FileUtilities.DeleteNoThrow(projA);
                }

                if (projB != null)
                {
                    FileUtilities.DeleteNoThrow(projB);
                }

                if (projC != null)
                {
                    FileUtilities.DeleteNoThrow(projC);
                }

                _buildManager.EndBuild();
            }
        }

        /// <summary>
        /// If two requests are made for the same project, and they call in with 
        /// just the right timing such that: 
        /// - request 1 builds for a while, reaches a P2P, and blocks
        /// - request 2 starts building, skips for a while, reaches the above P2P, and 
        ///   blocks waiting for request 1's results 
        /// - request 1 resumes building, errors, and exits
        /// - request 2 resumes building
        /// 
        /// Then request 2 should end up exiting in the same fashion.  
        /// 
        /// This test verifies that if the errors are in AfterTargets, we still 
        /// exit as though the target that those targets run after has already run. 
        /// </summary>
#if RUNTIME_TYPE_NETCORE
        [Fact(Skip = "https://github.com/Microsoft/msbuild/issues/933")]
#elif MONO
        [Fact(Skip = "https://github.com/Microsoft/msbuild/issues/1240")]
#else
        [Fact]
#endif
        public void VerifyMultipleRequestForSameProjectWithErrors_AfterTargets()
        {
            string projA = null;
            string projB = null;
            string projC = null;

            try
            {
                projA = FileUtilities.GetTemporaryFile(".proj");
                projB = FileUtilities.GetTemporaryFile(".proj");
                projC = FileUtilities.GetTemporaryFile(".proj");

                string contentsA = @"
<Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
  <Target Name=`Build`>
    <MSBuild Projects=`" + String.Join(";", projB, projC, projB) + @"` BuildInParallel=`true` />
  </Target>
</Project>
";

                string contentsB = @"
<Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
  <Target Name=`Build` DependsOnTargets=`CallMSBuild` />
  <Target Name=`CallMSBuild`>
    <MSBuild Projects=`" + projC + @"` Targets=`Sleep` BuildInParallel=`true` />
  </Target>
  <Target Name=`Error1` AfterTargets=`CallMSBuild`>
    <Error Text=`Error 1` />
  </Target>
  <Target Name=`Error2` AfterTargets=`CallMSBuild`>
    <Error Text=`Error 2` />
  </Target>
</Project>
";

                string contentsC = @"
<Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>

  <Target Name=`Build`>
    <Message Text=`foo` />
  </Target>

  <Target Name=`Sleep`>
    <Exec Command=`" + Helpers.GetSleepCommand(TimeSpan.FromMilliseconds(500)) + @"` />
  </Target>
</Project>
";

                File.WriteAllText(projA, ObjectModelHelpers.CleanupFileContents(contentsA));
                File.WriteAllText(projB, ObjectModelHelpers.CleanupFileContents(contentsB));
                File.WriteAllText(projC, ObjectModelHelpers.CleanupFileContents(contentsC));

                _parameters.MaxNodeCount = 2;
                _parameters.EnableNodeReuse = false;
                _buildManager.BeginBuild(_parameters);
                BuildRequestData data = new BuildRequestData(projA, new Dictionary<string, string>(), null, new[] { "Build" }, new HostServices());
                BuildResult result = _buildManager.PendBuildRequest(data).Execute();

                Assert.Equal(BuildResultCode.Failure, result.OverallResult);

                // We should never get to Error2, because we should never run its AfterTarget, after 
                // the AfterTarget with Error1 failed
                _logger.AssertLogDoesntContain("Error 2");
            }
            finally
            {
                if (projA != null)
                {
                    FileUtilities.DeleteNoThrow(projA);
                }

                if (projB != null)
                {
                    FileUtilities.DeleteNoThrow(projB);
                }

                if (projC != null)
                {
                    FileUtilities.DeleteNoThrow(projC);
                }

                _buildManager.EndBuild();
            }
        }

        /// <summary>
        /// Related to the two tests above, if two requests are made for the same project, but 
        /// for different entry targets, and a target fails in the first request, if the second 
        /// request also runs that target, its skip-unsuccessful should behave in the same 
        /// way as if the target had actually errored. 
        /// </summary>
        [Fact]
        [Trait("Category", "netcore-osx-failing")]
        [Trait("Category", "netcore-linux-failing")]
        public void VerifyMultipleRequestForSameProjectWithErrors_DifferentEntrypoints()
        {
            string projA = null;
            string projB = null;

            try
            {
                projA = FileUtilities.GetTemporaryFile(".proj");
                projB = FileUtilities.GetTemporaryFile(".proj");

                string contentsA = @"
<Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
  <ItemGroup> 
    <PR Include=`" + projB + @"`>
      <Targets>Build</Targets>
    </PR>
    <PR Include=`" + projB + @"`>
      <Targets>Build2</Targets>
    </PR>
  </ItemGroup>

  <Target Name=`Build`>
    <MSBuild Projects=`@(PR)` Targets=`%(PR.Targets)` ContinueOnError=`true` />
  </Target>
</Project>
";

                string contentsB = @"
<Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
  <Target Name=`Build` DependsOnTargets=`Target1;Error1`>
    <Message Text=`[Build]` />
  </Target>
  <Target Name=`Build2` DependsOnTargets=`Target2;Error1;Error2`>
    <Message Text=`[Build2]` />
  </Target>
  <Target Name=`Target1`>
    <Message Text=`[Target1]` />
  </Target>
  <Target Name=`Target2`>
    <Message Text=`[Target2]` />
  </Target>
  <Target Name=`Error1`>
    <Error Text=`[Error1]` />
  </Target>
  <Target Name=`Error2`>
    <Error Text=`[Error2]` />
  </Target>
</Project>
";

                File.WriteAllText(projA, ObjectModelHelpers.CleanupFileContents(contentsA));
                File.WriteAllText(projB, ObjectModelHelpers.CleanupFileContents(contentsB));

                _buildManager.BeginBuild(_parameters);
                BuildRequestData data = new BuildRequestData(projA, new Dictionary<string, string>(), null, new[] { "Build" }, new HostServices());
                BuildResult result = _buildManager.PendBuildRequest(data).Execute();

                Assert.Equal(BuildResultCode.Success, result.OverallResult);

                // We should never get to Error2, because it's only ever executed in the second 
                // request after Error1, which should skip-unsuccessful and exit
                _logger.AssertLogDoesntContain("[Error2]");
            }
            finally
            {
                if (projA != null)
                {
                    FileUtilities.DeleteNoThrow(projA);
                }

                if (projB != null)
                {
                    FileUtilities.DeleteNoThrow(projB);
                }

                _buildManager.EndBuild();
            }
        }

        /// <summary>
        /// Verify that we can submit multiple simultaneous submissions with 
        /// legacy threading mode active and successfully build.
        /// </summary>
        [Fact]
        public void TestSimultaneousSubmissionsWithLegacyThreadingData()
        {
            string projectPath1 = null;
            string projectPath2 = null;

            try
            {
                string projectContent = @"<Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
    <Target Name=`Build`>
        <!-- Wait 200 ms -->
        <Exec Command=`" + Helpers.GetSleepCommand(TimeSpan.FromMilliseconds(200)) + @"` />
    </Target>

</Project>
";
                projectPath1 = Path.GetTempFileName();
                File.WriteAllText(projectPath1, ObjectModelHelpers.CleanupFileContents(projectContent));

                Project project1 = new Project(projectPath1);

                projectPath2 = Path.GetTempFileName();
                File.WriteAllText(projectPath2, ObjectModelHelpers.CleanupFileContents(projectContent));

                Project project2 = new Project(projectPath2);

                ConsoleLogger cl = new ConsoleLogger();
                BuildParameters buildParameters = new BuildParameters(ProjectCollection.GlobalProjectCollection);
                buildParameters.Loggers = new ILogger[] { cl };
                buildParameters.LegacyThreadingSemantics = true;
                BuildManager.DefaultBuildManager.BeginBuild(buildParameters);

                AutoResetEvent project1DoneEvent = new AutoResetEvent(false);
                ThreadPool.QueueUserWorkItem(delegate
                {
                    ProjectInstance pi = BuildManager.DefaultBuildManager.GetProjectInstanceForBuild(project1);

                    BuildRequestData requestData = new BuildRequestData(pi, new string[] { "Build" });
                    BuildSubmission submission = BuildManager.DefaultBuildManager.PendBuildRequest(requestData);
                    BuildResult br = submission.Execute();
                    project1DoneEvent.Set();
                });

                AutoResetEvent project2DoneEvent = new AutoResetEvent(false);
                ThreadPool.QueueUserWorkItem(delegate
                {
                    ProjectInstance pi = BuildManager.DefaultBuildManager.GetProjectInstanceForBuild(project2);
                    BuildRequestData requestData = new BuildRequestData(pi, new string[] { "Build" });
                    BuildSubmission submission = BuildManager.DefaultBuildManager.PendBuildRequest(requestData);
                    BuildResult br = submission.Execute();
                    project2DoneEvent.Set();
                });

                project1DoneEvent.WaitOne();
                project2DoneEvent.WaitOne();

                BuildManager.DefaultBuildManager.EndBuild();
            }
            finally
            {
                if (projectPath1 != null)
                {
                    File.Delete(projectPath1);
                }

                if (projectPath2 != null)
                {
                    File.Delete(projectPath2);
                }
            }
        }

        /// <summary>
        /// Verify that we can submit multiple simultaneous submissions with 
        /// legacy threading mode active and successfully build, and that one of those
        /// submissions can P2P to the other.
        /// </summary>
        [Fact]
        [Trait("Category", "netcore-osx-failing")]
        [Trait("Category", "netcore-linux-failing")]
        public void TestSimultaneousSubmissionsWithLegacyThreadingData_P2P()
        {
            string projectPath1 = null;
            string projectPath2 = null;

            try
            {
                string projectContent1 = @"<Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
    <Target Name=`CopyRunEnvironmentFiles`>
        <!-- Wait 100 ms -->
        <Exec Command=`" + Helpers.GetSleepCommand(TimeSpan.FromMilliseconds(100)) + @"` />
    </Target>

    <Target Name=`MyConsoleTarget`>
        <!-- Wait 100 ms -->
        <Exec Command=`" + Helpers.GetSleepCommand(TimeSpan.FromMilliseconds(100)) + @"` />
    </Target>

</Project>
";

                projectPath1 = FileUtilities.GetTemporaryFile();
                File.WriteAllText(projectPath1, ObjectModelHelpers.CleanupFileContents(projectContent1));

                Project project1 = new Project(projectPath1);

                string projectContent2 = @"<Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
    <Target Name=`MSDeployPublish` />

    <Target Name=`DoStuff` AfterTargets=`MSDeployPublish`>
        <MSBuild Projects=`" + projectPath1 + @"` Targets=`MyConsoleTarget` />
    </Target>

</Project>
";

                projectPath2 = FileUtilities.GetTemporaryFile();
                File.WriteAllText(projectPath2, ObjectModelHelpers.CleanupFileContents(projectContent2));

                Project project2 = new Project(projectPath2);

                ConsoleLogger cl = new ConsoleLogger();
                BuildParameters buildParameters = new BuildParameters(ProjectCollection.GlobalProjectCollection);
                buildParameters.Loggers = new ILogger[] { cl };
                buildParameters.LegacyThreadingSemantics = true;
                BuildManager.DefaultBuildManager.BeginBuild(buildParameters);

                AutoResetEvent project1DoneEvent = new AutoResetEvent(false);
                ThreadPool.QueueUserWorkItem(delegate
                {
                    // need to kick off project 2 first so that it project 1 can get submitted before the P2P happens
                    ProjectInstance pi = BuildManager.DefaultBuildManager.GetProjectInstanceForBuild(project2);

                    BuildRequestData requestData = new BuildRequestData(pi, new[] { "MSDeployPublish" });
                    BuildSubmission submission = BuildManager.DefaultBuildManager.PendBuildRequest(requestData);
                    BuildResult br = submission.Execute();
                    Assert.Equal(BuildResultCode.Success, br.OverallResult);
                    project1DoneEvent.Set();
                });

                AutoResetEvent project2DoneEvent = new AutoResetEvent(false);
                ThreadPool.QueueUserWorkItem(delegate
                {
                    ProjectInstance pi = BuildManager.DefaultBuildManager.GetProjectInstanceForBuild(project1);
                    BuildRequestData requestData = new BuildRequestData(pi, new string[] { "CopyRunEnvironmentFiles" });
                    BuildSubmission submission = BuildManager.DefaultBuildManager.PendBuildRequest(requestData);
                    BuildResult br = submission.Execute();
                    Assert.Equal(BuildResultCode.Success, br.OverallResult);
                    project2DoneEvent.Set();
                });

                project1DoneEvent.WaitOne();
                project2DoneEvent.WaitOne();

                BuildManager.DefaultBuildManager.EndBuild();
            }
            finally
            {
                if (projectPath1 != null)
                {
                    File.Delete(projectPath1);
                }

                if (projectPath2 != null)
                {
                    File.Delete(projectPath2);
                }
            }
        }

        /// <summary>
        /// Verify that we can submit multiple simultaneous submissions with 
        /// legacy threading mode active and successfully build, and that one of those
        /// submissions can P2P to the other.
        /// 
        /// A variation of the above test, where multiple nodes are available, so the 
        /// submissions aren't restricted to running strictly serially by the single in-proc 
        /// node.
        /// </summary>
#if RUNTIME_TYPE_NETCORE
        [Fact(Skip = "https://github.com/Microsoft/msbuild/issues/933")]
#elif MONO
        [Fact(Skip = "https://github.com/Microsoft/msbuild/issues/1245")]
#else
        [Fact]
#endif
        public void TestSimultaneousSubmissionsWithLegacyThreadingData_P2P_MP()
        {
            string projectPath1 = null;
            string projectPath2 = null;

            try
            {
                string projectContent1 = @"<Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
    <Target Name=`CopyRunEnvironmentFiles`>
        <!-- Wait 100 ms -->
        <Exec Command=`" + Helpers.GetSleepCommand(TimeSpan.FromMilliseconds(100)) + @"` />
    </Target>

    <Target Name=`MyConsoleTarget`>
        <!-- Wait 100 ms -->
        <Exec Command=`" + Helpers.GetSleepCommand(TimeSpan.FromMilliseconds(100)) + @"` />
    </Target>

</Project>
";

                projectPath1 = FileUtilities.GetTemporaryFile();
                File.WriteAllText(projectPath1, ObjectModelHelpers.CleanupFileContents(projectContent1));

                Project project1 = new Project(projectPath1);

                string projectContent2 = @"<Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
    <Target Name=`MSDeployPublish` />

    <Target Name=`DoStuff` AfterTargets=`MSDeployPublish`>
        <MSBuild Projects=`" + projectPath1 + @"` Targets=`MyConsoleTarget` />
    </Target>

</Project>
";

                projectPath2 = FileUtilities.GetTemporaryFile();
                File.WriteAllText(projectPath2, ObjectModelHelpers.CleanupFileContents(projectContent2));

                Project project2 = new Project(projectPath2);

                ConsoleLogger cl = new ConsoleLogger();
                BuildParameters buildParameters = new BuildParameters(ProjectCollection.GlobalProjectCollection);
                buildParameters.Loggers = new ILogger[] { cl };
                buildParameters.LegacyThreadingSemantics = true;
                buildParameters.MaxNodeCount = 2;
                buildParameters.EnableNodeReuse = false;
                BuildManager.DefaultBuildManager.BeginBuild(buildParameters);

                AutoResetEvent project1DoneEvent = new AutoResetEvent(false);
                ThreadPool.QueueUserWorkItem(delegate
                {
                    // need to kick off project 2 first so that it project 1 can get submitted before the P2P happens
                    ProjectInstance pi = BuildManager.DefaultBuildManager.GetProjectInstanceForBuild(project2);

                    BuildRequestData requestData = new BuildRequestData(pi, new string[] { "MSDeployPublish" });
                    BuildSubmission submission = BuildManager.DefaultBuildManager.PendBuildRequest(requestData);
                    BuildResult br = submission.Execute();
                    Assert.Equal(BuildResultCode.Success, br.OverallResult);
                    project1DoneEvent.Set();
                });

                AutoResetEvent project2DoneEvent = new AutoResetEvent(false);
                ThreadPool.QueueUserWorkItem(delegate
                {
                    ProjectInstance pi = BuildManager.DefaultBuildManager.GetProjectInstanceForBuild(project1);
                    BuildRequestData requestData = new BuildRequestData(pi, new string[] { "CopyRunEnvironmentFiles" });
                    BuildSubmission submission = BuildManager.DefaultBuildManager.PendBuildRequest(requestData);
                    BuildResult br = submission.Execute();
                    Assert.Equal(BuildResultCode.Success, br.OverallResult);
                    project2DoneEvent.Set();
                });

                project1DoneEvent.WaitOne();
                project2DoneEvent.WaitOne();

                BuildManager.DefaultBuildManager.EndBuild();
            }
            finally
            {
                if (projectPath1 != null)
                {
                    File.Delete(projectPath1);
                }

                if (projectPath2 != null)
                {
                    File.Delete(projectPath2);
                }
            }
        }

        /// <summary>
        /// Ensures that properties and items are transferred from an out-of-proc project to an in-proc project.
        /// </summary>
        /// <remarks>
        /// This differs from transferring a project instance to an out-of-proc node because in this case the project
        /// was loaded by MSBuild, not supplied directly by the user.
        /// </remarks>
#if RUNTIME_TYPE_NETCORE
        [Fact(Skip = "https://github.com/Microsoft/msbuild/issues/933")]
#else
        [Fact]
        [Trait("Category", "mono-osx-failing")]
#endif
        public void Regress265010()
        {
            string contents = ObjectModelHelpers.CleanupFileContents(@"
<Project xmlns='msbuildnamespace' ToolsVersion='msbuilddefaulttoolsversion'>
 <PropertyGroup>
   <Prop>BaseValue</Prop>
 </PropertyGroup>
<ItemGroup>
  <Item Include='BaseItem'/>
</ItemGroup>
 <Target Name='BaseTest'>   
   <Message Text='[$(Prop)]'/>
   <Message Text='[@(Item)]'/>
    <PropertyGroup>
        <Prop>NewValue</Prop>
    </PropertyGroup>
    <ItemGroup>
        <Item Include='NewItem'/>
    </ItemGroup>
 </Target>

 <Target Name='MovedTest'>   
   <Message Text='[$(Prop)]'/>
   <Message Text='[@(Item)]'/>
 </Target>

</Project>
");

            string fileName = Path.GetTempFileName();
            File.WriteAllText(fileName, contents);
            _buildManager.BeginBuild(_parameters);
            try
            {
                HostServices services = new HostServices();
                services.SetNodeAffinity(fileName, NodeAffinity.OutOfProc);
                BuildRequestData data = new BuildRequestData(fileName, new Dictionary<string, string>(), ObjectModelHelpers.MSBuildDefaultToolsVersion, new[] { "BaseTest" }, services);
                _buildManager.PendBuildRequest(data).Execute();
                _logger.AssertLogContains("[BaseValue]");
                _logger.AssertLogContains("[BaseItem]");
                _logger.ClearLog();

                _parameters.ResetCaches = false;
                services.SetNodeAffinity(fileName, NodeAffinity.InProc);
                data = new BuildRequestData(fileName, new Dictionary<string, string>(), ObjectModelHelpers.MSBuildDefaultToolsVersion, new[] { "MovedTest" }, services);
                _buildManager.PendBuildRequest(data).Execute();
                _logger.AssertLogContains("[NewValue]");
                _logger.AssertLogContains("[BaseItem;NewItem]");
                _logger.AssertLogDoesntContain("[BaseValue]");
            }
            finally
            {
                if (File.Exists(fileName))
                {
                    File.Delete(fileName);
                }

                _buildManager.EndBuild();
            }
        }

        /// <summary>
        /// Verifies that all warnings are treated as errors and that the overall build result is a failure.
        /// </summary>
        [Fact]
        public void WarningsAreTreatedAsErrorsAll()
        {
            string contents = ObjectModelHelpers.CleanupFileContents(@"
<Project xmlns='msbuildnamespace' ToolsVersion='msbuilddefaulttoolsversion'>
 <Target Name='target1'>
    <Warning Text='This warning should be treated as an error' Code='ABC123'/>
    <Warning Text='This warning should NOT be treated as an error' />
 </Target>
</Project>
");
            _parameters.WarningsAsErrors = new HashSet<string>();

            Project project = CreateProject(contents, ObjectModelHelpers.MSBuildDefaultToolsVersion, _projectCollection, true);
            ProjectInstance instance = _buildManager.GetProjectInstanceForBuild(project);
            _buildManager.BeginBuild(_parameters);
            BuildResult result1 = _buildManager.BuildRequest(new BuildRequestData(instance, new string[] { "target1" }));
            _buildManager.EndBuild();

            Assert.Equal(0, _logger.WarningCount);
            Assert.Equal(2, _logger.ErrorCount);

            Assert.Equal(BuildResultCode.Failure, result1.OverallResult);
            Assert.True(result1.HasResultsForTarget("target1"));
        }

        /// <summary>
        /// Verifies that only the specified warnings are treated as errors and that the overall build result is a failure.
        /// </summary>
        [Fact]
        public void WarningsAreTreatedAsErrorsSpecific()
        {
            string contents = ObjectModelHelpers.CleanupFileContents(@"
<Project xmlns='msbuildnamespace' ToolsVersion='msbuilddefaulttoolsversion'>
 <Target Name='target1'>
    <Warning Text='This warning should be treated as an error' Code='ABC123'/>
    <Warning Text='This warning should NOT be treated as an error' Code='NA123' />
    <Warning Text='This warning should NOT be treated as an error' />
 </Target>
</Project>
");
            _parameters.WarningsAsErrors = new HashSet<string> { "ABC123" };

            Project project = CreateProject(contents, ObjectModelHelpers.MSBuildDefaultToolsVersion, _projectCollection, true);
            ProjectInstance instance = _buildManager.GetProjectInstanceForBuild(project);
            _buildManager.BeginBuild(_parameters);
            BuildResult result1 = _buildManager.BuildRequest(new BuildRequestData(instance, new string[] { "target1" }));
            _buildManager.EndBuild();

            Assert.Equal(2, _logger.WarningCount);
            Assert.Equal(1, _logger.ErrorCount);

            Assert.Equal(BuildResultCode.Failure, result1.OverallResult);
            Assert.True(result1.HasResultsForTarget("target1"));
        }

        /// <summary>
        /// Verifies that when building targets which emit warnings, they still show as succeeding but the overall build result is a failure.
        /// </summary>
        [Fact]
        public void WarningsAreTreatedAsErrorsButTargetsStillSucceed()
        {
            string contents = ObjectModelHelpers.CleanupFileContents(@"
<Project xmlns='msbuildnamespace' ToolsVersion='msbuilddefaulttoolsversion'>
<Target Name='target1'>
    <Message Text='text'/>
 </Target>
 <Target Name='target2'>
    <Warning Text='This warning should be treated as an error' Code='ABC123'/>
 </Target>
</Project>
");
            _parameters.WarningsAsErrors = new HashSet<string> { "ABC123" };

            Project project = CreateProject(contents, ObjectModelHelpers.MSBuildDefaultToolsVersion, _projectCollection, true);
            ProjectInstance instance = _buildManager.GetProjectInstanceForBuild(project);
            _buildManager.BeginBuild(_parameters);
            BuildResult buildResult = _buildManager.BuildRequest(new BuildRequestData(instance, new string[] { "target1", "target2" }));
            _buildManager.EndBuild();

            Assert.Equal(0, _logger.WarningCount);
            Assert.Equal(1, _logger.ErrorCount);

            Assert.Equal(BuildResultCode.Failure, buildResult.OverallResult);
            Assert.True(buildResult.HasResultsForTarget("target1"));
            Assert.True(buildResult.HasResultsForTarget("target2"));
            // The two targets should still show as success because they don't know their warning was changed to an error
            // Logging a warning as an error does not change execution, only the final result of the build
            Assert.Equal(TargetResultCode.Success, buildResult.ResultsByTarget["target1"].ResultCode);
            Assert.Equal(TargetResultCode.Success, buildResult.ResultsByTarget["target2"].ResultCode);
        }

        /// <summary>
        /// Helper for cache tests.  Builds a project and verifies the right cache files are created.
        /// </summary>
        private string BuildAndCheckCache(BuildManager localBuildManager, IEnumerable<string> exceptCacheDirectories)
        {
            string contents = ObjectModelHelpers.CleanupFileContents(@"
<Project xmlns='msbuildnamespace' ToolsVersion='msbuilddefaulttoolsversion'>
 <Target Name='One' Outputs='one.txt'>   
 </Target>

 <Target Name='Two' Outputs='two.txt'>   
 </Target>

 <Target Name='Three' Outputs='three.txt'>   
 </Target>
</Project>
");
            string fileName = Path.GetTempFileName();
            File.WriteAllText(fileName, contents);

            string cacheDirectory = FileUtilities.GetCacheDirectory();

            BuildParameters parameters = new BuildParameters();
            localBuildManager.BeginBuild(parameters);
            try
            {
                var services = new HostServices();
                BuildRequestData data = new BuildRequestData(fileName, new Dictionary<string, string>(), ObjectModelHelpers.MSBuildDefaultToolsVersion, new[] { "One", "Two", "Three" }, services);
                var result = localBuildManager.PendBuildRequest(data).Execute();
                Assert.Equal(result.OverallResult, BuildResultCode.Success); // "Test project failed to build correctly."
            }
            finally
            {
                localBuildManager.EndBuild();
            }

            // Ensure that we got the cache files we expected.  There should be one set of results in there, once we exclude
            // any of the specified directories from previous builds in the same test.
            string directory = Directory.EnumerateDirectories(cacheDirectory).Except(exceptCacheDirectories).First();

            // Within this directory should be a set of target results files, one for each of the targets we invoked.
            var resultsFiles = Directory.EnumerateFiles(directory).Select(Path.GetFileName);

            Assert.Equal(3, resultsFiles.Count());
            Assert.True(resultsFiles.Contains("One.cache"));
            Assert.True(resultsFiles.Contains("Two.cache"));
            Assert.True(resultsFiles.Contains("Three.cache"));

            // Return the cache directory created for this build.
            return directory;
        }

        /// <summary>
        /// Extract a string dictionary from the property enumeration on a project started event.
        /// </summary>
        private Dictionary<string, string> ExtractProjectStartedPropertyList(IEnumerable properties)
        {
            // Gather a sorted list of all the properties.
            Dictionary<string, string> list = null;

            if (properties != null)
            {
                foreach (DictionaryEntry prop in properties)
                {
                    if (list == null)
                    {
                        list = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    }

                    list.Add((string)prop.Key, (string)prop.Value);
                }
            }

            return list;
        }

        /// <summary>
        /// Retrieves a BuildRequestData using the specified contents, default targets and an empty project collection.
        /// </summary>
        private BuildRequestData GetBuildRequestData(string projectContents)
        {
            return GetBuildRequestData(projectContents, new string[] { });
        }

        /// <summary>
        /// Retrieves a BuildRequestData using the specified contents and targets with an empty project collection.
        /// </summary>
        private BuildRequestData GetBuildRequestData(string projectContents, string[] targets)
        {
            return GetBuildRequestData(projectContents, targets, null);
        }

        /// <summary>
        /// Retrieves a BuildRequestData using the specified contents, targets and project collection.
        /// </summary>
        private BuildRequestData GetBuildRequestData(string projectContents, string[] targets, string toolsVersion)
        {
            BuildRequestData data = new BuildRequestData(CreateProjectInstance(projectContents, toolsVersion, _projectCollection, true), targets, _projectCollection.HostServices);
            return data;
        }

        /// <summary>
        /// Retrieve a ProjectInstance evaluated with the specified contents using the specified projectCollection
        /// </summary>
        private ProjectInstance CreateProjectInstance(string contents, string toolsVersion, ProjectCollection projectCollection, bool deleteTempProject)
        {
            Project project = CreateProject(contents, toolsVersion, projectCollection, deleteTempProject);
            return project.CreateProjectInstance();
        }

        /// <summary>
        /// Retrieve a Project with the specified contents using the specified projectCollection
        /// </summary>
        private Project CreateProject(string contents, string toolsVersion, ProjectCollection projectCollection, bool deleteTempProject)
        {
            Project project = new Project(XmlReader.Create(new StringReader(contents)), (IDictionary<string, string>)null, toolsVersion, projectCollection);
            project.FullPath = FileUtilities.GetTemporaryFile();

            if (!deleteTempProject)
            {
                project.Save();
            }

            if (deleteTempProject)
            {
                File.Delete(project.FullPath);
            }

            return project;
        }

        /// <summary>
        /// Generate dummy projects
        /// </summary>
        private ProjectInstance GenerateDummyProjects(string shutdownProjectDirectory, int parallelProjectCount, ProjectCollection projectCollection)
        {
            Directory.CreateDirectory(shutdownProjectDirectory);

            // Generate the project.  It will have the following format.  Setting the AdditionalProperties
            // causes the projects to be built to be separate configs, which allows us to build the same project 
            // a bunch of times in parallel.  
            //
            // <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'/>
            //   <ItemGroup>
            //     <ProjectReference Include="RootProjectName.proj">
            //       <AdditionalProperties>p={incremented value}</AdditionalProperties>
            //     </ProjectReference>
            //     ...
            //   </ItemGroup>
            //
            //   <Target Name="Build">
            //     <MSBuild Projects="@(ProjectReference) Targets="ChildBuild" BuildInParallel="true" />
            //   </Target>
            //
            //   <Target Name="ChildBuild" />
            // </Project>
            string rootProjectPath = Path.Combine(shutdownProjectDirectory, String.Format(CultureInfo.InvariantCulture, "RootProj_{0}.proj", Guid.NewGuid().ToString("N")));
            ProjectRootElement rootProject = ProjectRootElement.Create(rootProjectPath, projectCollection);

            ProjectTargetElement buildTarget = rootProject.AddTarget("Build");
            ProjectTaskElement buildTask = buildTarget.AddTask("MSBuild");
            buildTask.SetParameter("Projects", "@(ProjectReference)");
            buildTask.SetParameter("BuildInParallel", "true");
            buildTask.SetParameter("Targets", "ChildBuild");

            rootProject.AddTarget("ChildBuild");

            IDictionary<string, string> metadata = new Dictionary<string, string>(1);
            for (int i = 0; i < parallelProjectCount; i++)
            {
                // Add the ProjectReference item for this actual config.
                metadata["AdditionalProperties"] = String.Format(CultureInfo.InvariantCulture, "p={0}", i);
                rootProject.AddItem("ProjectReference", rootProjectPath, metadata);
            }

            rootProject.Save();
            return new ProjectInstance(rootProject);
        }

        [Fact]
        public void ShouldBuildMutatedProjectInstanceWhoseProjectWasPreviouslyBuiltAsAP2PDependency()
        {
            var mainProjectContents =
@"<Project>

  <Target Name=""BuildOther"" Outputs=""@(ReturnValue)"">
    <MSBuild Projects=""{0}"" Targets=""Foo"">
      <Output TaskParameter=""TargetOutputs"" ItemName=""ReturnValue"" />
    </MSBuild>
  </Target>

</Project>";


            var p2pProjectContents =
@"<Project>

  <PropertyGroup>
    <P>InitialValue</P>
  </PropertyGroup>

  <Target Name=""Foo"" Outputs=""$(P)""/>

</Project>";

            using (var testFiles = new Helpers.TestProjectWithFiles(string.Empty, new[] { "p2p", "main" }))
            using (var collection = new ProjectCollection())
            using (var manager = new BuildManager())
            {
                try
                {
                    var p2pProjectPath = testFiles.CreatedFiles[0];
                    File.WriteAllText(p2pProjectPath, p2pProjectContents);

                    var mainRootElement = ProjectRootElement.Create(XmlReader.Create(new StringReader(string.Format(mainProjectContents, p2pProjectPath))), collection);

                    mainRootElement.FullPath = testFiles.CreatedFiles[1];
                    mainRootElement.Save();

                    // build p2p project as a real p2p dependency of some other project. This loads the p2p into msbuild's caches

                    var mainProject = new Project(mainRootElement, new Dictionary<string, string>(), MSBuildConstants.CurrentToolsVersion, collection);
                    var mainInstance = mainProject.CreateProjectInstance(ProjectInstanceSettings.Immutable).DeepCopy(isImmutable: false);

                    var request = new BuildRequestData(mainInstance, new[] { "BuildOther" });

                    var parameters = new BuildParameters()
                    {
                        DisableInProcNode = true,
                    };

                    manager.BeginBuild(parameters);

                    var submission = manager.PendBuildRequest(request);

                    var results = submission.Execute();
                    Assert.Equal(BuildResultCode.Success, results.OverallResult);
                    Assert.Equal("InitialValue", results.ResultsByTarget["BuildOther"].Items.First().ItemSpec);

                    // build p2p directly via mutated ProjectInstances based of the same Project.
                    // This should rebuild and the result shold reflect the in-memory changes and not reuse stale cache info

                    var p2pProject = new Project(p2pProjectPath, new Dictionary<string, string>(), MSBuildConstants.CurrentToolsVersion, collection);

                    for (var i = 0; i < 2; i++)
                    {
                        var p2pInstance = p2pProject.CreateProjectInstance(ProjectInstanceSettings.Immutable).DeepCopy(isImmutable: false);

                        var newPropertyValue = $"NewValue_{i}";

                        p2pInstance.SetProperty("P", newPropertyValue);

                        request = new BuildRequestData(p2pInstance, new[] { "Foo" });
                        submission = manager.PendBuildRequest(request);
                        results = submission.Execute();

                        Assert.Equal(BuildResultCode.Success, results.OverallResult);
                        Assert.Equal(newPropertyValue, results.ResultsByTarget["Foo"].Items.First().ItemSpec);
                    }
                }
                finally
                {
                    manager.EndBuild();
                }
            }
        }
    }
}
