// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Text.RegularExpressions;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging.LiveLogger;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests
{
    public class LiveLogger_Tests : IEventSource, IDisposable
    {
        private const int _nodeCount = 8;
        private const int _terminalWidth = 80;
        private const int _terminalHeight = 40;
        private const string _eventSender = "Test";
        private const string _projectFile = @"C:\src\project.proj";

        private readonly MockTerminal _mockTerminal;
        private readonly LiveLogger _liveLogger;

        private readonly DateTime _buildStartTime = new DateTime(2023, 3, 30, 16, 30, 0);
        private readonly DateTime _buildFinishTime = new DateTime(2023, 3, 30, 16, 30, 5);

        public LiveLogger_Tests()
        {
            _mockTerminal = new MockTerminal(_terminalWidth, _terminalHeight);
            _liveLogger = new LiveLogger(_mockTerminal);

            _liveLogger.Initialize(this, _nodeCount);
        }

        #region IEventSource implementation

#pragma warning disable CS0067
        public event BuildMessageEventHandler? MessageRaised;

        public event BuildErrorEventHandler? ErrorRaised;

        public event BuildWarningEventHandler? WarningRaised;

        public event BuildStartedEventHandler? BuildStarted;

        public event BuildFinishedEventHandler? BuildFinished;

        public event ProjectStartedEventHandler? ProjectStarted;

        public event ProjectFinishedEventHandler? ProjectFinished;

        public event TargetStartedEventHandler? TargetStarted;

        public event TargetFinishedEventHandler? TargetFinished;

        public event TaskStartedEventHandler? TaskStarted;

        public event TaskFinishedEventHandler? TaskFinished;

        public event CustomBuildEventHandler? CustomEventRaised;

        public event BuildStatusEventHandler? StatusEventRaised;

        public event AnyEventHandler? AnyEventRaised;
#pragma warning restore CS0067

        #endregion

        #region IDisposable implementation

        public void Dispose()
        {
            _liveLogger.Shutdown();
        }

        #endregion

        #region Event args helpers

        private BuildEventContext MakeBuildEventContext()
        {
            return new BuildEventContext(1, 1, 1, 1);
        }

        private BuildStartedEventArgs MakeBuildStartedEventArgs()
        {
            return new BuildStartedEventArgs(null, null, _buildStartTime);
        }

        private BuildFinishedEventArgs MakeBuildFinishedEventArgs(bool succeeded)
        {
            return new BuildFinishedEventArgs(null, null, succeeded, _buildFinishTime);
        }

        private ProjectStartedEventArgs MakeProjectStartedEventArgs(string projectFile, string targetNames = "Build")
        {
            return new ProjectStartedEventArgs("", "", projectFile, targetNames, new Dictionary<string, string>(), new List<DictionaryEntry>())
            {
                BuildEventContext = MakeBuildEventContext(),
            };
        }

        private ProjectFinishedEventArgs MakeProjectFinishedEventArgs(string projectFile, bool succeeded)
        {
            return new ProjectFinishedEventArgs(null, null, projectFile, succeeded)
            {
                BuildEventContext = MakeBuildEventContext(),
            };
        }

        private TargetStartedEventArgs MakeTargetStartedEventArgs(string projectFile, string targetName)
        {
            return new TargetStartedEventArgs("", "", targetName, projectFile, targetFile: projectFile)
            {
                BuildEventContext = MakeBuildEventContext(),
            };
        }

        private TargetFinishedEventArgs MakeTargetFinishedEventArgs(string projectFile, string targetName, bool succeeded)
        {
            return new TargetFinishedEventArgs("", "", targetName, projectFile, targetFile: projectFile, succeeded)
            {
                BuildEventContext = MakeBuildEventContext(),
            };
        }

        private TaskStartedEventArgs MakeTaskStartedEventArgs(string projectFile, string taskName)
        {
            return new TaskStartedEventArgs("", "", projectFile, taskFile: projectFile, taskName)
            {
                BuildEventContext = MakeBuildEventContext(),
            };
        }

        private TaskFinishedEventArgs MakeTaskFinishedEventArgs(string projectFile, string taskName, bool succeeded)
        {
            return new TaskFinishedEventArgs("", "", projectFile, taskFile: projectFile, taskName, succeeded)
            {
                BuildEventContext = MakeBuildEventContext(),
            };
        }

        private BuildWarningEventArgs MakeWarningEventArgs(string warning)
        {
            return new BuildWarningEventArgs("", "", "", 0, 0, 0, 0, warning, null, null)
            {
                BuildEventContext = MakeBuildEventContext(),
            };
        }

        private BuildErrorEventArgs MakeErrorEventArgs(string error)
        {
            return new BuildErrorEventArgs("", "", "", 0, 0, 0, 0, error, null, null)
            {
                BuildEventContext = MakeBuildEventContext(),
            };
        }

        #endregion

        #region Build summary tests

        private void InvokeLoggerCallbacksForSimpleProject(bool succeeded, Action additionalCallbacks)
        {
            BuildStarted?.Invoke(_eventSender, MakeBuildStartedEventArgs());
            ProjectStarted?.Invoke(_eventSender, MakeProjectStartedEventArgs(_projectFile));

            TargetStarted?.Invoke(_eventSender, MakeTargetStartedEventArgs(_projectFile, "Build"));
            TaskStarted?.Invoke(_eventSender, MakeTaskStartedEventArgs(_projectFile, "Task"));

            additionalCallbacks();

            TaskFinished?.Invoke(_eventSender, MakeTaskFinishedEventArgs(_projectFile, "Task", succeeded));
            TargetFinished?.Invoke(_eventSender, MakeTargetFinishedEventArgs(_projectFile, "Build", succeeded));

            ProjectFinished?.Invoke(_eventSender, MakeProjectFinishedEventArgs(_projectFile, succeeded));
            BuildFinished?.Invoke(_eventSender, MakeBuildFinishedEventArgs(succeeded));
        }

        [Fact]
        public void PrintsBuildSummary_Succeeded()
        {
            InvokeLoggerCallbacksForSimpleProject(succeeded: true, () => { });
            _mockTerminal.GetLastLine().WithoutAnsiCodes().ShouldBe("Build succeeded in 5.0s");
        }

        [Fact]
        public void PrintBuildSummary_SucceededWithWarnings()
        {
            InvokeLoggerCallbacksForSimpleProject(succeeded: true, () =>
            {
                WarningRaised?.Invoke(_eventSender, MakeWarningEventArgs("Warning!"));
            });
            _mockTerminal.GetLastLine().WithoutAnsiCodes().ShouldBe("Build succeeded with warnings in 5.0s");
        }

        [Fact]
        public void PrintBuildSummary_Failed()
        {
            InvokeLoggerCallbacksForSimpleProject(succeeded: false, () => { });
            _mockTerminal.GetLastLine().WithoutAnsiCodes().ShouldBe("Build failed in 5.0s");
        }

        [Fact]
        public void PrintBuildSummary_FailedWithErrors()
        {
            InvokeLoggerCallbacksForSimpleProject(succeeded: false, () =>
            {
                ErrorRaised?.Invoke(_eventSender, MakeErrorEventArgs("Error!"));
            });
            _mockTerminal.GetLastLine().WithoutAnsiCodes().ShouldBe("Build failed with errors in 5.0s");
        }

        #endregion

    }

    internal static class StringVT100Extensions
    {
        private static Regex s_removeAnsiCodes = new Regex("\\x1b\\[[0-9;]*[mGKHF]");

        public static string WithoutAnsiCodes(this string text)
        {
            return s_removeAnsiCodes.Replace(text, string.Empty);
        }
    }
}
