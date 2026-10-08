// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.BackEnd;
using Microsoft.Build.BackEnd.Components.Logging;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.BackEnd.SdkResolution;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Evaluation.Context;
using Microsoft.Build.Exceptions;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Shouldly;
using Xunit;
using InternalSdkLogger = Microsoft.Build.BackEnd.SdkResolution.SdkLogger;
using SdkResult = Microsoft.Build.BackEnd.SdkResolution.SdkResult;

#nullable enable

namespace Microsoft.Build.UnitTests.BackEnd;

public sealed class ProjectInstanceSnapshotValidation_Tests
{
    private static readonly BuildEventContext s_buildEventContext = new(1, 2, 3, 4);
    private readonly ITestOutputHelper _output;

    public ProjectInstanceSnapshotValidation_Tests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void MatchingRequestAndCurrentSdkAreAccepted()
    {
        ProjectInstanceSnapshotCacheKey key = CreateKey("matching.csproj");
        SdkResult result = CreateSdkResult("matching");
        ProjectInstanceSnapshotCacheEntry entry = CreateEntry(key, result);
        var resolver = new RecordingSdkResolverService(_ => result);
        var context = CreateContext(resolver);

        ProjectInstanceSnapshotValidationResult validation =
            FileSystemProjectInstanceSnapshotValidator.Instance.Validate(key, entry, context);

        validation.ShouldBe(ProjectInstanceSnapshotValidationResult.Valid);
        resolver.ResolveCount.ShouldBe(1);
    }

    [Fact]
    public void KeyMismatchAndMissingSdkContextAreRejected()
    {
        ProjectInstanceSnapshotCacheKey key = CreateKey("original.csproj");
        SdkResult result = CreateSdkResult("matching");
        ProjectInstanceSnapshotCacheEntry entry = CreateEntry(key, result);
        var resolver = new RecordingSdkResolverService(_ => result);

        FileSystemProjectInstanceSnapshotValidator.Instance.Validate(
                CreateKey("different.csproj"),
                entry,
                CreateContext(resolver))
            .ShouldBe(ProjectInstanceSnapshotValidationResult.Invalid);
        FileSystemProjectInstanceSnapshotValidator.Instance.Validate(key, entry)
            .ShouldBe(ProjectInstanceSnapshotValidationResult.Invalid);
        resolver.ResolveCount.ShouldBe(0);
    }

    [Fact]
    public void ChangedSdkResultIsRejected()
    {
        ProjectInstanceSnapshotCacheKey key = CreateKey("changed-sdk.csproj");
        SdkResult recorded = CreateSdkResult("recorded");
        SdkResult changed = CreateSdkResult("changed");
        ProjectInstanceSnapshotCacheEntry entry = CreateEntry(key, recorded);
        var resolver = new RecordingSdkResolverService(_ => changed);

        ProjectInstanceSnapshotValidationResult validation =
            FileSystemProjectInstanceSnapshotValidator.Instance.Validate(
                key,
                entry,
                CreateContext(resolver));

        validation.ShouldBe(ProjectInstanceSnapshotValidationResult.Invalid);
        resolver.ResolveCount.ShouldBe(1);
    }

    [Fact]
    public void MatchingSdkResultWithReturnedWarningIsRejected()
    {
        ProjectInstanceSnapshotCacheKey key = CreateKey("warning-result.csproj");
        var result = new SdkResult(
            new SdkReference("TestSdk", "1.0", null),
            ProjectPath("warning"),
            "1.0",
            warnings: ["resolver warning"]);
        ProjectInstanceSnapshotCacheEntry entry = CreateEntry(key, result);
        var resolver = new RecordingSdkResolverService(_ => result);

        FileSystemProjectInstanceSnapshotValidator.Instance.Validate(
                key,
                entry,
                CreateContext(resolver))
            .ShouldBe(ProjectInstanceSnapshotValidationResult.Invalid);
        resolver.ResolveCount.ShouldBe(1);
    }

    [Fact]
    public void DirectEnvironmentChangeRejectsBeforeSdkResolution()
    {
        const string variable = "MSBUILD_SNAPSHOT_VALIDATION_ENV";
        using TestEnvironment env = TestEnvironment.Create(_output);
        env.SetEnvironmentVariable(variable, "recorded");
        ProjectInstanceSnapshotCacheKey key = CreateKey("environment.csproj");
        SdkResult result = CreateSdkResult("matching");
        var recorder = new EvaluationInputRecorder();
        recorder.RecordEnvironmentRead(variable, "recorded");
        RecordSdk(recorder, key, result);
        ProjectInstanceSnapshotCacheEntry entry = CreateEntry(
            key,
            recorder.Freeze(key.ToEvaluationInputKey()));
        var resolver = new RecordingSdkResolverService(_ => result);

        env.SetEnvironmentVariable(variable, "changed");

        FileSystemProjectInstanceSnapshotValidator.Instance.Validate(
                key,
                entry,
                CreateContext(resolver))
            .ShouldBe(ProjectInstanceSnapshotValidationResult.Invalid);
        resolver.ResolveCount.ShouldBe(0);
    }

    [Theory]
    [InlineData(DeferredDiagnostic.Warning)]
    [InlineData(DeferredDiagnostic.Error)]
    [InlineData(DeferredDiagnostic.SdkMessage)]
    [InlineData(DeferredDiagnostic.FatalBuildError)]
    public void DiagnosticsRejectAndReplayExactlyOnceOnFreshFallback(DeferredDiagnostic diagnostic)
    {
        ProjectInstanceSnapshotCacheKey key = CreateKey("diagnostics.csproj");
        SdkResult result = CreateSdkResult("matching");
        ProjectInstanceSnapshotCacheEntry entry = CreateEntry(key, result);
        const string message = "deferred validation diagnostic";
        var logged = new List<string>();
        var loggingService = new MockLoggingService(logged.Add);
        var resolver = new RecordingSdkResolverService(
            loggingContext =>
            {
                LogDiagnostic(loggingContext, diagnostic, message);
                return result;
            });
        var context = new ProjectInstanceSnapshotValidationContext(
            resolver,
            loggingService,
            s_buildEventContext,
            submissionId: 7);

        FileSystemProjectInstanceSnapshotValidator.Instance.Validate(key, entry, context)
            .ShouldBe(ProjectInstanceSnapshotValidationResult.Invalid);
        resolver.ResolveCount.ShouldBe(1);
        logged.ShouldBeEmpty();

        ISdkResolverService fallback = context.GetResolverForFreshEvaluation();
        LoggingContext target = new EvaluationLoggingContext(
            loggingService,
            s_buildEventContext,
            ProjectFullPath(key));
        SdkDependency dependency = GetSdkDependency(entry);
        SdkResult first = Resolve(fallback, dependency, target);
        SdkResult second = Resolve(fallback, dependency, target);

        first.ShouldBeSameAs(result);
        second.ShouldBeSameAs(result);
        resolver.ResolveCount.ShouldBe(1);
        logged.ShouldBe([message]);
    }

    [Fact]
    public void MatchingSdkWithoutDiagnosticsIsAcceptedAndFallbackReusesResult()
    {
        ProjectInstanceSnapshotCacheKey key = CreateKey("no-diagnostics.csproj");
        SdkResult result = CreateSdkResult("matching");
        ProjectInstanceSnapshotCacheEntry entry = CreateEntry(key, result);
        var resolver = new RecordingSdkResolverService(_ => result);
        ProjectInstanceSnapshotValidationContext context = CreateContext(resolver);

        FileSystemProjectInstanceSnapshotValidator.Instance.Validate(key, entry, context)
            .ShouldBe(ProjectInstanceSnapshotValidationResult.Valid);

        SdkDependency dependency = GetSdkDependency(entry);
        SdkResult fallback = Resolve(
            context.GetResolverForFreshEvaluation(),
            dependency,
            CreateTargetLoggingContext(ProjectFullPath(key)));
        fallback.ShouldBeSameAs(result);
        resolver.ResolveCount.ShouldBe(1);
    }

    [Fact]
    public void UnrelatedContextDoesNotConsumePreResolvedResult()
    {
        ProjectInstanceSnapshotCacheKey key = CreateKey("context.csproj");
        SdkResult recorded = CreateSdkResult("recorded");
        SdkResult unrelated = CreateSdkResult("unrelated");
        ProjectInstanceSnapshotCacheEntry entry = CreateEntry(key, recorded);
        var resolver = new RecordingSdkResolverService(
            (call, _) => call == 1 ? recorded : unrelated);
        ProjectInstanceSnapshotValidationContext context = CreateContext(resolver);

        FileSystemProjectInstanceSnapshotValidator.Instance.Validate(key, entry, context)
            .ShouldBe(ProjectInstanceSnapshotValidationResult.Valid);

        SdkDependency dependency = GetSdkDependency(entry);
        var differentContext = dependency.Context with
        {
            ProjectPath = ProjectPath("different.csproj"),
        };
        ISdkResolverService fallback = context.GetResolverForFreshEvaluation();

        SdkResult delegated = Resolve(
            fallback,
            dependency.Reference,
            differentContext,
            CreateTargetLoggingContext(differentContext.ProjectPath));
        SdkResult reused = Resolve(
            fallback,
            dependency,
            CreateTargetLoggingContext(ProjectFullPath(key)));

        delegated.ShouldBeSameAs(unrelated);
        reused.ShouldBeSameAs(recorded);
        resolver.ResolveCount.ShouldBe(2);
    }

    [Fact]
    public void DifferentSubmissionDoesNotConsumePreResolvedResult()
    {
        ProjectInstanceSnapshotCacheKey key = CreateKey("submission.csproj");
        SdkResult recorded = CreateSdkResult("recorded");
        SdkResult delegated = CreateSdkResult("delegated");
        ProjectInstanceSnapshotCacheEntry entry = CreateEntry(key, recorded);
        var resolver = new RecordingSdkResolverService(
            (call, _) => call == 1 ? recorded : delegated);
        ProjectInstanceSnapshotValidationContext context = CreateContext(resolver);

        FileSystemProjectInstanceSnapshotValidator.Instance.Validate(key, entry, context)
            .ShouldBe(ProjectInstanceSnapshotValidationResult.Valid);

        SdkDependency dependency = GetSdkDependency(entry);
        ISdkResolverService fallback = context.GetResolverForFreshEvaluation();
        SdkResult differentSubmission = Resolve(
            fallback,
            dependency,
            CreateTargetLoggingContext(ProjectFullPath(key)),
            submissionId: 8);
        SdkResult validatingSubmission = Resolve(
            fallback,
            dependency,
            CreateTargetLoggingContext(ProjectFullPath(key)),
            submissionId: 7);

        differentSubmission.ShouldBeSameAs(delegated);
        validatingSubmission.ShouldBeSameAs(recorded);
        resolver.ResolveCount.ShouldBe(2);
    }

    [Fact]
    public void RecoverableResolverFailureRejectsAndFreshFallbackRetries()
    {
        ProjectInstanceSnapshotCacheKey key = CreateKey("recoverable.csproj");
        SdkResult result = CreateSdkResult("matching");
        ProjectInstanceSnapshotCacheEntry entry = CreateEntry(key, result);
        var resolver = new RecordingSdkResolverService(
            (call, _) => call == 1
                ? throw new SdkResolverException(
                    "wrapped resolver failure",
                    new IOException("transient resolver failure"))
                : result);
        ProjectInstanceSnapshotValidationContext context = CreateContext(resolver);

        FileSystemProjectInstanceSnapshotValidator.Instance.Validate(key, entry, context)
            .ShouldBe(ProjectInstanceSnapshotValidationResult.Invalid);

        SdkResult fallback = Resolve(
            context.GetResolverForFreshEvaluation(),
            GetSdkDependency(entry),
            CreateTargetLoggingContext(ProjectFullPath(key)));
        fallback.ShouldBeSameAs(result);
        resolver.ResolveCount.ShouldBe(2);
    }

    [Theory]
    [MemberData(nameof(PropagatedResolverExceptions))]
    public void ResolverCancellationAndBuildAbortPropagate(Exception expected)
    {
        ProjectInstanceSnapshotCacheKey key = CreateKey("exception.csproj");
        SdkResult result = CreateSdkResult("matching");
        ProjectInstanceSnapshotCacheEntry entry = CreateEntry(key, result);
        var resolver = new RecordingSdkResolverService(_ => throw expected);

        Exception actual = Record.Exception(
            () => FileSystemProjectInstanceSnapshotValidator.Instance.Validate(
                key,
                entry,
                CreateContext(resolver)))!;

        actual.GetType().ShouldBe(expected.GetType());
        actual.ShouldBeSameAs(expected);
        resolver.ResolveCount.ShouldBe(1);
    }

    [Theory]
    [MemberData(nameof(PropagatedResolverExceptions))]
    public void WrappedResolverCancellationBuildAbortAndCriticalFailurePropagate(Exception expected)
    {
        ProjectInstanceSnapshotCacheKey key = CreateKey("wrapped-exception.csproj");
        SdkResult result = CreateSdkResult("matching");
        ProjectInstanceSnapshotCacheEntry entry = CreateEntry(key, result);
        var resolver = new RecordingSdkResolverService(
            _ => throw new SdkResolverException("wrapped resolver failure", expected));

        Exception actual = Record.Exception(
            () => FileSystemProjectInstanceSnapshotValidator.Instance.Validate(
                key,
                entry,
                CreateContext(resolver)))!;

        actual.ShouldBeSameAs(expected);
        resolver.ResolveCount.ShouldBe(1);
    }

    public static TheoryData<Exception> PropagatedResolverExceptions =>
    [
        new OperationCanceledException("validation canceled"),
        new BuildAbortedException("validation aborted"),
        new OutOfMemoryException("critical validation failure"),
    ];

    private static ProjectInstanceSnapshotValidationContext CreateContext(
        ISdkResolverService resolver) =>
        new(
            resolver,
            new MockLoggingService(),
            s_buildEventContext,
            submissionId: 7);

    private static LoggingContext CreateTargetLoggingContext(string projectPath) =>
        new EvaluationLoggingContext(
            new MockLoggingService(),
            s_buildEventContext,
            projectPath);

    private static ProjectInstanceSnapshotCacheKey CreateKey(string fileName) =>
        new(
            ProjectPath(fileName),
            "Current",
            explicitToolsVersionSpecified: false,
            subToolsetVersion: null,
            ProjectLoadSettings.Default,
            new Dictionary<string, string>());

    private static string ProjectPath(string fileName) =>
        Path.Combine(Path.GetTempPath(), "snapshot-validation-tests", fileName);

    private static ProjectInstanceSnapshotCacheEntry CreateEntry(
        ProjectInstanceSnapshotCacheKey key,
        SdkResult result)
    {
        var recorder = new EvaluationInputRecorder();
        RecordSdk(recorder, key, result);
        return CreateEntry(key, recorder.Freeze(key.ToEvaluationInputKey()));
    }

    private static ProjectInstanceSnapshotCacheEntry CreateEntry(
        ProjectInstanceSnapshotCacheKey key,
        EvaluationInputs inputs)
    {
        using var collection = new ProjectCollection();
        using var projectFromString = new ProjectRootElementFromString(
            "<Project><PropertyGroup><Value>snapshot</Value></PropertyGroup></Project>",
            collection);
        ProjectInstanceSnapshot snapshot =
            ProjectInstanceSnapshot.Create(new ProjectInstance(projectFromString.Project));
        return new ProjectInstanceSnapshotCacheEntry(
            snapshot,
            new EvaluationInputsSnapshotValidationData(inputs));
    }

    private static void RecordSdk(
        EvaluationInputRecorder recorder,
        ProjectInstanceSnapshotCacheKey key,
        SdkResult result) =>
        recorder.RecordSdkResolution(
            result.SdkReference,
            result,
            ElementLocation.Create(ProjectFullPath(key), 2, 3),
            solutionPath: null,
            ProjectFullPath(key),
            interactive: false,
            isRunningInVisualStudio: false,
            failOnUnresolvedSdk: true);

    private static SdkDependency GetSdkDependency(ProjectInstanceSnapshotCacheEntry entry) =>
        ((EvaluationInputsSnapshotValidationData)entry.ValidationData).Inputs.SdkResolutions[0];

    private static string ProjectFullPath(ProjectInstanceSnapshotCacheKey key) =>
        key.ToEvaluationInputKey().ProjectFullPath;

    private static SdkResult CreateSdkResult(string directory) =>
        new(
            new SdkReference("TestSdk", "1.0", null),
            ProjectPath(directory),
            "1.0",
            warnings: null);

    private static SdkResult Resolve(
        ISdkResolverService resolver,
        SdkDependency dependency,
        LoggingContext loggingContext,
        int submissionId = 7) =>
        Resolve(
            resolver,
            dependency.Reference,
            dependency.Context,
            loggingContext,
            submissionId);

    private static SdkResult Resolve(
        ISdkResolverService resolver,
        SdkReference reference,
        SdkResolutionContext context,
        LoggingContext loggingContext,
        int submissionId = 7) =>
        resolver.ResolveSdk(
            submissionId,
            reference,
            loggingContext,
            context.ReferenceLocation,
            context.SolutionPath!,
            context.ProjectPath,
            context.Interactive,
            context.IsRunningInVisualStudio,
            context.FailOnUnresolvedSdk);

    private static void LogDiagnostic(
        LoggingContext context,
        DeferredDiagnostic diagnostic,
        string message)
    {
        switch (diagnostic)
        {
            case DeferredDiagnostic.Warning:
                context.LogWarningFromText(
                    subcategoryResourceName: null,
                    warningCode: "TEST0001",
                    helpKeyword: null!,
                    BuildEventFileInfo.Empty,
                    message);
                break;
            case DeferredDiagnostic.Error:
                context.LogErrorFromText(
                    subcategoryResourceName: null,
                    errorCode: "TEST0002",
                    helpKeyword: null,
                    BuildEventFileInfo.Empty,
                    message);
                break;
            case DeferredDiagnostic.SdkMessage:
                new InternalSdkLogger(context).LogMessage(message, MessageImportance.High);
                break;
            case DeferredDiagnostic.FatalBuildError:
                context.LogFatalBuildError(
                    new InvalidOperationException(message),
                    BuildEventFileInfo.Empty);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(diagnostic));
        }
    }

    public enum DeferredDiagnostic
    {
        Warning,
        Error,
        SdkMessage,
        FatalBuildError,
    }

    private sealed class RecordingSdkResolverService : ISdkResolverService
    {
        private readonly Func<int, LoggingContext, SdkResult> _resolve;

        internal RecordingSdkResolverService(Func<LoggingContext, SdkResult> resolve)
            : this((_, context) => resolve(context))
        {
        }

        internal RecordingSdkResolverService(Func<int, LoggingContext, SdkResult> resolve)
        {
            _resolve = resolve;
        }

        internal int ResolveCount { get; private set; }

        public Action<INodePacket> SendPacket => static _ => { };

        public bool IsNodeShutDown { get; set; }

        public void ClearCache(int submissionId)
        {
        }

        public void ClearCaches()
        {
        }

        public SdkResult ResolveSdk(
            int submissionId,
            SdkReference sdk,
            LoggingContext loggingContext,
            ElementLocation sdkReferenceLocation,
            string solutionPath,
            string projectPath,
            bool interactive,
            bool isRunningInVisualStudio,
            bool failOnUnresolvedSdk)
        {
            ResolveCount++;
            return _resolve(ResolveCount, loggingContext);
        }
    }
}
