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
using Microsoft.Build.BackEnd.Logging;
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
    internal sealed class MockBuildEventSink(int nodeNumber) : IBuildEventSink, IEventSource
    {
        public string Name { get; set; } = $"MockBuildEventSink{nodeNumber}";
        public bool HaveLoggedBuildStartedEvent { get; set; }
        public bool HaveLoggedBuildFinishedEvent { get; set; }

        void IBuildEventSink.Consume(BuildEventArgs buildEvent, int sinkId) => (this as IBuildEventSink).Consume(buildEvent);
        
        void IBuildEventSink.Consume(BuildEventArgs buildEvent)
        {
            // map the incoming build event to the appropriate event handler
            switch (buildEvent)
            {
                case BuildStartedEventArgs e:
                    HaveLoggedBuildStartedEvent = true;
                    BuildStarted?.Invoke(this, e);
                    break;
                case BuildFinishedEventArgs e:
                    BuildFinished?.Invoke(this, e);
                    break;
                case ProjectStartedEventArgs e:
                    ProjectStarted?.Invoke(this, e);
                    break;
                case ProjectFinishedEventArgs e:
                    ProjectFinished?.Invoke(this, e);
                    break;
                case TargetStartedEventArgs e:
                    TargetStarted?.Invoke(this, e);
                    break;
                case TargetFinishedEventArgs e:
                    TargetFinished?.Invoke(this, e);
                    break;
                case TaskStartedEventArgs e:
                    TaskStarted?.Invoke(this, e);
                    break;
                case TaskFinishedEventArgs e:
                    TaskFinished?.Invoke(this, e);
                    break;
                case BuildMessageEventArgs e:
                    MessageRaised?.Invoke(this, e);
                    break;
                case BuildWarningEventArgs e:
                    WarningRaised?.Invoke(this, e);
                    break;
                case BuildErrorEventArgs e:
                    ErrorRaised?.Invoke(this, e);
                    break;
                case BuildStatusEventArgs e:
                    StatusEventRaised?.Invoke(this, e);
                    break;
                case CustomBuildEventArgs c:
                    CustomEventRaised?.Invoke(this, c);
                    break;
            }
        }

        void IBuildEventSink.ShutDown()
        {
        }

        private const string _eventSender = "Test";

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

#pragma warning disable CS0067 // The event is never used
        public event AnyEventHandler? AnyEventRaised;
#pragma warning restore CS0067 // The event is never used

        // wrappers to invoke the events on this class
        public void InvokeBuildStarted(BuildStartedEventArgs args) => BuildStarted?.Invoke(_eventSender, args);
        public void InvokeBuildFinished(BuildFinishedEventArgs args) => BuildFinished?.Invoke(_eventSender, args);
        public void InvokeProjectStarted(ProjectStartedEventArgs args) => ProjectStarted?.Invoke(_eventSender, args);
        public void InvokeProjectFinished(ProjectFinishedEventArgs args) => ProjectFinished?.Invoke(_eventSender, args);
        public void InvokeTargetStarted(TargetStartedEventArgs args) => TargetStarted?.Invoke(_eventSender, args);
        public void InvokeTargetFinished(TargetFinishedEventArgs args) => TargetFinished?.Invoke(_eventSender, args);
        public void InvokeTaskStarted(TaskStartedEventArgs args) => TaskStarted?.Invoke(_eventSender, args);
        public void InvokeTaskFinished(TaskFinishedEventArgs args) => TaskFinished?.Invoke(_eventSender, args);
        public void InvokeMessageRaised(BuildMessageEventArgs args) => MessageRaised?.Invoke(_eventSender, args);
        public void InvokeWarningRaised(BuildWarningEventArgs args) => WarningRaised?.Invoke(_eventSender, args);
        public void InvokeErrorRaised(BuildErrorEventArgs args) => ErrorRaised?.Invoke(_eventSender, args);
        public void InvokeStatusEventRaised(BuildStatusEventArgs args) => StatusEventRaised?.Invoke(_eventSender, args);
        public void InvokeCustomEventRaised(CustomBuildEventArgs args) => CustomEventRaised?.Invoke(_eventSender, args);
    }

    [UsesVerify]
    [UseInvariantCulture]
    public class TerminalLogger_Tests
    {
        private const int _nodeCount = 8;

        private const string _immediateMessageString =
            "The plugin credential provider could not acquire credentials." +
            "Authentication may require manual action. Consider re-running the command with --interactive for `dotnet`, " +
            "/p:NuGetInteractive=\"true\" for MSBuild or removing the -NonInteractive switch for `NuGet`";

        private readonly string _projectFile = NativeMethods.IsUnixLike ? "/src/project.proj" : @"C:\src\project.proj";
        private readonly string _projectFile2 = NativeMethods.IsUnixLike ? "/src/project2.proj" : @"C:\src\project2.proj";
        private readonly string _projectFileWithNonAnsiSymbols = NativeMethods.IsUnixLike ? "/src/проектТерминал/㐇𠁠𪨰𫠊𫦠𮚮⿕.proj" : @"C:\src\проектТерминал\㐇𠁠𪨰𫠊𫦠𮚮⿕.proj";

        private readonly MockBuildEventSink _centralNodeEventSource = new MockBuildEventSink(0);
        private readonly MockBuildEventSink _remoteNodeEventSource = new MockBuildEventSink(1);

        private StringWriter _outputWriter = new();

        private readonly Terminal _mockTerminal;
        private readonly TerminalLogger _terminallogger;
        private readonly ForwardingTerminalLogger _remoteTerminalLogger;

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
            _terminallogger.Initialize(_centralNodeEventSource, _nodeCount);
            _terminallogger._createStopwatch = () => new MockStopwatch();

            _remoteTerminalLogger = new ForwardingTerminalLogger();
            _remoteTerminalLogger.BuildEventRedirector = new EventRedirectorToSink(0, _centralNodeEventSource);
            _remoteTerminalLogger.Initialize(_remoteNodeEventSource, 1);

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
            (ILogger logger, _) = TerminalLogger.CreateTerminalOrConsoleLoggerWithForwarding(args, supportsAnsi, outputIsScreen, default);

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
            (ILogger logger, _) = TerminalLogger.CreateTerminalOrConsoleLoggerWithForwarding(args, true, true, default);

            logger.ShouldNotBeNull();
            logger.Verbosity.ShouldBe(expectedVerbosity);
        }


        #region Helper methods to create BuildEventArgs with BuildEventContext

        /// <summary>
        /// Helper function to create a BuildEventContext keyed to specific scenarios.
        /// When you want to refer to the same eval properties, use the same evalId.
        /// When you want to refer to the same project, use the same projectContextId.
        /// When you want to refer to the same node, use the same nodeId.
        /// By default, nodeId, evalId, projectContextId, and targetId are all set to 1.
        /// </summary>
        private BuildEventContext MakeBuildEventContext(int evalId = 1, int projectContextId = 1, int nodeId = 1)
        {
            return BuildEventContext.CreateInitial(-1, nodeId)
                .WithEvaluationId(evalId)
                .WithProjectInstanceId(-1)
                .WithProjectContextId(projectContextId)
                .WithTargetId(1)
                .WithTaskId(1);
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

            _centralNodeEventSource.InvokeBuildStarted(MakeBuildStartedEventArgs());
            _centralNodeEventSource.InvokeStatusEventRaised(MakeProjectEvalFinishedArgs(projectFile, properties: properties));

            _centralNodeEventSource.InvokeProjectStarted(MakeProjectStartedEventArgs(projectFile));

            _centralNodeEventSource.InvokeTargetStarted(MakeTargetStartedEventArgs(projectFile, "Build"));
            _centralNodeEventSource.InvokeTaskStarted(MakeTaskStartedEventArgs(projectFile, "Task"));

            additionalCallbacks?.Invoke();

            _centralNodeEventSource.InvokeTaskFinished(MakeTaskFinishedEventArgs(projectFile, "Task", succeeded));
            _centralNodeEventSource.InvokeTargetFinished(MakeTargetFinishedEventArgs(projectFile, "Build", succeeded));

            _centralNodeEventSource.InvokeProjectFinished(MakeProjectFinishedEventArgs(projectFile, succeeded));
            _centralNodeEventSource.InvokeBuildFinished(MakeBuildFinishedEventArgs(succeeded));
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
            _centralNodeEventSource.InvokeBuildStarted(MakeBuildStartedEventArgs());
            _centralNodeEventSource.InvokeStatusEventRaised(MakeProjectEvalFinishedArgs(_projectFile));
            _centralNodeEventSource.InvokeProjectStarted(MakeProjectStartedEventArgs(_projectFile));

            _centralNodeEventSource.InvokeTargetStarted(MakeTargetStartedEventArgs(_projectFile, "_TestRunStart"));
            _centralNodeEventSource.InvokeTaskStarted(MakeTaskStartedEventArgs(_projectFile, "Task"));

            additionalCallbacks();

            _centralNodeEventSource.InvokeTaskFinished(MakeTaskFinishedEventArgs(_projectFile, "Task", succeeded));
            _centralNodeEventSource.InvokeTargetFinished(MakeTargetFinishedEventArgs(_projectFile, "_TestRunStart", succeeded));

            _centralNodeEventSource.InvokeProjectFinished(MakeProjectFinishedEventArgs(_projectFile, succeeded));

            _centralNodeEventSource.InvokeBuildFinished(MakeBuildFinishedEventArgs(succeeded));
        }

        private void InvokeLoggerCallbacksForTwoProjects(bool succeeded, Action additionalCallbacks, Action additionalCallbacks2)
        {
            _centralNodeEventSource.InvokeBuildStarted(MakeBuildStartedEventArgs());
            var p1BuildContext = MakeBuildEventContext(evalId: 1, projectContextId: 1);
            _centralNodeEventSource.InvokeStatusEventRaised(MakeProjectEvalFinishedArgs(_projectFile, buildEventContext: p1BuildContext));
            _centralNodeEventSource.InvokeProjectStarted(MakeProjectStartedEventArgs(_projectFile, buildEventContext: p1BuildContext));
            _centralNodeEventSource.InvokeTargetStarted(MakeTargetStartedEventArgs(_projectFile, "Build1", buildEventContext: p1BuildContext));
            _centralNodeEventSource.InvokeTaskStarted(MakeTaskStartedEventArgs(_projectFile, "Task1", buildEventContext: p1BuildContext));

            additionalCallbacks();

            _centralNodeEventSource.InvokeTaskFinished(MakeTaskFinishedEventArgs(_projectFile, "Task1", succeeded, buildEventContext: p1BuildContext));
            _centralNodeEventSource.InvokeTargetFinished(MakeTargetFinishedEventArgs(_projectFile, "Build1", succeeded, buildEventContext: p1BuildContext));
            _centralNodeEventSource.InvokeProjectFinished(MakeProjectFinishedEventArgs(_projectFile, succeeded, buildEventContext: p1BuildContext));

            var p2BuildContext = MakeBuildEventContext(evalId: 2, projectContextId: 2);
            _centralNodeEventSource.InvokeStatusEventRaised(MakeProjectEvalFinishedArgs(_projectFile2, buildEventContext: p2BuildContext));
            _centralNodeEventSource.InvokeProjectStarted(MakeProjectStartedEventArgs(_projectFile2, buildEventContext: p2BuildContext));
            _centralNodeEventSource.InvokeTargetStarted(MakeTargetStartedEventArgs(_projectFile2, "Build2", buildEventContext: p2BuildContext));
            _centralNodeEventSource.InvokeTaskStarted(MakeTaskStartedEventArgs(_projectFile2, "Task2", buildEventContext: p2BuildContext));

            additionalCallbacks2();

            _centralNodeEventSource.InvokeTaskFinished(MakeTaskFinishedEventArgs(_projectFile2, "Task2", succeeded, buildEventContext: p2BuildContext));
            _centralNodeEventSource.InvokeTargetFinished(MakeTargetFinishedEventArgs(_projectFile2, "Build2", succeeded, buildEventContext: p2BuildContext));
            _centralNodeEventSource.InvokeProjectFinished(MakeProjectFinishedEventArgs(_projectFile2, succeeded, buildEventContext: p2BuildContext));

            _centralNodeEventSource.InvokeBuildFinished(MakeBuildFinishedEventArgs(succeeded));
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
                _centralNodeEventSource.InvokeWarningRaised(MakeWarningEventArgs("A\nMulti\r\nLine\nWarning!"));
            });

            return Verify(_outputWriter.ToString(), _settings).UniqueForOSPlatform();
        }

        [Fact]
        public Task PrintImmediateWarningMessage_Succeeded()
        {
            InvokeLoggerCallbacksForSimpleProject(succeeded: true, () =>
            {
                _centralNodeEventSource.InvokeWarningRaised(MakeWarningEventArgs("[CredentialProvider]DeviceFlow: https://testfeed/index.json"));
                _centralNodeEventSource.InvokeWarningRaised(MakeWarningEventArgs(
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
                _centralNodeEventSource.InvokeWarningRaised(MakeCopyRetryWarning(1));
                _centralNodeEventSource.InvokeWarningRaised(MakeCopyRetryWarning(2));
                _centralNodeEventSource.InvokeWarningRaised(MakeCopyRetryWarning(3));
            });

            return Verify(_outputWriter.ToString(), _settings).UniqueForOSPlatform();
        }

        [Fact]
        public Task PrintImmediateMessage_Success()
        {
            InvokeLoggerCallbacksForSimpleProject(succeeded: true, () =>
            {
                _centralNodeEventSource.InvokeMessageRaised(MakeMessageEventArgs(_immediateMessageString, MessageImportance.High));
            });

            return Verify(_outputWriter.ToString(), _settings).UniqueForOSPlatform();
        }

        [Fact]
        public Task PrintImmediateMessage_Skipped()
        {
            InvokeLoggerCallbacksForSimpleProject(succeeded: true, () =>
            {
                _centralNodeEventSource.InvokeMessageRaised(MakeMessageEventArgs("--anycustomarg", MessageImportance.High));
            });

            return Verify(_outputWriter.ToString(), _settings).UniqueForOSPlatform();
        }

        [Fact]
        public Task PrintRestore_Failed()
        {
            _centralNodeEventSource.InvokeBuildStarted(MakeBuildStartedEventArgs());

            bool succeeded = false;
            _centralNodeEventSource.InvokeErrorRaised(MakeErrorEventArgs("Restore Failed"));

            _centralNodeEventSource.InvokeProjectFinished(MakeProjectFinishedEventArgs(_projectFile, succeeded));
            _centralNodeEventSource.InvokeBuildFinished(MakeBuildFinishedEventArgs(succeeded));

            return Verify(_outputWriter.ToString(), _settings).UniqueForOSPlatform();
        }

        [Fact]
        public Task PrintRestore_SuccessWithWarnings()
        {
            _centralNodeEventSource.InvokeBuildStarted(MakeBuildStartedEventArgs());

            bool succeeded = true;
            _centralNodeEventSource.InvokeWarningRaised(MakeWarningEventArgs("Restore with Warning"));

            _centralNodeEventSource.InvokeProjectFinished(MakeProjectFinishedEventArgs(_projectFile, succeeded));
            _centralNodeEventSource.InvokeBuildFinished(MakeBuildFinishedEventArgs(succeeded));

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
                _centralNodeEventSource.InvokeErrorRaised(MakeErrorEventArgs("Error!"));
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
                _centralNodeEventSource.InvokeWarningRaised(MakeWarningEventArgs("Warning!"));
                _centralNodeEventSource.InvokeErrorRaised(MakeErrorEventArgs("Error!"));
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
                _centralNodeEventSource.InvokeWarningRaised(MakeWarningEventArgs("Warning1!"));
                _centralNodeEventSource.InvokeWarningRaised(MakeWarningEventArgs("Warning2!"));
                _centralNodeEventSource.InvokeErrorRaised(MakeErrorEventArgs("Error1!"));
                _centralNodeEventSource.InvokeErrorRaised(MakeErrorEventArgs("Error2!"));
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
                    _centralNodeEventSource.InvokeWarningRaised(MakeWarningEventArgs("Warning1!", buildEventContext: p1Context));
                    _centralNodeEventSource.InvokeWarningRaised(MakeWarningEventArgs("Warning2!", buildEventContext: p1Context));
                    _centralNodeEventSource.InvokeErrorRaised(MakeErrorEventArgs("Error1!", buildEventContext: p1Context));
                    _centralNodeEventSource.InvokeErrorRaised(MakeErrorEventArgs("Error2!", buildEventContext: p1Context));
                },
                () =>
                {
                    var p2Context = MakeBuildEventContext(evalId: 2, projectContextId: 2);
                    _centralNodeEventSource.InvokeWarningRaised(MakeWarningEventArgs("Warning3!", buildEventContext: p2Context));
                    _centralNodeEventSource.InvokeWarningRaised(MakeWarningEventArgs("Warning4!", buildEventContext: p2Context));
                    _centralNodeEventSource.InvokeErrorRaised(MakeErrorEventArgs("Error3!", buildEventContext: p2Context));
                    _centralNodeEventSource.InvokeErrorRaised(MakeErrorEventArgs("Error4!", buildEventContext: p2Context));
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
                _centralNodeEventSource.InvokeMessageRaised(e);
            }, _projectFileWithNonAnsiSymbols);

            return Verify(_outputWriter.ToString(), _settings).UniqueForOSPlatform();
        }

        #endregion

        #region Helper methods to call specific orders of messages
        private void CallAllTypesOfMessagesWarningAndError()
        {
            _centralNodeEventSource.InvokeMessageRaised(MakeMessageEventArgs(_immediateMessageString, MessageImportance.High));
            _centralNodeEventSource.InvokeMessageRaised(MakeMessageEventArgs("High importance message!", MessageImportance.High));
            _centralNodeEventSource.InvokeMessageRaised(MakeMessageEventArgs("Normal importance message!", MessageImportance.Normal));
            _centralNodeEventSource.InvokeMessageRaised(MakeMessageEventArgs("Low importance message!", MessageImportance.Low));
            _centralNodeEventSource.InvokeWarningRaised(MakeWarningEventArgs("Warning!"));
            _centralNodeEventSource.InvokeWarningRaised(MakeWarningEventArgs("A\nMulti\r\nLine\nWarning!"));
            _centralNodeEventSource.InvokeErrorRaised(MakeErrorEventArgs("Error!"));
        }


        private void CallAllTypesOfTestMessages()
        {
            _centralNodeEventSource.InvokeMessageRaised(MakeExtendedMessageEventArgs(
                "Test passed.",
                MessageImportance.High,
                "TLTESTPASSED",
                new Dictionary<string, string?>() { { "displayName", "testName1" }, { "localizedResult", "passed" } }));
            _centralNodeEventSource.InvokeMessageRaised(MakeExtendedMessageEventArgs(
                "Test skipped.",
                MessageImportance.High,
                "TLTESTSKIPPED",
                new Dictionary<string, string?>() { { "displayName", "testName2" }, { "localizedResult", "skipped" } }));
            _centralNodeEventSource.InvokeMessageRaised(MakeExtendedMessageEventArgs(
                "Test results.",
                MessageImportance.High,
                "TLTESTFINISH",
                new Dictionary<string, string?>() { { "total", "10" }, { "passed", "7" }, { "skipped", "2" }, { "failed", "1" } }));
        }
        #endregion

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
                _centralNodeEventSource.InvokeErrorRaised(new BuildErrorEventArgs(
                    "MSB0001", "EvaluationError", "MSBUILD", 0, 0, 0, 0,
                    "An error occurred during evaluation.", null, null)
                {
                    BuildEventContext = BuildEventContext.CreateInitial(1, -1).WithEvaluationId(-1).WithProjectInstanceId(-1) // context that belongs to no project
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
                _centralNodeEventSource.InvokeMessageRaised(MakeTaskCommandLineEventArgs("Task Command Line.", MessageImportance.High));
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
                _centralNodeEventSource.InvokeMessageRaised(MakeTaskCommandLineEventArgs("Task Command Line.", MessageImportance.High));
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

            Func<StopwatchAbstraction>? createStopwatch = _terminallogger._createStopwatch;

            try
            {
                _terminallogger._createStopwatch = () =>
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
                _terminallogger._createStopwatch = createStopwatch;
            }
        }


        [Fact]
        public async Task DisplayNodesOverwritesWithNewTargetFramework()
        {
            _centralNodeEventSource.InvokeBuildStarted(MakeBuildStartedEventArgs());
            _centralNodeEventSource.InvokeStatusEventRaised(MakeProjectEvalFinishedArgs(_projectFile, properties: [("TargetFramework", "tfName")]));

            _centralNodeEventSource.InvokeProjectStarted(MakeProjectStartedEventArgs(_projectFile, "Build"));
            _centralNodeEventSource.InvokeTargetStarted(MakeTargetStartedEventArgs(_projectFile, "Build"));
            _centralNodeEventSource.InvokeTaskStarted(MakeTaskStartedEventArgs(_projectFile, "Task"));

            _terminallogger.DisplayNodes();

            // force the current node to stop building and 'yield'
            _centralNodeEventSource.InvokeTaskStarted(MakeTaskStartedEventArgs(_projectFile, "MSBuild"));

            // now create a new project with a different target framework that runs on the same node
            var buildContext2 = MakeBuildEventContext(evalId: 2, projectContextId: 2);
            _centralNodeEventSource.InvokeStatusEventRaised(MakeProjectEvalFinishedArgs(_projectFile, properties: [("TargetFramework", "tf2")], buildEventContext: buildContext2));
            _centralNodeEventSource.InvokeProjectStarted(MakeProjectStartedEventArgs(_projectFile, "Build", buildEventContext: buildContext2));
            _centralNodeEventSource.InvokeTargetStarted(MakeTargetStartedEventArgs(_projectFile, "Build", buildEventContext: buildContext2));

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
                RunnerUtilities.ExecMSBuild($"{projectFile.Path} /bl:{logFileWithTL} -flp:logfile={Path.Combine(logFolder.Path, "logFileWithTL.log")};verbosity=diagnostic -tl:on", out bool success,   outputHelper: _outputHelper);
                success.ShouldBeTrue();

                // Execute MSBuild with binary and file loggers
                RunnerUtilities.ExecMSBuild($"{projectFile.Path} /bl:{logFileWithoutTL} -flp:logfile={Path.Combine(logFolder.Path, "logFileWithoutTL.log")};verbosity=diagnostic", out success,   outputHelper: _outputHelper);
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

                // Check that amount of warnings and errors is equal in both cases. Presence of other loggers should not change behavior
                mockLogFromPlaybackWithoutTL.Errors.Count.ShouldBe(mockLogFromPlaybackWithTL.Errors.Count);
                mockLogFromPlaybackWithoutTL.Warnings.Count.ShouldBe(mockLogFromPlaybackWithTL.Warnings.Count);
                // Note: We don't compare AllBuildEvents.Count because internal events can vary between runs and with different logger configurations

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
                _centralNodeEventSource.InvokeMessageRaised(MakeMessageEventArgs("this message has a link because it has a code and a keyword", MessageImportance.High, code: "1234", keyword: "keyword"));
                _centralNodeEventSource.InvokeMessageRaised(MakeMessageEventArgs("this message has no link because it only has a code", MessageImportance.High, code: "1234", keyword: null));
                _centralNodeEventSource.InvokeMessageRaised(MakeMessageEventArgs("this message has no link because it only has a keyword", MessageImportance.High, keyword: "keyword"));
            });

            await Verify(_outputWriter.ToString(), _settings).UniqueForOSPlatform();
        }


        [Fact]
        public async Task PrintWarningLinks()
        {
            InvokeLoggerCallbacksForSimpleProject(succeeded: true, () =>
            {
                _centralNodeEventSource.InvokeWarningRaised(MakeWarningEventArgs("this warning has a link because it has an explicit link", link: "https://example.com"));
                _centralNodeEventSource.InvokeWarningRaised(MakeWarningEventArgs("this warning has a link because it has a keyword", keyword: "keyword"));
                _centralNodeEventSource.InvokeWarningRaised(MakeWarningEventArgs("this warning has a link to example.com because links take precedence over keywords", link: "https://example.com", keyword: "keyword"));
                _centralNodeEventSource.InvokeWarningRaised(MakeWarningEventArgs("this warning has no link because it has no link or keyword"));
            });

            await Verify(_outputWriter.ToString(), _settings).UniqueForOSPlatform();
        }

        [Fact]
        public async Task PrintErrorLinks()
        {
            InvokeLoggerCallbacksForSimpleProject(succeeded: true, () =>
            {
                _centralNodeEventSource.InvokeErrorRaised(MakeErrorEventArgs("this error has a link because it has an explicit link", link: "https://example.com"));
                _centralNodeEventSource.InvokeErrorRaised(MakeErrorEventArgs("this error has a link because it has a keyword", keyword: "keyword"));
                _centralNodeEventSource.InvokeErrorRaised(MakeErrorEventArgs("this error has a link to example.com because links take precedence over keywords", link: "https://example.com", keyword: "keyword"));
                _centralNodeEventSource.InvokeErrorRaised(MakeErrorEventArgs("this error has no link because it has no link or keyword"));
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
                _centralNodeEventSource.InvokeMessageRaised(buildOutputEvent);
            });
            await Verify(_outputWriter.ToString(), _settings).UniqueForOSPlatform();
        }

        [Fact]
        public async Task ProjectFinishedReportsTargetFrameworkAndRuntimeIdentifier()
        {
            // this project will report a TFM and a RID and so will show a both in the output
            var buildOutputEvent = MakeBuildOutputEventArgs(_projectFile);
            InvokeLoggerCallbacksForSimpleProject(succeeded: true, properties: [("TargetFramework", RunnerUtilities.LatestDotNetCoreForMSBuild), ("RuntimeIdentifier", "win-x64")], additionalCallbacks: () =>
            {
                _centralNodeEventSource.InvokeMessageRaised(buildOutputEvent);
            });
            await Verify(_outputWriter.ToString(), _settings).UniqueForOSPlatform();
        }

        [Fact]
        public void ReplayBinaryLogWithFewerNodesThanOriginalBuild()
        {
            // This test validates that replaying a binary log with terminal logger
            // using fewer nodes than the original build does not cause an IndexOutOfRangeException.
            // See issue: https://github.com/dotnet/msbuild/issues/10596

            using (TestEnvironment env = TestEnvironment.Create())
            {
                // Create multiple projects that will build in parallel
                TransientTestFolder logFolder = env.CreateFolder(createFolder: true);
                
                // Create three simple projects
                TransientTestFile project1 = env.CreateFile(logFolder, "project1.proj", @"
<Project>
    <Target Name='Build'>
        <Message Text='Building project1' Importance='High' />
    </Target>
</Project>");
                
                TransientTestFile project2 = env.CreateFile(logFolder, "project2.proj", @"
<Project>
    <Target Name='Build'>
        <Message Text='Building project2' Importance='High' />
    </Target>
</Project>");
                
                TransientTestFile project3 = env.CreateFile(logFolder, "project3.proj", @"
<Project>
    <Target Name='Build'>
        <Message Text='Building project3' Importance='High' />
    </Target>
</Project>");
                
                // Create a solution file that builds all projects in parallel
                string solutionContents = $@"
<Project>
    <Target Name='Build'>
        <MSBuild Projects='{project1.Path};{project2.Path};{project3.Path}' BuildInParallel='true' />
    </Target>
</Project>";
                TransientTestFile solutionFile = env.CreateFile(logFolder, "solution.proj", solutionContents);

                string binlogPath = env.ExpectFile(".binlog").Path;

                // Build with multiple nodes to create a binlog with higher node IDs
                RunnerUtilities.ExecMSBuild($"{solutionFile.Path} /m:4 /bl:{binlogPath}", out bool success, outputHelper: _outputHelper);
                success.ShouldBeTrue();

                // Replay the binlog with TerminalLogger using only 1 node
                // This should NOT throw an IndexOutOfRangeException
                var replayEventSource = new BinaryLogReplayEventSource();
                using var outputWriter = new StringWriter();
                using var mockTerminal = new Terminal(outputWriter);
                var terminalLogger = new TerminalLogger(mockTerminal);

                // Initialize with only 1 node (fewer than the original build)
                terminalLogger.Initialize(replayEventSource, nodeCount: 1);

                // This should complete without throwing an exception
                Should.NotThrow(() => replayEventSource.Replay(binlogPath));

                terminalLogger.Shutdown();
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task DisplayNodesRestoresStatusAfterMSBuildTaskYields_TestProject(bool runOnCentralNode)
        {
            // 1. Start Build
            _centralNodeEventSource.InvokeBuildStarted(MakeBuildStartedEventArgs());

            // the project can be evaluated and built either locally or remotely - so the actual innards should happen on either the central or remote event source

            var targetNodeEventSource = runOnCentralNode ? _centralNodeEventSource : _remoteNodeEventSource;
            // default build context is for the 'central' node
            var buildContext = runOnCentralNode ? null : MakeBuildEventContext(nodeId: 1);

            // 2. Project Eval Finished
            targetNodeEventSource.InvokeStatusEventRaised(MakeProjectEvalFinishedArgs(_projectFile, buildEventContext: buildContext));

            // 3. Project Started
            targetNodeEventSource.InvokeProjectStarted(MakeProjectStartedEventArgs(_projectFile, buildEventContext: buildContext));

            // 4. Target Started (Test Target)
            // This should set the display name to "Testing"
            targetNodeEventSource.InvokeTargetStarted(MakeTargetStartedEventArgs(_projectFile, "_TestRunStart", buildEventContext: buildContext));

            // 5. Task Started (The "Outer" task)
            targetNodeEventSource.InvokeTaskStarted(MakeTaskStartedEventArgs(_projectFile, "OuterTask", buildEventContext: buildContext));

            // Verify status shows Testing
            _terminallogger.DisplayNodes();

            // 6. MSBuild Task Started (Yield)
            targetNodeEventSource.InvokeTaskStarted(MakeTaskStartedEventArgs(_projectFile, "MSBuild", buildEventContext: buildContext));

            // Verify status is cleared
            _terminallogger.DisplayNodes();

            // 7. MSBuild Task Finished (Resume)
            // This should restore the status to "Testing" (not "_TestRunStart")
            targetNodeEventSource.InvokeTaskFinished(MakeTaskFinishedEventArgs(_projectFile, "MSBuild", true, buildEventContext: buildContext));

            // 8. Verify status shows Testing again
            _terminallogger.DisplayNodes();

            // Cleanup
            targetNodeEventSource.InvokeTaskFinished(MakeTaskFinishedEventArgs(_projectFile, "OuterTask", true, buildEventContext: buildContext));
            targetNodeEventSource.InvokeTargetFinished(MakeTargetFinishedEventArgs(_projectFile, "_TestRunStart", true, buildEventContext: buildContext));
            targetNodeEventSource.InvokeProjectFinished(MakeProjectFinishedEventArgs(_projectFile, true, buildEventContext: buildContext));

            _centralNodeEventSource.InvokeBuildFinished(MakeBuildFinishedEventArgs(true));

            await Verify(_outputWriter.ToString(), _settings).UniqueForOSPlatform().UseParameters(runOnCentralNode);
        }
    }
}
