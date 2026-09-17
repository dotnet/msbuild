// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.BuildCheck.Infrastructure;
using Microsoft.Build.Experimental.BuildCheck;
using Microsoft.Build.Experimental.BuildCheck.Infrastructure;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Shouldly;
using Xunit;

namespace Microsoft.Build.BuildCheck.UnitTests;

public class BuildCheckCentralContext_Tests
{
    private static readonly ICheckContext s_checkContext = new NullCheckContext();
    private static readonly IResultReporter s_resultReporter = new NullResultReporter();

    [Fact]
    public void RegisteringActionDuringDispatchUsesNextDispatch()
    {
        var context = new BuildCheckCentralContext(
            new EnabledConfigurationProvider(),
            (_, _) => { });
        using var checkRule = new CheckRuleMock("Rule");
        var check = new CheckWrapper(checkRule, s_resultReporter);
        int originalActionRuns = 0;
        int registeredActionRuns = 0;

        context.RegisterEvaluatedPropertiesAction(
            check,
            _ =>
            {
                originalActionRuns++;
                context.RegisterEvaluatedPropertiesAction(check, _ => registeredActionRuns++);
            });

        context.RunEvaluatedPropertiesActions(CreateCheckData(), s_checkContext, (_, _, _, _) => { });

        originalActionRuns.ShouldBe(1);
        registeredActionRuns.ShouldBe(0);

        context.RunEvaluatedPropertiesActions(CreateCheckData(), s_checkContext, (_, _, _, _) => { });

        originalActionRuns.ShouldBe(2);
        registeredActionRuns.ShouldBe(1);
    }

    [Fact]
    public async Task DeregisteringCheckDuringDispatchUsesCurrentSnapshot()
    {
        var context = new BuildCheckCentralContext(
            new EnabledConfigurationProvider(),
            (_, _) => { });
        using var checkRule = new CheckRuleMock("Rule");
        var check = new CheckWrapper(checkRule, s_resultReporter);
        using var actionStarted = new ManualResetEventSlim();
        using var continueAction = new ManualResetEventSlim();
        int firstActionRuns = 0;
        int secondActionRuns = 0;

        context.RegisterEvaluatedPropertiesAction(
            check,
            _ =>
            {
                firstActionRuns++;
                actionStarted.Set();
                continueAction.Wait();
            });
        context.RegisterEvaluatedPropertiesAction(check, _ => secondActionRuns++);

        Task dispatch = Task.Run(
            () => context.RunEvaluatedPropertiesActions(CreateCheckData(), s_checkContext, (_, _, _, _) => { }));

        try
        {
            actionStarted.Wait(TimeSpan.FromSeconds(10)).ShouldBeTrue();
            context.DeregisterCheck(check);
        }
        finally
        {
            continueAction.Set();
            await dispatch;
        }

        firstActionRuns.ShouldBe(1);
        secondActionRuns.ShouldBe(1);
        context.HasEvaluatedPropertiesActions.ShouldBeFalse();

        context.RunEvaluatedPropertiesActions(CreateCheckData(), s_checkContext, (_, _, _, _) => { });

        firstActionRuns.ShouldBe(1);
        secondActionRuns.ShouldBe(1);
    }

    [Fact]
    public void DisabledCheckDoesNotPreventLaterActions()
    {
        var context = new BuildCheckCentralContext(
            new SelectiveConfigurationProvider("Disabled"),
            (_, _) => { });
        using var disabledCheckRule = new CheckRuleMock("Disabled");
        using var enabledCheckRule = new CheckRuleMock("Enabled");
        var disabledCheck = new CheckWrapper(disabledCheckRule, s_resultReporter);
        var enabledCheck = new CheckWrapper(enabledCheckRule, s_resultReporter);
        int disabledActionRuns = 0;
        int enabledActionRuns = 0;

        context.RegisterEvaluatedPropertiesAction(disabledCheck, _ => disabledActionRuns++);
        context.RegisterEvaluatedPropertiesAction(enabledCheck, _ => enabledActionRuns++);

        context.RunEvaluatedPropertiesActions(CreateCheckData(), s_checkContext, (_, _, _, _) => { });

        disabledActionRuns.ShouldBe(0);
        enabledActionRuns.ShouldBe(1);
    }

    [Fact]
    public void DeregisterCheckClearsEveryCallbackRegistry()
    {
        var context = new BuildCheckCentralContext(
            new EnabledConfigurationProvider(),
            (_, _) => { });
        using var checkRule = new CheckRuleMock("Rule");
        var check = new CheckWrapper(checkRule, s_resultReporter);

        context.RegisterEvaluatedPropertiesAction(check, _ => { });
#pragma warning disable CS0618 // Type or member is obsolete
        context.RegisterParsedItemsAction(check, _ => { });
#pragma warning restore CS0618 // Type or member is obsolete
        context.RegisterEvaluatedItemsAction(check, _ => { });
        context.RegisterTaskInvocationAction(check, _ => { });
        context.RegisterPropertyReadAction(check, _ => { });
        context.RegisterPropertyWriteAction(check, _ => { });
        context.RegisterProjectRequestProcessingDoneAction(check, _ => { });
        context.RegisterBuildFinishedAction(check, _ => { });
        context.RegisterEnvironmentVariableReadAction(check, _ => { });
        context.RegisterProjectImportedAction(check, _ => { });

        AssertAllRegistriesHaveActions(context, true);

        context.DeregisterCheck(check);

        AssertAllRegistriesHaveActions(context, false);
    }

    [Fact]
    public void TaskFinishedRemovesTaskAfterLastCallbackIsDeregistered()
    {
        var context = new BuildCheckCentralContext(
            new EnabledConfigurationProvider(),
            (_, _) => { });
        var processor = new BuildEventsProcessor(context);
        using var checkRule = new CheckRuleMock("Rule");
        var check = new CheckWrapper(checkRule, s_resultReporter);
        var eventContext = new BuildEventContext(1, 2, 3, 4);
        var taskStarted = new TaskStartedEventArgs(null, null, "project.proj", "task.dll", "Task")
        {
            BuildEventContext = eventContext,
        };
        var taskFinished = new TaskFinishedEventArgs(null, null, "project.proj", "task.dll", "Task", true)
        {
            BuildEventContext = eventContext,
        };

        context.RegisterTaskInvocationAction(check, _ => { });
        processor.ProcessTaskStartedEventArgs(s_checkContext, taskStarted);

        context.DeregisterCheck(check);
        processor.ProcessTaskFinishedEventArgs(s_checkContext, taskFinished);

        context.RegisterTaskInvocationAction(check, _ => { });
        Should.NotThrow(() => processor.ProcessTaskStartedEventArgs(s_checkContext, taskStarted));
        processor.ProcessTaskFinishedEventArgs(s_checkContext, taskFinished);
    }

    private static void AssertAllRegistriesHaveActions(BuildCheckCentralContext context, bool expected)
    {
        context.HasEvaluatedPropertiesActions.ShouldBe(expected);
        context.HasParsedItemsActions.ShouldBe(expected);
        context.HasEvaluatedItemsActions.ShouldBe(expected);
        context.HasTaskInvocationActions.ShouldBe(expected);
        context.HasPropertyReadActions.ShouldBe(expected);
        context.HasPropertyWriteActions.ShouldBe(expected);
        context.HasProjectRequestProcessingDoneActions.ShouldBe(expected);
        context.HasBuildFinishedActions.ShouldBe(expected);
        context.HasEnvironmentVariableActions.ShouldBe(expected);
        context.HasProjectImportedActions.ShouldBe(expected);
    }

    private static EvaluatedPropertiesCheckData CreateCheckData()
        => new("project.proj", null, new Dictionary<string, string>(), new Dictionary<string, string>());

    private sealed class EnabledConfigurationProvider : IConfigurationProvider
    {
        private static readonly CheckConfigurationEffective[] s_configuration =
        [
            new("X01234", EvaluationCheckScope.ProjectFileOnly, CheckResultSeverity.Warning),
        ];

        public void CheckCustomConfigurationDataValidity(string projectFullPath, string ruleId) { }

        public CustomConfigurationData[] GetCustomConfigurations(string projectFullPath, IReadOnlyList<string> ruleIds) => [];

        public CheckConfigurationEffective[] GetMergedConfigurations(string projectFullPath, Check check) => s_configuration;

        public CheckConfigurationEffective[] GetMergedConfigurations(CheckConfiguration[] userConfigs, Check check) => s_configuration;

        public CheckConfiguration[] GetUserConfigurations(string projectFullPath, IReadOnlyList<string> ruleIds) => [];
    }

    private sealed class SelectiveConfigurationProvider(string disabledCheckName) : IConfigurationProvider
    {
        public void CheckCustomConfigurationDataValidity(string projectFullPath, string ruleId) { }

        public CustomConfigurationData[] GetCustomConfigurations(string projectFullPath, IReadOnlyList<string> ruleIds) => [];

        public CheckConfigurationEffective[] GetMergedConfigurations(string projectFullPath, Check check)
            =>
            [
                new(
                    "X01234",
                    EvaluationCheckScope.ProjectFileOnly,
                    check.FriendlyName == disabledCheckName ? CheckResultSeverity.None : CheckResultSeverity.Warning),
            ];

        public CheckConfigurationEffective[] GetMergedConfigurations(CheckConfiguration[] userConfigs, Check check)
            => GetMergedConfigurations(string.Empty, check);

        public CheckConfiguration[] GetUserConfigurations(string projectFullPath, IReadOnlyList<string> ruleIds) => [];
    }

    private sealed class NullResultReporter : IResultReporter
    {
        public void ReportResult(BuildEventArgs result, ICheckContext checkContext) { }
    }

    private sealed class NullCheckContext : ICheckContext
    {
        public BuildEventContext BuildEventContext => BuildEventContext.Invalid;

        public void DispatchAsComment(MessageImportance importance, string messageResourceName, params object?[] messageArgs) { }

        public void DispatchBuildEvent(BuildEventArgs buildEvent) { }

        public void DispatchAsErrorFromText(
            string? subcategoryResourceName,
            string? errorCode,
            string? helpKeyword,
            BuildEventFileInfo file,
            string message)
        { }

        public void DispatchAsCommentFromText(MessageImportance importance, string message) { }

        public void DispatchAsWarningFromText(
            string? subcategoryResourceName,
            string? errorCode,
            string? helpKeyword,
            BuildEventFileInfo file,
            string message)
        { }

        public void DispatchFailedAcquisitionTelemetry(string assemblyName, Exception exception) { }

        public void DispatchTelemetry(BuildCheckTracingData data) { }
    }
}
