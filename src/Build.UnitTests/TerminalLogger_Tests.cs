// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.CommandLine.UnitTests;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging;
using Microsoft.Build.UnitTests.Shared;
using Shouldly;
using VerifyTests;
using VerifyXunit;
using Xunit;
using static VerifyXunit.Verifier;

namespace Microsoft.Build.UnitTests
{
    [UsesVerify]
    public class TerminalLogger_Tests : IEventSource, IDisposable
    {
        private const int _nodeCount = 8;
        private const string _eventSender = "Test";

        private const string _immediateMessageString =
            "The plugin credential provider could not acquire credentials." +
            "Authentication may require manual action. Consider re-running the command with --interactive for `dotnet`, " +
            "/p:NuGetInteractive=\"true\" for MSBuild or removing the -NonInteractive switch for `NuGet`";

        private readonly string _projectFile = NativeMethods.IsUnixLike ? "/src/project.proj" : @"C:\src\project.proj";
        private readonly string _projectFile2 = NativeMethods.IsUnixLike ? "/src/project2.proj" : @"C:\src\project2.proj";
        private readonly string _projectFileWithNonAnsiSymbols = NativeMethods.IsUnixLike ? "/src/проектТерминал/㐇𠁠𪨰𫠊𫦠𮚮⿕.proj" : @"C:\src\проектТерминал\㐇𠁠𪨰𫠊𫦠𮚮⿕.proj";

        private StringWriter _outputWriter = new();

        private readonly Terminal _mockTerminal;
        private readonly TerminalLogger _terminallogger;

        private readonly DateTime _buildStartTime = new DateTime(2023, 3, 30, 16, 30, 0);
        private readonly DateTime _targetStartTime = new DateTime(2023, 3, 30, 16, 30, 1);
        private readonly DateTime _messageTime = new DateTime(2023, 3, 30, 16, 30, 2);
        private readonly DateTime _buildFinishTime = new DateTime(2023, 3, 30, 16, 30, 5);


        private VerifySettings _settings = new();

        private readonly CultureInfo _originalCulture = Thread.CurrentThread.CurrentCulture;

        public TerminalLogger_Tests()
        {
            _mockTerminal = new Terminal(_outputWriter);
            _terminallogger = new TerminalLogger(_mockTerminal);

            _terminallogger.Initialize(this, _nodeCount);

            _terminallogger.CreateStopwatch = () => new MockStopwatch();

            UseProjectRelativeDirectory("Snapshots");

            // Avoids issues with different cultures on different machines
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
        }

        [Theory]
        [InlineData(null, false, false, "", typeof(ConsoleLogger))]
        [InlineData(null, true, false, "", typeof(ConsoleLogger))]
        [InlineData(null, false, true, "", typeof(ConsoleLogger))]
        [InlineData(null, true, true, "off", typeof(ConsoleLogger))]
        [InlineData(null, true, true, "false", typeof(ConsoleLogger))]
        [InlineData("--tl:off", true, true, "", typeof(ConsoleLogger))]
        [InlineData(null, true, true, "", typeof(TerminalLogger))]
        [InlineData("-tl:on", true, true, "off", typeof(TerminalLogger))]
        public void CreateTerminalOrConsoleLogger_CreatesCorrectLoggerInstance(string? argsString, bool supportsAnsi, bool outputIsScreen, string evnVariableValue, Type expectedType)
        {
            using TestEnvironment testEnvironment = TestEnvironment.Create();
            testEnvironment.SetEnvironmentVariable("MSBUILDTERMINALLOGGER", evnVariableValue);

            string[]? args = argsString?.Split(' ');
            ILogger logger = TerminalLogger.CreateTerminalOrConsoleLogger(args, supportsAnsi, outputIsScreen, default);

            logger.ShouldNotBeNull();
            logger.GetType().ShouldBe(expectedType);
        }

        [Theory]
        [InlineData("-v:q", LoggerVerbosity.Quiet)]
        [InlineData("-verbosity:minimal", LoggerVerbosity.Minimal)]
        [InlineData("--v:d", LoggerVerbosity.Detailed)]
        [InlineData("/verbosity:diag", LoggerVerbosity.Diagnostic)]
        [InlineData(null, LoggerVerbosity.Normal)]
        public void CreateTerminalOrConsoleLogger_ParsesVerbosity(string? argsString, LoggerVerbosity expectedVerbosity)
        {
            string[]? args = argsString?.Split(' ');
            ILogger logger = TerminalLogger.CreateTerminalOrConsoleLogger(args, true, true, default);

            logger.ShouldNotBeNull();
            logger.Verbosity.ShouldBe(expectedVerbosity);
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
            _terminallogger.Shutdown();
            Thread.CurrentThread.CurrentCulture = _originalCulture;
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
            return new TargetStartedEventArgs("", "", targetName, projectFile, targetFile: projectFile, String.Empty, TargetBuiltReason.None, _targetStartTime)
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
            return new BuildWarningEventArgs("", "AA0000", "directory/file", 1, 2, 3, 4, warning, null, null)
            {
                BuildEventContext = MakeBuildEventContext(),
            };
        }

        private BuildWarningEventArgs MakeCopyRetryWarning(int retryCount)
        {
            return new BuildWarningEventArgs("", "MSB3026", "directory/file", 1, 2, 3, 4,
                $"MSB3026: Could not copy \"sourcePath\" to \"destinationPath\". Beginning retry {retryCount} in x ms.",
                null, null)
            {
                BuildEventContext = MakeBuildEventContext(),
            };
        }

        private BuildMessageEventArgs MakeMessageEventArgs(string message, MessageImportance importance)
        {
            return new BuildMessageEventArgs(message, "keyword", null, importance)
            {
                BuildEventContext = MakeBuildEventContext(),
            };
        }

        private BuildMessageEventArgs MakeTaskCommandLineEventArgs(string message, MessageImportance importance)
        {
            return new TaskCommandLineEventArgs(message, "Task", importance)
            {
                BuildEventContext = MakeBuildEventContext(),
            };
        }

        private BuildMessageEventArgs MakeExtendedMessageEventArgs(string message, MessageImportance importance, string extendedType, Dictionary<string, string?>? extendedMetadata)
        {
            return new ExtendedBuildMessageEventArgs(extendedType, message, "keyword", null, importance, _messageTime)
            {
                BuildEventContext = MakeBuildEventContext(),
                ExtendedMetadata = extendedMetadata
            };
        }

        private BuildErrorEventArgs MakeErrorEventArgs(string error)
        {
            return new BuildErrorEventArgs("", "AA0000", "directory/file", 1, 2, 3, 4, error, null, null)
            {
                BuildEventContext = MakeBuildEventContext(),
            };
        }

        #endregion

        #region Build summary tests

        private void InvokeLoggerCallbacksForSimpleProject(bool succeeded, Action additionalCallbacks, string? projectFile = null)
        {
            projectFile ??= _projectFile;

            BuildStarted?.Invoke(_eventSender, MakeBuildStartedEventArgs());
            ProjectStarted?.Invoke(_eventSender, MakeProjectStartedEventArgs(projectFile));

            TargetStarted?.Invoke(_eventSender, MakeTargetStartedEventArgs(projectFile, "Build"));
            TaskStarted?.Invoke(_eventSender, MakeTaskStartedEventArgs(projectFile, "Task"));

            additionalCallbacks();

            TaskFinished?.Invoke(_eventSender, MakeTaskFinishedEventArgs(projectFile, "Task", succeeded));
            TargetFinished?.Invoke(_eventSender, MakeTargetFinishedEventArgs(projectFile, "Build", succeeded));

            ProjectFinished?.Invoke(_eventSender, MakeProjectFinishedEventArgs(projectFile, succeeded));
            BuildFinished?.Invoke(_eventSender, MakeBuildFinishedEventArgs(succeeded));
        }

        private void InvokeLoggerCallbacksForTestProject(bool succeeded, Action additionalCallbacks)
        {
            BuildStarted?.Invoke(_eventSender, MakeBuildStartedEventArgs());
            ProjectStarted?.Invoke(_eventSender, MakeProjectStartedEventArgs(_projectFile));

            TargetStarted?.Invoke(_eventSender, MakeTargetStartedEventArgs(_projectFile, "_TestRunStart"));
            TaskStarted?.Invoke(_eventSender, MakeTaskStartedEventArgs(_projectFile, "Task"));

            additionalCallbacks();

            TaskFinished?.Invoke(_eventSender, MakeTaskFinishedEventArgs(_projectFile, "Task", succeeded));
            TargetFinished?.Invoke(_eventSender, MakeTargetFinishedEventArgs(_projectFile, "_TestRunStart", succeeded));

            ProjectFinished?.Invoke(_eventSender, MakeProjectFinishedEventArgs(_projectFile, succeeded));

            BuildFinished?.Invoke(_eventSender, MakeBuildFinishedEventArgs(succeeded));
        }

        private void InvokeLoggerCallbacksForTwoProjects(bool succeeded, Action additionalCallbacks, Action additionalCallbacks2)
        {
            BuildStarted?.Invoke(_eventSender, MakeBuildStartedEventArgs());

            ProjectStarted?.Invoke(_eventSender, MakeProjectStartedEventArgs(_projectFile));
            TargetStarted?.Invoke(_eventSender, MakeTargetStartedEventArgs(_projectFile, "Build1"));
            TaskStarted?.Invoke(_eventSender, MakeTaskStartedEventArgs(_projectFile, "Task1"));

            additionalCallbacks();

            TaskFinished?.Invoke(_eventSender, MakeTaskFinishedEventArgs(_projectFile, "Task1", succeeded));
            TargetFinished?.Invoke(_eventSender, MakeTargetFinishedEventArgs(_projectFile, "Build1", succeeded));
            ProjectFinished?.Invoke(_eventSender, MakeProjectFinishedEventArgs(_projectFile, succeeded));

            ProjectStarted?.Invoke(_eventSender, MakeProjectStartedEventArgs(_projectFile2));
            TargetStarted?.Invoke(_eventSender, MakeTargetStartedEventArgs(_projectFile2, "Build2"));
            TaskStarted?.Invoke(_eventSender, MakeTaskStartedEventArgs(_projectFile2, "Task2"));

            additionalCallbacks2();

            TaskFinished?.Invoke(_eventSender, MakeTaskFinishedEventArgs(_projectFile2, "Task2", succeeded));
            TargetFinished?.Invoke(_eventSender, MakeTargetFinishedEventArgs(_projectFile2, "Build2", succeeded));
            ProjectFinished?.Invoke(_eventSender, MakeProjectFinishedEventArgs(_projectFile2, succeeded));

            BuildFinished?.Invoke(_eventSender, MakeBuildFinishedEventArgs(succeeded));
        }

        [Fact]
        public Task PrintsBuildSummary_Succeeded()
        {
            InvokeLoggerCallbacksForSimpleProject(succeeded: true, () => { });

            return Verify(_outputWriter.ToString(), _settings).UniqueForOSPlatform();
        }

        [Fact]
        public Task PrintBuildSummary_SucceededWithWarnings()
        {
            InvokeLoggerCallbacksForSimpleProject(succeeded: true, () =>
            {
                WarningRaised?.Invoke(_eventSender, MakeWarningEventArgs("A\nMulti\r\nLine\nWarning!"));
            });

            return Verify(_outputWriter.ToString(), _settings).UniqueForOSPlatform();
        }

        [Fact]
        public Task PrintImmediateWarningMessage_Succeeded()
        {
            InvokeLoggerCallbacksForSimpleProject(succeeded: true, () =>
            {
                WarningRaised?.Invoke(_eventSender, MakeWarningEventArgs("[CredentialProvider]DeviceFlow: https://testfeed/index.json"));
                WarningRaised?.Invoke(_eventSender, MakeWarningEventArgs(
                    "[CredentialProvider]ATTENTION: User interaction required." +
                    "**********************************************************************" +
                    "To sign in, use a web browser to open the page https://devicelogin and enter the code XXXXXX to authenticate." +
                    "**********************************************************************"));
            });

            return Verify(_outputWriter.ToString(), _settings).UniqueForOSPlatform();
        }

        [Fact]
        public Task PrintCopyTaskRetryWarningAsImmediateMessage_Failed()
        {
            InvokeLoggerCallbacksForSimpleProject(succeeded: false, () =>
            {
                WarningRaised?.Invoke(_eventSender, MakeCopyRetryWarning(1));
                WarningRaised?.Invoke(_eventSender, MakeCopyRetryWarning(2));
                WarningRaised?.Invoke(_eventSender, MakeCopyRetryWarning(3));
            });

            return Verify(_outputWriter.ToString(), _settings).UniqueForOSPlatform();
        }

        [Fact]
        public Task PrintImmediateMessage_Success()
        {
            InvokeLoggerCallbacksForSimpleProject(succeeded: true, () =>
            {
                MessageRaised?.Invoke(_eventSender, MakeMessageEventArgs(_immediateMessageString, MessageImportance.High));
            });

            return Verify(_outputWriter.ToString(), _settings).UniqueForOSPlatform();
        }

        [Fact]
        public Task PrintImmediateMessage_Skipped()
        {
            InvokeLoggerCallbacksForSimpleProject(succeeded: true, () =>
            {
                MessageRaised?.Invoke(_eventSender, MakeMessageEventArgs("--anycustomarg", MessageImportance.High));
            });

            return Verify(_outputWriter.ToString(), _settings).UniqueForOSPlatform();
        }

        [Fact]
        public Task PrintRestore_Failed()
        {
            BuildStarted?.Invoke(_eventSender, MakeBuildStartedEventArgs());

            bool succeeded = false;
            ErrorRaised?.Invoke(_eventSender, MakeErrorEventArgs("Restore Failed"));

            ProjectFinished?.Invoke(_eventSender, MakeProjectFinishedEventArgs(_projectFile, succeeded));
            BuildFinished?.Invoke(_eventSender, MakeBuildFinishedEventArgs(succeeded));

            return Verify(_outputWriter.ToString(), _settings).UniqueForOSPlatform();
        }

        [Fact]
        public Task PrintRestore_SuccessWithWarnings()
        {
            BuildStarted?.Invoke(_eventSender, MakeBuildStartedEventArgs());

            bool succeeded = true;
            WarningRaised?.Invoke(_eventSender, MakeWarningEventArgs("Restore with Warning"));

            ProjectFinished?.Invoke(_eventSender, MakeProjectFinishedEventArgs(_projectFile, succeeded));
            BuildFinished?.Invoke(_eventSender, MakeBuildFinishedEventArgs(succeeded));

            return Verify(_outputWriter.ToString(), _settings).UniqueForOSPlatform();
        }

        [Fact]
        public Task PrintBuildSummary_Failed()
        {
            InvokeLoggerCallbacksForSimpleProject(succeeded: false, () => { });
            return Verify(_outputWriter.ToString(), _settings).UniqueForOSPlatform();
        }

        [Fact]
        public Task PrintBuildSummary_FailedWithErrors()
        {
            InvokeLoggerCallbacksForSimpleProject(succeeded: false, () =>
            {
                ErrorRaised?.Invoke(_eventSender, MakeErrorEventArgs("Error!"));
            });

            return Verify(_outputWriter.ToString(), _settings).UniqueForOSPlatform();
        }

        [Fact]
        public Task PrintDetailedBuildSummary_FailedWithErrorAndWarning()
        {
            string? originalParameters = _terminallogger.Parameters;
            _terminallogger.Parameters = "SUMMARY";
            _terminallogger.ParseParameters();

            InvokeLoggerCallbacksForSimpleProject(succeeded: false, () =>
            {
                WarningRaised?.Invoke(_eventSender, MakeWarningEventArgs("Warning!"));
                ErrorRaised?.Invoke(_eventSender, MakeErrorEventArgs("Error!"));
            });

            // Restore original parameters
            _terminallogger.Parameters = originalParameters;
            _terminallogger.ParseParameters();

            return Verify(_outputWriter.ToString(), _settings).UniqueForOSPlatform();
        }

        [Fact]
        public Task PrintBuildSummary_FailedWithErrorsAndWarnings()
        {
            InvokeLoggerCallbacksForSimpleProject(succeeded: false, () =>
            {
                WarningRaised?.Invoke(_eventSender, MakeWarningEventArgs("Warning1!"));
                WarningRaised?.Invoke(_eventSender, MakeWarningEventArgs("Warning2!"));
                ErrorRaised?.Invoke(_eventSender, MakeErrorEventArgs("Error1!"));
                ErrorRaised?.Invoke(_eventSender, MakeErrorEventArgs("Error2!"));
            });

            return Verify(_outputWriter.ToString(), _settings).UniqueForOSPlatform();
        }


        [Fact]
        public Task PrintBuildSummary_2Projects_FailedWithErrorsAndWarnings()
        {
            InvokeLoggerCallbacksForTwoProjects(
                succeeded: false,
                () =>
                {
                    WarningRaised?.Invoke(_eventSender, MakeWarningEventArgs("Warning1!"));
                    WarningRaised?.Invoke(_eventSender, MakeWarningEventArgs("Warning2!"));
                    ErrorRaised?.Invoke(_eventSender, MakeErrorEventArgs("Error1!"));
                    ErrorRaised?.Invoke(_eventSender, MakeErrorEventArgs("Error2!"));
                },
                () =>
                {
                    WarningRaised?.Invoke(_eventSender, MakeWarningEventArgs("Warning3!"));
                    WarningRaised?.Invoke(_eventSender, MakeWarningEventArgs("Warning4!"));
                    ErrorRaised?.Invoke(_eventSender, MakeErrorEventArgs("Error3!"));
                    ErrorRaised?.Invoke(_eventSender, MakeErrorEventArgs("Error4!"));
                });

            return Verify(_outputWriter.ToString(), _settings).UniqueForOSPlatform();
        }

        [Fact]
        public Task PrintProjectOutputDirectoryLink()
        {
            // Send message in order to set project output path
            BuildMessageEventArgs e = MakeMessageEventArgs(
                    $"㐇𠁠𪨰𫠊𫦠𮚮⿕ -> {_projectFileWithNonAnsiSymbols.Replace("proj", "dll")}",
                    MessageImportance.High);
            e.ProjectFile = _projectFileWithNonAnsiSymbols;

            InvokeLoggerCallbacksForSimpleProject(succeeded: true, () =>
            {
                MessageRaised?.Invoke(_eventSender, e);
            }, _projectFileWithNonAnsiSymbols);

            return Verify(_outputWriter.ToString(), _settings).UniqueForOSPlatform();
        }

        #endregion

        private void CallAllTypesOfMessagesWarningAndError()
        {
            MessageRaised?.Invoke(_eventSender, MakeMessageEventArgs(_immediateMessageString, MessageImportance.High));
            MessageRaised?.Invoke(_eventSender, MakeMessageEventArgs("High importance message!", MessageImportance.High));
            MessageRaised?.Invoke(_eventSender, MakeMessageEventArgs("Normal importance message!", MessageImportance.Normal));
            MessageRaised?.Invoke(_eventSender, MakeMessageEventArgs("Low importance message!", MessageImportance.Low));
            WarningRaised?.Invoke(_eventSender, MakeWarningEventArgs("Warning!"));
            WarningRaised?.Invoke(_eventSender, MakeWarningEventArgs("A\nMulti\r\nLine\nWarning!"));
            ErrorRaised?.Invoke(_eventSender, MakeErrorEventArgs("Error!"));
        }

        private void CallAllTypesOfTestMessages()
        {
            MessageRaised?.Invoke(_eventSender, MakeExtendedMessageEventArgs(
                "Test passed.",
                MessageImportance.High,
                "TLTESTPASSED",
                new Dictionary<string, string?>() { { "displayName", "testName1" }, { "localizedResult", "passed" } }));
            MessageRaised?.Invoke(_eventSender, MakeExtendedMessageEventArgs(
                "Test skipped.",
                MessageImportance.High,
                "TLTESTSKIPPED",
                new Dictionary<string, string?>() { { "displayName", "testName2" }, { "localizedResult", "skipped" } }));
            MessageRaised?.Invoke(_eventSender, MakeExtendedMessageEventArgs(
                "Test results.",
                MessageImportance.High,
                "TLTESTFINISH",
                new Dictionary<string, string?>() { { "total", "10" }, { "passed", "7" }, { "skipped", "2" }, { "failed", "1" } }));
        }

        [Fact]
        public Task PrintBuildSummaryQuietVerbosity_FailedWithErrors()
        {
            _terminallogger.Verbosity = LoggerVerbosity.Quiet;
            InvokeLoggerCallbacksForSimpleProject(succeeded: false, CallAllTypesOfMessagesWarningAndError);

            return Verify(_outputWriter.ToString(), _settings).UniqueForOSPlatform();
        }


        [Fact]
        public Task PrintBuildSummaryMinimalVerbosity_FailedWithErrors()
        {
            _terminallogger.Verbosity = LoggerVerbosity.Minimal;
            InvokeLoggerCallbacksForSimpleProject(succeeded: false, CallAllTypesOfMessagesWarningAndError);

            return Verify(_outputWriter.ToString(), _settings).UniqueForOSPlatform();
        }

        [Fact]
        public Task PrintBuildSummaryNormalVerbosity_FailedWithErrors()
        {
            _terminallogger.Verbosity = LoggerVerbosity.Normal;
            InvokeLoggerCallbacksForSimpleProject(succeeded: false, CallAllTypesOfMessagesWarningAndError);

            return Verify(_outputWriter.ToString(), _settings).UniqueForOSPlatform();
        }

        [Fact]
        public Task PrintBuildSummaryDetailedVerbosity_FailedWithErrors()
        {
            _terminallogger.Verbosity = LoggerVerbosity.Detailed;
            InvokeLoggerCallbacksForSimpleProject(succeeded: false, CallAllTypesOfMessagesWarningAndError);

            return Verify(_outputWriter.ToString(), _settings).UniqueForOSPlatform();
        }


        [Fact]
        public Task PrintBuildSummaryDiagnosticVerbosity_FailedWithErrors()
        {
            _terminallogger.Verbosity = LoggerVerbosity.Diagnostic;
            InvokeLoggerCallbacksForSimpleProject(succeeded: false, CallAllTypesOfMessagesWarningAndError);

            return Verify(_outputWriter.ToString(), _settings).UniqueForOSPlatform();
        }

        [Fact]
        public Task PrintTestSummaryNormalVerbosity_Succeeded()
        {
            _terminallogger.Verbosity = LoggerVerbosity.Normal;
            InvokeLoggerCallbacksForTestProject(succeeded: true, CallAllTypesOfTestMessages);

            return Verify(_outputWriter.ToString(), _settings).UniqueForOSPlatform();
        }

        [Fact]
        public Task PrintTestSummaryQuietVerbosity_Succeeded()
        {
            _terminallogger.Verbosity = LoggerVerbosity.Quiet;
            InvokeLoggerCallbacksForTestProject(succeeded: true, CallAllTypesOfTestMessages);

            return Verify(_outputWriter.ToString(), _settings).UniqueForOSPlatform();
        }

        [Fact]
        public Task PrintSummaryWithOverwrittenVerbosity_FailedWithErrors()
        {
            _terminallogger.Verbosity = LoggerVerbosity.Minimal;
            _terminallogger.Parameters = "v=diag";
            _terminallogger.ParseParameters();

            InvokeLoggerCallbacksForSimpleProject(succeeded: false, CallAllTypesOfMessagesWarningAndError);

            return Verify(_outputWriter.ToString(), _settings).UniqueForOSPlatform();
        }

        [Fact]
        public Task PrintSummaryWithTaskCommandLineEventArgs_Succeeded()
        {
            _terminallogger.Verbosity = LoggerVerbosity.Detailed;
            _terminallogger.Parameters = "SHOWCOMMANDLINE=on";
            _terminallogger.ParseParameters();

            InvokeLoggerCallbacksForSimpleProject(succeeded: true, () =>
            {
                MessageRaised?.Invoke(_eventSender, MakeTaskCommandLineEventArgs("Task Command Line.", MessageImportance.High));
            });

            return Verify(_outputWriter.ToString(), _settings).UniqueForOSPlatform();
        }

        [Fact]
        public Task PrintSummaryWithoutTaskCommandLineEventArgs_Succeeded()
        {
            _terminallogger.Verbosity = LoggerVerbosity.Detailed;
            _terminallogger.Parameters = "SHOWCOMMANDLINE=off";
            _terminallogger.ParseParameters();

            InvokeLoggerCallbacksForSimpleProject(succeeded: true, () =>
            {
                MessageRaised?.Invoke(_eventSender, MakeTaskCommandLineEventArgs("Task Command Line.", MessageImportance.High));
            });

            return Verify(_outputWriter.ToString(), _settings).UniqueForOSPlatform();
        }

        [Fact]
        public void DisplayNodesShowsCurrent()
        {
            InvokeLoggerCallbacksForSimpleProject(succeeded: false, async () =>
            {
                _terminallogger.DisplayNodes();

                await Verify(_outputWriter.ToString(), _settings).UniqueForOSPlatform();
            });
        }

        [Fact]
        public void DisplayNodesOverwritesTime()
        {
            List<MockStopwatch> stopwatches = new();

            Func<StopwatchAbstraction>? createStopwatch = _terminallogger.CreateStopwatch;

            try
            {
                _terminallogger.CreateStopwatch = () =>
                {
                    MockStopwatch stopwatch = new();
                    stopwatches.Add(stopwatch);
                    return stopwatch;
                };

                InvokeLoggerCallbacksForSimpleProject(succeeded: false, async () =>
                {
                    foreach (var stopwatch in stopwatches)
                    {
                        // Tick time forward by at least 10 seconds,
                        // as a regression test for https://github.com/dotnet/msbuild/issues/9562
                        stopwatch.Tick(111.0);
                    }

                    _terminallogger.DisplayNodes();

                    await Verify(_outputWriter.ToString(), _settings).UniqueForOSPlatform();
                });
            }
            finally
            {
                _terminallogger.CreateStopwatch = createStopwatch;
            }
        }


        [Fact]
        public async Task DisplayNodesOverwritesWithNewTargetFramework()
        {
            BuildStarted?.Invoke(_eventSender, MakeBuildStartedEventArgs());

            ProjectStartedEventArgs pse = MakeProjectStartedEventArgs(_projectFile, "Build");
            pse.GlobalProperties = new Dictionary<string, string>() { ["TargetFramework"] = "tfName" };

            ProjectStarted?.Invoke(_eventSender, pse);

            TargetStarted?.Invoke(_eventSender, MakeTargetStartedEventArgs(_projectFile, "Build"));
            TaskStarted?.Invoke(_eventSender, MakeTaskStartedEventArgs(_projectFile, "Task"));

            _terminallogger.DisplayNodes();

            // This is a bit fast and loose with the events that would be fired
            // in a real "stop building that TF for the project and start building
            // a new TF of the same project" situation, but it's enough now.
            ProjectStartedEventArgs pse2 = MakeProjectStartedEventArgs(_projectFile, "Build");
            pse2.GlobalProperties = new Dictionary<string, string>() { ["TargetFramework"] = "tf2" };

            ProjectStarted?.Invoke(_eventSender, pse2);
            TargetStarted?.Invoke(_eventSender, MakeTargetStartedEventArgs(_projectFile, "Build"));

            _terminallogger.DisplayNodes();

            await Verify(_outputWriter.ToString(), _settings).UniqueForOSPlatform();
        }

        [Fact]
        public void TestTerminalLoggerTogetherWithOtherLoggers()
        {
            using (TestEnvironment env = TestEnvironment.Create())
            {
                string contents = @"
<Project>
    <ItemGroup>
        <Compile Include=""MyItem1.cs"" />
        <Compile Include=""MyItem2.cs"" />
    </ItemGroup>
    <PropertyGroup>
        <MyProp1>MyProperty1</MyProp1>
    </PropertyGroup>
    <Target Name = ""Build"">
        <Message Text = ""Build target is executing."" Importance = ""High"" />
    </Target>
</Project>";
                TransientTestFolder logFolder = env.CreateFolder(createFolder: true);
                TransientTestFile projectFile = env.CreateFile(logFolder, "myProj.proj", contents);

                string logFileWithTL = env.ExpectFile(".binlog").Path;
                string logFileWithoutTL = env.ExpectFile(".binlog").Path;

                // Execute MSBuild with binary, file and terminal loggers
                RunnerUtilities.ExecMSBuild($"{projectFile.Path} /m /bl:{logFileWithTL} -flp:logfile={Path.Combine(logFolder.Path, "logFileWithTL.log")};verbosity=diagnostic -tl:on", out bool success);
                success.ShouldBeTrue();

                // Execute MSBuild with binary and file loggers
                RunnerUtilities.ExecMSBuild($"{projectFile.Path} /m /bl:{logFileWithoutTL} -flp:logfile={Path.Combine(logFolder.Path, "logFileWithoutTL.log")};verbosity=diagnostic", out success);
                success.ShouldBeTrue();

                // Read the binary log and replay into mockLogger
                var mockLogFromPlaybackWithTL = new MockLogger();
                var binaryLogReaderWithTL = new BinaryLogReplayEventSource();
                mockLogFromPlaybackWithTL.Initialize(binaryLogReaderWithTL);

                var mockLogFromPlaybackWithoutTL = new MockLogger();
                var binaryLogReaderWithoutTL = new BinaryLogReplayEventSource();
                mockLogFromPlaybackWithoutTL.Initialize(binaryLogReaderWithoutTL);

                binaryLogReaderWithTL.Replay(logFileWithTL);
                binaryLogReaderWithoutTL.Replay(logFileWithoutTL);

                // Check that amount of events, warnings, errors is equal in both cases. Presence of other loggers should not change behavior
                mockLogFromPlaybackWithoutTL.Errors.Count.ShouldBe(mockLogFromPlaybackWithTL.Errors.Count);
                mockLogFromPlaybackWithoutTL.Warnings.Count.ShouldBe(mockLogFromPlaybackWithTL.Warnings.Count);
                mockLogFromPlaybackWithoutTL.AllBuildEvents.Count.ShouldBe(mockLogFromPlaybackWithTL.AllBuildEvents.Count);

                // Check presence of some items and properties and that they have at least 1 item and property
                mockLogFromPlaybackWithoutTL.EvaluationFinishedEvents.ShouldContain(x => (x.Items != null) && x.Items.GetEnumerator().MoveNext());
                mockLogFromPlaybackWithTL.EvaluationFinishedEvents.ShouldContain(x => (x.Items != null) && x.Items.GetEnumerator().MoveNext());

                mockLogFromPlaybackWithoutTL.EvaluationFinishedEvents.ShouldContain(x => (x.Properties != null) && x.Properties.GetEnumerator().MoveNext());
                mockLogFromPlaybackWithTL.EvaluationFinishedEvents.ShouldContain(x => (x.Properties != null) && x.Properties.GetEnumerator().MoveNext());
            }
        }
    }
}
