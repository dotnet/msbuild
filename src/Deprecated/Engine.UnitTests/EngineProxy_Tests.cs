// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Xml;

using NUnit.Framework;

using Microsoft.Build.Framework;
using Microsoft.Build.BuildEngine;
using Microsoft.Build.BuildEngine.Shared;
using System.Collections;

namespace Microsoft.Build.UnitTests
{
    [TestFixture]
    public class EngineProxy_Tests
    {
        EngineProxy engineProxy;
        EngineProxy engineProxy2;
        MockTaskExecutionModule taskExecutionModule;
        MockTaskExecutionModule taskExecutionModule2;
        Engine engine;
        string project1 = "Project1";
        string project2 = "Project2";

        [SetUp]
        public void SetUp()
        {
            // Whole bunch of setup code.
            XmlElement taskNode = new XmlDocument().CreateElement("MockTask");
            LoadedType taskClass = new LoadedType(typeof(MockTask), new AssemblyLoadInfo(typeof(MockTask).Assembly.FullName, null));
            engine = new Engine(@"c:\");
            Project project = new Project(engine);
            EngineCallback engineCallback = new EngineCallback(engine);
            taskExecutionModule = new MockTaskExecutionModule(engineCallback);
            int handleId = engineCallback.CreateTaskContext(project, null, null, taskNode, EngineCallback.inProcNode, new BuildEventContext(BuildEventContext.InvalidNodeId, BuildEventContext.InvalidTargetId, BuildEventContext.InvalidProjectContextId, BuildEventContext.InvalidTaskId));
            TaskEngine taskEngine = new TaskEngine
                                (
                                    taskNode,
                                    null, /* host object */
                                    "In Memory",
                                    project.FullFileName,
                                    engine.LoggingServices,
                                    handleId,
                                    taskExecutionModule,
                                    null
                                );
            taskEngine.TaskClass = taskClass;

            engineProxy = new EngineProxy(taskExecutionModule, handleId, project.FullFileName, project.FullFileName, engine.LoggingServices, null);
            taskExecutionModule2 = new MockTaskExecutionModule(engineCallback, TaskExecutionModule.TaskExecutionModuleMode.MultiProcFullNodeMode);
            engineProxy2 = new EngineProxy(taskExecutionModule2, handleId, project.FullFileName, project.FullFileName, engine.LoggingServices, null);

        }

        [TearDown]
        public void TearDownAttribute()
        {
            engine.Shutdown();
            engineProxy = null;
            engineProxy2 = null;
            engine = null;
        }


        /// <summary>
        /// Class which implements a simple custom build error
        /// </summary>
        [Serializable]
        internal class MyCustomBuildErrorEventArgs : BuildErrorEventArgs
        {

            internal MyCustomBuildErrorEventArgs
                (
                string message
                )
                : base(null, null, null, 0, 0, 0, 0, message, null, null)
            {
            }

            internal string FXCopRule
            {
                get
                {
                    return fxcopRule;
                }
                set
                {
                    fxcopRule = value;
                }
            }

            private string fxcopRule;
        }

        /// <summary>
        /// Custom logger which will be used for testing
        /// </summary>
        internal class MyCustomLogger : ILogger
        {
            public LoggerVerbosity Verbosity
            {
                get
                {
                    return LoggerVerbosity.Normal;
                }
                set
                {
                }
            }

            public string Parameters
            {
                get
                {
                    return String.Empty;
                }
                set
                {
                }
            }

            public void Initialize(IEventSource eventSource)
            {
                eventSource.ErrorRaised += new BuildErrorEventHandler(MyCustomErrorHandler);
                eventSource.WarningRaised += new BuildWarningEventHandler(MyCustomWarningHandler);
                eventSource.MessageRaised += new BuildMessageEventHandler(MyCustomMessageHandler);
                eventSource.CustomEventRaised += new CustomBuildEventHandler(MyCustomBuildHandler);
                eventSource.AnyEventRaised += new AnyEventHandler(eventSource_AnyEventRaised);
            }

            void eventSource_AnyEventRaised(object sender, BuildEventArgs e)
            {
                if (e.Message != null)
                {
                    Console.Out.WriteLine("AnyEvent:"+e.Message.ToString());
                }
            }

            public void Shutdown()
            {
            }

            internal void MyCustomErrorHandler(object s, BuildErrorEventArgs e)
            {
                numberOfError++;
                this.lastError = e;
                if (e.Message != null)
                {
                    Console.Out.WriteLine("CustomError:"+e.Message.ToString());
                }
            }

            internal void MyCustomWarningHandler(object s, BuildWarningEventArgs e)
            {
                numberOfWarning++;
                this.lastWarning = e;
                if (e.Message != null)
                {
                    Console.Out.WriteLine("CustomWarning:" + e.Message.ToString());
                }
            }

            internal void MyCustomMessageHandler(object s, BuildMessageEventArgs e)
            {
                numberOfMessage++;
                this.lastMessage = e;
                if (e.Message != null)
                {
                    Console.Out.WriteLine("CustomMessage:" + e.Message.ToString());
                }
            }

            internal void MyCustomBuildHandler(object s, CustomBuildEventArgs e)
            {
                numberOfCustom++;
                this.lastCustom = e;
                if (e.Message != null)
                {
                    Console.Out.WriteLine("CustomEvent:" + e.Message.ToString());
                }
            }
            internal BuildErrorEventArgs lastError = null;
            internal BuildWarningEventArgs lastWarning = null;
            internal BuildMessageEventArgs lastMessage = null;
            internal CustomBuildEventArgs lastCustom = null;
            internal int numberOfError = 0;
            internal int numberOfWarning =0;
            internal int numberOfMessage = 0;
            internal int numberOfCustom = 0;
        }

        /// <summary>
        /// Makes sure that if a task tries to log a custom error event that subclasses our own
        /// BuildErrorEventArgs, that the subclass makes it all the way to the logger.  In other
        /// words, the engine should not try to read data out of the event args and construct
        /// its own.  Bug VSWhidbey 440801.
        /// </summary>
        [Test]
        public void CustomBuildErrorEventIsPreserved()
        {

            MyCustomLogger myLogger = new MyCustomLogger();
            engine.RegisterLogger(myLogger);
            // Create a custom build event args that derives from MSBuild's BuildErrorEventArgs.
            // Set a custom field on this event (FXCopRule).
            MyCustomBuildErrorEventArgs fxcopError = new MyCustomBuildErrorEventArgs("Your code is lame.");
            fxcopError.FXCopRule = "CodeLamenessViolation";

            // Log the custom event args.  (Pretend that the task actually did this.)
            engineProxy.LogErrorEvent(fxcopError);
            engine.LoggingServices.ProcessPostedLoggingEvents();

            // Make sure our custom logger received the actual custom event and not some fake.
            Assertion.Assert("Expected Custom Error Event", myLogger.lastError is MyCustomBuildErrorEventArgs);

            // Make sure the special fields in the custom event match what we originally logged.
            fxcopError = myLogger.lastError as MyCustomBuildErrorEventArgs;
            Assertion.AssertEquals("Your code is lame.", fxcopError.Message);
            Assertion.AssertEquals("CodeLamenessViolation", fxcopError.FXCopRule);
        }

        /// <summary>
        /// Test that error events are correctly logged
        /// </summary>
        [Test]
        public void TestLogErrorEvent()
        {

            MyCustomLogger myLogger = new MyCustomLogger();
            engine.RegisterLogger(myLogger);

            engineProxy.UpdateContinueOnError(false);
            // Log the custom event args.  (Pretend that the task actually did this.)
            engineProxy.LogErrorEvent(new BuildErrorEventArgs("SubCategory", "code", null, 0, 1, 2, 3, "message", "Help", "Sender"));
            engine.LoggingServices.ProcessPostedLoggingEvents();

            // Make sure our custom logger received the actual custom event and not some fake.
            Assertion.Assert("Expected Error Event", myLogger.lastError is BuildErrorEventArgs);
            Assertion.Assert("Expected line number to be 0", myLogger.lastError.LineNumber == 0);


            engineProxy.UpdateContinueOnError(true);
            // Log the custom event args.  (Pretend that the task actually did this.)
            engineProxy.LogErrorEvent(new BuildErrorEventArgs("SubCategory", "code", null, 0, 1, 2, 3, "message", "Help", "Sender"));
            engine.LoggingServices.ProcessPostedLoggingEvents();

            // Make sure our custom logger received the actual custom event and not some fake.
            Assertion.Assert("Expected Warning Event", myLogger.lastWarning is BuildWarningEventArgs);
            Assertion.Assert("Expected line number to be 0", myLogger.lastWarning.LineNumber == 0);
        }

        /// <summary>
        /// Test that a null error event will cause an exception
        /// </summary>
        [Test]
        [ExpectedException(typeof(ArgumentNullException))]
        public void TestLogErrorEventNull()
        {
            MyCustomLogger myLogger = new MyCustomLogger();
            engine.RegisterLogger(myLogger);
            engineProxy.UpdateContinueOnError(true);
            engineProxy.LogErrorEvent(null);
            engine.LoggingServices.ProcessPostedLoggingEvents();
        }

        /// <summary>
        /// Test that a null error event will cause an exception
        /// </summary>
        [Test]
        [ExpectedException(typeof(ArgumentNullException))]
        public void TestLogErrorEventNull2()
        {
            MyCustomLogger myLogger = new MyCustomLogger();
            engine.RegisterLogger(myLogger);
            engineProxy.UpdateContinueOnError(false);
            engineProxy.LogErrorEvent(null);
            engine.LoggingServices.ProcessPostedLoggingEvents();
        }
        /// <summary>
        /// Test that warnings are logged properly
        /// </summary>
        [Test]
        public void TestLogWarningEvent()
        {

            MyCustomLogger myLogger = new MyCustomLogger();
            engine.RegisterLogger(myLogger);

            // Log the custom event args.  (Pretend that the task actually did this.)
            engineProxy.LogWarningEvent(new BuildWarningEventArgs("SubCategory", "code", null, 0, 1, 2, 3, "message", "Help", "Sender"));
            engine.LoggingServices.ProcessPostedLoggingEvents();

            // Make sure our custom logger received the actual custom event and not some fake.
            Assertion.Assert("Expected Warning Event", myLogger.lastWarning is BuildWarningEventArgs);
            Assertion.Assert("Expected line number to be 0", myLogger.lastWarning.LineNumber == 0);
        }

        /// <summary>
        /// Test that messages are logged properly
        /// </summary>
        [Test]
        public void TestLogMessageEvent()
        {
            MyCustomLogger myLogger = new MyCustomLogger();
            engine.RegisterLogger(myLogger);

            // Log the custom event args.  (Pretend that the task actually did this.)
            engineProxy.LogMessageEvent(new BuildMessageEventArgs("message", "HelpKeyword", "senderName", MessageImportance.High));
            engine.LoggingServices.ProcessPostedLoggingEvents();

            // Make sure our custom logger received the actual custom event and not some fake.
            Assertion.Assert("Expected Message Event", myLogger.lastMessage is BuildMessageEventArgs);
            Assertion.Assert("Expected Message importance to be high", myLogger.lastMessage.Importance == MessageImportance.High);
        }
        [Serializable]
        class MyCustomBuildEventArgs : CustomBuildEventArgs
        {
            public MyCustomBuildEventArgs() : base() { }
            public MyCustomBuildEventArgs(string message) : base(message, "HelpKeyword", "SenderName") { }
        }
        /// <summary>
        /// Test that custom events are logged properly
        /// </summary>
        [Test]
        public void TestLogCustomEvent()
        {
            MyCustomLogger myLogger = new MyCustomLogger();
            engine.RegisterLogger(myLogger);

            // Log the custom event args.  (Pretend that the task actually did this.)
            engineProxy.LogCustomEvent(new MyCustomBuildEventArgs("testCustomBuildEvent"));
            engine.LoggingServices.ProcessPostedLoggingEvents();

            // Make sure our custom logger received the actual custom event and not some fake.
            Assertion.Assert("Expected custom build Event", myLogger.lastCustom is CustomBuildEventArgs);
            Assertion.AssertEquals("testCustomBuildEvent", myLogger.lastCustom.Message);
        }
        /// <summary>
        /// Test the building of a single project file
        /// </summary>
        [Test]
        public void BuildProjectFile()
        {
            string[] targets;
            MyCustomLogger myLogger = new MyCustomLogger();
            engine.RegisterLogger(myLogger);
            Dictionary<string, string> globalProperties = new Dictionary<string, string>();
            targets = new string[1];
            targets[0] = "Build";
            Assert.IsTrue(engineProxy.BuildProjectFile(project1, targets, null, new Dictionary<object, object>()), "Expected Build 2 to work");
        }
        /// <summary>
        /// Test the building of multiple files in parallel
        /// </summary>
        [Test]
        public void BuildProjectFilesInParallel()
        {
            string[] targets;
            string[] projects;
            Dictionary<string, string> globalProperties = new Dictionary<string, string>();
            targets = new string[1];
            targets[0] = "Build";
            projects = new string[2];
            projects[0] = project2;
            projects[1] = project1;
            Dictionary<object, object>[] dictionaryList = new Dictionary<object, object>[2];
            dictionaryList[0] = new Dictionary<object, object>();
            dictionaryList[1] = new Dictionary<object, object>();
            Dictionary<string, string>[] globalPropertiesArray = new Dictionary<string, string>[2];
            globalProperties.Add("MyGlobalProp", "SomePropertyText");
            globalPropertiesArray[0] = globalProperties;
            globalPropertiesArray[1] = globalProperties;
            string[] toolVersions = new string[] { null, null };
            Assert.IsTrue(engineProxy.BuildProjectFilesInParallel(projects, targets, globalPropertiesArray, dictionaryList, toolVersions, false, false));
        }

        [Test]
        public void ContinueOnErrorShouldConvertErrorsToWarnings()
        {
            MockLogger logger = ObjectModelHelpers.BuildProjectExpectSuccess(@"

                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>

                    <Target Name=`Build`>

                        <Copy SourceFiles=`RandomNonExistentSourceFile.123456789`
                              DestinationFiles=`foo`
                              ContinueOnError=`true` />

                    </Target>

                </Project>
        
                ");

            Assertion.AssertEquals("Expected zero errors", 0, logger.ErrorCount);
            Assertion.AssertEquals("Expected one warning", 1, logger.WarningCount);
        }
        /// <summary>
        /// Check that the properties are correctly set and retreived
        /// </summary>
        [Test]
        public void Properties()
        {
            Assert.IsTrue(engineProxy.LineNumberOfTaskNode == 0, "Expected LineNumberOfTaskNode to be 0");
            Assert.IsTrue(engineProxy.ColumnNumberOfTaskNode == 0, "Expected ColumnNumberOfTaskNode to be 0");
            Assert.IsTrue(string.Compare(engineProxy.ProjectFileOfTaskNode, string.Empty, StringComparison.OrdinalIgnoreCase) == 0, "Expected ProjectFileOfTaskNode to be empty");
        }

        /// <summary>
        /// Verify IsRunningMultipleNodes
        /// </summary>
        [Test]
        public void IsRunningMultipleNodes()
        {
            // Verify TEM is running singleProc mode before we can test to make sure EngineProxy is correctly using the value
            Assertion.Assert("Expected TEM to be running singleProcMode", taskExecutionModule.GetExecutionModuleMode() == TaskExecutionModule.TaskExecutionModuleMode.SingleProcMode);
            Assertion.Assert("Expected EngineProxy for TEM running in singleProc mode to return false for IsRunningMultipleNodes", engineProxy.IsRunningMultipleNodes == false);
            
            // Verify TEM is running MultiProc mode before we can test to make sure EngineProxy is correctly using the value 
            TaskExecutionModule.TaskExecutionModuleMode moduleMode = taskExecutionModule2.GetExecutionModuleMode();
            Assertion.Assert("Expected TEM to be not be running SingleProcMode",moduleMode != TaskExecutionModule.TaskExecutionModuleMode.SingleProcMode);
            Assertion.Assert("Expected EngineProxy for TEM running in MultiProc mode to return true for IsRunningMultipleNodes", engineProxy2.IsRunningMultipleNodes);
        }

        #region ToolsVersion tests

        private ITaskItem ToolsVersionTestHelper(string parentProjectToolsVersionInProject,
                                                 string parentProjectToolsVersionOverride,
                                                 string toolsVersionPassedToEngineProxy)
        {
            Engine engine = new Engine(Path.GetDirectoryName(new Uri(Assembly.GetExecutingAssembly().EscapedCodeBase).LocalPath));
            engine.AddToolset(new Toolset("44.0", "someToolsPath"));
            engine.AddToolset(new Toolset("55.0", "anotherToolsPath"));
            engine.AddToolset(new Toolset("66.0", "yetanotherToolsPath"));

            // The child project declares its ToolsVersion
            string childProjectFullPath = ObjectModelHelpers.CreateFileInTempProjectDirectory("child.proj", @"
                      <Project ToolsVersion='44.0' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                          <ItemGroup>
                              <ToolsVersionItem Include='$(MSBuildToolsVersion)'>
                                  <ToolsPath>$(MSBuildToolsPath)</ToolsPath>
                                  <BinPath>$(MSBuildBinPath)</BinPath>
                              </ToolsVersionItem>
                          </ItemGroup>
                          <Target Name='Build' Outputs='@(ToolsVersionItem)' />
                      </Project>
                      ");

            // The parent project doesn't declare its ToolsVersion, and its ToolsVersion is not overridden
            string parentProjectContent = @"<Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'";
            if (parentProjectToolsVersionInProject != null)
            {
                parentProjectContent += String.Format(" ToolsVersion=\"{0}\"", parentProjectToolsVersionInProject);
            }
            parentProjectContent += "/>";

            Project parentProject =
                ObjectModelHelpers.CreateInMemoryProject(engine, parentProjectContent, null);

            if (parentProjectToolsVersionOverride != null)
            {
                parentProject.ToolsVersion = parentProjectToolsVersionOverride;
            }

            Dictionary<string, ITaskItem[]> targetOutputs =
                new Dictionary<string, ITaskItem[]>(StringComparer.OrdinalIgnoreCase);

            EngineProxy engineProxy = CreateEngineProxyWithDummyTaskEngine(engine, parentProject);
            bool success = engineProxy.BuildProjectFile
                (childProjectFullPath, null, null, targetOutputs, toolsVersionPassedToEngineProxy);

            ITaskItem toolsVersionItem = targetOutputs["Build"][0];

            Assertion.Assert("Expected a successful build!", success);

            return toolsVersionItem;
        }

        /// <summary>
        /// Basic test using the toolsVersion to override the child's toolsVersion.
        /// </summary>
        [Test]
        public void ToolsVersionCanBeOverriddenUsingBuildProjectFile()
        {
            ITaskItem toolsVersionItem = ToolsVersionTestHelper(null, null, "55.0");

            Assertion.AssertEquals("55.0", toolsVersionItem.ItemSpec);
            Assertion.Assert("ToolsPath should've been 'anotherToolsPath'.",
                             0 == String.Compare(toolsVersionItem.GetMetadata("ToolsPath"), "anotherToolsPath", StringComparison.OrdinalIgnoreCase));
            Assertion.Assert("BinPath should've been 'anotherToolsPath'.",
                             0 == String.Compare(toolsVersionItem.GetMetadata("BinPath"), "anotherToolsPath", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// If toolsVersion is not passed to BuildProjectFile, and the parent project
        /// is building with a toolsVersion override, then the child project should
        /// inherit that toolsVersion override.
        /// </summary>
        [Test]
        public void UseParentProjectToolsVersionOverrideIfNotOverriddenByTask()
        {
            ITaskItem toolsVersionItem = ToolsVersionTestHelper(null, "55.0", null);

            Assertion.AssertEquals("55.0", toolsVersionItem.ItemSpec);
            Assertion.Assert("ToolsPath should've been 'anotherToolsPath'.",
                             0 == String.Compare(toolsVersionItem.GetMetadata("ToolsPath"), "anotherToolsPath", StringComparison.OrdinalIgnoreCase));
            Assertion.Assert("BinPath should've been 'anotherToolsPath'.",
                             0 == String.Compare(toolsVersionItem.GetMetadata("BinPath"), "anotherToolsPath", StringComparison.OrdinalIgnoreCase));

        }

        /// <summary>
        /// If a toolsVersion override was previously specified on the command line or
        /// a prior call to the MSBuild task, the toolsVersion specified on this
        /// task call should still take highest precedence.
        /// </summary>
        [Test]
        public void ToolsVersionFromTaskWinsEvenIfParentProjectUsingOverride()
        {
            ITaskItem toolsVersionItem = ToolsVersionTestHelper(null, "66.0", "55.0");

            Assertion.AssertEquals("55.0", toolsVersionItem.ItemSpec);
            Assertion.Assert("ToolsPath should've been 'anotherToolsPath'.",
                             0 == String.Compare(toolsVersionItem.GetMetadata("ToolsPath"), "anotherToolsPath", StringComparison.OrdinalIgnoreCase));
            Assertion.Assert("BinPath should've been 'anotherToolsPath'.",
                             0 == String.Compare(toolsVersionItem.GetMetadata("BinPath"), "anotherToolsPath", StringComparison.OrdinalIgnoreCase));

        }

        /// <summary>
        /// If no override was specified previously or here on BuildProjectFile, should use the
        /// value from the &lt;Project /&gt; element of the child.
        /// This is probably adequately tested elsewhere, but it doesn't hurt to cover here as well.
        /// </summary>
        [Test]
        public void ToolsVersionFromProjectElementUsedIfNoOverride()
        {
            ITaskItem toolsVersionItem = ToolsVersionTestHelper("66.0", null, null);

            Assertion.AssertEquals("44.0", toolsVersionItem.ItemSpec);
            Assertion.Assert("ToolsPath should've been 'someToolsPath'.",
                             0 == String.Compare(toolsVersionItem.GetMetadata("ToolsPath"), "someToolsPath", StringComparison.OrdinalIgnoreCase));
            Assertion.Assert("BinPath should've been 'someToolsPath'.",
                             0 == String.Compare(toolsVersionItem.GetMetadata("BinPath"), "someToolsPath", StringComparison.OrdinalIgnoreCase));

        }

        /// <summary>
        /// The toolsVersion override should be honored even when building a solution file.
        /// </summary>
        [Test]
        public void ToolsVersionRespectedWhenBuildingASolution()
        {
            ObjectModelHelpers.DeleteTempProjectDirectory();

            Engine engine = new Engine(Path.GetDirectoryName(new Uri(Assembly.GetExecutingAssembly().EscapedCodeBase).LocalPath));

            // We've intentionally assigned the correct BinPath value to the tools version '3.5' since later
            // we will request the engine build the solution with that tools version, and the engine will
            // need to locate the default tasks. If the override we're going to specify isn't in fact applied,
            // the engine will try to load the tasks from the bogus toolpath, and that will fail the solution build,
            // so this test will fail.
            engine.AddToolset(new Toolset("3.5", engine.BinPath));
            engine.AddToolset(new Toolset("2.0", "anotherToolsPath"));

            string solutionFileContents =
                @"
                Microsoft Visual Studio Solution File, Format Version 9.00
                # Visual Studio 2005
                Global
                    GlobalSection(SolutionConfigurationPlatforms) = preSolution
                        Debug|AnyCPU = Debug|AnyCPU
                        Release|AnyCPU = Release|AnyCPU
                    EndGlobalSection
                    GlobalSection(SolutionProperties) = preSolution
                        HideSolutionNode = FALSE
                    EndGlobalSection
                EndGlobal
                ";

            string solutionFullPath = ObjectModelHelpers.CreateFileInTempProjectDirectory("ConsoleApplication1.sln", solutionFileContents);

            // The parent project doesn't declare its ToolsVersion, and its ToolsVersion is not overridden
            string parentProjectContent = @"<Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' />";
            Project parentProject =
                ObjectModelHelpers.CreateInMemoryProject(engine, parentProjectContent, null);

            EngineProxy engineProxy = CreateEngineProxyWithDummyTaskEngine(engine, parentProject);
            bool success = engineProxy.BuildProjectFile
                (solutionFullPath, null, null, null, "3.5");

            Assertion.Assert("Expected a successful build!", success);
        }

        /// <summary>
        /// If the project we've been asked to build has the same full path, global properties,
        /// and toolsVersion as those of another loaded Project, we should simply pass this
        /// call along to that loaded Project object.
        /// </summary>
        [Test]
        [Ignore("undone")]
        public void UseSameProjectObjectIfChildIsEquivalent()
        {
            // UNDONE -- need new msbuild task
            //            Engine engine = new Engine(Path.GetDirectoryName(new Uri(Assembly.GetExecutingAssembly().EscapedCodeBase).LocalPath));
            //            engine.AddToolset(new Toolset("44.0", "someToolsPath");
            //            engine.AddToolset(new Toolset("55.0", "anotherToolsPath");
            //            engine.AddToolset(new Toolset("66.0", "yetnotherToolsPath");

            //            string childProjectFullPath = ObjectModelHelpers.CreateFileInTempProjectDirectory("child.proj", @"
            //                      <Project ToolsVersion='44.0' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
            //                          <UsingTask TaskName='CreateItem' AssemblyName='Microsoft.Build.Tasks.v3.5, Version=3.5.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'/>    
            //                          <Target Name='BuildTarget'>
            //                              <CreateItem Include='BuildTargetRan'>
            //                                  <Output TaskParameter='Include' ItemName='BuildTargetRan'/>
            //                              </CreateItem>
            //                          </Target>
            //                          <Target Name='OtherTarget' Outputs='@(BuildTargetRan)' />
            //                      </Project>
            //                      ");

            //            Project parentProject = new Project(engine, "55.0");

            //            parentProject.GlobalProperties.SetProperty("foo", "bar");
            //            Hashtable propertiesTable = new Hashtable();
            //            propertiesTable.Add("foo", "bar");

            //            Dictionary<string, ITaskItem[]> targetOutputs =
            //                new Dictionary<string, ITaskItem[]>(StringComparer.OrdinalIgnoreCase);

            //            // Now build through engineProxy, passing the same set of global properties in
            //            EngineProxy engineProxy = CreateEngineProxyWithDummyTaskEngine(engine, parentProject);
            //            bool success = engineProxy.BuildProjectFile
            //                (childProjectFullPath, new string[] { "BuildTarget" }, propertiesTable, targetOutputs, "55.0");
            //            Assertion.Assert("Expected a successful build!", success);
            //            success = engineProxy.BuildProjectFile
            //                (childProjectFullPath, new string[] { "OtherTarget" }, propertiesTable, targetOutputs, "55.0");
            //            Assertion.Assert("Expected a successful build!", success);

            //            Assertion.AssertEquals(1, targetOutputs["OtherTarget"].Length);
            //            ITaskItem toolsVersionItem = targetOutputs["OtherTarget"][0];
            //            Assertion.AssertEquals("BuildTargetRan", toolsVersionItem.ItemSpec);
        }

        /// <summary>
        /// If the project we've been asked to build has different full path, global properties,
        /// and toolsVersion as those of other loaded Projects, we should create a new project object.
        /// </summary>
        [Test]
        [Ignore("undone")]
        public void UseDifferentProjectObjectIfChildIsNotEquivalent()
        {
            // UNDONE
        }

        /// <summary>
        /// If the project we've been asked to build has the same full path, global properties,
        /// and toolsVersion as those of another loaded Project, we should simply pass this
        /// call along to that loaded Project object.
        /// </summary>
        [Test]
        [Ignore("undone")]
        public void UseSameProjectObjectIfUsingCallTarget()
        {
            // UNDONE
        }

        internal EngineProxy CreateEngineProxyWithDummyTaskEngine(Engine e, Project p)
        {
            // UNDONE need a real handle Id and a real TEM pointer
            XmlElement taskNode = new XmlDocument().CreateElement("MockTask");

            BuildRequest request = new BuildRequest(EngineCallback.invalidEngineHandle, "mockproject", null, (BuildPropertyGroup)null, null, -1, false, false);
            ProjectBuildState context = new ProjectBuildState(request, new ArrayList(), new BuildEventContext(0, 0, 0, 0));
            int handleId = e.EngineCallback.CreateTaskContext(p, null, context, taskNode, 0, new BuildEventContext(0, 0, 0, 0));
            TaskEngine taskEngine = new TaskEngine
                                (
                                    taskNode,
                                    null, /* host object */
                                    p.FullFileName,
                                    p.FullFileName,
                                    e.LoggingServices,
                                    handleId,
                                    e.NodeManager.TaskExecutionModule,
                                    new BuildEventContext(0, 0, 0, 0)
                                );
            e.Scheduler.Initialize(new INodeDescription[] { null });
            return new EngineProxy(e.NodeManager.TaskExecutionModule, handleId, p.FullFileName, p.FullFileName, e.LoggingServices, new BuildEventContext(0, 0, 0, 0));
        }

        #endregion

        #region SerializationTests

        internal class CustomEvent : CustomBuildEventArgs
        {
            internal CustomEvent() { }
        }

        internal class CustomMessageEvent : BuildMessageEventArgs
        {
            internal CustomMessageEvent() { }
        }

        internal class CustomErrorEvent : BuildErrorEventArgs
        {
            internal CustomErrorEvent() { }
        }

        internal class CustomWarningEvent : BuildWarningEventArgs
        {
            internal CustomWarningEvent() { }
        }

        [Test]
        public void TestCustomEventException()
        {
            MyCustomLogger myLogger = new MyCustomLogger();
            engine.RegisterLogger(myLogger);
            engineProxy2.LogCustomEvent(new CustomEvent());
            engine.LoggingServices.ProcessPostedLoggingEvents();
            Assertion.AssertNull("Expected customEvent to be null", myLogger.lastCustom);
            Assertion.AssertNotNull("Expected WarningEvent Not to be null", myLogger.lastWarning);
            Assertion.AssertEquals(1, myLogger.numberOfWarning);
            Assertion.AssertEquals(0, myLogger.numberOfCustom);
            myLogger = null;

            myLogger = new MyCustomLogger();
            engine.RegisterLogger(myLogger);
            engineProxy.LogCustomEvent(new CustomEvent());
            engine.LoggingServices.ProcessPostedLoggingEvents();
            Assertion.AssertNotNull("Expected customEvent to Not be null", myLogger.lastCustom);
            Assertion.AssertNull("Expected WarningEvent to be null", myLogger.lastWarning);
            Assertion.AssertEquals(0, myLogger.numberOfWarning);
            Assertion.AssertEquals(1, myLogger.numberOfCustom);
            myLogger = null;
        }

        [Test]
        public void TestCustomErrorEventException()
        {
            MyCustomLogger myLogger = new MyCustomLogger();
            engine.RegisterLogger(myLogger);
            engineProxy2.LogErrorEvent(new CustomErrorEvent());
            engine.LoggingServices.ProcessPostedLoggingEvents();
            Assertion.AssertNull("Expected customErrorEvent to be null", myLogger.lastError);
            Assertion.AssertNotNull("Expected WarningEvent Not to be null", myLogger.lastWarning);
            Assertion.AssertEquals(1, myLogger.numberOfWarning);
            Assertion.AssertEquals(0, myLogger.numberOfError);
            myLogger = null;

            myLogger = new MyCustomLogger();
            engine.RegisterLogger(myLogger);
            engineProxy.LogErrorEvent(new CustomErrorEvent());
            engine.LoggingServices.ProcessPostedLoggingEvents();
            Assertion.AssertNotNull("Expected customErrorEvent to not be null", myLogger.lastError);
            Assertion.AssertNull("Expected WarningEvent to be null", myLogger.lastWarning);
            Assertion.AssertEquals(1, myLogger.numberOfError);
            Assertion.AssertEquals(0, myLogger.numberOfWarning);
            myLogger = null;
        }
        [Test]
        public void TestCustomWarningEventException()
        {
            MyCustomLogger myLogger = new MyCustomLogger();
            engine.RegisterLogger(myLogger);
            engineProxy2.LogWarningEvent(new CustomWarningEvent());
            engine.LoggingServices.ProcessPostedLoggingEvents();
            Assertion.AssertNotNull("Expected WarningEvent Not to be null", myLogger.lastWarning);
            Assertion.AssertEquals(1, myLogger.numberOfWarning);
            myLogger = null;

            myLogger = new MyCustomLogger();
            engine.RegisterLogger(myLogger);
            engineProxy.LogWarningEvent(new CustomWarningEvent());
            engine.LoggingServices.ProcessPostedLoggingEvents();
            Assertion.AssertNotNull("Expected WarningEvent Not to be null", myLogger.lastWarning);
            Assertion.AssertEquals(1, myLogger.numberOfWarning);
            myLogger = null;
        }
        [Test]
        public void TestCustomMessageEventException()
        {
            MyCustomLogger myLogger = new MyCustomLogger();
            engine.RegisterLogger(myLogger);
            engineProxy2.LogMessageEvent(new CustomMessageEvent());
            engine.LoggingServices.ProcessPostedLoggingEvents();
            Assertion.AssertNull("Expected customMessageEvent to be null", myLogger.lastMessage);
            Assertion.AssertNotNull("Expected WarningEvent Not to be null", myLogger.lastWarning);
            Assertion.AssertEquals(0, myLogger.numberOfMessage);
            Assertion.AssertEquals(1, myLogger.numberOfWarning);
            myLogger = null;

            myLogger = new MyCustomLogger();
            engine.RegisterLogger(myLogger);
            engineProxy.LogMessageEvent(new CustomMessageEvent());
            engine.LoggingServices.ProcessPostedLoggingEvents();
            Assertion.AssertNotNull("Expected customMessageEvent to not be null", myLogger.lastMessage);
            Assertion.AssertNull("Expected WarningEvent to be null", myLogger.lastWarning);
            Assertion.AssertEquals(1, myLogger.numberOfMessage);
            Assertion.AssertEquals(0, myLogger.numberOfWarning);
            myLogger = null;
        }
        #endregion
    }
}
