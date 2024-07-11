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
    public void ProcessAnalyzerAcquisitionTest(bool isAnalyzerRuleExist, string[] expectedMessages)
    {
        MockBuildCheckAcquisition(isAnalyzerRuleExist);
        MockEnabledDataSourcesDefinition();

        _testedInstance.ProcessAnalyzerAcquisition(new AnalyzerAcquisitionData("DummyPath"), new AnalysisLoggingContext(_loggingService, new BuildEventContext(1, 2, 3, 4, 5, 6, 7)));

        _logger.AllBuildEvents.Where(be => be.GetType() == typeof(BuildMessageEventArgs)).Select(be => be.Message).ToArray()
            .ShouldBeEquivalentTo(expectedMessages);
    }

    private void MockBuildCheckAcquisition(bool isAnalyzerRuleExist) => MockField("_acquisitionModule", new BuildCheckAcquisitionModuleMock(isAnalyzerRuleExist));

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
    private readonly bool _isAnalyzerRuleExistForTest = true;

    internal BuildCheckAcquisitionModuleMock(bool isAnalyzerRuleExistForTest) => _isAnalyzerRuleExistForTest = isAnalyzerRuleExistForTest;

    public List<BuildAnalyzerFactory> CreateBuildAnalyzerFactories(AnalyzerAcquisitionData analyzerAcquisitionData, IAnalysisContext analysisContext)
        => _isAnalyzerRuleExistForTest
        ? new List<BuildAnalyzerFactory>() { () => new BuildAnalyzerRuleMock("Rule1"), () => new BuildAnalyzerRuleMock("Rule2") }
        : new List<BuildAnalyzerFactory>();
}

internal sealed class BuildAnalyzerRuleMock : BuildAnalyzer
{
    public static BuildAnalyzerRule SupportedRule = new BuildAnalyzerRule(
        "X01234",
        "Title",
        "Description",
        "Message format: {0}",
        new BuildAnalyzerConfiguration());

    internal BuildAnalyzerRuleMock(string friendlyName)
    {
        FriendlyName = friendlyName;
    }

    public override string FriendlyName { get; }

    public override IReadOnlyList<BuildAnalyzerRule> SupportedRules { get; } = new List<BuildAnalyzerRule>() { SupportedRule };

    public override void Initialize(ConfigurationContext configurationContext)
    {
        // configurationContext to be used only if analyzer needs external configuration data.
    }

    public override void RegisterActions(IBuildCheckRegistrationContext registrationContext)
    {
        registrationContext.RegisterEvaluatedPropertiesAction(EvaluatedPropertiesAction);
    }

    private void EvaluatedPropertiesAction(BuildCheckDataContext<EvaluatedPropertiesAnalysisData> context)
    {
        context.ReportResult(BuildCheckResult.Create(
            SupportedRule,
            ElementLocation.EmptyLocation,
            "Argument for the message format"));
    }
}
