// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.Build.BackEnd;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Execution;
using Microsoft.Build.Experimental.BuildCheck;
using Microsoft.Build.Experimental.BuildCheck.Infrastructure;
using Microsoft.Build.Framework;
using Microsoft.Build.UnitTests;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests.BackEnd;

public class BuildCheckInfrastructureWarning_Tests(ITestOutputHelper output)
{
    private readonly ITestOutputHelper _output = output;

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void InfrastructureWarningsAffectProjectFinishAccountingOnlyWhenGloballyPromoted(bool promote)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string project = env.CreateFile(".proj", "<Project />").Path;
        var host = new MockHost(new BuildParameters { IsBuildCheckEnabled = true });
        var service = (LoggingService)LoggingService.CreateLoggingService(LoggerMode.Asynchronous, 1);
        service.InitializeComponent(host);
        service.WarningsAsErrors = promote ? new HashSet<string>() : null;
        var manager = new BuildCheckManagerProvider.BuildCheckManager();
        manager.SetDataSource(BuildCheckDataSource.EventArgs);
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var logger = new MockLogger(_output);
        int depth = 0;
        int maximumDepth = 0;
        bool? errorsAtFinish = null;
        service.RegisterLogger(logger);
        service.RegisterLogger(new CallbackLogger(e =>
        {
            depth++;
            maximumDepth = Math.Max(maximumDepth, depth);
            try
            {
                if (e is BuildMessageEventArgs { Message: "hold logging event queue" })
                {
                    entered.Set();
                    release.Wait(TimeSpan.FromSeconds(30)).ShouldBeTrue("release the bounded logging barrier");
                }
            }
            finally
            {
                depth--;
            }
        }));
        service.OnProjectFinished += (_, e) =>
            errorsAtFinish = service.HasBuildSubmissionLoggedErrors(e.BuildEventContext.ShouldNotBeNull().SubmissionId);

        var request = new BuildRequestData(project, new Dictionary<string, string?>(), "Current", ["Build"], null);
        host.GetComponent<ConfigCache>(BuildComponentType.ConfigCache).AddConfiguration(
            new BuildRequestConfiguration(2, request, request.ExplicitlySpecifiedToolsVersion));
        BuildEventContext started = service.LogProjectStarted(
            new BuildEventContext(0, 2, 2, -1, -1, -1), 0, 2, BuildEventContext.Invalid,
            project, "Build", [], [], evaluationId: 3);
        var evaluation = new BuildEventContext(started.SubmissionId, started.NodeId, 3,
            BuildEventContext.InvalidProjectInstanceId, BuildEventContext.InvalidProjectContextId, -1, -1);
        var context = new CheckLoggingContext(service, evaluation);

        try
        {
            service.WaitForLoggingToProcessEvents();
            logger.Warnings.ShouldBeEmpty();
            logger.Errors.ShouldBeEmpty();
            service.HasBuildSubmissionLoggedErrors(started.SubmissionId).ShouldBeFalse();
            evaluation.ProjectContextId.ShouldBe(BuildEventContext.InvalidProjectContextId);
            service.ShouldTreatWarningAsError(evaluation, null).ShouldBe(promote);
            service.LogBuildEvent(new BuildMessageEventArgs("hold logging event queue", null, "test", MessageImportance.High));
            entered.Wait(TimeSpan.FromSeconds(30)).ShouldBeTrue("the logging event queue must be held before reporting");

            // Exercise a real contained setup failure while its project policy is unavailable.
            manager.RegisterCustomCheck(project, BuildCheckDataSource.EventArgs, [() => new FailingCheck()], context);
            logger.Warnings.ShouldBeEmpty();
            logger.Errors.ShouldBeEmpty();
            service.LogProjectFinished(started, project, true);
            release.Set();
            service.WaitForLoggingToProcessEvents();

            errorsAtFinish.ShouldBe(promote);
            if (promote)
            {
                logger.Errors.ShouldHaveSingleItem().Message.ShouldNotBeNull().ShouldContain(FailingCheck.Failure);
                logger.Warnings.ShouldBeEmpty();
            }
            else
            {
                logger.Errors.ShouldBeEmpty();
                logger.Warnings.ShouldBeEmpty("unpromoted infrastructure warnings retain deferred reporting");
            }

            manager.FinalizeProcessing(context);
            service.WaitForLoggingToProcessEvents();
            logger.Errors.Count.ShouldBe(promote ? 1 : 0);
            logger.Warnings.Count.ShouldBe(promote ? 0 : 1);
            maximumDepth.ShouldBe(1);
        }
        finally
        {
            release.Set();
            service.WaitForLoggingToProcessEvents();
            service.ShutdownComponent();
            manager.Shutdown();
        }
    }

    private sealed class FailingCheck : Check
    {
        internal const string Failure = "contained infrastructure setup failure";

        public override string FriendlyName => nameof(FailingCheck);

        public override IReadOnlyList<CheckRule> SupportedRules { get; } =
            [new("INFRA001", "Title", "Description", "Message", new CheckConfiguration { Severity = CheckResultSeverity.Warning })];

        public override void Initialize(ConfigurationContext configurationContext) => throw new InvalidOperationException(Failure);

        public override void RegisterActions(IBuildCheckRegistrationContext registrationContext) { }
    }

    private sealed class CallbackLogger(Action<BuildEventArgs> callback) : ILogger
    {
        public LoggerVerbosity Verbosity { get; set; } = LoggerVerbosity.Diagnostic;

        public string? Parameters { get; set; }

        public void Initialize(IEventSource eventSource) => eventSource.AnyEventRaised += (_, e) => callback(e);

        public void Shutdown() { }
    }
}
