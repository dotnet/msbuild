// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.CommandLine.UnitTests;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging;
using Microsoft.Build.UnitTests.Shared;
using Shouldly;
using VerifyTests;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;
using Xunit.NetCore.Extensions;
using static VerifyXunit.Verifier;

namespace Microsoft.Build.UnitTests
{
    [UsesVerify]
    [UseInvariantCulture]
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

        private readonly ITestOutputHelper _outputHelper;

        public TerminalLogger_Tests(ITestOutputHelper outputHelper)
        {
            _outputHelper = outputHelper;
            _mockTerminal = new Terminal(_outputWriter);
            _terminallogger = new TerminalLogger(_mockTerminal);

            _terminallogger.Initialize(this, _nodeCount);

            _terminallogger.CreateStopwatch = () => new MockStopwatch();

            UseProjectRelativeDirectory("Snapshots");
        }

        [Theory]
        [InlineData(null, false, false, "", typeof(ConsoleLogger))]
        [InlineData(null, true, false, "", typeof(ConsoleLogger))]
        [InlineData(null, false, true, "", typeof(ConsoleLogger))]
        [InlineData(null, true, true, "off", typeof(ConsoleLogger))]
        [InlineData(null, true, true, "false", typeof(ConsoleLogger))]
        [InlineData("--tl:off", true, true, "", typeof(ConsoleLogger))]
        [InlineData("--tl:false", true, true, "", typeof(ConsoleLogger))]
        [InlineData(null, true, true, "", typeof(TerminalLogger))]
        [InlineData("-tl:on", true, true, "off", typeof(TerminalLogger))] // arg overrides env
        [InlineData("-tl:true", true, true, "off", typeof(TerminalLogger))] // arg overrides env
        [InlineData("-tl:off", true, true, "on", typeof(ConsoleLogger))] // arg overrides env (disable)
        [InlineData("-tl:false", true, true, "true", typeof(ConsoleLogger))] // arg overrides env (disable)
        [InlineData("-tl:on", false, false, "", typeof(TerminalLogger))] // Force when explicitly set to "on"
        [InlineData("-tl:true", false, false, "", typeof(TerminalLogger))] // Force when explicitly set to "true"
        [InlineData("-tl:on", true, false, "", typeof(TerminalLogger))] // Force when explicitly set to "on"
        [InlineData("-tl:true", false, true, "", typeof(TerminalLogger))] // Force when explicitly set to "true"
        [InlineData(null, false, false, "on", typeof(TerminalLogger))] // Force when env var set to "on"
        [InlineData(null, false, false, "true", typeof(TerminalLogger))] // Force when env var set to "true"
        [InlineData(null, true, false, "on", typeof(TerminalLogger))] // Force when env var set to "on"
        [InlineData(null, false, true, "true", typeof(TerminalLogger))] // Force when env var set to "true"
        [InlineData("-tl:auto", false, false, "", typeof(ConsoleLogger))] // Auto respects system capabilities (no ANSI, no screen)
        [InlineData("-tl:auto", true, false, "", typeof(ConsoleLogger))] // Auto respects system capabilities (ANSI but no screen)
        [InlineData("-tl:auto", false, true, "", typeof(ConsoleLogger))] // Auto respects system capabilities (screen but no ANSI)
        [InlineData("-tl:auto", true, true, "", typeof(TerminalLogger))] // Auto respects system capabilities (both ANSI and screen)
        [InlineData("-tl:auto", true, true, "off", typeof(TerminalLogger))] // Auto ignores env var when explicitly set
        [InlineData("-tl:auto", false, false, "on", typeof(ConsoleLogger))] // Auto ignores env var and respects system capabilities
        [InlineData(null, false, false, "auto", typeof(ConsoleLogger))] // Auto via env var respects system capabilities
        [InlineData(null, true, true, "auto", typeof(TerminalLogger))] // Auto via env var respects system capabilities
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
        }

        #endregion

        #region Event args helpers

        /// <summary>
        /// Helper function to create a BuildEventContext keyed to specific scenarios.
        /// When you want to refer to the same eval properties, use the same evalId.
        /// When you want to refer to the same project, use the same projectContextId.
        /// By default, nodeId, evalId, projectContextId, and targetId are all set to 1.
        /// </summary>
        private BuildEventContext MakeBuildEventContext(int evalId = 1, int projectContextId = 1)
        {
            return new BuildEventContext(
            submissionId: -1,
            nodeId: 1,
            evaluationId: evalId,
            projectInstanceId: -1,
            projectContextId: projectContextId,
            targetId: 1,
            taskId: 1);
        }

        private BuildStartedEventArgs MakeBuildStartedEventArgs(BuildEventContext? buildEventContext = null)
        {
            return new BuildStartedEventArgs(null, null, _buildStartTime)
            {
                BuildEventContext = buildEventContext ?? MakeBuildEventContext(),
            };
        }

        private BuildFinishedEventArgs MakeBuildFinishedEventArgs(bool succeeded, BuildEventContext? buildEventContext = null)
        {
            return new BuildFinishedEventArgs(null, null, succeeded, _buildFinishTime)
            {
                BuildEventContext = buildEventContext ?? MakeBuildEventContext(),
            };
        }

        private ProjectStartedEventArgs MakeProjectStartedEventArgs(string projectFile, string targetNames = "Build", BuildEventContext? buildEventContext = null)
        {
            return new ProjectStartedEventArgs("", "", projectFile, targetNames, new Dictionary<string, string>(), new List<DictionaryEntry>())
            {
                BuildEventContext = buildEventContext ?? MakeBuildEventContext(),
            };
        }

        private ProjectFinishedEventArgs MakeProjectFinishedEventArgs(string projectFile, bool succeeded, BuildEventContext? buildEventContext = null)
        {
            return new ProjectFinishedEventArgs(null, null, projectFile, succeeded)
            {
                BuildEventContext = buildEventContext ?? MakeBuildEventContext(),
            };
        }

        private TargetStartedEventArgs MakeTargetStartedEventArgs(string projectFile, string targetName, BuildEventContext? buildEventContext = null)
        {
            return new TargetStartedEventArgs("", "", targetName, projectFile, targetFile: projectFile, String.Empty, TargetBuiltReason.None, _targetStartTime)
            {
                BuildEventContext = buildEventContext ?? MakeBuildEventContext(),
            };
        }

        private TargetFinishedEventArgs MakeTargetFinishedEventArgs(string projectFile, string targetName, bool succeeded, BuildEventContext? buildEventContext = null)
        {
            return new TargetFinishedEventArgs("", "", targetName, projectFile, targetFile: projectFile, succeeded)
            {
                BuildEventContext = buildEventContext ?? MakeBuildEventContext(),
            };
        }

        private TaskStartedEventArgs MakeTaskStartedEventArgs(string projectFile, string taskName, BuildEventContext? buildEventContext = null)
        {
            return new TaskStartedEventArgs("", "", projectFile, taskFile: projectFile, taskName)
            {
                BuildEventContext = buildEventContext ?? MakeBuildEventContext(),
            };
        }

        private TaskFinishedEventArgs MakeTaskFinishedEventArgs(string projectFile, string taskName, bool succeeded, BuildEventContext? buildEventContext = null)
        {
            return new TaskFinishedEventArgs("", "", projectFile, taskFile: projectFile, taskName, succeeded)
            {
                BuildEventContext = buildEventContext ?? MakeBuildEventContext(),
            };
        }

        private BuildWarningEventArgs MakeCopyRetryWarning(int retryCount, BuildEventContext? buildEventContext = null)
        {
            return new BuildWarningEventArgs("", "MSB3026", "directory/file", 1, 2, 3, 4,
                $"MSB3026: Could not copy \"sourcePath\" to \"destinationPath\". Beginning retry {retryCount} in x ms.",
                null, null)
            {
                BuildEventContext = buildEventContext ?? MakeBuildEventContext(),
            };
        }

        private BuildMessageEventArgs MakeMessageEventArgs(string message, MessageImportance importance, string? code = null, string? keyword = "keyword", BuildEventContext? buildEventContext = null)
        {
            return new BuildMessageEventArgs(message: message, helpKeyword: keyword, senderName: null, importance: importance, eventTimestamp: DateTime.UtcNow, lineNumber: 0, columnNumber: 0, endLineNumber: 0, endColumnNumber: 0, code: code, subcategory: null, file: null)
            {
                BuildEventContext = buildEventContext ?? MakeBuildEventContext(),
            };
        }

        private BuildMessageEventArgs MakeBuildOutputEventArgs(string projectFilePath, BuildEventContext? buildEventContext = null)
        {
            var projectName = Path.GetFileNameWithoutExtension(projectFilePath);
            var outputPath = Path.ChangeExtension(projectFilePath, "dll");
            var messageString = $"{projectName} -> {outputPath}";
            var args = MakeMessageEventArgs(messageString, MessageImportance.High, buildEventContext: buildEventContext);
            args.ProjectFile = projectFilePath;
            return args;
        }

        private BuildMessageEventArgs MakeTaskCommandLineEventArgs(string message, MessageImportance importance, BuildEventContext? buildEventContext = null)
        {
            return new TaskCommandLineEventArgs(message, "Task", importance)
            {
                BuildEventContext = buildEventContext ?? MakeBuildEventContext(),
            };
        }

        private BuildMessageEventArgs MakeExtendedMessageEventArgs(string message, MessageImportance importance, string extendedType, Dictionary<string, string?>? extendedMetadata, BuildEventContext? buildEventContext = null)
        {
            return new ExtendedBuildMessageEventArgs(extendedType, message, "keyword", null, importance, _messageTime)
            {
                BuildEventContext = buildEventContext ?? MakeBuildEventContext(),
                ExtendedMetadata = extendedMetadata
            };
        }

        private BuildErrorEventArgs MakeErrorEventArgs(string error, string? link = null, string? keyword = null, BuildEventContext? buildEventContext = null)
        {
            return new BuildErrorEventArgs(subcategory: null, code: "AA0000", file: "directory/file", lineNumber: 1, columnNumber: 2, endLineNumber: 3, endColumnNumber: 4, message: error, helpKeyword: keyword, helpLink: link, senderName: null, eventTimestamp: DateTime.UtcNow)
            {
                BuildEventContext = buildEventContext ?? MakeBuildEventContext(),
            };
        }

        private BuildWarningEventArgs MakeWarningEventArgs(string warning, string? link = null, string? keyword = null, BuildEventContext? buildEventContext = null)
        {
            return new BuildWarningEventArgs(subcategory: null, code: "AA0000", file: "directory/file", lineNumber: 1, columnNumber: 2, endLineNumber: 3, endColumnNumber: 4, message: warning, helpKeyword: keyword, helpLink: link, senderName: null, eventTimestamp: DateTime.UtcNow)
            {
                BuildEventContext = buildEventContext ?? MakeBuildEventContext(),
            };
        }

        #endregion

        #region Build summary tests

        private void InvokeLoggerCallbacksForSimpleProject(bool succeeded, Action? additionalCallbacks = null, string? projectFile = null, List<(string, string)>? properties = null)
        {
            projectFile ??= _projectFile;

            BuildStarted?.Invoke(_eventSender, MakeBuildStartedEventArgs());
            StatusEventRaised?.Invoke(_eventSender, MakeProjectEvalFinishedArgs(projectFile, properties: properties));

            ProjectStarted?.Invoke(_eventSender, MakeProjectStartedEventArgs(projectFile));

            TargetStarted?.Invoke(_eventSender, MakeTargetStartedEventArgs(projectFile, "Build"));
            TaskStarted?.Invoke(_eventSender, MakeTaskStartedEventArgs(projectFile, "Task"));

            additionalCallbacks?.Invoke();

            TaskFinished?.Invoke(_eventSender, MakeTaskFinishedEventArgs(projectFile, "Task", succeeded));
            TargetFinished?.Invoke(_eventSender, MakeTargetFinishedEventArgs(projectFile, "Build", succeeded));

            ProjectFinished?.Invoke(_eventSender, MakeProjectFinishedEventArgs(projectFile, succeeded));
            BuildFinished?.Invoke(_eventSender, MakeBuildFinishedEventArgs(succeeded));
        }

        private ProjectEvaluationFinishedEventArgs MakeProjectEvalFinishedArgs(string projectFile, List<(string, string)>? properties = null, List<(string, string)>? items = null, BuildEventContext? buildEventContext = null)
        {
            return new ProjectEvaluationFinishedEventArgs
            {
                ProjectFile = projectFile,
                Properties = properties?.ToDictionary(k => k.Item1, v => v.Item2) ?? new Dictionary<string, string>(),
                Items = items?.Select(kvp => new DictionaryEntry(kvp.Item1, kvp.Item2)).ToList() ?? new List<DictionaryEntry>(),
                BuildEventContext = buildEventContext ?? MakeBuildEventContext(),
            };
        }

        private void InvokeLoggerCallbacksForTestProject(bool succeeded, Action additionalCallbacks)
        {
            BuildStarted?.Invoke(_eventSender, MakeBuildStartedEventArgs());
            StatusEventRaised?.Invoke(_eventSender, MakeProjectEvalFinishedArgs(_projectFile));
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
            var p1BuildContext = MakeBuildEventContext(evalId: 1, projectContextId: 1);
            StatusEventRaised?.Invoke(_eventSender, MakeProjectEvalFinishedArgs(_projectFile, buildEventContext: p1BuildContext));
            ProjectStarted?.Invoke(_eventSender, MakeProjectStartedEventArgs(_projectFile, buildEventContext: p1BuildContext));
            TargetStarted?.Invoke(_eventSender, MakeTargetStartedEventArgs(_projectFile, "Build1", buildEventContext: p1BuildContext));
            TaskStarted?.Invoke(_eventSender, MakeTaskStartedEventArgs(_projectFile, "Task1", buildEventContext: p1BuildContext));

            additionalCallbacks();

            TaskFinished?.Invoke(_eventSender, MakeTaskFinishedEventArgs(_projectFile, "Task1", succeeded, buildEventContext: p1BuildContext));
            TargetFinished?.Invoke(_eventSender, MakeTargetFinishedEventArgs(_projectFile, "Build1", succeeded, buildEventContext: p1BuildContext));
            ProjectFinished?.Invoke(_eventSender, MakeProjectFinishedEventArgs(_projectFile, succeeded, buildEventContext: p1BuildContext));

            var p2BuildContext = MakeBuildEventContext(evalId: 2, projectContextId: 2);
            StatusEventRaised?.Invoke(_eventSender, MakeProjectEvalFinishedArgs(_projectFile2, buildEventContext: p2BuildContext));
            ProjectStarted?.Invoke(_eventSender, MakeProjectStartedEventArgs(_projectFile2, buildEventContext: p2BuildContext));
            TargetStarted?.Invoke(_eventSender, MakeTargetStartedEventArgs(_projectFile2, "Build2", buildEventContext: p2BuildContext));
            TaskStarted?.Invoke(_eventSender, MakeTaskStartedEventArgs(_projectFile2, "Task2", buildEventContext: p2BuildContext));

            additionalCallbacks2();

            TaskFinished?.Invoke(_eventSender, MakeTaskFinishedEventArgs(_projectFile2, "Task2", succeeded, buildEventContext: p2BuildContext));
            TargetFinished?.Invoke(_eventSender, MakeTargetFinishedEventArgs(_projectFile2, "Build2", succeeded, buildEventContext: p2BuildContext));
            ProjectFinished?.Invoke(_eventSender, MakeProjectFinishedEventArgs(_projectFile2, succeeded, buildEventContext: p2BuildContext));

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
                    var p1Context = MakeBuildEventContext(evalId: 1, projectContextId: 1);
                    WarningRaised?.Invoke(_eventSender, MakeWarningEventArgs("Warning1!", buildEventContext: p1Context));
                    WarningRaised?.Invoke(_eventSender, MakeWarningEventArgs("Warning2!", buildEventContext: p1Context));
                    ErrorRaised?.Invoke(_eventSender, MakeErrorEventArgs("Error1!", buildEventContext: p1Context));
                    ErrorRaised?.Invoke(_eventSender, MakeErrorEventArgs("Error2!", buildEventContext: p1Context));
                },
                () =>
                {
                    var p2Context = MakeBuildEventContext(evalId: 2, projectContextId: 2);
                    WarningRaised?.Invoke(_eventSender, MakeWarningEventArgs("Warning3!", buildEventContext: p2Context));
                    WarningRaised?.Invoke(_eventSender, MakeWarningEventArgs("Warning4!", buildEventContext: p2Context));
                    ErrorRaised?.Invoke(_eventSender, MakeErrorEventArgs("Error3!", buildEventContext: p2Context));
                    ErrorRaised?.Invoke(_eventSender, MakeErrorEventArgs("Error4!", buildEventContext: p2Context));
                });

            return Verify(_outputWriter.ToString(), _settings).UniqueForOSPlatform();
        }

        [Fact]
        public Task PrintProjectOutputDirectoryLink()
        {
            // Send message in order to set project output path
            BuildMessageEventArgs e = MakeBuildOutputEventArgs(_projectFileWithNonAnsiSymbols);
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
        public Task LogEvaluationErrorFromEngine()
        {
            _terminallogger.Verbosity = LoggerVerbosity.Normal;
            InvokeLoggerCallbacksForSimpleProject(succeeded: false, () =>
            {
                ErrorRaised?.Invoke(_eventSender, new BuildErrorEventArgs(
                    "MSB0001", "EvaluationError", "MSBUILD", 0, 0, 0, 0,
                    "An error occurred during evaluation.", null, null)
                {
                    BuildEventContext = new BuildEventContext(1, -1, -1, -1) // context that belongs to no project
                });
            });

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
            StatusEventRaised?.Invoke(_eventSender, MakeProjectEvalFinishedArgs(_projectFile, properties: [("TargetFramework", "tfName")]));

            ProjectStarted?.Invoke(_eventSender, MakeProjectStartedEventArgs(_projectFile, "Build"));
            TargetStarted?.Invoke(_eventSender, MakeTargetStartedEventArgs(_projectFile, "Build"));
            TaskStarted?.Invoke(_eventSender, MakeTaskStartedEventArgs(_projectFile, "Task"));

            _terminallogger.DisplayNodes();

            // force the current node to stop building and 'yield'
            TaskStarted?.Invoke(_eventSender, MakeTaskStartedEventArgs(_projectFile, "MSBuild"));

            // now create a new project with a different target framework that runs on the same node
            var buildContext2 = MakeBuildEventContext(evalId: 2, projectContextId: 2);
            StatusEventRaised?.Invoke(_eventSender, MakeProjectEvalFinishedArgs(_projectFile, properties: [("TargetFramework", "tf2")], buildEventContext: buildContext2));
            ProjectStarted?.Invoke(_eventSender, MakeProjectStartedEventArgs(_projectFile, "Build", buildEventContext: buildContext2));
            TargetStarted?.Invoke(_eventSender, MakeTargetStartedEventArgs(_projectFile, "Build", buildEventContext: buildContext2));

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
                RunnerUtilities.ExecMSBuild($"{projectFile.Path} /m /bl:{logFileWithTL} -flp:logfile={Path.Combine(logFolder.Path, "logFileWithTL.log")};verbosity=diagnostic -tl:on", out bool success,   outputHelper: _outputHelper);
                success.ShouldBeTrue();

                // Execute MSBuild with binary and file loggers
                RunnerUtilities.ExecMSBuild($"{projectFile.Path} /m /bl:{logFileWithoutTL} -flp:logfile={Path.Combine(logFolder.Path, "logFileWithoutTL.log")};verbosity=diagnostic", out success,   outputHelper: _outputHelper);
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

        [Fact]
        public async Task PrintMessageLinks()
        {
            _terminallogger.Verbosity = LoggerVerbosity.Detailed;
            _terminallogger.ParseParameters();

            InvokeLoggerCallbacksForSimpleProject(succeeded: true, () =>
            {
                MessageRaised?.Invoke(_eventSender, MakeMessageEventArgs("this message has a link because it has a code and a keyword", MessageImportance.High, code: "1234", keyword: "keyword"));
                MessageRaised?.Invoke(_eventSender, MakeMessageEventArgs("this message has no link because it only has a code", MessageImportance.High, code: "1234", keyword: null));
                MessageRaised?.Invoke(_eventSender, MakeMessageEventArgs("this message has no link because it only has a keyword", MessageImportance.High, keyword: "keyword"));
            });

            await Verify(_outputWriter.ToString(), _settings).UniqueForOSPlatform();
        }


        [Fact]
        public async Task PrintWarningLinks()
        {
            InvokeLoggerCallbacksForSimpleProject(succeeded: true, () =>
            {
                WarningRaised?.Invoke(_eventSender, MakeWarningEventArgs("this warning has a link because it has an explicit link", link: "https://example.com"));
                WarningRaised?.Invoke(_eventSender, MakeWarningEventArgs("this warning has a link because it has a keyword", keyword: "keyword"));
                WarningRaised?.Invoke(_eventSender, MakeWarningEventArgs("this warning has a link to example.com because links take precedence over keywords", link: "https://example.com", keyword: "keyword"));
                WarningRaised?.Invoke(_eventSender, MakeWarningEventArgs("this warning has no link because it has no link or keyword"));
            });

            await Verify(_outputWriter.ToString(), _settings).UniqueForOSPlatform();
        }

        [Fact]
        public async Task PrintErrorLinks()
        {
            InvokeLoggerCallbacksForSimpleProject(succeeded: true, () =>
            {
                ErrorRaised?.Invoke(_eventSender, MakeErrorEventArgs("this error has a link because it has an explicit link", link: "https://example.com"));
                ErrorRaised?.Invoke(_eventSender, MakeErrorEventArgs("this error has a link because it has a keyword", keyword: "keyword"));
                ErrorRaised?.Invoke(_eventSender, MakeErrorEventArgs("this error has a link to example.com because links take precedence over keywords", link: "https://example.com", keyword: "keyword"));
                ErrorRaised?.Invoke(_eventSender, MakeErrorEventArgs("this error has no link because it has no link or keyword"));
            });

            await Verify(_outputWriter.ToString(), _settings).UniqueForOSPlatform();
        }

        [Fact]
        public async Task ProjectFinishedReportsRuntimeIdentifier()
        {
            // this project will report a RID and so will show a RID in the build output
            var buildOutputEvent = MakeBuildOutputEventArgs(_projectFile);
            InvokeLoggerCallbacksForSimpleProject(succeeded: true, properties: [("RuntimeIdentifier", "win-x64")], additionalCallbacks: () =>
            {
                MessageRaised?.Invoke(_eventSender, buildOutputEvent);
            });
            await Verify(_outputWriter.ToString(), _settings).UniqueForOSPlatform();
        }

        [Fact]
        public async Task ProjectFinishedReportsTargetFrameworkAndRuntimeIdentifier()
        {
            // this project will report a TFM and a RID and so will show a both in the output
            var buildOutputEvent = MakeBuildOutputEventArgs(_projectFile);
            InvokeLoggerCallbacksForSimpleProject(succeeded: true, properties: [("TargetFramework", "net10.0"), ("RuntimeIdentifier", "win-x64")], additionalCallbacks: () =>
            {
                MessageRaised?.Invoke(_eventSender, buildOutputEvent);
            });
            await Verify(_outputWriter.ToString(), _settings).UniqueForOSPlatform();
        }
    }
}
