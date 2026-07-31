// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.Build.BuildCheck.Infrastructure;
using Microsoft.Build.Experimental.BuildCheck;
using Microsoft.Build.Experimental.BuildCheck.Acquisition;
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
}
