// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Experimental.BuildCheck.Infrastructure;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging;
using Microsoft.Build.Shared;
using Microsoft.Build.UnitTests;
using Shouldly;
using Xunit;
using static Microsoft.Build.UnitTests.ObjectModelHelpers;

#nullable disable

namespace Microsoft.Build.Engine.UnitTests.BackEnd
{
    public class BuildManager_Logging_Tests : IDisposable
    {
        /// <summary>
        /// A project whose evaluated properties and items make it observable whether
        /// ProjectStarted events eagerly carry evaluation data.
        /// </summary>
        private const string EvaluationDataProjectContents = @"
<Project>
  <PropertyGroup>
    <EvaluatedProperty>property-value</EvaluatedProperty>
  </PropertyGroup>
  <ItemGroup>
    <EvaluatedItem Include=""input.txt"" />
  </ItemGroup>
  <Target Name=""Build"">
    <Message Text=""built"" Importance=""High"" />
  </Target>
</Project>";

        private string _mainProject = @"
<Project>

  <Target Name=`MainTarget`>
    <MSBuild Projects=`{0}` Targets=`ChildTarget` />
  </Target>

</Project>";

        private string _childProjectWithCustomBuildEvent = $@"
<Project>

    <UsingTask TaskName=""CustomBuildEventTask"" AssemblyFile=""{Assembly.GetExecutingAssembly().Location}"" />
    <Target Name=`ChildTarget`>
        <CustomBuildEventTask />
    </Target>

</Project>";


        /// <summary>
        /// The mock logger for testing.
        /// </summary>
        private readonly MockLogger _logger;

        /// <summary>
        /// The standard build manager for each test.
        /// </summary>
        private readonly BuildManager _buildManager;

        /// <summary>
        /// The project collection used.
        /// </summary>
        private readonly ProjectCollection _projectCollection;

        private readonly TestEnvironment _env;

        /// <summary>
        /// Output helper for creating loggers.
        /// </summary>
        private readonly ITestOutputHelper _output;

        /// <summary>
        /// SetUp
        /// </summary>
        public BuildManager_Logging_Tests(ITestOutputHelper output)
        {
            // Ensure that any previous tests which may have been using the default BuildManager do not conflict with us.
            BuildManager.DefaultBuildManager.Dispose();

            _output = output;
            _logger = new MockLogger(output);
            _buildManager = new BuildManager();
            _projectCollection = new ProjectCollection();

            _env = TestEnvironment.Create(output);
            _env.SetEnvironmentVariable("MSBUILD_TELEMETRY_OPTOUT", "1");
            _env.SetEnvironmentVariable("DOTNET_CLI_TELEMETRY_OPTOUT", "1");
            _env.SetEnvironmentVariable("MSBUILDLOGPROPERTIESANDITEMSAFTEREVALUATION", null);
            _env.SetEnvironmentVariable("MSBUILDNOINPROCNODE", null);
        }

        [DotNetOnlyFact]
        public void Build_WithCustomBuildArgs_ShouldEmitErrorOnNetCore() => Build_WithCustomBuildArgs_ShouldEmitEvent<BuildErrorEventArgs>();

        [WindowsFullFrameworkOnlyFact]
        public void Build_WithCustomBuildArgs_ShouldEmitWarningOnFramework() => Build_WithCustomBuildArgs_ShouldEmitEvent<BuildWarningEventArgs>();

        private void Build_WithCustomBuildArgs_ShouldEmitEvent<T>() where T : LazyFormattedBuildEventArgs
        {
            var testFiles = _env.CreateTestProjectWithFiles(string.Empty, ["main", "child1"], string.Empty);

            ILoggingService service = LoggingService.CreateLoggingService(LoggerMode.Synchronous, 1);
            service.RegisterLogger(_logger);

            _env.SetEnvironmentVariable("MSBUILDNOINPROCNODE", "1");

            _buildManager.BeginBuild(BuildParameters);

            try
            {
                var child1ProjectPath = testFiles.CreatedFiles[1];
                var cleanedUpChildContents = CleanupFileContents(_childProjectWithCustomBuildEvent);
                File.WriteAllText(child1ProjectPath, cleanedUpChildContents);

                var mainProjectPath = testFiles.CreatedFiles[0];
                var cleanedUpMainContents = CleanupFileContents(string.Format(_mainProject, child1ProjectPath));
                File.WriteAllText(mainProjectPath, cleanedUpMainContents);

                var buildRequestData = new BuildRequestData(
                   mainProjectPath,
                   new Dictionary<string, string>(),
                   MSBuildConstants.CurrentToolsVersion,
                   ["MainTarget"],
                   null);

                var submission = _buildManager.PendBuildRequest(buildRequestData);
                var result = submission.Execute();
                var allEvents = _logger.AllBuildEvents;

                allEvents.OfType<T>().ShouldHaveSingleItem();
                allEvents.First(x => x is T).Message.ShouldContain(
                    string.Format(ResourceUtilities.GetResourceString("DeprecatedEventSerialization"),
                    "MyCustomBuildEventArgs"));
            }
            finally
            {
                _buildManager.EndBuild();
            }
        }

        private BuildParameters BuildParameters => new BuildParameters(_projectCollection)
        {
            DisableInProcNode = true,
            EnableNodeReuse = false,
            Loggers = new ILogger[] { _logger }
        };

        [Fact]
        public void MinimalForwarderIsNotAddedWhenForwardingTerminalLoggerIsRegistered()
        {
            MockLogger logger = new(_output);
            BuildParameters parameters = new BuildParameters
            {
                ForwardingLoggers = [CreateForwarder(new EvaluationDataOptInLogger(logger), typeof(ForwardingTerminalLogger))],
                MaxNodeCount = 1,
                MultiThreaded = false,
                EnableNodeReuse = false,
                UseSynchronousLogging = true,
            };

            BuildEvaluationDataProject(parameters).OverallResult.ShouldBe(BuildResultCode.Success);

            // The ForwardingTerminalLogger forwards the project started and finished events, so no
            // minimal forwarder with its internal NullLogger is needed. That NullLogger used to
            // force eagerly copying properties and items into ProjectStarted events.
            ProjectStartedEventArgs started = logger.ProjectStartedEvents.ShouldHaveSingleItem();
            started.EnumerateProperties().ShouldBeEmpty();
            started.EnumerateItems().ShouldBeEmpty();
        }

        [Fact]
        public void MinimalForwarderIsNotAddedWhenCentralForwardingLoggerIsRegistered()
        {
            MockLogger logger = new(_output);
            BuildParameters parameters = new BuildParameters
            {
                ForwardingLoggers = [CreateForwarder(new EvaluationDataOptInLogger(logger), typeof(CentralForwardingLogger))],
                MaxNodeCount = 1,
                MultiThreaded = false,
                EnableNodeReuse = false,
                UseSynchronousLogging = true,
            };

            BuildEvaluationDataProject(parameters).OverallResult.ShouldBe(BuildResultCode.Success);

            // The CentralForwardingLogger forwards every event, so no minimal forwarder is needed.
            ProjectStartedEventArgs started = logger.ProjectStartedEvents.ShouldHaveSingleItem();
            started.EnumerateProperties().ShouldBeEmpty();
            started.EnumerateItems().ShouldBeEmpty();
        }

        [Fact]
        public void MinimalForwarderIsNotAddedForConfigurableForwardingLoggerAtNormalVerbosity()
        {
            MockLogger logger = new(_output);
            BuildParameters parameters = new BuildParameters
            {
                // A ConfigurableForwardingLogger without explicit event parameters forwards the
                // project started and finished events from Normal verbosity up.
                ForwardingLoggers = [CreateForwarder(new EvaluationDataOptInLogger(logger), typeof(ConfigurableForwardingLogger), verbosity: LoggerVerbosity.Normal)],
                MaxNodeCount = 1,
                MultiThreaded = false,
                EnableNodeReuse = false,
                UseSynchronousLogging = true,
            };

            BuildEvaluationDataProject(parameters).OverallResult.ShouldBe(BuildResultCode.Success);

            ProjectStartedEventArgs started = logger.ProjectStartedEvents.ShouldHaveSingleItem();
            started.EnumerateProperties().ShouldBeEmpty();
            started.EnumerateItems().ShouldBeEmpty();
        }

        [Fact]
        public void MinimalForwarderIsNotAddedForExplicitlyConfiguredConfigurableForwardingLogger()
        {
            MockLogger logger = new(_output);
            BuildParameters parameters = new BuildParameters
            {
                // Event parameters are matched case-insensitively, like in
                // ConfigurableForwardingLogger.ApplyParameter.
                ForwardingLoggers = [CreateForwarder(new EvaluationDataOptInLogger(logger), typeof(ConfigurableForwardingLogger), "projectstartedevent;projectfinishedevent")],
                MaxNodeCount = 1,
                MultiThreaded = false,
                EnableNodeReuse = false,
                UseSynchronousLogging = true,
            };

            BuildEvaluationDataProject(parameters).OverallResult.ShouldBe(BuildResultCode.Success);

            ProjectStartedEventArgs started = logger.ProjectStartedEvents.ShouldHaveSingleItem();
            started.EnumerateProperties().ShouldBeEmpty();
            started.EnumerateItems().ShouldBeEmpty();
        }

        [Fact]
        public void ExplicitlyConfiguredConfigurableForwardingLoggerIsNotAdjusted()
        {
            MockLogger logger = new(_output);
            ForwardingLoggerRecord forwarder = CreateForwarder(
                logger,
                typeof(ConfigurableForwardingLogger),
                "ERROREVENT",
                verbosity: LoggerVerbosity.Normal);
            BuildParameters parameters = new BuildParameters
            {
                ForwardingLoggers = [forwarder],
                MaxNodeCount = 2,
                DisableInProcNode = true,
                MultiThreaded = false,
                EnableNodeReuse = false,
                UseSynchronousLogging = true,
            };

            BuildEvaluationDataProject(parameters).OverallResult.ShouldBe(BuildResultCode.Success);

            // A caller-owned logger description must not be adjusted...
            forwarder.ForwardingLoggerDescription.LoggerSwitchParameters.ShouldBe("ERROREVENT");

            // ...and the central logger keeps receiving only the explicitly requested events.
            // (The build started and finished events are raised by the manager itself and always delivered.)
            logger.AllBuildEvents
                .Where(e => e is not BuildStartedEventArgs and not BuildFinishedEventArgs)
                .ShouldBeEmpty();
        }

        [Fact]
        public void MinimalForwarderIsAddedForUnknownForwardingLogger()
        {
            MockLogger logger = new(_output);
            ForwardingLoggerRecord forwarder = new ForwardingLoggerRecord(
                logger,
                new LoggerDescription(
                    typeof(NonForwardingLogger).FullName,
                    null,
                    Assembly.GetExecutingAssembly().Location,
                    null,
                    LoggerVerbosity.Quiet));
            BuildParameters parameters = new BuildParameters
            {
                ForwardingLoggers = [forwarder],
                MaxNodeCount = 2,
                DisableInProcNode = true,
                MultiThreaded = false,
                EnableNodeReuse = false,
                UseSynchronousLogging = true,
            };

            // The out-of-proc worker node forwards nothing itself, so the build only completes
            // when the minimal forwarder delivers the project started and finished events.
            BuildEvaluationDataProject(parameters).OverallResult.ShouldBe(BuildResultCode.Success);
        }

        [Fact]
        public void MinimalForwarderIsAddedForQuietConfigurableForwardingLoggerWithoutParameters()
        {
            MockLogger logger = new(_output);
            BuildParameters parameters = new BuildParameters
            {
                // At Quiet verbosity a ConfigurableForwardingLogger forwards neither the project
                // started nor the project finished event.
                ForwardingLoggers = [CreateForwarder(logger, typeof(ConfigurableForwardingLogger))],
                MaxNodeCount = 2,
                DisableInProcNode = true,
                MultiThreaded = false,
                EnableNodeReuse = false,
                UseSynchronousLogging = true,
            };

            BuildEvaluationDataProject(parameters).OverallResult.ShouldBe(BuildResultCode.Success);
        }

        [Fact]
        public void MinimalForwarderIsAddedWhenNoForwardingLoggersAreRegistered()
        {
            BuildParameters parameters = new BuildParameters
            {
                // Without any loggers or forwarding loggers (as with "msbuild -m -noconlog")
                // the minimal forwarder is the only source of the project started and finished
                // events needed to complete the submission.
                MaxNodeCount = 2,
                DisableInProcNode = true,
                MultiThreaded = false,
                EnableNodeReuse = false,
                UseSynchronousLogging = true,
            };

            BuildEvaluationDataProject(parameters).OverallResult.ShouldBe(BuildResultCode.Success);
        }

        [Fact]
        public void ProcessForwardingLoggers_AppendsOnlyTheMinimalEvents()
        {
            List<ForwardingLoggerRecord> records = BuildManager.ProcessForwardingLoggers(null);

            ForwardingLoggerRecord appended = records.ShouldHaveSingleItem();
            appended.CentralLogger.ShouldBeOfType<Execution.BuildManager.NullLogger>();
            appended.ForwardingLoggerDescription.Name.ShouldBe(typeof(ConfigurableForwardingLogger).FullName);
            appended.ForwardingLoggerDescription.LoggerAssemblyName.ShouldBe(typeof(ConfigurableForwardingLogger).Assembly.FullName);
            // The minimal forwarder must forward exactly what the manager needs to complete
            // submissions: the project started and finished events. Forwarding evaluation
            // events as well only duplicates the evaluation IPC for the NullLogger to discard.
            appended.ForwardingLoggerDescription.LoggerSwitchParameters.ShouldBe("PROJECTSTARTEDEVENT;PROJECTFINISHEDEVENT");
        }

        [Fact]
        public void ProcessForwardingLoggers_LeavesRecognizedForwardersUntouched()
        {
            ForwardingLoggerRecord forwarder = CreateForwarder(new MockLogger(_output), typeof(ForwardingTerminalLogger));

            List<ForwardingLoggerRecord> records = BuildManager.ProcessForwardingLoggers([forwarder]);

            records.ShouldHaveSingleItem().ShouldBeSameAs(forwarder);
        }

        [Fact]
        public void ProcessForwardingLoggers_AppendsMinimalForwarderForUnknownForwarders()
        {
            ForwardingLoggerRecord forwarder = new ForwardingLoggerRecord(
                new MockLogger(_output),
                new LoggerDescription(
                    typeof(NonForwardingLogger).FullName,
                    null,
                    Assembly.GetExecutingAssembly().Location,
                    null,
                    LoggerVerbosity.Quiet));

            List<ForwardingLoggerRecord> records = BuildManager.ProcessForwardingLoggers([forwarder]);

            records.Count.ShouldBe(2);
            records[0].ShouldBeSameAs(forwarder);
            records[1].CentralLogger.ShouldBeOfType<Execution.BuildManager.NullLogger>();
            records[1].ForwardingLoggerDescription.LoggerSwitchParameters.ShouldBe("PROJECTSTARTEDEVENT;PROJECTFINISHEDEVENT");
        }

        [Fact]
        public void ForwarderRecognitionRequiresEngineAssemblyAndKnownLoggerClass()
        {
            // These built-in forwarding loggers forward the project started and finished events
            // unconditionally.
            BuildManager.ForwardsProjectStartedAndFinishedEvents(
                CreateForwarder(new MockLogger(_output), typeof(CentralForwardingLogger))).ShouldBeTrue();
            BuildManager.ForwardsProjectStartedAndFinishedEvents(
                CreateForwarder(new MockLogger(_output), typeof(ForwardingTerminalLogger))).ShouldBeTrue();
            BuildManager.ForwardsProjectStartedAndFinishedEvents(
                CreateForwarder(new MockLogger(_output), typeof(BuildCheckForwardingLogger))).ShouldBeTrue();

            // A ConfigurableForwardingLogger forwards those events from Normal verbosity up,
            // or when they are requested explicitly (case-insensitively), or through a
            // performance summary.
            BuildManager.ForwardsProjectStartedAndFinishedEvents(
                CreateForwarder(new MockLogger(_output), typeof(ConfigurableForwardingLogger), verbosity: LoggerVerbosity.Normal)).ShouldBeTrue();
            BuildManager.ForwardsProjectStartedAndFinishedEvents(
                CreateForwarder(new MockLogger(_output), typeof(ConfigurableForwardingLogger), "PROJECTSTARTEDEVENT;PROJECTFINISHEDEVENT")).ShouldBeTrue();
            BuildManager.ForwardsProjectStartedAndFinishedEvents(
                CreateForwarder(new MockLogger(_output), typeof(ConfigurableForwardingLogger), "projectstartedevent;projectfinishedevent")).ShouldBeTrue();
            BuildManager.ForwardsProjectStartedAndFinishedEvents(
                CreateForwarder(new MockLogger(_output), typeof(ConfigurableForwardingLogger), "PERFORMANCESUMMARY")).ShouldBeTrue();

            // ...but not at lower verbosity, when only one of the events is requested, or when
            // other events are requested exclusively. FORWARDPROJECTCONTEXTEVENTS alone only
            // subscribes to ProjectStarted (this is how the console logger's forwarder is set up
            // at Minimal verbosity).
            BuildManager.ForwardsProjectStartedAndFinishedEvents(
                CreateForwarder(new MockLogger(_output), typeof(ConfigurableForwardingLogger))).ShouldBeFalse();
            BuildManager.ForwardsProjectStartedAndFinishedEvents(
                CreateForwarder(new MockLogger(_output), typeof(ConfigurableForwardingLogger), "PROJECTSTARTEDEVENT")).ShouldBeFalse();
            BuildManager.ForwardsProjectStartedAndFinishedEvents(
                CreateForwarder(new MockLogger(_output), typeof(ConfigurableForwardingLogger), "PROJECTSTARTEDEVENT;FORWARDPROJECTCONTEXTEVENTS")).ShouldBeFalse();
            BuildManager.ForwardsProjectStartedAndFinishedEvents(
                CreateForwarder(new MockLogger(_output), typeof(ConfigurableForwardingLogger), "ERROREVENT")).ShouldBeFalse();
            BuildManager.ForwardsProjectStartedAndFinishedEvents(
                CreateForwarder(new MockLogger(_output), typeof(ConfigurableForwardingLogger), "FORWARDPROJECTCONTEXTEVENTS")).ShouldBeFalse();

            // The engine assembly alone is not enough: the DistributedFileLogger lives in it but
            // forwards nothing.
            BuildManager.ForwardsProjectStartedAndFinishedEvents(
                CreateForwarder(new MockLogger(_output), typeof(DistributedFileLogger))).ShouldBeFalse();

            // A known logger class from another assembly is not ours either, and neither is an
            // unknown logger class from any assembly.
            BuildManager.ForwardsProjectStartedAndFinishedEvents(
                new ForwardingLoggerRecord(
                    new MockLogger(_output),
                    new LoggerDescription(
                        typeof(ConfigurableForwardingLogger).FullName,
                        typeof(NonForwardingLogger).Assembly.FullName,
                        null,
                        null,
                        LoggerVerbosity.Normal))).ShouldBeFalse();
            BuildManager.ForwardsProjectStartedAndFinishedEvents(
                CreateForwarder(new MockLogger(_output), typeof(NonForwardingLogger))).ShouldBeFalse();
        }

        private BuildResult BuildEvaluationDataProject(BuildParameters parameters)
        {
            string projectFile = _env.CreateFile("test.proj", EvaluationDataProjectContents).Path;

            return Helpers.BuildProjectFileUsingBuildManager(projectFile, logger: null, parameters: parameters);
        }

        private static ForwardingLoggerRecord CreateForwarder(
            ILogger centralLogger,
            Type forwarderType,
            string loggerSwitchParameters = null,
            LoggerVerbosity verbosity = LoggerVerbosity.Quiet)
        {
            return new ForwardingLoggerRecord(
                centralLogger,
                new LoggerDescription(forwarderType.FullName, forwarderType.Assembly.FullName, null, loggerSwitchParameters, verbosity));
        }

        /// <summary>
        /// A central logger that opts into receiving evaluation data through
        /// <see cref="IEventSource4.IncludeEvaluationPropertiesAndItems"/> and records
        /// everything into the given <see cref="MockLogger"/>.
        /// </summary>
        private sealed class EvaluationDataOptInLogger : ILogger
        {
            private readonly MockLogger _logger;

            public EvaluationDataOptInLogger(MockLogger logger)
            {
                _logger = logger;
            }

            public LoggerVerbosity Verbosity { get; set; }

            public string Parameters { get; set; }

            public void Initialize(IEventSource eventSource)
            {
                ((IEventSource4)eventSource).IncludeEvaluationPropertiesAndItems();
                eventSource.AnyEventRaised += _logger.LoggerEventHandler;
            }

            public void Shutdown()
            {
            }
        }

        /// <summary>
        /// TearDown
        /// </summary>
        public void Dispose()
        {
            try
            {
                _buildManager.Dispose();
                _projectCollection.Dispose();
            }
            finally
            {
                _env.Dispose();
            }
        }
    }

    /// <summary>
    /// A forwarding logger from outside the engine assembly that subscribes to nothing.
    /// </summary>
    public sealed class NonForwardingLogger : IForwardingLogger
    {
        public IEventRedirector BuildEventRedirector { get; set; }

        public int NodeId { get; set; }

        public LoggerVerbosity Verbosity { get; set; }

        public string Parameters { get; set; }

        public void Initialize(IEventSource eventSource)
        {
        }

        public void Initialize(IEventSource eventSource, int nodeCount)
        {
        }

        public void Shutdown()
        {
        }
    }
}
