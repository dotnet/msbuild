// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.BuildCheck.Infrastructure;
using Microsoft.Build.Construction;
using Microsoft.Build.Experimental.BuildCheck;
using Microsoft.Build.Experimental.BuildCheck.Acquisition;
using Microsoft.Build.Experimental.BuildCheck.Infrastructure;
using Microsoft.Build.Framework;
using Microsoft.Build.UnitTests;
using Shouldly;
using Xunit;
using Xunit.Abstractions;
using static Microsoft.Build.Experimental.BuildCheck.Infrastructure.BuildCheckManagerProvider;

namespace Microsoft.Build.BuildCheck.UnitTests;

public class BuildCheckManagerTests
{
    private readonly IBuildCheckManager _testedInstance;
    private readonly ILoggingService _loggingService;
    private readonly MockLogger _logger;

    public BuildCheckManagerTests(ITestOutputHelper output)
    {
        _loggingService = LoggingService.CreateLoggingService(LoggerMode.Synchronous, 1);
        _logger = new MockLogger(output);
        _loggingService.RegisterLogger(_logger);
        _testedInstance = new BuildCheckManager();
    }

    [Theory]
    [InlineData(true, new[] { "Custom check rule: 'Rule1' has been registered successfully.", "Custom check rule: 'Rule2' has been registered successfully." })]
    [InlineData(false, new[] { "Failed to register the custom check: 'DummyPath'." })]
    public void ProcessCheckAcquisitionTest(bool isCheckRuleExist, string[] expectedMessages)
    {
        MockConfigurationProvider();
        MockBuildCheckAcquisition(isCheckRuleExist);
        MockEnabledDataSourcesDefinition();

        _testedInstance.ProcessCheckAcquisition(new CheckAcquisitionData("DummyPath", "ProjectPath"), new CheckLoggingContext(_loggingService, BuildEventContext.CreateInitial(1, 2).WithEvaluationId(3).WithProjectInstanceId(4).WithProjectContextId(5).WithTargetId(6).WithTaskId(7)));

        _logger.AllBuildEvents.Where(be => be.GetType() == typeof(BuildMessageEventArgs)).Select(be => be.Message).ToArray()
            .ShouldBeEquivalentTo(expectedMessages);
    }

    private void MockBuildCheckAcquisition(bool isCheckRuleExist) => MockField("_acquisitionModule", new BuildCheckAcquisitionModuleMock(isCheckRuleExist));

    private void MockEnabledDataSourcesDefinition() => MockField("_enabledDataSources", new[] { true, true });

    private void MockConfigurationProvider() => MockField("_configurationProvider", new ConfigurationProviderMock());

    private void MockField(string fieldName, object mockedValue)
    {
        var mockedField = _testedInstance.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        if (mockedField != null)
        {
            mockedField.SetValue(_testedInstance, mockedValue);
        }
    }
}

internal sealed class ConfigurationProviderMock : IConfigurationProvider
{
    public void CheckCustomConfigurationDataValidity(string projectFullPath, string ruleId) { }

    public CustomConfigurationData[] GetCustomConfigurations(string projectFullPath, IReadOnlyList<string> ruleIds) => [];

    public CheckConfigurationEffective[] GetMergedConfigurations(string projectFullPath, Check check) => [];

    public CheckConfigurationEffective[] GetMergedConfigurations(CheckConfiguration[] userConfigs, Check check) => [];

    public CheckConfiguration[] GetUserConfigurations(string projectFullPath, IReadOnlyList<string> ruleIds) => [];
}

internal sealed class BuildCheckAcquisitionModuleMock : IBuildCheckAcquisitionModule
{
    private readonly bool _isCheckRuleExistForTest = true;

    internal BuildCheckAcquisitionModuleMock(bool isCheckRuleExistForTest) => _isCheckRuleExistForTest = isCheckRuleExistForTest;

    public List<CheckFactory> CreateCheckFactories(CheckAcquisitionData checkAcquisitionData, ICheckContext checkContext)
        => _isCheckRuleExistForTest
        ? new List<CheckFactory>() { () => new CheckRuleMock("Rule1"), () => new CheckRuleMock("Rule2") }
        : new List<CheckFactory>();
}

internal sealed class CheckRuleMock : Check
{
    public static CheckRule SupportedRule = new CheckRule(
        "X01234",
        "Title",
        "Description",
        "Message format: {0}",
        new CheckConfiguration());

    internal CheckRuleMock(string friendlyName)
    {
        FriendlyName = friendlyName;
    }

    public override string FriendlyName { get; }

    public override IReadOnlyList<CheckRule> SupportedRules { get; } = new List<CheckRule>() { SupportedRule };

    public override void Initialize(ConfigurationContext configurationContext)
    {
        // configurationContext to be used only if check needs external configuration data.
    }

    public override void RegisterActions(IBuildCheckRegistrationContext registrationContext)
    {
        registrationContext.RegisterEvaluatedPropertiesAction(EvaluatedPropertiesAction);
    }

    private void EvaluatedPropertiesAction(BuildCheckDataContext<EvaluatedPropertiesCheckData> context)
    {
        context.ReportResult(BuildCheckResult.Create(
            SupportedRule,
            ElementLocation.EmptyLocation,
            "Argument for the message format"));
    }
}
