// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.Build.Framework;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Construction;
using Microsoft.Build.Shared;
using Microsoft.Build.BackEnd.Logging;
using System.Collections.Generic;
using Microsoft.Build.Execution;
using Microsoft.Build.Collections;
using System.Collections;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Unittest;
using TaskItem = Microsoft.Build.Execution.ProjectItemInstance.TaskItem;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Build.UnitTests.BackEnd
{
    /// <summary>
    /// Test the task host class which acts as a communication mechanism between tasks and the msbuild engine.
    /// </summary>
    public class TaskHost_Tests
    {
        /// <summary>
        /// Task host for the test
        /// </summary>
        private TaskHost _taskHost;

        /// <summary>
        /// Mock host for the tests
        /// </summary>
        private MockHost _mockHost;

        /// <summary>
        /// Custom logger for the tests
        /// </summary>
        private MyCustomLogger _customLogger;

        /// <summary>
        /// Element location for the tests
        /// </summary>
        private ElementLocation _elementLocation;

        /// <summary>
        /// Logging service for the tests
        /// </summary>
        private ILoggingService _loggingService;

        /// <summary>
        /// Mock request callback that provides the build results.
        /// </summary>
        private MockIRequestBuilderCallback _mockRequestCallback;

        /// <summary>
        /// Set up and initialize before each test is run
        /// </summary>
        public TaskHost_Tests()
        {
            LoggingServiceFactory loggingFactory = new LoggingServiceFactory(LoggerMode.Synchronous, 1);

            _loggingService = loggingFactory.CreateInstance(BuildComponentType.LoggingService) as LoggingService;

            _customLogger = new MyCustomLogger();
            _mockHost = new MockHost();
            _mockHost.LoggingService = _loggingService;

            _loggingService.RegisterLogger(_customLogger);
            _elementLocation = ElementLocation.Create("MockFile", 5, 5);

            BuildRequest buildRequest = new BuildRequest(1 /* submissionId */, 1, 1, new List<string>(), null, BuildEventContext.Invalid, null);
            BuildRequestConfiguration configuration = new BuildRequestConfiguration(1, new BuildRequestData("Nothing", new Dictionary<string, string>(), "4.0", new string[0], null), "2.0");

            configuration.Project = new ProjectInstance(ProjectRootElement.Create());

            BuildRequestEntry entry = new BuildRequestEntry(buildRequest, configuration);

            BuildResult buildResult = new BuildResult(buildRequest, false);
            buildResult.AddResultsForTarget("Build", new TargetResult(new TaskItem[] { new TaskItem("IamSuper", configuration.ProjectFullPath) }, BuildResultUtilities.GetSkippedResult()));
            _mockRequestCallback = new MockIRequestBuilderCallback(new BuildResult[] { buildResult });
            entry.Builder = (IRequestBuilder)_mockRequestCallback;

            _taskHost = new TaskHost(_mockHost, entry, _elementLocation, null /*Don't care about the callback either unless doing a build*/);
            _taskHost.LoggingContext = new TaskLoggingContext(_loggingService, BuildEventContext.Invalid);
        }

        /// <summary>
        /// Verify when pulling target outputs out that we do not get the lives ones which are in the cache.
        /// This is to prevent changes to the target outputs from being reflected in the cache if the changes are made in the task which calls the msbuild callback.
        /// </summary>
        [Fact]
        public void TestLiveTargetOutputs()
        {
            IDictionary targetOutputs = new Hashtable();
            IDictionary projectProperties = new Hashtable();

            _taskHost.BuildProjectFile("ProjectFile", new string[] { "Build" }, projectProperties, targetOutputs);

            Assert.NotNull(((ITaskItem[])targetOutputs["Build"])[0]);

            TaskItem targetOutputItem = ((ITaskItem[])targetOutputs["Build"])[0] as TaskItem;
            TaskItem mockItemInCache = _mockRequestCallback.BuildResultsToReturn[0].ResultsByTarget["Build"].Items[0] as TaskItem;

            // Assert the contents are the same
            Assert.True(targetOutputItem.Equals(mockItemInCache));

            // Assert they are different instances.
            Assert.False(object.ReferenceEquals(targetOutputItem, mockItemInCache));
        }

        /// <summary>
        /// Makes sure that if a task tries to log a custom error event that subclasses our own
        /// BuildErrorEventArgs, that the subclass makes it all the way to the logger.  In other
        /// words, the engine should not try to read data out of the event args and construct
        /// its own.
        /// </summary>
        [Fact]
        public void CustomBuildErrorEventIsPreserved()
        {
            // Create a custom build event args that derives from MSBuild's BuildErrorEventArgs.
            // Set a custom field on this event (FXCopRule).
            MyCustomBuildErrorEventArgs fxcopError = new MyCustomBuildErrorEventArgs("Your code failed.");
            fxcopError.FXCopRule = "CodeViolation";

            // Log the custom event args.  (Pretend that the task actually did this.)
            _taskHost.LogErrorEvent(fxcopError);

            // Make sure our custom logger received the actual custom event and not some fake.
            Assert.True(_customLogger.LastError is MyCustomBuildErrorEventArgs); // "Expected Custom Error Event"

            // Make sure the special fields in the custom event match what we originally logged.
            fxcopError = _customLogger.LastError as MyCustomBuildErrorEventArgs;
            Assert.Equal("Your code failed.", fxcopError.Message);
            Assert.Equal("CodeViolation", fxcopError.FXCopRule);
        }

        /// <summary>
        /// Makes sure that if a task tries to log a custom warning event that subclasses our own
        /// BuildWarningEventArgs, that the subclass makes it all the way to the logger.  In other
        /// words, the engine should not try to read data out of the event args and construct
        /// its own.
        /// </summary>
        [Fact]
        public void CustomBuildWarningEventIsPreserved()
        {
            // Create a custom build event args that derives from MSBuild's BuildWarningEventArgs.
            // Set a custom field on this event (FXCopRule).
            MyCustomBuildWarningEventArgs fxcopWarning = new MyCustomBuildWarningEventArgs("Your code failed.");
            fxcopWarning.FXCopRule = "CodeViolation";

            _taskHost.LogWarningEvent(fxcopWarning);

            // Make sure our custom logger received the actual custom event and not some fake.
            Assert.True(_customLogger.LastWarning is MyCustomBuildWarningEventArgs); // "Expected Custom Warning Event"

            // Make sure the special fields in the custom event match what we originally logged.
            fxcopWarning = _customLogger.LastWarning as MyCustomBuildWarningEventArgs;
            Assert.Equal("Your code failed.", fxcopWarning.Message);
            Assert.Equal("CodeViolation", fxcopWarning.FXCopRule);
        }

        /// <summary>
        /// Makes sure that if a task tries to log a custom message event that subclasses our own
        /// BuildMessageEventArgs, that the subclass makes it all the way to the logger.  In other
        /// words, the engine should not try to read data out of the event args and construct
        /// its own.
        /// </summary>
        [Fact]
        public void CustomBuildMessageEventIsPreserved()
        {
            // Create a custom build event args that derives from MSBuild's BuildMessageEventArgs.
            // Set a custom field on this event (FXCopRule).
            MyCustomMessageEvent customMessage = new MyCustomMessageEvent("I am a message");
            customMessage.CustomMessage = "CodeViolation";

            _taskHost.LogMessageEvent(customMessage);

            // Make sure our custom logger received the actual custom event and not some fake.
            Assert.True(_customLogger.LastMessage is MyCustomMessageEvent); // "Expected Custom message Event"

            customMessage = _customLogger.LastMessage as MyCustomMessageEvent;
            Assert.Equal("I am a message", customMessage.Message);
            Assert.Equal("CodeViolation", customMessage.CustomMessage);
        }

        /// <summary>
        /// Test that error events are correctly logged and take into account continue on error
        /// </summary>
        [Fact]
        public void TestLogErrorEventWithContinueOnError()
        {
            _taskHost.ContinueOnError = false;

            _taskHost.LogErrorEvent(new BuildErrorEventArgs("SubCategory", "code", null, 0, 1, 2, 3, "message", "Help", "Sender"));

            // Make sure our custom logger received the actual custom event and not some fake.
            Assert.True(_customLogger.LastError is BuildErrorEventArgs); // "Expected Error Event"
            Assert.Equal(0, _customLogger.LastError.LineNumber); // "Expected line number to be 0"

            _taskHost.ContinueOnError = true;
            _taskHost.ConvertErrorsToWarnings = true;

            Assert.Null(_customLogger.LastWarning); // "Expected no Warning Event at this point"

            // Log the custom event args.  (Pretend that the task actually did this.)
            _taskHost.LogErrorEvent(new BuildErrorEventArgs("SubCategory", "code", null, 0, 1, 2, 3, "message", "Help", "Sender"));

            // Make sure our custom logger received the actual custom event and not some fake.
            Assert.True(_customLogger.LastWarning is BuildWarningEventArgs); // "Expected Warning Event"
            Assert.Equal(0, _customLogger.LastWarning.LineNumber); // "Expected line number to be 0"

            _taskHost.ContinueOnError = true;
            _taskHost.ConvertErrorsToWarnings = false;

            Assert.Equal(1, _customLogger.NumberOfWarning); // "Expected one Warning Event at this point"
            Assert.Equal(1, _customLogger.NumberOfError); // "Expected one Warning Event at this point"

            // Log the custom event args.  (Pretend that the task actually did this.)
            _taskHost.LogErrorEvent(new BuildErrorEventArgs("SubCategory", "code", null, 0, 1, 2, 3, "message", "Help", "Sender"));

            // Make sure our custom logger received the actual custom event and not some fake.
            Assert.True(_customLogger.LastError is BuildErrorEventArgs); // "Expected Error Event"
            Assert.Equal(0, _customLogger.LastWarning.LineNumber); // "Expected line number to be 0"
        }

        /// <summary>
        /// Test that a null error event will cause an exception
        /// </summary>
        [Fact]
        public void TestLogErrorEventNull()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                _taskHost.LogErrorEvent(null);
            }
           );
        }
        /// <summary>
        /// Test that a null warning event will cause an exception
        /// </summary>
        [Fact]
        public void TestLogWarningEventNull()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                _taskHost.LogWarningEvent(null);
            }
           );
        }
        /// <summary>
        /// Test that a null message event will cause an exception
        /// </summary>
        [Fact]
        public void TestLogMessageEventNull()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                _taskHost.LogMessageEvent(null);
            }
           );
        }
        /// <summary>
        /// Test that a null custom event will cause an exception
        /// </summary>
        [Fact]
        public void TestLogCustomEventNull()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                _taskHost.LogCustomEvent(null);
            }
           );
        }
        /// <summary>
        /// Test that errors are logged properly
        /// </summary>
        [Fact]
        public void TestLogErrorEvent()
        {
            // Log the custom event args.  (Pretend that the task actually did this.)
            _taskHost.LogErrorEvent(new BuildErrorEventArgs("SubCategory", "code", null, 0, 1, 2, 3, "message", "Help", "Sender"));

            // Make sure our custom logger received the actual custom event and not some fake.
            Assert.True(_customLogger.LastError is BuildErrorEventArgs); // "Expected Error Event"
            Assert.Equal(0, _customLogger.LastError.LineNumber); // "Expected line number to be 0"
        }

        /// <summary>
        /// Test that warnings are logged properly
        /// </summary>
        [Fact]
        public void TestLogWarningEvent()
        {
            // Log the custom event args.  (Pretend that the task actually did this.)
            _taskHost.LogWarningEvent(new BuildWarningEventArgs("SubCategory", "code", null, 0, 1, 2, 3, "message", "Help", "Sender"));

            // Make sure our custom logger received the actual custom event and not some fake.
            Assert.True(_customLogger.LastWarning is BuildWarningEventArgs); // "Expected Warning Event"
            Assert.Equal(0, _customLogger.LastWarning.LineNumber); // "Expected line number to be 0"
        }

        /// <summary>
        /// Test that messages are logged properly
        /// </summary>
        [Fact]
        public void TestLogMessageEvent()
        {
            // Log the custom event args.  (Pretend that the task actually did this.)
            _taskHost.LogMessageEvent(new BuildMessageEventArgs("message", "HelpKeyword", "senderName", MessageImportance.High));

            // Make sure our custom logger received the actual custom event and not some fake.
            Assert.True(_customLogger.LastMessage is BuildMessageEventArgs); // "Expected Message Event"
            Assert.Equal(MessageImportance.High, _customLogger.LastMessage.Importance); // "Expected Message importance to be high"
        }

        /// <summary>
        /// Test that custom events are logged properly
        /// </summary>
        [Fact]
        public void TestLogCustomEvent()
        {
            // Log the custom event args.  (Pretend that the task actually did this.)
            _taskHost.LogCustomEvent(new MyCustomBuildEventArgs("testCustomBuildEvent"));

            // Make sure our custom logger received the actual custom event and not some fake.
            Assert.True(_customLogger.LastCustom is CustomBuildEventArgs); // "Expected custom build Event"
            Assert.Equal("testCustomBuildEvent", _customLogger.LastCustom.Message);
        }

        #region NotSerializableEvents

        /// <summary>
        /// Test that errors are logged properly
        /// </summary>
        [Fact]
        public void TestLogErrorEventNotSerializableSP()
        {
            // Log the custom event args.  (Pretend that the task actually did this.)
            _taskHost.LogErrorEvent(new MyCustomBuildErrorEventArgsNotSerializable("SubCategory"));

            // Make sure our custom logger received the actual custom event and not some fake.
            Assert.True(_customLogger.LastError is BuildErrorEventArgs); // "Expected Error Event"
            Assert.Contains("SubCategory", _customLogger.LastError.Message); // "Expected line number to be 0"
        }

        /// <summary>
        /// Test that warnings are logged properly
        /// </summary>
        [Fact]
        public void TestLogWarningEventNotSerializableSP()
        {
            // Log the custom event args.  (Pretend that the task actually did this.)
            _taskHost.LogWarningEvent(new MyCustomBuildWarningEventArgsNotSerializable("SubCategory"));

            // Make sure our custom logger received the actual custom event and not some fake.
            Assert.True(_customLogger.LastWarning is MyCustomBuildWarningEventArgsNotSerializable); // "Expected Warning Event"
            Assert.Contains("SubCategory", _customLogger.LastWarning.Message); // "Expected line number to be 0"
        }

        /// <summary>
        /// Test that messages are logged properly
        /// </summary>
        [Fact]
        public void TestLogMessageEventNotSerializableSP()
        {
            // Log the custom event args.  (Pretend that the task actually did this.)
            _taskHost.LogMessageEvent(new MyCustomMessageEventNotSerializable("message"));

            // Make sure our custom logger received the actual custom event and not some fake.
            Assert.True(_customLogger.LastMessage is MyCustomMessageEventNotSerializable); // "Expected Message Event"
            Assert.Contains("message", _customLogger.LastMessage.Message); // "Expected Message importance to be high"
        }

        /// <summary>
        /// Test that custom events are logged properly
        /// </summary>
        [Fact]
        public void TestLogCustomEventNotSerializableSP()
        {
            // Log the custom event args.  (Pretend that the task actually did this.)
            _taskHost.LogCustomEvent(new MyCustomBuildEventArgsNotSerializable("testCustomBuildEvent"));

            // Make sure our custom logger received the actual custom event and not some fake.
            Assert.True(_customLogger.LastCustom is MyCustomBuildEventArgsNotSerializable); // "Expected custom build Event"
            Assert.Equal("testCustomBuildEvent", _customLogger.LastCustom.Message);
        }

        /// <summary>
        /// Test that errors are logged properly
        /// </summary>
        [Fact]
        public void TestLogErrorEventNotSerializableMP()
        {
            MyCustomBuildErrorEventArgsNotSerializable e = new MyCustomBuildErrorEventArgsNotSerializable("SubCategory");

            _mockHost.BuildParameters.MaxNodeCount = 4;
            Assert.True(_taskHost.IsRunningMultipleNodes);

            // Log the custom event args.  (Pretend that the task actually did this.)
            _taskHost.LogErrorEvent(e);

            Assert.Null(_customLogger.LastError); // "Expected no error Event"
            Assert.True(_customLogger.LastWarning is BuildWarningEventArgs); // "Expected Warning Event"

            string message = ResourceUtilities.FormatResourceStringStripCodeAndKeyword("ExpectedEventToBeSerializable", e.GetType().Name);
            Assert.Contains(message, _customLogger.LastWarning.Message); // "Expected line to contain NotSerializable message but it did not"
        }

        /// <summary>
        /// Test that warnings are logged properly
        /// </summary>
        [Fact]
        public void TestLogWarningEventNotSerializableMP()
        {
            MyCustomBuildWarningEventArgsNotSerializable e = new MyCustomBuildWarningEventArgsNotSerializable("SubCategory");

            _mockHost.BuildParameters.MaxNodeCount = 4;
            _taskHost.LogWarningEvent(e);
            Assert.True(_taskHost.IsRunningMultipleNodes);

            Assert.True(_customLogger.LastWarning is BuildWarningEventArgs); // "Expected Warning Event"
            Assert.Equal(1, _customLogger.NumberOfWarning); // "Expected there to be only one warning"

            string message = ResourceUtilities.FormatResourceStringStripCodeAndKeyword("ExpectedEventToBeSerializable", e.GetType().Name);
            Assert.Contains(message, _customLogger.LastWarning.Message); // "Expected line to contain NotSerializable message but it did not"
        }

        /// <summary>
        /// Test that messages are logged properly
        /// </summary>
        [Fact]
        public void TestLogMessageEventNotSerializableMP()
        {
            MyCustomMessageEventNotSerializable e = new MyCustomMessageEventNotSerializable("Message");

            _mockHost.BuildParameters.MaxNodeCount = 4;
            _taskHost.LogMessageEvent(e);
            Assert.True(_taskHost.IsRunningMultipleNodes);

            // Make sure our custom logger received the actual custom event and not some fake.
            Assert.True(_customLogger.LastWarning is BuildWarningEventArgs); // "Expected Warning Event"
            Assert.Equal(1, _customLogger.NumberOfWarning); // "Expected there to be only one warning"

            string message = ResourceUtilities.FormatResourceStringStripCodeAndKeyword("ExpectedEventToBeSerializable", e.GetType().Name);
            Assert.Contains(message, _customLogger.LastWarning.Message); // "Expected line to contain NotSerializable message but it did not"
        }

        /// <summary>
        /// Test that custom events are logged properly
        /// </summary>
        [Fact]
        public void TestLogCustomEventNotSerializableMP()
        {
            MyCustomBuildEventArgsNotSerializable e = new MyCustomBuildEventArgsNotSerializable("testCustomBuildEvent");

            _mockHost.BuildParameters.MaxNodeCount = 4;
            _taskHost.LogCustomEvent(e);
            Assert.True(_taskHost.IsRunningMultipleNodes);
            Assert.Null(_customLogger.LastCustom as MyCustomBuildEventArgsNotSerializable); // "Expected no custom Event"

            // Make sure our custom logger received the actual custom event and not some fake.
            Assert.True(_customLogger.LastWarning is BuildWarningEventArgs); // "Expected Warning Event"
            Assert.Equal(1, _customLogger.NumberOfWarning); // "Expected there to be only one warning"
            string message = ResourceUtilities.FormatResourceStringStripCodeAndKeyword("ExpectedEventToBeSerializable", e.GetType().Name);
            Assert.Contains(message, _customLogger.LastWarning.Message); // "Expected line to contain NotSerializable message but it did not"
        }
        #endregion

        /// <summary>
        /// Verify IsRunningMultipleNodes
        /// </summary>
        [Fact]
        public void IsRunningMultipleNodes1Node()
        {
            _mockHost.BuildParameters.MaxNodeCount = 1;
            Assert.False(_taskHost.IsRunningMultipleNodes); // "Expect IsRunningMultipleNodes to be false with 1 node"
        }

        /// <summary>
        /// Verify IsRunningMultipleNodes
        /// </summary>
        [Fact]
        public void IsRunningMultipleNodes4Nodes()
        {
            _mockHost.BuildParameters.MaxNodeCount = 4;
            Assert.True(_taskHost.IsRunningMultipleNodes); // "Expect IsRunningMultipleNodes to be true with 4 nodes"
        }

#if FEATURE_CODETASKFACTORY
        /// <summary>
        /// Task logging after it's done should not crash us.
        /// </summary>
        [Fact]
        public void LogCustomAfterTaskIsDone()
        {
            string projectFileContents = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' ToolsVersion='msbuilddefaulttoolsversion'>
                        <UsingTask TaskName='test' TaskFactory='CodeTaskFactory' AssemblyFile='$(MSBuildToolsPath)\Microsoft.Build.Tasks.Core.dll' >
                            <Task>
                              <Using Namespace='System' />
                              <Using Namespace='System.Threading' />
                              <Code Type='Fragment' Language='cs'>
                                <![CDATA[
                                  Log.LogWarning(""[1]"");
                                  ThreadPool.QueueUserWorkItem(state=>
                                  {
                                          Thread.Sleep(100);
                                          Log.LogExternalProjectStarted(""a"", ""b"", ""c"", ""d""); // this logs a custom event
                                  });

                                ]]>
                              </Code>
                            </Task>
                        </UsingTask>
                        <Target Name='Build'>
                            <test/>
                            <Warning Text=""[3]""/>
                        </Target>
                    </Project>";

            MockLogger mockLogger = Helpers.BuildProjectWithNewOMExpectSuccess(projectFileContents);
            mockLogger.AssertLogContains("[1]");
            mockLogger.AssertLogContains("[3]"); // [2] may or may not appear.
        }

        /// <summary>
        /// Task logging after it's done should not crash us.
        /// </summary>
        [Fact]
        public void LogCommentAfterTaskIsDone()
        {
            string projectFileContents = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' ToolsVersion='msbuilddefaulttoolsversion'>
                        <UsingTask TaskName='test' TaskFactory='CodeTaskFactory' AssemblyFile='$(MSBuildToolsPath)\Microsoft.Build.Tasks.Core.dll' >
                            <Task>
                              <Using Namespace='System' />
                              <Using Namespace='System.Threading' />
                              <Code Type='Fragment' Language='cs'>
                                <![CDATA[
                                  Log.LogMessage(""[1]"");
                                  ThreadPool.QueueUserWorkItem(state=>
                                  {
                                          Thread.Sleep(100);
                                          Log.LogMessage(""[2]"");
                                  });

                                ]]>
                              </Code>
                            </Task>
                        </UsingTask>
                        <Target Name='Build'>
                            <test/>
                            <Message Text=""[3]""/>
                        </Target>
                    </Project>";

            MockLogger mockLogger = Helpers.BuildProjectWithNewOMExpectSuccess(projectFileContents);
            mockLogger.AssertLogContains("[1]");
            mockLogger.AssertLogContains("[3]"); // [2] may or may not appear.
        }

        /// <summary>
        /// Task logging after it's done should not crash us.
        /// </summary>
        [Fact]
        public void LogWarningAfterTaskIsDone()
        {
            string projectFileContents = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' ToolsVersion='msbuilddefaulttoolsversion'>
                        <UsingTask TaskName='test' TaskFactory='CodeTaskFactory' AssemblyFile='$(MSBuildToolsPath)\Microsoft.Build.Tasks.Core.dll' >
                            <Task>
                              <Using Namespace='System' />
                              <Using Namespace='System.Threading' />
                              <Code Type='Fragment' Language='cs'>
                                <![CDATA[
                                  Log.LogWarning(""[1]"");
                                  ThreadPool.QueueUserWorkItem(state=>
                                  {
                                          Thread.Sleep(100);
                                          Log.LogWarning(""[2]"");
                                  });

                                ]]>
                              </Code>
                            </Task>
                        </UsingTask>
                        <Target Name='Build'>
                            <test/>
                            <Warning Text=""[3]""/>
                        </Target>
                    </Project>";

            MockLogger mockLogger = Helpers.BuildProjectWithNewOMExpectSuccess(projectFileContents);
            mockLogger.AssertLogContains("[1]");
            mockLogger.AssertLogContains("[3]"); // [2] may or may not appear.
        }

        /// <summary>
        /// Task logging after it's done should not crash us.
        /// </summary>
        [Fact]
        public void LogErrorAfterTaskIsDone()
        {
            string projectFileContents = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' ToolsVersion='msbuilddefaulttoolsversion'>
                        <UsingTask TaskName='test' TaskFactory='CodeTaskFactory' AssemblyFile='$(MSBuildToolsPath)\Microsoft.Build.Tasks.Core.dll' >
                            <Task>
                              <Using Namespace='System' />
                              <Using Namespace='System.Threading' />
                              <Code Type='Fragment' Language='cs'>
                                <![CDATA[
                                  Log.LogError(""[1]"");
                                  ThreadPool.QueueUserWorkItem(state=>
                                  {
                                          Thread.Sleep(100);
                                          Log.LogError(""[2]"");
                                  });

                                ]]>
                              </Code>
                            </Task>
                        </UsingTask>
                        <Target Name='Build'>
                            <test ContinueOnError=""true""/>
                            <Warning Text=""[3]""/>
                        </Target>
                    </Project>";

            MockLogger mockLogger = Helpers.BuildProjectWithNewOMExpectSuccess(projectFileContents);
            mockLogger.AssertLogContains("[1]");
            mockLogger.AssertLogContains("[3]"); // [2] may or may not appear.
        }
#endif

        #region Helper Classes

        /// <summary>
        /// Create a custom message event to make sure it can get sent correctly
        /// </summary>
        [Serializable]
        internal class MyCustomMessageEvent : BuildMessageEventArgs
        {
            /// <summary>
            /// Some custom data for the custom event.
            /// </summary>
            private string _customMessage;

            /// <summary>
            /// Constructor
            /// </summary>
            internal MyCustomMessageEvent
                (
                string message
                )
                : base(message, null, null, MessageImportance.High)
            {
            }

            /// <summary>
            /// Some data which can be set on the custom message event to make sure it makes it to the logger.
            /// </summary>
            internal string CustomMessage
            {
                get
                {
                    return _customMessage;
                }

                set
                {
                    _customMessage = value;
                }
            }
        }

        /// <summary>
        /// Create a custom build event to test the logging of custom build events against the task host
        /// </summary>
        [Serializable]
        internal class MyCustomBuildEventArgs : CustomBuildEventArgs
        {
            /// <summary>
            /// Constructor
            /// </summary>
            public MyCustomBuildEventArgs() : base()
            {
            }

            /// <summary>
            /// Constructor which adds a message
            /// </summary>
            public MyCustomBuildEventArgs(string message) : base(message, "HelpKeyword", "SenderName")
            {
            }
        }

        /// <summary>
        /// Class which implements a simple custom build error
        /// </summary>
        [Serializable]
        internal class MyCustomBuildErrorEventArgs : BuildErrorEventArgs
        {
            /// <summary>
            /// Some custom data for the custom event.
            /// </summary>
            private string _fxcopRule;

            /// <summary>
            /// Constructor
            /// </summary>
            internal MyCustomBuildErrorEventArgs
                (
                string message
                )
                : base(null, null, null, 0, 0, 0, 0, message, null, null)
            {
            }

            /// <summary>
            /// Some data which can be set on the custom error event to make sure it makes it to the logger.
            /// </summary>
            internal string FXCopRule
            {
                get
                {
                    return _fxcopRule;
                }

                set
                {
                    _fxcopRule = value;
                }
            }
        }

        /// <summary>
        /// Class which implements a simple custom build warning
        /// </summary>
        [Serializable]
        internal class MyCustomBuildWarningEventArgs : BuildWarningEventArgs
        {
            /// <summary>
            /// Custom data for the custom event
            /// </summary>
            private string _fxcopRule;

            /// <summary>
            /// Constructor
            /// </summary>
            internal MyCustomBuildWarningEventArgs
                (
                string message
                )
                : base(null, null, null, 0, 0, 0, 0, message, null, null)
            {
            }

            /// <summary>
            /// Getter for the custom data in the custom event.
            /// </summary>
            internal string FXCopRule
            {
                get
                {
                    return _fxcopRule;
                }

                set
                {
                    _fxcopRule = value;
                }
            }
        }

        /// <summary>
        /// Create a custom message event to make sure it can get sent correctly
        /// </summary>
        internal class MyCustomMessageEventNotSerializable : BuildMessageEventArgs
        {
            /// <summary>
            /// Some custom data for the custom event.
            /// </summary>
            private string _customMessage;

            /// <summary>
            /// Constructor
            /// </summary>
            internal MyCustomMessageEventNotSerializable
                (
                string message
                )
                : base(message, null, null, MessageImportance.High)
            {
            }

            /// <summary>
            /// Some data which can be set on the custom message event to make sure it makes it to the logger.
            /// </summary>
            internal string CustomMessage
            {
                get
                {
                    return _customMessage;
                }

                set
                {
                    _customMessage = value;
                }
            }
        }

        /// <summary>
        /// Custom build event which is not marked serializable. This is used to make sure we warn if we try and log a not serializable type in multiproc.
        /// </summary>
        internal class MyCustomBuildEventArgsNotSerializable : CustomBuildEventArgs
        {
            //  If binary serialization is not available, then we use a simple serializer which relies on a default constructor.  So to test
            //  what happens for an event that's not serializable, don't include a default constructor.
            /// <summary>
            /// Default constructor
            /// </summary>
            public MyCustomBuildEventArgsNotSerializable() : base()
            {
            }

            /// <summary>
            /// Constructor which takes a message
            /// </summary>
            public MyCustomBuildEventArgsNotSerializable(string message) : base(message, "HelpKeyword", "SenderName")
            {
            }
        }

        /// <summary>
        /// Class which implements a simple custom build error which is not serializable
        /// </summary>
        internal class MyCustomBuildErrorEventArgsNotSerializable : BuildErrorEventArgs
        {
            /// <summary>
            /// Custom data for the custom event
            /// </summary>
            private string _fxcopRule;

            /// <summary>
            /// Constructor
            /// </summary>
            internal MyCustomBuildErrorEventArgsNotSerializable
                (
                string message
                )
                : base(null, null, null, 0, 0, 0, 0, message, null, null)
            {
            }

            /// <summary>
            /// Getter and setter for the custom data
            /// </summary>
            internal string FXCopRule
            {
                get
                {
                    return _fxcopRule;
                }

                set
                {
                    _fxcopRule = value;
                }
            }
        }

        /// <summary>
        /// Class which implements a simple custom build warning which is not serializable
        /// </summary>
        internal class MyCustomBuildWarningEventArgsNotSerializable : BuildWarningEventArgs
        {
            /// <summary>
            /// Custom data for the custom event
            /// </summary>
            private string _fxcopRule;

            /// <summary>
            /// Constructor
            /// </summary>
            internal MyCustomBuildWarningEventArgsNotSerializable
                (
                string message
                )
                : base(null, null, null, 0, 0, 0, 0, message, null, null)
            {
            }

            /// <summary>
            /// Getter and setter for the custom data
            /// </summary>
            internal string FXCopRule
            {
                get
                {
                    return _fxcopRule;
                }

                set
                {
                    _fxcopRule = value;
                }
            }
        }

        /// <summary>
        /// Custom logger which will be used for testing
        /// </summary>
        internal class MyCustomLogger : ILogger
        {
            /// <summary>
            /// Last error event the logger encountered
            /// </summary>
            private BuildErrorEventArgs _lastError = null;

            /// <summary>
            /// Last warning event the logger encountered
            /// </summary>
            private BuildWarningEventArgs _lastWarning = null;

            /// <summary>
            /// Last message event the logger encountered
            /// </summary>
            private BuildMessageEventArgs _lastMessage = null;

            /// <summary>
            /// Last custom build event the logger encountered
            /// </summary>
            private CustomBuildEventArgs _lastCustom = null;

            /// <summary>
            /// Number of errors
            /// </summary>
            private int _numberOfError = 0;

            /// <summary>
            /// Number of warnings
            /// </summary>
            private int _numberOfWarning = 0;

            /// <summary>
            /// Number of messages
            /// </summary>
            private int _numberOfMessage = 0;

            /// <summary>
            /// Number of custom build events
            /// </summary>
            private int _numberOfCustom = 0;

            /// <summary>
            /// Last error logged
            /// </summary>
            public BuildErrorEventArgs LastError
            {
                get { return _lastError; }
                set { _lastError = value; }
            }

            /// <summary>
            /// Last warning logged
            /// </summary>
            public BuildWarningEventArgs LastWarning
            {
                get { return _lastWarning; }
                set { _lastWarning = value; }
            }

            /// <summary>
            /// Last message logged
            /// </summary>
            public BuildMessageEventArgs LastMessage
            {
                get { return _lastMessage; }
                set { _lastMessage = value; }
            }

            /// <summary>
            /// Last custom event logged
            /// </summary>
            public CustomBuildEventArgs LastCustom
            {
                get { return _lastCustom; }
                set { _lastCustom = value; }
            }

            /// <summary>
            /// Number of errors logged
            /// </summary>
            public int NumberOfError
            {
                get { return _numberOfError; }
                set { _numberOfError = value; }
            }

            /// <summary>
            /// Number of warnings logged
            /// </summary>
            public int NumberOfWarning
            {
                get { return _numberOfWarning; }
                set { _numberOfWarning = value; }
            }

            /// <summary>
            /// Number of message logged
            /// </summary>
            public int NumberOfMessage
            {
                get { return _numberOfMessage; }
                set { _numberOfMessage = value; }
            }

            /// <summary>
            /// Number of custom events logged
            /// </summary>
            public int NumberOfCustom
            {
                get { return _numberOfCustom; }
                set { _numberOfCustom = value; }
            }

            /// <summary>
            /// Verbosity of the log;
            /// </summary>
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

            /// <summary>
            /// Parameters for the logger
            /// </summary>
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

            /// <summary>
            /// Initialize the logger against the event source
            /// </summary>
            public void Initialize(IEventSource eventSource)
            {
                eventSource.ErrorRaised += new BuildErrorEventHandler(MyCustomErrorHandler);
                eventSource.WarningRaised += new BuildWarningEventHandler(MyCustomWarningHandler);
                eventSource.MessageRaised += new BuildMessageEventHandler(MyCustomMessageHandler);
                eventSource.CustomEventRaised += new CustomBuildEventHandler(MyCustomBuildHandler);
                eventSource.AnyEventRaised += new AnyEventHandler(EventSource_AnyEventRaised);
            }

            /// <summary>
            /// Do any cleanup and shutdown once the logger is done.
            /// </summary>
            public void Shutdown()
            {
            }

            /// <summary>
            /// Log if we have received any event.
            /// </summary>
            internal void EventSource_AnyEventRaised(object sender, BuildEventArgs e)
            {
                if (e.Message != null)
                {
                    Console.Out.WriteLine("AnyEvent:" + e.Message.ToString());
                }
            }

            /// <summary>
            /// Log and record the number of errors.
            /// </summary>
            internal void MyCustomErrorHandler(object s, BuildErrorEventArgs e)
            {
                _numberOfError++;
                _lastError = e;
                if (e.Message != null)
                {
                    Console.Out.WriteLine("CustomError:" + e.Message.ToString());
                }
            }

            /// <summary>
            /// Log and record the number of warnings.
            /// </summary>
            internal void MyCustomWarningHandler(object s, BuildWarningEventArgs e)
            {
                _numberOfWarning++;
                _lastWarning = e;
                if (e.Message != null)
                {
                    Console.Out.WriteLine("CustomWarning:" + e.Message.ToString());
                }
            }

            /// <summary>
            /// Log and record the number of messages.
            /// </summary>
            internal void MyCustomMessageHandler(object s, BuildMessageEventArgs e)
            {
                _numberOfMessage++;
                _lastMessage = e;
                if (e.Message != null)
                {
                    Console.Out.WriteLine("CustomMessage:" + e.Message.ToString());
                }
            }

            /// <summary>
            /// Log and record the number of custom build events.
            /// </summary>
            internal void MyCustomBuildHandler(object s, CustomBuildEventArgs e)
            {
                _numberOfCustom++;
                _lastCustom = e;
                if (e.Message != null)
                {
                    Console.Out.WriteLine("CustomEvent:" + e.Message.ToString());
                }
            }
        }

        /// <summary>
        /// Mock this class so that we can determine if build results are being cloned or if the live copies are being returned to the callers of the msbuild callback.
        /// </summary>
        internal class MockIRequestBuilderCallback : IRequestBuilderCallback, IRequestBuilder
        {
            /// <summary>
            /// BuildResults to return from the BuildProjects method.
            /// </summary>
            private BuildResult[] _buildResultsToReturn;

            /// <summary>
            /// Constructor which takes an array of build results to return from the BuildProjects method when it is called.
            /// </summary>
            internal MockIRequestBuilderCallback(BuildResult[] buildResultsToReturn)
            {
                _buildResultsToReturn = buildResultsToReturn;
                OnNewBuildRequests += new NewBuildRequestsDelegate(MockIRequestBuilderCallback_OnNewBuildRequests);
                OnBuildRequestCompleted += new BuildRequestCompletedDelegate(MockIRequestBuilderCallback_OnBuildRequestCompleted);
                OnBuildRequestBlocked += new BuildRequestBlockedDelegate(MockIRequestBuilderCallback_OnBuildRequestBlocked);
            }

#pragma warning disable 0067 // not used
            /// <summary>
            /// Not Implemented
            /// </summary>
            public event NewBuildRequestsDelegate OnNewBuildRequests;

            /// <summary>
            /// Not Implemented
            /// </summary>
            public event BuildRequestCompletedDelegate OnBuildRequestCompleted;

            /// <summary>
            /// Not Implemented
            /// </summary>
            public event BuildRequestBlockedDelegate OnBuildRequestBlocked;
#pragma warning restore

            /// <summary>
            /// BuildResults to return from the BuildProjects method.
            /// </summary>
            public BuildResult[] BuildResultsToReturn
            {
                get { return _buildResultsToReturn; }
                set { _buildResultsToReturn = value; }
            }

            /// <summary>
            /// Mock of the BuildProjects method on the callback.
            /// </summary>
            public Task<BuildResult[]> BuildProjects(string[] projectFiles, PropertyDictionary<ProjectPropertyInstance>[] properties, string[] toolsVersions, string[] targets, bool waitForResults, bool skipNonexistentTargets)
            {
                return Task<BuildResult[]>.FromResult(_buildResultsToReturn);
            }

            /// <summary>
            /// Mock of Yield
            /// </summary>
            public void Yield()
            {
            }

            /// <summary>
            /// Mock of Reacquire
            /// </summary>
            public void Reacquire()
            {
            }

            /// <summary>
            /// Mock
            /// </summary>
            public void EnterMSBuildCallbackState()
            {
            }

            /// <summary>
            /// Mock
            /// </summary>
            public void ExitMSBuildCallbackState()
            {
            }

            /// <summary>
            /// Mock of the Block on target in progress.
            /// </summary>
            public Task BlockOnTargetInProgress(int blockingRequestId, string blockingTarget, BuildResult partialBuildResult)
            {
                throw new NotImplementedException();
            }

            /// <summary>
            /// Not Implemented
            /// </summary>
            public void BuildRequest(NodeLoggingContext nodeLoggingContext, BuildRequestEntry entry)
            {
                throw new NotImplementedException();
            }

            /// <summary>
            /// Not Implemented
            /// </summary>
            public void ContinueRequest()
            {
                throw new NotImplementedException();
            }

            /// <summary>
            /// Not Implemented
            /// </summary>
            public void CancelRequest()
            {
                throw new NotImplementedException();
            }

            /// <summary>
            /// Not Implemented
            /// </summary>
            public void BeginCancel()
            {
                throw new NotImplementedException();
            }

            /// <summary>
            /// Not Implemented
            /// </summary>
            public void WaitForCancelCompletion()
            {
                throw new NotImplementedException();
            }

            /// <summary>
            /// Not Implemented
            /// </summary>
            private void MockIRequestBuilderCallback_OnBuildRequestBlocked(BuildRequestEntry issuingEntry, int blockingGlobalRequestId, string blockingTarget, IBuildResults partialBuildResult = null)
            {
                throw new NotImplementedException();
            }

            /// <summary>
            /// Not Implemented
            /// </summary>
            private void MockIRequestBuilderCallback_OnBuildRequestCompleted(BuildRequestEntry completedEntry)
            {
                throw new NotImplementedException();
            }

            /// <summary>
            /// Not Implemented
            /// </summary>
            private void MockIRequestBuilderCallback_OnNewBuildRequests(BuildRequestEntry issuingEntry, FullyQualifiedBuildRequest[] requests)
            {
                throw new NotImplementedException();
            }
        }
        #endregion
    }
}
