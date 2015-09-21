// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Tests for the MuxLogger.</summary>
//-----------------------------------------------------------------------

using System;
using System.IO;
using System.Threading;
using System.Xml;

using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.UnitTests;

using MuxLogger = Microsoft.Build.Utilities.MuxLogger;
using Xunit;
using Microsoft.Build.Framework;

namespace Microsoft.VisualStudio.Build.UnitTest
{
    /// <summary>
    /// Tests for the MuxLogger.
    /// </summary>
    public class MuxLogger_Tests
    {
        /// <summary>
        /// Verifies that an empty build with no loggers causes no exceptions.
        /// </summary>
        [Fact]
        public void EmptyBuildWithNoLoggers()
        {
            BuildManager buildManager = BuildManager.DefaultBuildManager;
            MuxLogger muxLogger = new MuxLogger();
            BuildParameters parameters = new BuildParameters();
            parameters.Loggers = new ILogger[] { muxLogger };
            buildManager.BeginBuild(parameters);
            buildManager.EndBuild();
        }

        /// <summary>
        /// Verifies that a simple build with no loggers causes no exceptions.
        /// </summary>
        [Fact]
        public void SimpleBuildWithNoLoggers()
        {
            string projectBody = ObjectModelHelpers.CleanupFileContents(@"
<Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
    <Target Name='Test'>
        <Message Text='Foo'/>
        <Error Text='Error'/>
    </Target>
</Project>
");
            ProjectInstance project = (new Project(XmlReader.Create(new StringReader(projectBody)))).CreateProjectInstance();

            BuildManager buildManager = BuildManager.DefaultBuildManager;
            MuxLogger muxLogger = new MuxLogger();
            BuildParameters parameters = new BuildParameters(ProjectCollection.GlobalProjectCollection);
            parameters.Loggers = new ILogger[] { muxLogger };
            buildManager.Build(parameters, new BuildRequestData(project, new string[0], null));
        }

        /// <summary>
        /// Verifies that attempting to register a logger before a build has started is invalid.
        /// </summary>
        [Fact]
        public void RegisteringLoggerBeforeBuildStartedThrows()
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                MuxLogger muxLogger = new MuxLogger();
                muxLogger.RegisterLogger(1, new MockLogger());
            }
           );
        }
        /// <summary>
        /// Verifies that building with a logger attached to the mux logger is equivalent to building with the logger directly.
        /// </summary>
        [Fact]
        public void BuildWithMuxLoggerEquivalentToNormalLogger()
        {
            string projectBody = ObjectModelHelpers.CleanupFileContents(@"
<Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
    <Target Name='Test'>
        <Message Text='Foo'/>
        <Error Text='Error'/>
    </Target>
</Project>
");

            BuildManager buildManager = BuildManager.DefaultBuildManager;

            // Build with a 'normal' logger
            MockLogger mockLogger2 = new MockLogger();
            mockLogger2.LogBuildFinished = false;
            ProjectCollection projectCollection = new ProjectCollection();
            ProjectInstance project = (new Project(XmlReader.Create(new StringReader(projectBody)), null, ObjectModelHelpers.MSBuildDefaultToolsVersion, projectCollection)).CreateProjectInstance();
            BuildParameters parameters = new BuildParameters(projectCollection);
            parameters.Loggers = new ILogger[] { mockLogger2 };
            buildManager.Build(parameters, new BuildRequestData(project, new string[0], null));

            // Build with the mux logger
            MuxLogger muxLogger = new MuxLogger();
            muxLogger.Verbosity = LoggerVerbosity.Normal;
            projectCollection = new ProjectCollection();
            project = (new Project(XmlReader.Create(new StringReader(projectBody)), null, ObjectModelHelpers.MSBuildDefaultToolsVersion, projectCollection)).CreateProjectInstance();
            parameters = new BuildParameters(projectCollection);
            parameters.Loggers = new ILogger[] { muxLogger };
            buildManager.BeginBuild(parameters);
            MockLogger mockLogger = new MockLogger();
            mockLogger.LogBuildFinished = false;

            try
            {
                BuildSubmission submission = buildManager.PendBuildRequest(new BuildRequestData(project, new string[0], null));
                muxLogger.RegisterLogger(submission.SubmissionId, mockLogger);
                submission.Execute();
            }
            finally
            {
                buildManager.EndBuild();
            }

            Assert.True(mockLogger2.BuildFinishedEvents.Count > 0);
            Assert.Equal(mockLogger2.BuildFinishedEvents.Count, mockLogger.BuildFinishedEvents.Count);
            Assert.Equal(mockLogger2.BuildFinishedEvents[0].Succeeded, mockLogger.BuildFinishedEvents[0].Succeeded);
            Assert.Equal(mockLogger2.FullLog, mockLogger.FullLog);
        }

        /// <summary>
        /// Verifies correctness of a simple build with one logger.
        /// </summary>
        [Fact]
        public void OneSubmissionOneLogger()
        {
            string projectBody = ObjectModelHelpers.CleanupFileContents(@"
<Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
    <Target Name='Test'>
        <Message Text='Foo'/>
        <Error Text='Error'/>
    </Target>
</Project>
");
            ProjectInstance project = (new Project(XmlReader.Create(new StringReader(projectBody)))).CreateProjectInstance();

            BuildManager buildManager = BuildManager.DefaultBuildManager;
            MuxLogger muxLogger = new MuxLogger();
            BuildParameters parameters = new BuildParameters(ProjectCollection.GlobalProjectCollection);
            parameters.Loggers = new ILogger[] { muxLogger };
            buildManager.BeginBuild(parameters);
            MockLogger mockLogger = new MockLogger();

            try
            {
                BuildSubmission submission = buildManager.PendBuildRequest(new BuildRequestData(project, new string[0], null));

                muxLogger.RegisterLogger(submission.SubmissionId, mockLogger);
                submission.Execute();
            }
            finally
            {
                buildManager.EndBuild();
            }

            mockLogger.AssertLogContains("Foo");
            mockLogger.AssertLogContains("Error");
            Assert.Equal(1, mockLogger.ErrorCount);
            mockLogger.AssertNoWarnings();
        }

        /// <summary>
        /// Verifies correctness of a two submissions in a single build using separate loggers.
        /// </summary>
        [Fact]
        public void TwoSubmissionsWithSeparateLoggers()
        {
            string projectBody1 = ObjectModelHelpers.CleanupFileContents(@"
<Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
    <Target Name='Test'>
        <Message Text='Foo'/>
        <Error Text='Error'/>
    </Target>
</Project>
");

            string projectBody2 = ObjectModelHelpers.CleanupFileContents(@"
<Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
    <Target Name='Test'>
        <Message Text='Bar'/>
        <Warning Text='Warning'/>
    </Target>
</Project>
");

            ProjectInstance project1 = (new Project(XmlReader.Create(new StringReader(projectBody1)))).CreateProjectInstance();
            ProjectInstance project2 = (new Project(XmlReader.Create(new StringReader(projectBody2)))).CreateProjectInstance();

            BuildManager buildManager = BuildManager.DefaultBuildManager;
            MuxLogger muxLogger = new MuxLogger();
            BuildParameters parameters = new BuildParameters(ProjectCollection.GlobalProjectCollection);
            parameters.Loggers = new ILogger[] { muxLogger };
            MockLogger mockLogger1 = new MockLogger();
            MockLogger mockLogger2 = new MockLogger();
            buildManager.BeginBuild(parameters);

            try
            {
                BuildSubmission submission1 = buildManager.PendBuildRequest(new BuildRequestData(project1, new string[0], null));
                muxLogger.RegisterLogger(submission1.SubmissionId, mockLogger1);
                submission1.Execute();

                BuildSubmission submission2 = buildManager.PendBuildRequest(new BuildRequestData(project2, new string[0], null));
                muxLogger.RegisterLogger(submission2.SubmissionId, mockLogger2);
                submission2.Execute();
            }
            finally
            {
                buildManager.EndBuild();
            }

            mockLogger1.AssertLogContains("Foo");
            mockLogger1.AssertLogContains("Error");
            mockLogger1.AssertLogDoesntContain("Bar");
            mockLogger1.AssertLogDoesntContain("Warning");
            Assert.Equal(1, mockLogger1.ErrorCount);
            Assert.Equal(0, mockLogger1.WarningCount);

            mockLogger2.AssertLogDoesntContain("Foo");
            mockLogger2.AssertLogDoesntContain("Error");
            mockLogger2.AssertLogContains("Bar");
            mockLogger2.AssertLogContains("Warning");
            Assert.Equal(0, mockLogger2.ErrorCount);
            Assert.Equal(1, mockLogger2.WarningCount);
        }

        /// <summary>
        /// Verifies correctness of a simple build with one logger.
        /// </summary>
        [Fact]
        public void OneSubmissionTwoLoggers()
        {
            string projectBody = ObjectModelHelpers.CleanupFileContents(@"
<Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
    <Target Name='Test'>
        <Message Text='Foo'/>
        <Error Text='Error'/>
    </Target>
</Project>
");
            ProjectInstance project = (new Project(XmlReader.Create(new StringReader(projectBody)))).CreateProjectInstance();

            BuildManager buildManager = BuildManager.DefaultBuildManager;
            MuxLogger muxLogger = new MuxLogger();
            BuildParameters parameters = new BuildParameters(ProjectCollection.GlobalProjectCollection);
            parameters.Loggers = new ILogger[] { muxLogger };
            MockLogger mockLogger1 = new MockLogger();
            MockLogger mockLogger2 = new MockLogger();
            buildManager.BeginBuild(parameters);
            try
            {
                BuildSubmission submission = buildManager.PendBuildRequest(new BuildRequestData(project, new string[0], null));

                muxLogger.RegisterLogger(submission.SubmissionId, mockLogger1);
                muxLogger.RegisterLogger(submission.SubmissionId, mockLogger2);
                submission.Execute();
            }
            finally
            {
                buildManager.EndBuild();
            }

            mockLogger1.AssertLogContains("Foo");
            mockLogger1.AssertLogContains("Error");
            Assert.Equal(1, mockLogger1.ErrorCount);
            mockLogger1.AssertNoWarnings();

            mockLogger2.AssertLogContains("Foo");
            mockLogger2.AssertLogContains("Error");
            Assert.Equal(1, mockLogger2.ErrorCount);
            mockLogger2.AssertNoWarnings();

            Assert.Equal(mockLogger1.FullLog, mockLogger2.FullLog);
        }

        /// <summary>
        /// Verifies correctness of a simple build with one logger.
        /// </summary>
        [Fact]
        public void RegisteringLoggerDuringBuildThrowsException()
        {
            string projectBody = ObjectModelHelpers.CleanupFileContents(@"
<Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
    <Target Name='Test'>
        <Exec Command='Sleep 1' />
    </Target>
</Project>
");
            ProjectInstance project = (new Project(XmlReader.Create(new StringReader(projectBody)))).CreateProjectInstance();

            BuildManager buildManager = BuildManager.DefaultBuildManager;
            MuxLogger muxLogger = new MuxLogger();
            BuildParameters parameters = new BuildParameters(ProjectCollection.GlobalProjectCollection);
            AutoResetEvent projectStartedEvent = new AutoResetEvent(false);
            parameters.Loggers = new ILogger[] { muxLogger, new EventingLogger(projectStartedEvent) };
            MockLogger mockLogger = new MockLogger();
            bool gotException = false;
            buildManager.BeginBuild(parameters);

            try
            {
                BuildSubmission submission = buildManager.PendBuildRequest(new BuildRequestData(project, new string[0], null));

                submission.ExecuteAsync(null, null);
                projectStartedEvent.WaitOne();

                try
                {
                    muxLogger.RegisterLogger(submission.SubmissionId, mockLogger);
                }
                catch (InvalidOperationException)
                {
                    gotException = true;
                }
                catch
                {
                }
            }
            finally
            {
                buildManager.EndBuild();
            }

            Assert.True(gotException); // "Failed to get exception registering logger during build."
        }

        /// <summary>
        /// A logger which signals an event when it gets a project started message.
        /// </summary>
        private class EventingLogger : ILogger
        {
            /// <summary>
            /// The event source
            /// </summary>
            private IEventSource _eventSource;

            /// <summary>
            /// The event handler
            /// </summary>
            private ProjectStartedEventHandler _eventHandler;

            /// <summary>
            /// The event to signal.
            /// </summary>
            private AutoResetEvent _projectStartedEvent;

            /// <summary>
            /// Constructor.
            /// </summary>
            public EventingLogger(AutoResetEvent projectStartedEvent)
            {
                _projectStartedEvent = projectStartedEvent;
            }

            #region ILogger Members

            /// <summary>
            /// Verbosity accessor.
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
            /// Parameters accessor.
            /// </summary>
            public string Parameters
            {
                get
                {
                    return null;
                }

                set
                {
                }
            }

            /// <summary>
            /// Initialize the logger.
            /// </summary>
            public void Initialize(IEventSource eventSource)
            {
                _eventSource = eventSource;
                _eventHandler = new ProjectStartedEventHandler(ProjectStarted);
                _eventSource.ProjectStarted += _eventHandler;
            }

            /// <summary>
            /// Shut down the logger.
            /// </summary>
            public void Shutdown()
            {
                _eventSource.ProjectStarted -= _eventHandler;
            }

            /// <summary>
            /// Event handler which signals the event.
            /// </summary>
            private void ProjectStarted(object sender, ProjectStartedEventArgs e)
            {
                _projectStartedEvent.Set();
            }

            #endregion
        }
    }
}
