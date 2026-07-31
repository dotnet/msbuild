// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.BuildCheck.Infrastructure;
using Microsoft.Build.Experimental.BuildCheck;
using Microsoft.Build.Experimental.BuildCheck.Infrastructure;
using Shouldly;
using Xunit;

namespace Microsoft.Build.BuildCheck.UnitTests;

public class BuildCheckCentralContext_Tests
{
    [Fact]
    public void RegisteringActionDuringDispatchUsesNextDispatch()
    {
        var context = new BuildCheckCentralContext(
            new EnabledConfigurationProvider(),
            (_, _) => { });
        using var checkRule = new CheckRuleMock("Rule");
        var check = new CheckWrapper(checkRule, null!);
        int originalActionRuns = 0;
        int registeredActionRuns = 0;

        context.RegisterEvaluatedPropertiesAction(
            check,
            _ =>
            {
                originalActionRuns++;
                context.RegisterEvaluatedPropertiesAction(check, _ => registeredActionRuns++);
            });

        context.RunEvaluatedPropertiesActions(CreateCheckData(), null!, (_, _, _, _) => { });

        originalActionRuns.ShouldBe(1);
        registeredActionRuns.ShouldBe(0);

        context.RunEvaluatedPropertiesActions(CreateCheckData(), null!, (_, _, _, _) => { });

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
        var check = new CheckWrapper(checkRule, null!);
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
            () => context.RunEvaluatedPropertiesActions(CreateCheckData(), null!, (_, _, _, _) => { }));

        try
        {
            actionStarted.Wait(TimeSpan.FromSeconds(10)).ShouldBeTrue();
            context.DeregisterCheck(check);
        }
        finally
        {
            continueAction.Set();
        }

        await dispatch;

        firstActionRuns.ShouldBe(1);
        secondActionRuns.ShouldBe(1);
        context.HasEvaluatedPropertiesActions.ShouldBeFalse();

        context.RunEvaluatedPropertiesActions(CreateCheckData(), null!, (_, _, _, _) => { });

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
        var disabledCheck = new CheckWrapper(disabledCheckRule, null!);
        var enabledCheck = new CheckWrapper(enabledCheckRule, null!);
        int disabledActionRuns = 0;
        int enabledActionRuns = 0;

        context.RegisterEvaluatedPropertiesAction(disabledCheck, _ => disabledActionRuns++);
        context.RegisterEvaluatedPropertiesAction(enabledCheck, _ => enabledActionRuns++);

        context.RunEvaluatedPropertiesActions(CreateCheckData(), null!, (_, _, _, _) => { });

        disabledActionRuns.ShouldBe(0);
        enabledActionRuns.ShouldBe(1);
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
}
