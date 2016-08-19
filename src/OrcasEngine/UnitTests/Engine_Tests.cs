// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Xml;
using System.Threading;
using System.Reflection;

using NUnit.Framework;

using Microsoft.Build.Framework;
using Microsoft.Build.BuildEngine;
using Microsoft.Build.BuildEngine.Shared;


namespace Microsoft.Build.UnitTests
{
    [TestFixture]
    public class Engine_Tests
    {
        [Test]
        public void TestMSBuildForwardPropertiesFromChild()
        {
            Environment.SetEnvironmentVariable("MSBuildForwardPropertiesFromChild", null);
            Engine childEngine = new Engine(new BuildPropertyGroup(), new ToolsetDefinitionLocations(), 3, true, 3, string.Empty, string.Empty);
            string[] propertiesToSerialize = childEngine.PropertyListToSerialize;
            Assert.IsNull(propertiesToSerialize, "Expected propertiesToSerialize to be null");
	    childEngine.Shutdown();
            
	    Environment.SetEnvironmentVariable("MSBuildForwardPropertiesFromChild", string.Empty);
            childEngine = new Engine(new BuildPropertyGroup(), new ToolsetDefinitionLocations(), 3, true, 3, string.Empty, string.Empty);
            propertiesToSerialize = childEngine.PropertyListToSerialize;
            Assert.IsNull(propertiesToSerialize, "Expected propertiesToSerialize to be null");
            childEngine.Shutdown();

             Environment.SetEnvironmentVariable("MSBuildForwardPropertiesFromChild", "Platform;Configuration");
             childEngine = new Engine(new BuildPropertyGroup(), new ToolsetDefinitionLocations(), 3, true, 3, string.Empty, string.Empty);
             propertiesToSerialize = childEngine.PropertyListToSerialize;
             Assert.IsTrue(string.Compare(propertiesToSerialize[0], "Platform") == 0);
             Assert.IsTrue(string.Compare(propertiesToSerialize[1], "Configuration") == 0);
             Assert.IsTrue(propertiesToSerialize.Length == 2);
             childEngine.Shutdown();

             Environment.SetEnvironmentVariable("MSBuildForwardPropertiesFromChild", "Platform;;;;;;;;;;Configuration");
             childEngine = new Engine(new BuildPropertyGroup(), new ToolsetDefinitionLocations(), 3, true, 3, string.Empty, string.Empty);
             propertiesToSerialize = childEngine.PropertyListToSerialize;
             Assert.IsTrue(string.Compare(propertiesToSerialize[0], "Platform") == 0);
             Assert.IsTrue(string.Compare(propertiesToSerialize[1], "Configuration") == 0);
             Assert.IsTrue(propertiesToSerialize.Length == 2);
             childEngine.Shutdown();

             Environment.SetEnvironmentVariable("MSBuildForwardPropertiesFromChild", "Platform;");
             childEngine = new Engine(new BuildPropertyGroup(), new ToolsetDefinitionLocations(), 3, true, 3, string.Empty, string.Empty);
             propertiesToSerialize = childEngine.PropertyListToSerialize;
             Assert.IsTrue(string.Compare(propertiesToSerialize[0], "Platform") == 0);
             Assert.IsTrue(propertiesToSerialize.Length == 1);
             childEngine.Shutdown();

             Environment.SetEnvironmentVariable("MSBuildForwardPropertiesFromChild", "Platform");
             childEngine = new Engine(new BuildPropertyGroup(), new ToolsetDefinitionLocations(), 3, true, 3, string.Empty, string.Empty);
             propertiesToSerialize = childEngine.PropertyListToSerialize;
             Assert.IsTrue(string.Compare(propertiesToSerialize[0], "Platform") == 0);
             Assert.IsTrue(propertiesToSerialize.Length == 1);
             childEngine.Shutdown();
             
            Environment.SetEnvironmentVariable("MSBuildForwardPropertiesFromChild", ";Platform");
             childEngine = new Engine(new BuildPropertyGroup(), new ToolsetDefinitionLocations(), 3, true, 3, string.Empty, string.Empty);
             propertiesToSerialize = childEngine.PropertyListToSerialize;
             Assert.IsTrue(string.Compare(propertiesToSerialize[0], "Platform") == 0);
             Assert.IsTrue(propertiesToSerialize.Length == 1);
             childEngine.Shutdown();


             Environment.SetEnvironmentVariable("MSBuildForwardPropertiesFromChild", ";Platform;");
             childEngine = new Engine(new BuildPropertyGroup(), new ToolsetDefinitionLocations(), 3, true, 3, string.Empty, string.Empty);
             propertiesToSerialize = childEngine.PropertyListToSerialize;
             Assert.IsTrue(string.Compare(propertiesToSerialize[0], "Platform") == 0);
             Assert.IsTrue(propertiesToSerialize.Length == 1);
             childEngine.Shutdown();
        }

        [Test]
        public void DefaultToolsVersionInitializedWithBinPath()
        {
            Engine e = new Engine(@"C:\binpath");
            Assertion.AssertEquals(Constants.defaultToolsVersion, e.DefaultToolsVersion);
            Assertion.AssertEquals(@"C:\binpath", e.Toolsets[Constants.defaultToolsVersion].ToolsPath);
        }

        [Test]
        public void TestTEMBatchSizeSettings()
        {
            Engine e = new Engine(@"C:\binpath");
            EngineLoggingServicesHelper loggingServicesHelper = new EngineLoggingServicesHelper();
            e.LoggingServices = loggingServicesHelper;
            EngineCallback engineCallback = new EngineCallback(e);
            Environment.SetEnvironmentVariable("MSBUILDREQUESTBATCHSIZE", "-4");
            TaskExecutionModule TEM = new TaskExecutionModule(engineCallback, TaskExecutionModule.TaskExecutionModuleMode.MultiProcFullNodeMode, false);
            DualQueue<BuildEventArgs> currentQueue = loggingServicesHelper.GetCurrentQueueBuildEvents();
            BuildEventArgs currentEvent = currentQueue.Dequeue();
            Assertion.Assert("Expected event to be a warning event", currentEvent is BuildWarningEventArgs);
            Assertion.Assert(String.Compare(ResourceUtilities.FormatResourceString("BatchRequestSizeOutOfRange", "-4"), ((BuildWarningEventArgs)currentEvent).Message, StringComparison.OrdinalIgnoreCase) == 0);

            e = new Engine(@"C:\binpath");
            loggingServicesHelper = new EngineLoggingServicesHelper();
            e.LoggingServices = loggingServicesHelper;
            engineCallback = new EngineCallback(e);
            Environment.SetEnvironmentVariable("MSBUILDREQUESTBATCHSIZE", "0");
            TEM = new TaskExecutionModule(engineCallback, TaskExecutionModule.TaskExecutionModuleMode.MultiProcFullNodeMode, false);
            currentQueue = loggingServicesHelper.GetCurrentQueueBuildEvents();
            currentEvent = currentQueue.Dequeue();
            Assertion.Assert("Expected event to be a warning event", currentEvent is BuildWarningEventArgs);
            Assertion.Assert(String.Compare(ResourceUtilities.FormatResourceString("BatchRequestSizeOutOfRange", "0"), ((BuildWarningEventArgs)currentEvent).Message, StringComparison.OrdinalIgnoreCase) == 0);

            e = new Engine(@"C:\binpath");
            loggingServicesHelper = new EngineLoggingServicesHelper();
            e.LoggingServices = loggingServicesHelper;
            engineCallback = new EngineCallback(e);
            Environment.SetEnvironmentVariable("MSBUILDREQUESTBATCHSIZE", int.MaxValue.ToString());
            TEM = new TaskExecutionModule(engineCallback, TaskExecutionModule.TaskExecutionModuleMode.MultiProcFullNodeMode, false);
            currentQueue = loggingServicesHelper.GetCurrentQueueBuildEvents();
            Assertion.Assert(currentQueue.Count == 0);

            e = new Engine(@"C:\binpath");
            loggingServicesHelper = new EngineLoggingServicesHelper();
            e.LoggingServices = loggingServicesHelper;
            engineCallback = new EngineCallback(e);
            Environment.SetEnvironmentVariable("MSBUILDREQUESTBATCHSIZE", "4");
            TEM = new TaskExecutionModule(engineCallback, TaskExecutionModule.TaskExecutionModuleMode.MultiProcFullNodeMode, false);
            currentQueue = loggingServicesHelper.GetCurrentQueueBuildEvents();
            Assertion.Assert(currentQueue.Count == 0);

            e = new Engine(@"C:\binpath");
            loggingServicesHelper = new EngineLoggingServicesHelper();
            e.LoggingServices = loggingServicesHelper;
            engineCallback = new EngineCallback(e);
            Environment.SetEnvironmentVariable("MSBUILDREQUESTBATCHSIZE", "Giberish");
            TEM = new TaskExecutionModule(engineCallback, TaskExecutionModule.TaskExecutionModuleMode.MultiProcFullNodeMode, false);
            currentQueue = loggingServicesHelper.GetCurrentQueueBuildEvents();
            currentEvent = currentQueue.Dequeue();
            Assertion.Assert("Expected event to be a warning event", currentEvent is BuildWarningEventArgs);
            Assertion.Assert(String.Compare(ResourceUtilities.FormatResourceString("BatchRequestSizeOutOfRange", "Giberish"), ((BuildWarningEventArgs)currentEvent).Message, StringComparison.OrdinalIgnoreCase) == 0);
        }

        [Test]
        [ExpectedException(typeof(InvalidOperationException))]
        public void SettingDefaultToolsVersionThrowsIfProjectsAlreadyLoaded()
        {
            Engine e = new Engine(ToolsetDefinitionLocations.None);
            
            try
            {
                e.AddToolset(new Toolset("1.0", "someToolsPath"));
                e.AddToolset(new Toolset("2.0", "someToolsPath"));
                e.DefaultToolsVersion = "1.0"; // OK
            }
            catch(InvalidOperationException)
            {
                // Make sure the first one doesn't throw
                Assertion.Assert(false); 
            }

            try
            {
                string p1Path = ObjectModelHelpers.CreateFileInTempProjectDirectory("p1.csproj", @"<Project DefaultTargets=`Build` xmlns=`msbuildnamespace`/>");
                Project p1 = new Project(e);
                p1.Load(p1Path);

                e.DefaultToolsVersion = "2.0"; // Throws
            }
            finally
            {
                ObjectModelHelpers.DeleteTempProjectDirectory();
            }
        }

        [Test]
        public void AddingAnExistingToolsVersionDirtiesLoadedProjects()
        {
            try
            {
                Engine e = new Engine(ToolsetDefinitionLocations.None);
                e.AddToolset(new Toolset("2.0", "someToolsPath"));
                e.AddToolset(new Toolset("3.0", "anotherToolsPath"));

                string p1Path = ObjectModelHelpers.CreateFileInTempProjectDirectory("p1.csproj", @"<Project DefaultTargets=`Build` ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`/>");
                string p2Path = ObjectModelHelpers.CreateFileInTempProjectDirectory("p2.csproj", @"<Project DefaultTargets=`Build` ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`/>");

                Project p1 = new Project(e, "2.0");
                p1.Load(p1Path);
                Project p2 = new Project(e, "3.0");
                p2.Load(p2Path);

                Assertion.Assert("Expected p1.IsDirty to be false", !p1.IsDirty);
                Assertion.Assert("Expected p2.IsDirty to be false", !p2.IsDirty);

                e.AddToolset(new Toolset("2.0", "someTotallyDifferentToolsPath"));

                Assertion.Assert("Expected p1.IsDirty to be true", p1.IsDirty);
                Assertion.Assert("Expected p2.IsDirty to be false", !p2.IsDirty);

            }
            finally
            {
                ObjectModelHelpers.DeleteTempProjectDirectory();
            }
        }

        [Test]
        public void BinPathAfterDefaultToolsVersionChange()
        {
            Engine e = new Engine(ToolsetDefinitionLocations.None);
            e.AddToolset(new Toolset("orcas", @"C:\OrcasBinPath"));
            e.DefaultToolsVersion = "Orcas";
            Assertion.AssertEquals(@"C:\OrcasBinPath", e.BinPath);
            Assertion.AssertEquals("Orcas", e.DefaultToolsVersion);
        }

        [Test]
        public void GetToolsVersionNames()
        {
            // Check the contents of GetToolsVersions on engine creation
            Engine e = new Engine(ToolsetDefinitionLocations.None);

            List<string> toolsVersions = new List<string>(e.Toolsets.ToolsVersions);
            Assertion.AssertEquals(1, toolsVersions.Count);
            Assertion.AssertEquals(Constants.defaultToolsVersion, toolsVersions[0]);

            // Check the contents after adding two more tools versions
            e.Toolsets.Add(new Toolset("Whidbey", @"C:\WhidbeyPath"));
            e.Toolsets.Add(new Toolset("orcas", @"C:\OrcasBinPath"));
            
            Dictionary<string, object> toolsVersionNamesDictionary = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            foreach (string name in e.Toolsets.ToolsVersions)
            {
                toolsVersionNamesDictionary[name] = null;
            }

            Assertion.AssertEquals(3, toolsVersionNamesDictionary.Count);
            Assertion.AssertEquals(true, toolsVersionNamesDictionary.ContainsKey(Constants.defaultToolsVersion));
            Assertion.AssertEquals(true, toolsVersionNamesDictionary.ContainsKey("Whidbey"));
            Assertion.AssertEquals(true, toolsVersionNamesDictionary.ContainsKey("Orcas"));
        }

        [Test]
        public void GetToolsetSettings()
        {
            Engine e = new Engine(@"C:\binpath");

            e.Toolsets.Add(new Toolset("Whidbey", @"C:\WhidbeyPath"));
            BuildPropertyGroup properties = new BuildPropertyGroup();
            properties.SetProperty("foo", "bar");
            e.Toolsets.Add(new Toolset("orcas", @"C:\OrcasBinPath", properties));

            Toolset ts1 = e.Toolsets["Whidbey"];
            Toolset ts2 = e.Toolsets["Orcas"];

            Assertion.AssertEquals(@"C:\WhidbeyPath", ts1.ToolsPath);
            Assertion.AssertEquals(@"C:\OrcasBinPath", ts2.ToolsPath);
            Assertion.AssertEquals(0, ts1.BuildProperties.Count);
            Assertion.AssertEquals(1, ts2.BuildProperties.Count);
            Assertion.AssertEquals("bar", ts2.BuildProperties["foo"].Value);
        }

        [Test]
        public void GetToolsetProxiesToolset()
        {
            Engine e = new Engine(@"C:\binpath");

            e.Toolsets.Add(new Toolset("Whidbey", @"C:\WhidbeyPath"));

            BuildPropertyGroup properties = new BuildPropertyGroup();
            properties.SetProperty("foo", "bar");
            e.Toolsets.Add(new Toolset("orcas", @"C:\OrcasBinPath", properties));

            Toolset ts1 = e.Toolsets["orcas"];
            ts1.BuildProperties["foo"].Value = "bar2";
            ts1.BuildProperties.SetProperty("foo2", "bar3");
            Assertion.AssertEquals("bar2", ts1.BuildProperties["foo"].Value);
            Assertion.AssertEquals("bar3", ts1.BuildProperties["foo2"].Value);

            // Get it again, should be unchanged
            Toolset ts1b = e.Toolsets["orcas"];
            Assertion.AssertEquals("bar", ts1b.BuildProperties["foo"].Value);
            Assertion.AssertNull(ts1b.BuildProperties["foo2"]);
        }

        /// <summary>
        /// Check the code that assigns values to MSBuildBinPath
        /// </summary>
        [Test]
        public void MSBuildBinPath()
        {
            Engine engine = new Engine();

            engine.BinPath = @"C:";
            Assertion.AssertEquals(@"C:", engine.BinPath); // the user should be able to pass in a raw drive letter
            engine.BinPath = @"C:\";
            Assertion.AssertEquals(@"C:\", engine.BinPath);
            engine.BinPath = @"C:\\";
            Assertion.AssertEquals(@"C:\", engine.BinPath);

            engine.BinPath = @"C:\foo";
            Assertion.AssertEquals(@"C:\foo", engine.BinPath);
            engine.BinPath = @"C:\foo\";
            Assertion.AssertEquals(@"C:\foo", engine.BinPath);
            engine.BinPath = @"C:\foo\\";
            Assertion.AssertEquals(@"C:\foo\", engine.BinPath); // trim at most one slash

            engine.BinPath = @"\\foo\share";
            Assertion.AssertEquals(@"\\foo\share", engine.BinPath);
            engine.BinPath = @"\\foo\share\";
            Assertion.AssertEquals(@"\\foo\share", engine.BinPath);
            engine.BinPath = @"\\foo\share\\";
            Assertion.AssertEquals(@"\\foo\share\", engine.BinPath); // trim at most one slash
        }

        /// <summary>
        /// When a project fails to load successfully, the engine should not be tracking it.
        /// Bug VSWhidbey 415236.
        /// </summary>
        [Test]
        public void MalformedProjectDoesNotGetAddedToEngine
            (
            )
        {
            Engine myEngine = new Engine(@"c:\");
            Project myProject = myEngine.CreateNewProject();

            // Create a temp file to be used as our project file.
            string projectFile = Path.GetTempFileName();
            try
            {
                // Write some garbage into a project file.
                File.WriteAllText(projectFile, "blah");
                Assertion.AssertNull("Engine should not know about project before project has been loaded",
                    myEngine.GetLoadedProject(projectFile));

                int exceptionCount = 0;

                // Load the garbage project file.  We should get an exception.
                try
                {
                    myProject.Load(projectFile);
                }
                catch (InvalidProjectFileException)
                {
                    exceptionCount++;
                }

                Assertion.AssertEquals("Should have received invalid project file exception.", 1, exceptionCount);

                Assertion.AssertNull("Engine should not know about project if project load failed.",
                    myEngine.GetLoadedProject(projectFile));
            }
            finally
            {
                // Get a little extra code coverage
                myEngine.UnregisterAllLoggers();
                myEngine.UnloadAllProjects();
                
                File.Delete(projectFile);
            }
        }
        
        /// <summary>
        /// Engine.BuildProjectFile method with project file specified does not honor global properties set in the engine object
        /// Bug VSWhidbey 570988.
        /// </summary>
        [Test]
        public void BuildProjectFileWithGlobalPropertiesSetInEngineObjectWithProjectFileSpecified
            (
            )
        {
            MockEngine myEngine = new MockEngine();
            string projectFile = CreateGlobalPropertyProjectFile();
            try
            {
                myEngine.GlobalProperties.SetProperty("MyGlobalProp", "SomePropertyText");
                myEngine.BuildProjectFile(projectFile);
                myEngine.AssertLogContains("SomePropertyText");
            }
            finally
            {
                myEngine.UnregisterAllLoggers();
                myEngine.UnloadAllProjects();
                File.Delete(projectFile);
            }
        }
        
        /// <summary>
        /// Engine.BuildProjectFile method with project file and target specified does not honor global properties set in the engine object
        /// Bug VSWhidbey 570988.
        /// </summary>
        [Test]
        public void BuildProjectFileWithGlobalPropertiesSetInEngineObjectWithProjectFileAndTargetSpecified
            (
            )
        {
            MockEngine myEngine = new MockEngine();
            string projectFile = CreateGlobalPropertyProjectFile();
            try
            {
                myEngine.GlobalProperties.SetProperty("MyGlobalProp", "SomePropertyText");
                myEngine.BuildProjectFile(projectFile, "Build");
                myEngine.AssertLogContains("SomePropertyText");
            }
            finally
            {
                myEngine.UnregisterAllLoggers();
                myEngine.UnloadAllProjects();
                File.Delete(projectFile);
            }
        }
        
        /// <summary>
        /// Engine.BuildProjectFile method with project file and target list specified does not honor global properties set in the engine object
        /// Bug VSWhidbey 570988.
        /// </summary>
        [Test]
        public void BuildProjectFileWithGlobalPropertiesSetInEngineObjectWithProjectFileAndTargetListSpecified
            (
            )
        {
            string[] targets;
            MockEngine myEngine = new MockEngine();
            string projectFile = CreateGlobalPropertyProjectFile();
            try
            {
                targets = new string[1];
                targets[0] = "Build";
                myEngine.GlobalProperties.SetProperty("MyGlobalProp", "SomePropertyText");
                myEngine.BuildProjectFile(projectFile, targets);
                myEngine.AssertLogContains("SomePropertyText");
            }
            finally
            {
                myEngine.UnregisterAllLoggers();
                myEngine.UnloadAllProjects();
                File.Delete(projectFile);
            }
        }


        /// <summary>
        /// Try building a project where the global properties passed in are null. The project should build and not crash
        /// </summary>
        [Test]
        public void BuildProjectFileWithNullGlobalProperties
            (
            )
        {
            string[] targets;
            MockEngine myEngine = new MockEngine();
            string projectFile = CreateGlobalPropertyProjectFile();
            try
            {
                targets = new string[1];
                targets[0] = "Build";
                bool result = myEngine.BuildProjectFile(projectFile, targets, null);
                myEngine.AssertLogDoesntContain("SomePropertyText");
                Assert.IsTrue(result);
            }
            finally
            {
                myEngine.UnregisterAllLoggers();
                myEngine.UnloadAllProjects();
                File.Delete(projectFile);
            }
        }


        /// <summary>
        /// Test building multiple projects in paralle using the object model, both individual projects and traversal projects
        /// </summary>
        [Test]
        [Ignore("Commenting this out as it sporadically fails. It is not clear what scenarios we will support for the reshipped MSBuild engine and we will decide in beta 2.")]
        public void BuildProjectFilesInParallel()
        {

            //Gets the currently loaded assembly in which the specified class is defined
            Assembly engineAssembly = Assembly.GetAssembly(typeof(Engine));
            string loggerClassName = "Microsoft.Build.BuildEngine.ConfigurableForwardingLogger";
            string loggerAssemblyName = engineAssembly.GetName().FullName;
            LoggerDescription forwardingLoggerDescription = new LoggerDescription(loggerClassName, loggerAssemblyName, null, null, LoggerVerbosity.Normal);

            string[] fileNames = new string[10];
            string traversalProject = TraversalProjectFile("ABC");
            string[][] targetNamesPerProject = new string[fileNames.Length][];
            IDictionary[] targetOutPutsPerProject = new IDictionary[fileNames.Length];
            BuildPropertyGroup[] globalPropertiesPerProject = new BuildPropertyGroup[fileNames.Length];
            string[] tempfilesToDelete = new string[fileNames.Length];
            Engine engine = new Engine(null, ToolsetDefinitionLocations.ConfigurationFile | ToolsetDefinitionLocations.Registry, 4, "msbuildlocation="+AppDomain.CurrentDomain.BaseDirectory);
            engine.RegisterLogger(new ConsoleLogger(LoggerVerbosity.Normal));
            try
            {
                for (int i = 0; i < fileNames.Length; i++)
                {
                    string[] ProjectFiles1 = CreateGlobalPropertyProjectFileWithExtension("ABC");
                    fileNames[i] = ProjectFiles1[0];
                    tempfilesToDelete[i] = ProjectFiles1[1];
                    targetNamesPerProject[i] = new string[] { "Build" };
                }

                // Test building a traversal
              
                engine.BuildProjectFile(traversalProject);
                engine.Shutdown();
                
                // Test building the same set of files in parallel
                Console.Out.WriteLine("1:"+Process.GetCurrentProcess().MainModule.FileName);
                Console.Out.WriteLine("2:" + AppDomain.CurrentDomain.BaseDirectory);
                engine = new Engine(null, ToolsetDefinitionLocations.ConfigurationFile | ToolsetDefinitionLocations.Registry, 4, "msbuildlocation="+ AppDomain.CurrentDomain.BaseDirectory);
                engine.RegisterDistributedLogger(new ConsoleLogger(LoggerVerbosity.Normal), forwardingLoggerDescription);
                engine.BuildProjectFiles(fileNames, targetNamesPerProject, globalPropertiesPerProject, targetOutPutsPerProject, BuildSettings.None, new string[fileNames.Length]);
                engine.Shutdown();

                // Do the same using singleproc
                engine = new Engine(null, ToolsetDefinitionLocations.ConfigurationFile | ToolsetDefinitionLocations.Registry, 4, "msbuildlocation="+ AppDomain.CurrentDomain.BaseDirectory);
                engine.RegisterLogger(new ConsoleLogger(LoggerVerbosity.Normal));
                engine.BuildProjectFile(traversalProject);
                engine.Shutdown();

                engine = new Engine(null, ToolsetDefinitionLocations.ConfigurationFile | ToolsetDefinitionLocations.Registry, 1, "msbuildlocation="+ AppDomain.CurrentDomain.BaseDirectory);
                engine.RegisterLogger(new ConsoleLogger(LoggerVerbosity.Normal));
                engine.BuildProjectFiles(fileNames, targetNamesPerProject, globalPropertiesPerProject, targetOutPutsPerProject, BuildSettings.None, new string[fileNames.Length]);

            }
            finally
            {
                engine.Shutdown();
                for (int i = 0; i < fileNames.Length; i++)
                {
                    File.Delete(fileNames[i]);
                    File.Delete(tempfilesToDelete[i]);
                }
                File.Delete(traversalProject);
            }
        }


        /// <summary>
        /// Test building multiple projects in parallel using the OM. Try building multiple project builds on the same engine.
        /// </summary>
        [Test]
        [Ignore("Commenting this out as it sporadically fails. It is not clear what scenarios we will support for the reshipped MSBuild engine and we will decide in beta 2.")]
        public void BuildProjectFilesInParallel2()
        {
            string[] fileNames = new string[10];
            string[] fileNames2 = new string[10];
            string[] fileNamesLeafs = new string[10];
            string[] childTraversals = new string[10];
            string parentTraversal = TraversalProjectFile("CTrav");
            string traversalProject = TraversalProjectFile("ABC");

            string[][] targetNamesPerProject = new string[fileNames.Length][];
            IDictionary[] targetOutPutsPerProject = new IDictionary[fileNames.Length];
            BuildPropertyGroup[] globalPropertiesPerProject = new BuildPropertyGroup[fileNames.Length];
            
            string[] tempfilesToDelete = new string[fileNames.Length];
            string[] tempfilesToDelete2 = new string[fileNames.Length];
            string[] tempfilesToDelete3 = new string[fileNames.Length];
            string[] tempfilesToDelete4 = new string[fileNames.Length];
            Engine engine = new Engine(null, ToolsetDefinitionLocations.ConfigurationFile | ToolsetDefinitionLocations.Registry, 4, "msbuildlocation="+ AppDomain.CurrentDomain.BaseDirectory);
            try
            {
                for (int i = 0; i < fileNames.Length; i++)
                {
                    string[] ProjectFiles1 = CreateGlobalPropertyProjectFileWithExtension("ABC");
                    string[] ProjectFiles2 = CreateGlobalPropertyProjectFileWithExtension("DEF");
                    string[] FileNamesLeafs = CreateGlobalPropertyProjectFileWithExtension("LEAF");
                    string[] ChildTraversals = CreateSingleProjectTraversalFileWithExtension(FileNamesLeafs[0],"CTrav");

                    fileNames[i] = ProjectFiles1[0];
                    fileNames2[i] = ProjectFiles2[0];
                    fileNamesLeafs[i] = FileNamesLeafs[0];
                    childTraversals[i] = ChildTraversals[0];
                    
                    tempfilesToDelete[i] = ProjectFiles1[1];
                    tempfilesToDelete2[i] = ProjectFiles2[1];
                    tempfilesToDelete3[i] = FileNamesLeafs[1];
                    tempfilesToDelete4[i] = ChildTraversals[1];
                    targetNamesPerProject[i] = new string[] { "Build" };
                }


                // Try building a traversal project that had other traversals
                
                engine.RegisterLogger(new ConsoleLogger(LoggerVerbosity.Normal));
                engine.BuildProjectFile(parentTraversal, new string[] { "Build" }, new BuildPropertyGroup(), null, BuildSettings.None, "3.5");
                engine.Shutdown();

                 engine = new Engine(null, ToolsetDefinitionLocations.ConfigurationFile | ToolsetDefinitionLocations.Registry, 4, "msbuildlocation="+ AppDomain.CurrentDomain.BaseDirectory);
                engine.RegisterLogger(new ConsoleLogger(LoggerVerbosity.Normal));
                // Try building the same traversal project on the same engine one after another
                engine.BuildProjectFile(traversalProject);
                engine.BuildProjectFile(traversalProject);
                engine.Shutdown();

                // Try building the same set of project files on the same engine one after another
                engine = new Engine(null, ToolsetDefinitionLocations.ConfigurationFile | ToolsetDefinitionLocations.Registry, 4, "msbuildlocation="+ AppDomain.CurrentDomain.BaseDirectory);
                engine.RegisterLogger(new ConsoleLogger(LoggerVerbosity.Normal));
                engine.BuildProjectFiles(fileNames, targetNamesPerProject, globalPropertiesPerProject, targetOutPutsPerProject, BuildSettings.None, new string[fileNames.Length]);
                engine.BuildProjectFiles(fileNames, targetNamesPerProject, globalPropertiesPerProject, targetOutPutsPerProject, BuildSettings.None, new string[fileNames.Length]);
                engine.Shutdown();

                // Try building a set of project files, then the same set as a traversal on the same engine
                engine = new Engine(null, ToolsetDefinitionLocations.ConfigurationFile | ToolsetDefinitionLocations.Registry, 4, "msbuildlocation="+ AppDomain.CurrentDomain.BaseDirectory);
                engine.RegisterLogger(new ConsoleLogger(LoggerVerbosity.Normal));
                engine.BuildProjectFiles(fileNames2, targetNamesPerProject, globalPropertiesPerProject, targetOutPutsPerProject, BuildSettings.None, new string[fileNames.Length]);
                engine.BuildProjectFile(traversalProject);
                engine.Shutdown();


                // Try building a traversal, then the same files which are in the traversal in parallel on the same engine
                engine = new Engine(null, ToolsetDefinitionLocations.ConfigurationFile | ToolsetDefinitionLocations.Registry, 4, "msbuildlocation="+ AppDomain.CurrentDomain.BaseDirectory);
                engine.RegisterLogger(new ConsoleLogger(LoggerVerbosity.Normal));
                engine.BuildProjectFile(traversalProject);
                engine.BuildProjectFiles(fileNames2, targetNamesPerProject, globalPropertiesPerProject, targetOutPutsPerProject, BuildSettings.None, new string[fileNames.Length]);
                engine.Shutdown();
                /* Do the same as above using single proc */

                // Try building the same traversal project on the same engine one after another
                engine = new Engine(null, ToolsetDefinitionLocations.ConfigurationFile | ToolsetDefinitionLocations.Registry, 1, "msbuildlocation="+ AppDomain.CurrentDomain.BaseDirectory);
                engine.RegisterLogger(new ConsoleLogger(LoggerVerbosity.Normal));
                engine.BuildProjectFile(traversalProject);
                engine.BuildProjectFile(traversalProject);
                engine.Shutdown();

                // Try building the same set of project files on the same engine one after another
                engine = new Engine(null, ToolsetDefinitionLocations.ConfigurationFile | ToolsetDefinitionLocations.Registry, 1, "msbuildlocation="+ AppDomain.CurrentDomain.BaseDirectory);
                engine.RegisterLogger(new ConsoleLogger(LoggerVerbosity.Normal));
                engine.BuildProjectFiles(fileNames, targetNamesPerProject, globalPropertiesPerProject, targetOutPutsPerProject, BuildSettings.None, new string[fileNames.Length]);
                engine.BuildProjectFiles(fileNames, targetNamesPerProject, globalPropertiesPerProject, targetOutPutsPerProject, BuildSettings.None, new string[fileNames.Length]);
                engine.Shutdown();

                // Try building a set of project files, then the same set as a traversal on the same engine
                engine = new Engine(null, ToolsetDefinitionLocations.ConfigurationFile | ToolsetDefinitionLocations.Registry, 1, "msbuildlocation="+ AppDomain.CurrentDomain.BaseDirectory);
                engine.RegisterLogger(new ConsoleLogger(LoggerVerbosity.Normal));
                engine.BuildProjectFiles(fileNames2, targetNamesPerProject, globalPropertiesPerProject, targetOutPutsPerProject, BuildSettings.None, new string[fileNames.Length]);
                engine.BuildProjectFile(traversalProject);
                engine.Shutdown();

                // Try building a traversal, then the same files which are in the traversal in parallel on the same engine
                engine = new Engine(null, ToolsetDefinitionLocations.ConfigurationFile | ToolsetDefinitionLocations.Registry, 4, "msbuildlocation="+ AppDomain.CurrentDomain.BaseDirectory);
                engine.RegisterLogger(new ConsoleLogger(LoggerVerbosity.Normal));
                engine.BuildProjectFile(traversalProject);
                engine.BuildProjectFiles(fileNames2, targetNamesPerProject, globalPropertiesPerProject, targetOutPutsPerProject, BuildSettings.None, new string[fileNames.Length]);
            }
            finally
            {
                engine.Shutdown();
                for (int i = 0; i < fileNames.Length; i++)
                {
                    File.Delete(fileNames[i]);
                    File.Delete(fileNames2[i]);
                    File.Delete(fileNamesLeafs[i]);
                    File.Delete(childTraversals[i]);
                    File.Delete(tempfilesToDelete[i]);
                    File.Delete(tempfilesToDelete2[i]);
                    File.Delete(tempfilesToDelete3[i]);
                    File.Delete(tempfilesToDelete4[i]);
                }
                File.Delete(traversalProject);
            }
        }

        private string CreateGlobalPropertyProjectFile()
        {
            string projectFileContents = ObjectModelHelpers.CleanupFileContents(@"
                    <Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
                        <Target Name=""Build"">
                            <Message Text=""MyGlobalProp = $(MyGlobalProp)"" />
                        </Target>
                    </Project>
                ");
            string projectFile = Path.GetTempFileName();
            using (StreamWriter fileStream =
                new StreamWriter(projectFile))
            {
                fileStream.Write(projectFileContents);
            }
            return projectFile;
        }


        /// <summary>
        /// Create a new project file that outputs a property
        /// </summary>
        /// <returns></returns>
        private string[] CreateGlobalPropertyProjectFileWithExtension(string extension)
        {
            string projectFileContents = @"
                    <Project ToolsVersion=""3.5"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
                        <Target Name=""Build"">
                            <Message Text=""MyGlobalProp = $(MyGlobalProp)"" />
                        </Target>
                    </Project>
                ";
            string tempFile = Path.GetTempFileName();
            string projectFile = tempFile + extension;
            using (StreamWriter fileStream =
                new StreamWriter(projectFile))
            {
                fileStream.Write(projectFileContents);
            }
            return new string[] { projectFile, tempFile };
        }

        private string[] CreateSingleProjectTraversalFileWithExtension(string projectName, string extension)
        {
            string projectFileContents = @"
                <Project ToolsVersion=""3.5"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
                    <ItemGroup>";
            projectFileContents = projectFileContents + @"<ProjectReferences Include=""";
            projectFileContents = projectFileContents + projectName;
            projectFileContents = projectFileContents + @"""/>
                    </ItemGroup>
                <Target Name=""Build"">
                <MSBuild
                    BuildInParallel=""true""
                    Projects=""@(ProjectReferences)""
                    Targets=""Build"">
                </MSBuild>
            </Target>
        </Project>
                ";
            string tempFile = Path.GetTempFileName();
            string projectFile = tempFile + extension;
            using (StreamWriter fileStream =
                new StreamWriter(projectFile))
            {
                fileStream.Write(projectFileContents);
            }
            return new string[] { projectFile, tempFile };
        } 

        private string TraversalProjectFile(string extensionForChildProjects)
        {
            
            string projectFileContents = @"
                <Project ToolsVersion=""3.5"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
                    <ItemGroup>";
                projectFileContents = projectFileContents + @"<ProjectReferences Include=""*.*";
                projectFileContents = projectFileContents + extensionForChildProjects;
                projectFileContents = projectFileContents + @"""/>
                    </ItemGroup>
                <Target Name=""Build"">
                <MSBuild
                    BuildInParallel=""true""
                    Projects=""@(ProjectReferences)""
                    Targets=""Build"">
                </MSBuild>
            </Target>
        </Project>
                ";
            string projectFile = Path.GetTempFileName();
            using (StreamWriter fileStream =
                new StreamWriter(projectFile))
            {
                fileStream.Write(projectFileContents);
            }
            return projectFile;
        }

        [Test]
        public void RestoringProjectIdFromCache()
        {
            string childProject = ObjectModelHelpers.CreateTempFileOnDisk(@"
                <Project xmlns=`http://schemas.microsoft.com/developer/msbuild/2003`>

                  <Target Name=`Build`>
                    <Message Text=`Hi`/>
                  </Target>
                </Project>
            ");

            string mainProject = ObjectModelHelpers.CreateTempFileOnDisk(@"
                <Project xmlns=`http://schemas.microsoft.com/developer/msbuild/2003`>
                  <Target Name=`Build`>
                    <MSBuild Projects=`{0}` UnloadProjectsOnCompletion=`true` UseResultsCache=`true`/>
                    <MSBuild Projects=`{0}` UnloadProjectsOnCompletion=`true` UseResultsCache=`true`/>
                  </Target>
                </Project>
            ", childProject);

            ProjectIdLogger logger = new ProjectIdLogger();
            Engine engine = new Engine();
            engine.RegisterLogger(logger);

            Project project = new Project(engine, "4.0");
            project.Load(mainProject);

            bool success = project.Build(null, null);
            Assertion.Assert("Build failed.  See Standard Out tab for details", success);

            Assert.AreEqual(3, logger.ProjectStartedEvents.Count);
            // Project ID should be preserved between runs
            Assert.AreEqual(logger.ProjectStartedEvents[1].ProjectId, logger.ProjectStartedEvents[2].ProjectId);
            // Project context ID should be different for every entry into the project.
            Assert.AreNotEqual(logger.ProjectStartedEvents[1].BuildEventContext.ProjectContextId, logger.ProjectStartedEvents[2].BuildEventContext.ProjectContextId);
        }

        /// <summary>
        /// Build a project where the global properties of the child project is poorly formatted.
        /// </summary>
        [Test]
        public void BuildProjectWithPoorlyFormattedGlobalProperty()
        {
            string childProject = ObjectModelHelpers.CreateTempFileOnDisk(@"
                <Project xmlns=`http://schemas.microsoft.com/developer/msbuild/2003`>
                    <Target Name=`Build` Outputs=`$(a)$(b)`>
                        <Message Text=`[b]`/>
                    </Target>
                 </Project>
            ");

            string mainProject = ObjectModelHelpers.CreateTempFileOnDisk(@"
                <Project xmlns=`http://schemas.microsoft.com/developer/msbuild/2003`>
                     <ItemGroup>
                         <Prj Include=`{0}`>
                            <Properties>``=1</Properties>
                         </Prj>
                       </ItemGroup>
                    <Target Name=`1`>
                        <MSBuild Projects=`@(Prj)` />            
                    </Target>
                 </Project>
                 ", childProject);

            ProjectIdLogger logger = new ProjectIdLogger();
            Engine engine = new Engine();
            engine.RegisterLogger(logger);

            Project project = new Project(engine, "4.0");
            project.Load(mainProject);

            bool success = project.Build(null, null);
            Assertion.Assert("Build succeeded and should have failed.  See Standard Out tab for details", !success);
        }

        /// <summary>
        /// Build a project where the global properties of the child project is a reserved property.
        /// </summary>
        [Test]
        public void BuildProjectWithReservedGlobalProperty()
        {
            string childProject = ObjectModelHelpers.CreateTempFileOnDisk(@"
                <Project xmlns=`http://schemas.microsoft.com/developer/msbuild/2003`>
                    <Target Name=`1`></Target>
                 </Project>
            ");

            string mainProject = ObjectModelHelpers.CreateTempFileOnDisk(@"
                <Project xmlns=`http://schemas.microsoft.com/developer/msbuild/2003`>
                    <Target Name=`1`>
                        <MSBuild Projects=`@(ProjectReference)`/>
                    </Target>
                    <ItemGroup>
                        <ProjectReference Include=`{0}`><Properties>Target=1</Properties></ProjectReference>
                    </ItemGroup>
                 </Project>
                 ", childProject);

            ProjectIdLogger logger = new ProjectIdLogger();
            Engine engine = new Engine();
            engine.RegisterLogger(logger);

            Project project = new Project(engine, "4.0");
            project.Load(mainProject);

            bool success = project.Build(null, null);
            Assertion.Assert("Build succeded and should have failed.  See Standard Out tab for details", !success);
        }

        /// <summary>
        /// We should use the Prjects tools version if a particular tools version is not specified.
        /// </summary>
        [Test]
        public void ProjectShouldBuildUsingProjectToolsVersion()
        {
            try
            {
                string projectContent =
                    @"
                    <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                    <Target Name=`a`>
                        <Message Text=`[a=$(MSBuildToolsVersion)]`/>
                    </Target>
                    </Project>
                    ";
                MockLogger logger = new MockLogger();
                Engine e = new Engine();
                e.RegisterLogger(logger);

                string p1Path = ObjectModelHelpers.CreateFileInTempProjectDirectory("p1.proj", projectContent);

                Project p1 = new Project(e);
                p1.Load(p1Path);
                p1.Build();

                logger.AssertLogContains("[a=" + ObjectModelHelpers.MSBuildDefaultToolsVersion + "]");
                Assertion.Assert("Cachescope should have an entry with " + ObjectModelHelpers.MSBuildDefaultToolsVersion, e.CacheManager.GetCacheScope(p1Path, new BuildPropertyGroup(), ObjectModelHelpers.MSBuildDefaultToolsVersion, CacheContentType.BuildResults) != null);

            }
            finally
            {
                ObjectModelHelpers.DeleteTempProjectDirectory();
            }
        }

        /// <summary>
        /// We should use the default tools version if a particular tools version is not specified.
        /// </summary>
        [Test]
        public void ProjectShouldUseDefaultToolsVersionIfOneIsNotSpecified()
        {
            try
            {
                string projectContent =
                    @"
                    <Project xmlns=`msbuildnamespace`>
                    <Target Name=`a`>
                        <Message Text=`[a=$(MSBuildToolsVersion)]`/>
                    </Target>
                    </Project>
                    ";
                MockLogger logger = new MockLogger();
                Engine e = new Engine();
                e.RegisterLogger(logger);

                string p1Path = ObjectModelHelpers.CreateFileInTempProjectDirectory("p1.proj", projectContent);

                Project p1 = new Project(e);
                p1.Load(p1Path);
                p1.Build();

                Assertion.Assert("Cachescope should have an entry with default tools version", e.CacheManager.GetCacheScope(p1Path, new BuildPropertyGroup(), e.DefaultToolsVersion, CacheContentType.BuildResults) != null);

            }
            finally
            {
                ObjectModelHelpers.DeleteTempProjectDirectory();
            }
        }

        
        /// <summary>
        /// Project built using MSBuild task should use the default tools version if one is not specified.
        /// </summary>
        [Test]
        public void ProjectBuiltUsingMSBuildTaskShouldBuildUsingDefaultToolsVersionIfOneIsNotSpecified()
        {
            try
            {
                string projectContent =
                    @"
                    <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                    <Target Name=`a`>
                        <Message Text=`[a=$(MSBuildToolsVersion)]`/>
                        <MSBuild Projects=`p2.proj`/>
                    </Target>
                    </Project>
                    ";
                string projectContent2 =
                    @"
                    <Project xmlns=`msbuildnamespace`>
                    <Target Name=`a`>
                        <Message Text=`[b=$(MSBuildToolsVersion)]`/>
                    </Target>
                    </Project>
                    ";

                MockLogger logger = new MockLogger();
                Engine e = new Engine();
                e.RegisterLogger(logger);

                string p1Path = ObjectModelHelpers.CreateFileInTempProjectDirectory("p1.proj", projectContent);
                string p2Path = ObjectModelHelpers.CreateFileInTempProjectDirectory("p2.proj", projectContent2);

                Project p1 = new Project(e);
                p1.Load(p1Path);
                p1.Build();

                logger.AssertLogContains("[a="+ ObjectModelHelpers.MSBuildDefaultToolsVersion + "]");
                Assertion.Assert("Cachescope should have an entry with 4.0", e.CacheManager.GetCacheScope(p1Path, new BuildPropertyGroup(), ObjectModelHelpers.MSBuildDefaultToolsVersion, CacheContentType.BuildResults) != null);
                Assertion.Assert("Cachescope should have an entry with default tools version", e.CacheManager.GetCacheScope(p2Path, new BuildPropertyGroup(), e.DefaultToolsVersion, CacheContentType.BuildResults) != null);

            }
            finally
            {
                ObjectModelHelpers.DeleteTempProjectDirectory();
            }
        }

        /// <summary>
        /// Project built using MSBuild task should use the default tools version if one is not specified.
        /// </summary>
        [Test]
        public void ProjectBuiltUsingMSBuildTaskShouldUseProjectToolsVersionIfOneIsSpecified()
        {
            try
            {
                string projectContent =
                    @"
                    <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                    <Target Name=`a`>
                        <Message Text=`[a=$(MSBuildToolsVersion)]`/>
                        <MSBuild Projects=`p2.proj`/>
                    </Target>
                    </Project>
                    ";
                string projectContent2 =
                    @"
                    <Project  ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                    <Target Name=`a`>
                        <Message Text=`[b=$(MSBuildToolsVersion)]`/>
                    </Target>
                    </Project>
                    ";

                MockLogger logger = new MockLogger();
                Engine e = new Engine();
                e.RegisterLogger(logger);

                string p1Path = ObjectModelHelpers.CreateFileInTempProjectDirectory("p1.proj", projectContent);
                string p2Path = ObjectModelHelpers.CreateFileInTempProjectDirectory("p2.proj", projectContent2);

                Project p1 = new Project(e);
                p1.Load(p1Path);
                p1.Build();

                logger.AssertLogContains("[a="+ ObjectModelHelpers.MSBuildDefaultToolsVersion + "]");
                logger.AssertLogContains("[b="+ ObjectModelHelpers.MSBuildDefaultToolsVersion + "]");
                Assertion.Assert("Cachescope should have an entry with " + ObjectModelHelpers.MSBuildDefaultToolsVersion, e.CacheManager.GetCacheScope(p1Path, new BuildPropertyGroup(), ObjectModelHelpers.MSBuildDefaultToolsVersion, CacheContentType.BuildResults) != null);
                Assertion.Assert("Cachescope should have an entry with " + ObjectModelHelpers.MSBuildDefaultToolsVersion, e.CacheManager.GetCacheScope(p2Path, new BuildPropertyGroup(), ObjectModelHelpers.MSBuildDefaultToolsVersion, CacheContentType.BuildResults) != null);

            }
            finally
            {
                ObjectModelHelpers.DeleteTempProjectDirectory();
            }
        }


        /// <summary>
        /// Project built using MSBuild task should use the tools version scecified in the task.
        /// </summary>
        [Test]
        public void ProjectBuiltUsingMSBuildTaskAndToolsVersionShouldUseTheOneSpecifiedInMSBuildTask()
        {
            try
            {
                string projectContent =
                    @"
                    <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                    <Target Name=`a`>
                        <Message Text=`[a=$(MSBuildToolsVersion)]`/>
                        <MSBuild Projects=`p2.proj`/>
                        <MSBuild Projects=`p2.proj` ToolsVersion='msbuilddefaulttoolsversion'/>
                        <MSBuild Projects=`p2.proj` ToolsVersion='2.0'/>
                    </Target>
                    </Project>
                    ";
                string projectContent2 =
                    @"
                    <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                    <Target Name=`a`>
                        <Message Text=`[b=$(MSBuildToolsVersion)]`/>
                    </Target>
                    </Project>
                    ";

                MockLogger logger = new MockLogger();
                Engine e = new Engine();
                e.RegisterLogger(logger);
        
                string p1Path = ObjectModelHelpers.CreateFileInTempProjectDirectory("p1.proj", projectContent);
                string p2Path = ObjectModelHelpers.CreateFileInTempProjectDirectory("p2.proj", projectContent2);

                Project p1 = new Project(e);
                p1.Load(p1Path);
                p1.Build();

                logger.AssertLogContains("[a=" + ObjectModelHelpers.MSBuildDefaultToolsVersion + "]");
                logger.AssertLogContains("[b=2.0]");
                logger.AssertLogContains("[b=" + ObjectModelHelpers.MSBuildDefaultToolsVersion + "]");
                Assertion.Assert("Cachescope should have an entry with " + ObjectModelHelpers.MSBuildDefaultToolsVersion, e.CacheManager.GetCacheScope(p1Path, new BuildPropertyGroup(), ObjectModelHelpers.MSBuildDefaultToolsVersion, CacheContentType.BuildResults) != null);
                Assertion.Assert("Cachescope should have an entry with 2.0", e.CacheManager.GetCacheScope(p2Path, new BuildPropertyGroup(), "2.0", CacheContentType.BuildResults) != null);
                Assertion.Assert("Cachescope should have an entry with " + ObjectModelHelpers.MSBuildDefaultToolsVersion, e.CacheManager.GetCacheScope(p2Path, new BuildPropertyGroup(), ObjectModelHelpers.MSBuildDefaultToolsVersion, CacheContentType.BuildResults) != null);
            }
            finally
            {
                ObjectModelHelpers.DeleteTempProjectDirectory();
            }
        }

        /// <summary>
        /// OverridingToolsVersion in Project should be false if the build request tools version and the project's tools version are the same
        /// </summary>
        [Test]
        public void ProjectToolsVersionOverwriteIsFalse()
        {
            try
            {
                string projectContent =
                    @"
                    <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                    <Target Name=`a`>
                        <Message Text=`[a=$(MSBuildToolsVersion)]`/>
                        <MSBuild Projects=`p2.proj`/>
                        <MSBuild Projects=`p2.proj` ToolsVersion='3.5'/>
                        <MSBuild Projects=`p2.proj` ToolsVersion='2.0'/>
                    </Target>
                    </Project>
                    ";
                string projectContent2 =
                    @"
                    <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                    <Target Name=`a`>
                        <Message Text=`[b=$(MSBuildToolsVersion)]`/>
                    </Target>
                    </Project>
                    ";

                MockLogger logger = new MockLogger();
                Engine e = new Engine();
                e.RegisterLogger(logger);

                string p1Path = ObjectModelHelpers.CreateFileInTempProjectDirectory("p1.proj", projectContent);
                string p2Path = ObjectModelHelpers.CreateFileInTempProjectDirectory("p2.proj", projectContent2);

                Project p1 = new Project(e);
                p1.Load(p1Path);
                p1.Build();

                logger.AssertLogContains("[a="+ ObjectModelHelpers.MSBuildDefaultToolsVersion + "]");
                logger.AssertLogContains("[b=" + ObjectModelHelpers.MSBuildDefaultToolsVersion + "]");

                Assertion.Assert("Cachescope should have an entry with " + ObjectModelHelpers.MSBuildDefaultToolsVersion, e.CacheManager.GetCacheScope(p1Path, new BuildPropertyGroup(), ObjectModelHelpers.MSBuildDefaultToolsVersion, CacheContentType.BuildResults) != null);
                Assertion.Assert("Cachescope should have an entry with " + ObjectModelHelpers.MSBuildDefaultToolsVersion, e.CacheManager.GetCacheScope(p2Path, new BuildPropertyGroup(), ObjectModelHelpers.MSBuildDefaultToolsVersion, CacheContentType.BuildResults) != null);
            }
            finally
            {
                ObjectModelHelpers.DeleteTempProjectDirectory();
            }
        }

        /// <summary>
        /// OverridingToolsVersion in Project should be true if the build request tools version and the project's tools version are not the same
        /// </summary>
        [Test]
        public void ProjectToolsVersionOverwriteIsTrue()
        {
            try
            {
                string projectContent =
                    @"
                    <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                    <Target Name=`a`>
                        <Message Text=`[a=$(MSBuildToolsVersion)]`/>
                        <MSBuild Projects=`p2.proj`/>
                        <MSBuild Projects=`p2.proj` ToolsVersion='3.5'/>
                        <MSBuild Projects=`p2.proj` ToolsVersion='2.0'/>
                    </Target>
                    </Project>
                    ";
                string projectContent2 =
                    @"
                    <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                    <Target Name=`a`>
                        <Message Text=`[b=$(MSBuildToolsVersion)]`/>
                    </Target>
                    </Project>
                    ";

                MockLogger logger = new MockLogger();
                Engine e = new Engine();
                e.RegisterLogger(logger);

                string p1Path = ObjectModelHelpers.CreateFileInTempProjectDirectory("p1.proj", projectContent);
                string p2Path = ObjectModelHelpers.CreateFileInTempProjectDirectory("p2.proj", projectContent2);

                Project p1 = new Project(e, "2.0");
                p1.Load(p1Path);
                p1.Build();

                logger.AssertLogContains("[a=2.0]");
                logger.AssertLogContains("[b=2.0]");

                Assertion.Assert("Cachescope should have an entry with 2.0", e.CacheManager.GetCacheScope(p1Path, new BuildPropertyGroup(), "2.0", CacheContentType.BuildResults) != null);
                Assertion.Assert("Cachescope should have an entry with 2.0", e.CacheManager.GetCacheScope(p2Path, new BuildPropertyGroup(), "2.0", CacheContentType.BuildResults) != null);
            }
            finally
            {
                ObjectModelHelpers.DeleteTempProjectDirectory();
            }
        }
    }
}
