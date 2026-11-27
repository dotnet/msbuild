// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.BuildCheck.Infrastructure;
using Microsoft.Build.Construction;
using Microsoft.Build.Experimental.BuildCheck;
using Microsoft.Build.Experimental.BuildCheck.Acquisition;
using Microsoft.Build.Experimental.BuildCheck.Infrastructure;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging;
using Microsoft.Build.UnitTests;
using Shouldly;
using Xunit;
using static Microsoft.Build.Experimental.BuildCheck.Infrastructure.BuildCheckManagerProvider;

namespace Microsoft.Build.BuildCheck.UnitTests;

public class BuildCheckManagerTests : IDisposable
{
    private readonly IBuildCheckManager _testedInstance;
    private readonly ILoggingService _loggingService;
    private readonly MockLogger _logger;
    private readonly ITestOutputHelper _output;

    public BuildCheckManagerTests(ITestOutputHelper output)
    {
        _output = output;
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

        _testedInstance.ProcessCheckAcquisition(new CheckAcquisitionData("DummyPath", "ProjectPath"), new CheckLoggingContext(_loggingService, new BuildEventContext(1, 2, 3, 4, 5, 6, 7)));

        _logger.AllBuildEvents.Where(be => be.GetType() == typeof(BuildMessageEventArgs)).Select(be => be.Message).ToArray()
            .ShouldBeEquivalentTo(expectedMessages);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ProcessCheckAcquisitionRunsCustomCheck(bool enableBuildExecution)
    {
        using TestEnvironment testEnvironment = TestEnvironment.Create(_output);
        TransientTestFolder folder = testEnvironment.CreateFolder(createFolder: true);
        TransientTestFile project = testEnvironment.CreateFile(folder, "test.proj", "<Project />");
        testEnvironment.CreateFile(folder, ".editorconfig", """
            root = true

            [*.proj]
            build_check.X01235.severity = warning
            """);

        _testedInstance.SetDataSource(BuildCheckDataSource.EventArgs);
        if (enableBuildExecution)
        {
            _testedInstance.SetDataSource(BuildCheckDataSource.BuildExecution);
        }

        var dispatcher = new EventArgsDispatcher();
        var logger = new MockLogger(testEnvironment.Output);
        logger.Initialize(dispatcher);
        var eventContext = new BuildEventContext(1, 2, 3, 4, 5, 6, 7);
        var checkContext = new CheckDispatchingContext(dispatcher, eventContext);
        var evaluationFinished = new ProjectEvaluationFinishedEventArgs
        {
            ProjectFile = project.Path,
            BuildEventContext = eventContext,
        };

        logger.WarningCount.ShouldBe(0);
        _testedInstance.ProcessCheckAcquisition(
            new CheckAcquisitionData(Path.Combine(AppContext.BaseDirectory, "CustomCheck.dll"), project.Path), checkContext);
        _testedInstance.ProcessEvaluationFinishedEventArgs(checkContext, evaluationFinished);

        BuildWarningEventArgs warning = logger.Warnings.ShouldHaveSingleItem();
        warning.Code.ShouldBe("X01235");
        warning.Message.ShouldBe("Message format: Argument for the message format");
        logger.AllBuildEvents.ShouldNotContain(e => e is BuildCheckAcquisitionEventArgs);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ProcessCheckAcquisitionWithoutEventArgsForwardsToCentralNode(bool enableBuildExecution)
    {
        if (enableBuildExecution)
        {
            _testedInstance.SetDataSource(BuildCheckDataSource.BuildExecution);
        }

        var acquisitionData = new CheckAcquisitionData("DummyPath", "ProjectPath");
        var eventContext = new BuildEventContext(1, 2, 3, 4, 5, 6, 7);
        _logger.AllBuildEvents.ShouldBeEmpty();

        _testedInstance.ProcessCheckAcquisition(acquisitionData, new CheckLoggingContext(_loggingService, eventContext));

        var acquisitionEvent = _logger.AllBuildEvents.ShouldHaveSingleItem().ShouldBeOfType<BuildCheckAcquisitionEventArgs>();
        acquisitionEvent.AcquisitionPath.ShouldBe(acquisitionData.AssemblyPath);
        acquisitionEvent.ProjectPath.ShouldBe(acquisitionData.ProjectPath);
        acquisitionEvent.BuildEventContext.ShouldBeSameAs(eventContext);
    }

    public void Dispose()
    {
        _testedInstance.Shutdown();
        _loggingService.ShutdownComponent();
    }

    [Theory]
    [InlineData("warning", "warning", 2, 0)]
    [InlineData("none", "warning", 1, 0)]
    [InlineData("warning", "error", 1, 1)]
    [InlineData("warning", "none", 1, 0)]
    public void RepeatedAcquisitionRunsOncePerProject(
        string firstSeverity, string secondSeverity, int expectedWarnings, int expectedErrors)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        var folder = env.CreateFolder(createFolder: true);
        env.CreateFile(folder, ".editorconfig", $"""
            root = true

            [first.csproj]
            build_check.X01235.severity = {firstSeverity}

            [second.csproj]
            build_check.X01235.severity = {secondSeverity}
            """);
        string first = env.CreateFile(folder, "first.csproj", "<Project />").Path;
        string second = env.CreateFile(folder, "second.csproj", "<Project />").Path;
        _testedInstance.SetDataSource(BuildCheckDataSource.EventArgs);
        _testedInstance.SetDataSource(BuildCheckDataSource.BuildExecution);

        var dispatcher = new EventArgsDispatcher();
        var logger = new MockLogger(_output);
        logger.Initialize(dispatcher);

        logger.Warnings.Where(w => w.Code == "X01235").ShouldBeEmpty();
        logger.Errors.Where(e => e.Code == "X01235").ShouldBeEmpty();

        AcquireAndEvaluate(first, 1);
        logger.Warnings.Count(w => w.Code == "X01235").ShouldBe(firstSeverity == "warning" ? 1 : 0);
        logger.Errors.Where(e => e.Code == "X01235").ShouldBeEmpty();

        AcquireAndEvaluate(second, 2);
        logger.Warnings.Count(w => w.Code == "X01235").ShouldBe(expectedWarnings);
        logger.Errors.Count(e => e.Code == "X01235").ShouldBe(expectedErrors);

        void AcquireAndEvaluate(string projectPath, int evaluationId)
        {
            var context = new CheckDispatchingContext(dispatcher,
                new BuildEventContext(1, 2, evaluationId, BuildEventContext.InvalidProjectInstanceId, BuildEventContext.InvalidProjectContextId, 6, 7));
            _testedInstance.ProcessCheckAcquisition(new CheckAcquisitionData(Path.Combine(AppContext.BaseDirectory, "CustomCheck.dll"), projectPath), context);
            _testedInstance.ProcessEvaluationFinishedEventArgs(context, new ProjectEvaluationFinishedEventArgs
            {
                ProjectFile = projectPath,
                BuildEventContext = context.BuildEventContext,
            });
            var projectContext = new BuildEventContext(1, 2, evaluationId, evaluationId, evaluationId, 6, 7);
            _testedInstance.StartProjectRequest(new CheckDispatchingContext(dispatcher, projectContext), projectPath);
        }
    }

    [Theory]
    [InlineData(10, BuildEventContext.InvalidProjectInstanceId)]
    [InlineData(BuildEventContext.InvalidEvaluationId, 20)]
    public void ProjectStartDispatchesOnlyMatchingDeferredWarnings(int evaluationId, int projectInstanceId)
    {
        var reporter = (IResultReporter)_testedInstance;
        var projectContext = new BuildEventContext(1, 2, 10, 20, 30, 6, 7);
        var dispatcher = new EventArgsDispatcher();
        var logger = new MockLogger(_output);
        logger.Initialize(dispatcher);
        var checkContext = new CheckDispatchingContext(dispatcher, projectContext);
        BuildWarningEventArgs matching = CreateDeferredWarning(evaluationId, projectInstanceId, "matching");
        BuildWarningEventArgs unrelated = CreateDeferredWarning(11, 21, "unrelated");
        logger.Warnings.ShouldBeEmpty();

        reporter.ReportResult(matching, checkContext);
        reporter.ReportResult(unrelated, checkContext);
        logger.Warnings.ShouldBeEmpty();

        _testedInstance.StartProjectRequest(checkContext, "matching.proj");

        logger.Warnings.ShouldHaveSingleItem().ShouldBeSameAs(matching);
        matching.BuildEventContext.ShouldNotBeNull().ProjectInstanceId.ShouldBe(projectContext.ProjectInstanceId);
        matching.BuildEventContext.ProjectContextId.ShouldBe(projectContext.ProjectContextId);
        unrelated.BuildEventContext.ShouldNotBeNull().ProjectContextId.ShouldBe(BuildEventContext.InvalidProjectContextId);

        _testedInstance.StartProjectRequest(checkContext, "matching.proj");
        logger.Warnings.ShouldHaveSingleItem();
        _testedInstance.FinalizeProcessing(checkContext);
        logger.Warnings.ShouldBe([matching, unrelated]);
        _testedInstance.FinalizeProcessing(checkContext);
        logger.Warnings.Count.ShouldBe(2);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DeferredWarningDispatchCanEnqueueAnotherWarning(bool projectStarts)
    {
        var reporter = (IResultReporter)_testedInstance;
        var dispatcher = new EventArgsDispatcher();
        var checkContext = new CheckDispatchingContext(dispatcher, new BuildEventContext(1, 2, 10, 20, 30, 6, 7));
        BuildWarningEventArgs first = CreateDeferredWarning(10, BuildEventContext.InvalidProjectInstanceId, "first");
        BuildWarningEventArgs second = CreateDeferredWarning(10, BuildEventContext.InvalidProjectInstanceId, "second");
        List<BuildEventArgs> dispatched = [];
        dispatcher.AnyEventRaised += (_, e) =>
        {
            dispatched.Add(e);
            if (ReferenceEquals(e, first))
            {
                reporter.ReportResult(second, checkContext);
            }
        };
        dispatched.ShouldBeEmpty();
        reporter.ReportResult(first, checkContext);
        dispatched.ShouldBeEmpty();

        Drain();
        dispatched.ShouldBe([first]);
        Drain();
        dispatched.ShouldBe([first, second]);
        Drain();
        dispatched.Count.ShouldBe(2);

        void Drain()
        {
            if (projectStarts)
            {
                _testedInstance.StartProjectRequest(checkContext, "test.proj");
            }
            else
            {
                _testedInstance.FinalizeProcessing(checkContext);
            }
        }
    }

    [Fact]
    public async Task ConcurrentDeferredWarningDrainsPreserveEveryWarningOnce()
    {
        const int warningCount = 512;
        var reporter = (IResultReporter)_testedInstance;
        var dispatcher = new EventArgsDispatcher();
        var checkContext = new CheckDispatchingContext(dispatcher, new BuildEventContext(1, 2, 10, 20, 30, 6, 7));
        ConcurrentBag<BuildEventArgs> dispatched = [];
        dispatcher.AnyEventRaised += (_, e) => dispatched.Add(e);
        var start = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        List<Task> operations = [];
        dispatched.ShouldBeEmpty();

        for (int producer = 0; producer < 4; producer++)
        {
            int first = producer * warningCount / 4;
            operations.Add(Task.Run(async () =>
            {
                await start.Task;
                for (int i = first; i < first + warningCount / 4; i++)
                {
                    reporter.ReportResult(CreateDeferredWarning(10, BuildEventContext.InvalidProjectInstanceId, i.ToString()), checkContext);
                }
            }));
        }
        operations.Add(Task.Run(async () =>
        {
            await start.Task;
            for (int i = 0; i < warningCount; i++)
            {
                _testedInstance.StartProjectRequest(checkContext, "test.proj");
            }
        }));
        operations.Add(Task.Run(async () =>
        {
            await start.Task;
            for (int i = 0; i < warningCount; i++)
            {
                _testedInstance.FinalizeProcessing(checkContext);
            }
        }));

        start.SetResult(true);
        await Task.WhenAll(operations);
        _testedInstance.FinalizeProcessing(checkContext);

        dispatched.Count.ShouldBe(warningCount);
        dispatched.Select(e => int.Parse(e.Message.ShouldNotBeNull())).OrderBy(i => i).ShouldBe(Enumerable.Range(0, warningCount));
        _testedInstance.FinalizeProcessing(checkContext);
        dispatched.Count.ShouldBe(warningCount);
    }

    [Theory]
    [InlineData("global", true, false)]
    [InlineData("origin-project", true, false)]
    [InlineData("unrelated-project", false, false)]
    [InlineData("excluded", false, false)]
    [InlineData("message", false, true)]
    [InlineData("default", false, false)]
    public void FailureWarningsUseMSBuildPolicyInTheirOriginatingContext(string policy, bool promotes, bool demotes)
    {
        const string code = "POL001";
        var origin = new BuildEventContext(1, 2, 10, 20, BuildEventContext.InvalidProjectContextId, 6, 7);
        var unrelated = new BuildEventContext(1, 2, 11, 21, 31, 6, 7);
        var context = new CheckLoggingContext(_loggingService, origin);
        _loggingService.ShouldTreatWarningAsError(origin, code).ShouldBeFalse();
        _logger.AllBuildEvents.ShouldBeEmpty();
        if (policy is "global" or "excluded" or "message")
        {
            _loggingService.WarningsAsErrors = new HashSet<string>();
        }
        if (policy is "origin-project" or "unrelated-project")
        {
            _loggingService.AddWarningsAsErrors(policy == "origin-project" ? origin : unrelated, new HashSet<string>());
        }
        if (policy == "excluded")
        {
            _loggingService.WarningsNotAsErrors = new HashSet<string> { code };
        }
        if (policy == "message")
        {
            _loggingService.WarningsAsMessages = new HashSet<string> { code };
        }
        var warning = new BuildWarningEventArgs(null, code, "test.proj", 3, 4, 3, 9, "infrastructure warning", null, "BuildCheck")
        {
            BuildEventContext = origin,
        };
        context.ShouldTreatWarningAsError(code).ShouldBe(promotes);

        ((IResultReporter)_testedInstance).ReportFailure(warning, context);

        _logger.Errors.Count.ShouldBe(promotes ? 1 : 0);
        _logger.Warnings.ShouldBeEmpty();
        if (!promotes)
        {
            _logger.AllBuildEvents.ShouldBeEmpty("MSBuild warning exclusions and message demotion retain the existing deferred path");
        }
        _testedInstance.FinalizeProcessing(context);

        BuildEventArgs diagnostic = _logger.AllBuildEvents.ShouldHaveSingleItem();
        diagnostic.BuildEventContext.ShouldBeSameAs(origin);
        diagnostic.Message.ShouldBe(warning.Message);
        if (demotes)
        {
            diagnostic.ShouldBeOfType<BuildMessageEventArgs>();
        }
        else if (promotes)
        {
            diagnostic.ShouldBeOfType<BuildErrorEventArgs>().Code.ShouldBe(code);
        }
        else
        {
            diagnostic.ShouldBeSameAs(warning);
        }
        _testedInstance.FinalizeProcessing(context);
        _logger.AllBuildEvents.ShouldHaveSingleItem();
    }

    private static BuildWarningEventArgs CreateDeferredWarning(int evaluationId, int projectInstanceId, string message)
        => new(null, "DEFER001", "test.proj", 0, 0, 0, 0, message, null, "BuildCheck")
        {
            BuildEventContext = new BuildEventContext(1, 2, evaluationId, projectInstanceId, BuildEventContext.InvalidProjectContextId, 6, 7),
        };

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
            ElementLocation.Empty,
            "Argument for the message format"));
    }
}
