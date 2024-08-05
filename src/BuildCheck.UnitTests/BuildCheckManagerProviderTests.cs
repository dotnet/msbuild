// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Build.BackEnd.Logging;
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
        _logger = new MockLogger();
        _loggingService.RegisterLogger(_logger);
        _testedInstance = new BuildCheckManager();
    }

    [Theory]
    [InlineData(true, new[] { "Custom analyzer rule: 'Rule1' has been registered successfully.", "Custom analyzer rule: 'Rule2' has been registered successfully." })]
    [InlineData(false, new[] { "Failed to register the custom analyzer: 'DummyPath'." })]
    public void ProcessCheckAcquisitionTest(bool isCheckRuleExist, string[] expectedMessages)
    {
        MockBuildCheckAcquisition(isCheckRuleExist);
        MockEnabledDataSourcesDefinition();

        _testedInstance.ProcessCheckAcquisition(new CheckAcquisitionData("DummyPath"), new CheckLoggingContext(_loggingService, new BuildEventContext(1, 2, 3, 4, 5, 6, 7)));

        _logger.AllBuildEvents.Where(be => be.GetType() == typeof(BuildMessageEventArgs)).Select(be => be.Message).ToArray()
            .ShouldBeEquivalentTo(expectedMessages);
    }

    private void MockBuildCheckAcquisition(bool isCheckRuleExist) => MockField("_acquisitionModule", new BuildCheckAcquisitionModuleMock(isCheckRuleExist));

    private void MockEnabledDataSourcesDefinition() => MockField("_enabledDataSources", new[] { true, true });

    private void MockField(string fieldName, object mockedValue)
    {
        var mockedField = _testedInstance.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        if (mockedField != null)
        {
            mockedField.SetValue(_testedInstance, mockedValue);
        }
    }
}

internal sealed class BuildCheckAcquisitionModuleMock : IBuildCheckAcquisitionModule
{
    private readonly bool _isCheckRuleExistForTest = true;

    internal BuildCheckAcquisitionModuleMock(bool isCheckRuleExistForTest) => _isCheckRuleExistForTest = isCheckRuleExistForTest;

    public List<BuildExecutionCheckFactory> CreateBuildExecutionCheckFactories(CheckAcquisitionData checkAcquisitionData, ICheckContext checkContext)
        => _isCheckRuleExistForTest
        ? new List<BuildExecutionCheckFactory>() { () => new BuildExecutionCheckRuleMock("Rule1"), () => new BuildExecutionCheckRuleMock("Rule2") }
        : new List<BuildExecutionCheckFactory>();
}

internal sealed class BuildExecutionCheckRuleMock : BuildExecutionCheck
{
    public static BuildExecutionCheckRule SupportedRule = new BuildExecutionCheckRule(
        "X01234",
        "Title",
        "Description",
        "Message format: {0}",
        new BuildExecutionCheckConfiguration());

    internal BuildExecutionCheckRuleMock(string friendlyName)
    {
        FriendlyName = friendlyName;
    }

    public override string FriendlyName { get; }

    public override IReadOnlyList<BuildExecutionCheckRule> SupportedRules { get; } = new List<BuildExecutionCheckRule>() { SupportedRule };

    public override void Initialize(ConfigurationContext configurationContext)
    {
        // configurationContext to be used only if analyzer needs external configuration data.
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
