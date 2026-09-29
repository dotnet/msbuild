// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
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
using SdkResult = Microsoft.Build.BackEnd.SdkResolution.SdkResult;

#nullable enable

namespace Microsoft.Build.UnitTests.BackEnd;

public sealed class ProjectInstanceSnapshotValidationDiagnostics_Tests
{
    private const string Secret = "sensitive-value-not-for-diagnostics";
    private const string SdkName = "ValidationDiagnosticsSdk";
    private static readonly SdkReference s_sdkReference = new(SdkName, "1.0-" + Secret, null);
    private static readonly BuildEventContext s_buildEventContext = new(1, 2, 3, 4);
    private readonly ITestOutputHelper _output;

    public ProjectInstanceSnapshotValidationDiagnostics_Tests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void CurrentInputsReturnDefaultFailure()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        const string variable = "MSBUILD_VALIDATION_DIAGNOSTICS_CURRENT";
        env.SetEnvironmentVariable(variable, Secret);
        string path = env.CreateFile("input.props", "<Project />").Path;
        var recorder = new EvaluationInputRecorder();
        recorder.RecordPath(path);
        recorder.RecordEnvironmentRead(variable, Secret);
        ProjectInstanceSnapshotCacheKey key = CreateKey();
        EvaluationInputs inputs = CreateInputs(key, recorder: recorder);

        EvaluationInputValidator.IsFileSystemCurrent(inputs, out string? reason, out EvaluationInputValidationFailure failure)
            .ShouldBeTrue();
        reason.ShouldBeNull();
        failure.ShouldBe(default);
        EvaluationInputValidator.IsFileSystemCurrent(inputs, out reason).ShouldBeTrue();
        reason.ShouldBeNull();
        ProjectInstanceSnapshotCacheEntry entry = CreateEntry(env, inputs);
        FileSystemProjectInstanceSnapshotValidator.Instance.Validate(key, entry, null, out failure)
            .ShouldBe(ProjectInstanceSnapshotValidationResult.Valid);
        failure.ShouldBe(default);
        FileSystemProjectInstanceSnapshotValidator.Instance.Validate(key, entry, null)
            .ShouldBe(ProjectInstanceSnapshotValidationResult.Valid);
        IProjectInstanceSnapshotValidator validator = FileSystemProjectInstanceSnapshotValidator.Instance;
        validator.Validate(key, entry).ShouldBe(ProjectInstanceSnapshotValidationResult.Valid);
    }

    [Fact]
    public void MissingManifestHasNoDetail()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        ProjectInstanceSnapshotCacheKey key = CreateKey();
        ProjectInstanceSnapshotCacheEntry entry = CreateEntry(env, inputs: null);

        FileSystemProjectInstanceSnapshotValidator.Instance.Validate(key, entry, null, out EvaluationInputValidationFailure failure)
            .ShouldBe(ProjectInstanceSnapshotValidationResult.Invalid);

        failure.ShouldBe(new("MissingValidationManifest", null));
        FileSystemProjectInstanceSnapshotValidator.Instance.Validate(key, entry)
            .ShouldBe(ProjectInstanceSnapshotValidationResult.Invalid);
    }

    [Theory]
    [InlineData(nameof(EvaluationInputKey.ProjectFullPath))]
    [InlineData(nameof(EvaluationInputKey.GlobalProperties))]
    [InlineData(nameof(EvaluationInputKey.EnvironmentFingerprint))]
    public void KeyMismatchReportsOnlyFieldNameBeforeResolvingSdk(string field)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        ProjectInstanceSnapshotCacheKey key = CreateKey();
        EvaluationInputs inputs = CreateInputs(key, CreateSdkResult());
        inputs = inputs with
        {
            Key = field switch
            {
                nameof(EvaluationInputKey.ProjectFullPath) => inputs.Key with { ProjectFullPath = ProjectPath(Secret) },
                nameof(EvaluationInputKey.GlobalProperties) => inputs.Key with { GlobalProperties = "TOKEN=" + Secret + "\0" },
                nameof(EvaluationInputKey.EnvironmentFingerprint) => inputs.Key with { EnvironmentFingerprint = 123456789 },
                _ => throw new ArgumentOutOfRangeException(nameof(field)),
            },
        };
        var resolver = new RecordingSdkResolverService(_ => CreateSdkResult());

        FileSystemProjectInstanceSnapshotValidator.Instance.Validate(
                key, CreateEntry(env, inputs), CreateContext(resolver), out EvaluationInputValidationFailure failure)
            .ShouldBe(ProjectInstanceSnapshotValidationResult.Invalid);

        failure.ShouldBe(new("RequestKeyMismatch", field));
        resolver.ResolveCount.ShouldBe(0);
    }

    [Fact]
    public void NonCacheableReportsEnumNameWithoutRecordedDetail()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        ProjectInstanceSnapshotCacheKey key = CreateKey();
        var recorder = new EvaluationInputRecorder();
        recorder.MarkNonCacheable(NonCacheableReason.RecorderFailure, Secret);
        EvaluationInputs inputs = CreateInputs(key, recorder: recorder);

        AssertFileSystemFailure(inputs, "NonCacheable", nameof(NonCacheableReason.RecorderFailure), $"RecorderFailure: {Secret}");
        ProjectInstanceSnapshotCacheEntry entry = CreateEntry(env, inputs);
        FileSystemProjectInstanceSnapshotValidator.Instance.Validate(
                key, entry, null, out EvaluationInputValidationFailure failure)
            .ShouldBe(ProjectInstanceSnapshotValidationResult.Invalid);
        failure.ShouldBe(new("NonCacheable", nameof(NonCacheableReason.RecorderFailure)));
        FileSystemProjectInstanceSnapshotValidator.Instance.Validate(key, entry, null)
            .ShouldBe(ProjectInstanceSnapshotValidationResult.Invalid);
        IProjectInstanceSnapshotValidator validator = FileSystemProjectInstanceSnapshotValidator.Instance;
        validator.Validate(key, entry).ShouldBe(ProjectInstanceSnapshotValidationResult.Invalid);
    }

    [Fact]
    public void ChangedEnvironmentReportsOnlyVariableNameBeforeResolvingSdk()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        const string variable = "MSBUILD_VALIDATION_DIAGNOSTICS_CHANGED";
        env.SetEnvironmentVariable(variable, Secret + "-current");
        ProjectInstanceSnapshotCacheKey key = CreateKey();
        var recorder = new EvaluationInputRecorder();
        recorder.RecordEnvironmentRead(variable, Secret + "-recorded");
        EvaluationInputs inputs = CreateInputs(key, CreateSdkResult(), recorder);
        var resolver = new RecordingSdkResolverService(_ => CreateSdkResult());

        AssertFileSystemFailure(inputs, "EnvironmentReadChanged", variable, variable);
        FileSystemProjectInstanceSnapshotValidator.Instance.Validate(
                key, CreateEntry(env, inputs), CreateContext(resolver), out EvaluationInputValidationFailure failure)
            .ShouldBe(ProjectInstanceSnapshotValidationResult.Invalid);

        failure.ShouldBe(new("EnvironmentReadChanged", variable));
        resolver.ResolveCount.ShouldBe(0);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void ChangedFileSystemInputReportsPath(bool existed, bool exists)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        TransientTestFolder folder = env.CreateFolder();
        string path = Path.Combine(folder.Path, "input.props");
        if (existed)
        {
            env.CreateFile(folder, "input.props", "original");
        }

        ProjectInstanceSnapshotCacheKey key = CreateKey();
        var recorder = new EvaluationInputRecorder();
        recorder.RecordPath(path);
        EvaluationInputs inputs = CreateInputs(key, recorder: recorder);
        if (exists)
        {
            File.WriteAllText(path, "changed with a different length");
        }
        else
        {
            File.Delete(path);
        }

        AssertFileSystemFailure(inputs, "FileSystemInputChanged", path, path);
        FileSystemProjectInstanceSnapshotValidator.Instance.Validate(
                key, CreateEntry(env, inputs), null, out EvaluationInputValidationFailure failure)
            .ShouldBe(ProjectInstanceSnapshotValidationResult.Invalid);
        failure.ShouldBe(new("FileSystemInputChanged", path));
    }

#if FEATURE_SYMLINK_TARGET
    [RequiresSymbolicLinksFact]
    public void UnstatableInputReportsPath()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        TransientTestFolder folder = env.CreateFolder();
        string target = env.CreateFile(folder, "target.props", "target").Path;
        string path = env.CreateFile(folder, "input.props", "original").Path;
        var recorder = new EvaluationInputRecorder();
        recorder.RecordPath(path);
        EvaluationInputs inputs = CreateInputs(CreateKey(), recorder: recorder);
        File.Delete(path);
        File.CreateSymbolicLink(path, target);

        AssertFileSystemFailure(inputs, "FileSystemInputUnstatable", path, path);
    }
#endif

    [Fact]
    public void MetadataCheckExceptionReportsOnlyTypeAndPreservesLegacyMessage()
    {
        EvaluationInputs inputs = CreateInputs(CreateKey()) with
        {
            Files = new ThrowingFileDependencies(new IOException(Secret)),
        };

        AssertFileSystemFailure(inputs, "MetadataCheckException", nameof(IOException), Secret);
    }

    [Theory]
    [MemberData(nameof(NonRecoverableExceptions))]
    public void FileSystemNonRecoverableExceptionsStillPropagate(Exception expected)
    {
        EvaluationInputs inputs = CreateInputs(CreateKey()) with
        {
            Files = new ThrowingFileDependencies(expected),
        };

        Record.Exception(() => EvaluationInputValidator.IsFileSystemCurrent(inputs, out _, out _))
            .ShouldBeSameAs(expected);
        Record.Exception(() => EvaluationInputValidator.IsFileSystemCurrent(inputs, out _))
            .ShouldBeSameAs(expected);
    }

    [Fact]
    public void MissingSdkContextReportsOnlySdkName()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        ProjectInstanceSnapshotCacheKey key = CreateKey();
        ProjectInstanceSnapshotCacheEntry entry = CreateEntry(env, CreateInputs(key, CreateSdkResult()));

        FileSystemProjectInstanceSnapshotValidator.Instance.Validate(key, entry, null, out EvaluationInputValidationFailure failure)
            .ShouldBe(ProjectInstanceSnapshotValidationResult.Invalid);

        failure.ShouldBe(new("MissingSdkContext", SdkName));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MatchingSdkValidationModesReuseSingleResolution(bool captureDetails)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        ProjectInstanceSnapshotCacheKey key = CreateKey();
        SdkResult result = CreateSdkResult();
        EvaluationInputs inputs = CreateInputs(key, result);
        var resolver = new RecordingSdkResolverService(_ => result);
        ProjectInstanceSnapshotValidationContext context = CreateContext(resolver);
        ProjectInstanceSnapshotCacheEntry entry = CreateEntry(env, inputs);
        EvaluationInputValidationFailure failure = default;

        ProjectInstanceSnapshotValidationResult validation = captureDetails
            ? FileSystemProjectInstanceSnapshotValidator.Instance.Validate(key, entry, context, out failure)
            : FileSystemProjectInstanceSnapshotValidator.Instance.Validate(key, entry, context);

        validation.ShouldBe(ProjectInstanceSnapshotValidationResult.Valid);
        failure.ShouldBe(default);
        ResolveForFreshEvaluation(context, inputs.SdkResolutions[0]).ShouldBeSameAs(result);
        resolver.ResolveCount.ShouldBe(1);
    }

    [Theory]
    [InlineData("Null")]
    [InlineData("Unsuccessful")]
    [InlineData("UnsuccessfulWithDiagnostics")]
    public void FailedSdkResolutionReportsOnlySdkName(string kind)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        ProjectInstanceSnapshotCacheKey key = CreateKey();
        EvaluationInputs inputs = CreateInputs(key, CreateSdkResult());
        var resolver = new RecordingSdkResolverService(_ => kind switch
        {
            "Null" => null!,
            "Unsuccessful" => new SdkResult(s_sdkReference, errors: null, warnings: null),
            "UnsuccessfulWithDiagnostics" => new SdkResult(s_sdkReference, errors: [Secret], warnings: null),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        });

        FileSystemProjectInstanceSnapshotValidator.Instance.Validate(
                key, CreateEntry(env, inputs), CreateContext(resolver), out EvaluationInputValidationFailure failure)
            .ShouldBe(ProjectInstanceSnapshotValidationResult.Invalid);

        failure.ShouldBe(new("SdkResolutionFailed", SdkName));
        resolver.ResolveCount.ShouldBe(1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MatchingUnsuccessfulSdkRetainsExistingAcceptance(bool captureDetails)
    {
        SdkResult result = new(s_sdkReference, errors: null, warnings: null);
        EvaluationInputs inputs = CreateInputs(CreateKey(), result);
        var resolver = new RecordingSdkResolverService(_ => result);
        ProjectInstanceSnapshotValidationContext context = CreateContext(resolver);
        EvaluationInputValidationFailure failure = default;

        bool valid = captureDetails
            ? context.Validate(inputs.SdkResolutions[0], out failure)
            : context.Validate(inputs.SdkResolutions[0]);

        valid.ShouldBeTrue();
        failure.ShouldBe(default);
        resolver.ResolveCount.ShouldBe(1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ResolverExceptionDoesNotRetryAndOnlyDiagnosticOverloadCapturesFailure(bool captureDetails)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        ProjectInstanceSnapshotCacheKey key = CreateKey();
        EvaluationInputs inputs = CreateInputs(key, CreateSdkResult());
        var resolver = new RecordingSdkResolverService(_ => throw new SdkResolverException(Secret, new IOException(Secret)));
        ProjectInstanceSnapshotValidationContext context = CreateContext(resolver);
        ProjectInstanceSnapshotCacheEntry entry = CreateEntry(env, inputs);
        EvaluationInputValidationFailure failure = default;

        ProjectInstanceSnapshotValidationResult validation = captureDetails
            ? FileSystemProjectInstanceSnapshotValidator.Instance.Validate(key, entry, context, out failure)
            : FileSystemProjectInstanceSnapshotValidator.Instance.Validate(key, entry, context);

        validation.ShouldBe(ProjectInstanceSnapshotValidationResult.Invalid);
        failure.ShouldBe(captureDetails ? new("SdkResolutionException", SdkName) : default);
        context.GetResolverForFreshEvaluation().ShouldBeSameAs(resolver);
        resolver.ResolveCount.ShouldBe(1);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void SdkValidationModesPreserveDiagnosticsAndDeferredReplay(bool deferred, bool captureDetails)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        ProjectInstanceSnapshotCacheKey key = CreateKey();
        SdkResult result = deferred
            ? CreateSdkResult()
            : new SdkResult(s_sdkReference, ProjectPath("sdk"), "1.0", warnings: [Secret]);
        EvaluationInputs inputs = CreateInputs(key, result);
        var logged = new List<string>();
        var loggingService = new MockLoggingService(logged.Add);
        var resolver = new RecordingSdkResolverService(logging =>
        {
            if (deferred)
            {
                logging.LogWarningFromText(null, "TEST0001", null!, BuildEventFileInfo.Empty, Secret);
            }

            return result;
        });
        ProjectInstanceSnapshotValidationContext context = CreateContext(resolver, loggingService);
        ProjectInstanceSnapshotCacheEntry entry = CreateEntry(env, inputs);
        EvaluationInputValidationFailure failure = default;

        ProjectInstanceSnapshotValidationResult validation = captureDetails
            ? FileSystemProjectInstanceSnapshotValidator.Instance.Validate(key, entry, context, out failure)
            : FileSystemProjectInstanceSnapshotValidator.Instance.Validate(key, entry, context);

        validation.ShouldBe(ProjectInstanceSnapshotValidationResult.Invalid);
        failure.ShouldBe(captureDetails ? new("SdkDiagnostics", SdkName) : default);
        resolver.ResolveCount.ShouldBe(1);
        logged.ShouldBeEmpty();
        if (deferred)
        {
            ResolveForFreshEvaluation(context, inputs.SdkResolutions[0], loggingService).ShouldBeSameAs(result);
            ResolveForFreshEvaluation(context, inputs.SdkResolutions[0], loggingService).ShouldBeSameAs(result);
            resolver.ResolveCount.ShouldBe(1);
            logged.ShouldBe([Secret]);
        }
        else
        {
            context.GetResolverForFreshEvaluation().ShouldBeSameAs(resolver);
        }
    }

    [Theory]
    [InlineData(nameof(SdkResult.Path))]
    [InlineData(nameof(SdkResult.Version))]
    [InlineData(nameof(SdkResult.PropertiesToAdd))]
    [InlineData(nameof(SdkResult.ItemsToAdd))]
    [InlineData(nameof(SdkResult.EnvironmentVariablesToAdd))]
    public void ChangedSdkResultReportsOnlySdkNameAndReusesResolution(string field)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        ProjectInstanceSnapshotCacheKey key = CreateKey();
        EvaluationInputs inputs = CreateInputs(key, CreateSdkResult());
        SdkResult changed = CreateSdkResult(field);
        var resolver = new RecordingSdkResolverService(_ => changed);
        ProjectInstanceSnapshotValidationContext context = CreateContext(resolver);

        FileSystemProjectInstanceSnapshotValidator.Instance.Validate(
                key, CreateEntry(env, inputs), context, out EvaluationInputValidationFailure failure)
            .ShouldBe(ProjectInstanceSnapshotValidationResult.Invalid);

        failure.ShouldBe(new("SdkResultChanged", SdkName));
        ResolveForFreshEvaluation(context, inputs.SdkResolutions[0]).ShouldBeSameAs(changed);
        resolver.ResolveCount.ShouldBe(1);
    }

    [Theory]
    [MemberData(nameof(NonRecoverableExceptions))]
    public void DirectAndWrappedResolverNonRecoverableExceptionsStillPropagate(Exception expected)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        ProjectInstanceSnapshotCacheKey key = CreateKey();
        ProjectInstanceSnapshotCacheEntry entry = CreateEntry(env, CreateInputs(key, CreateSdkResult()));
        Exception[] exceptions = [expected, new SdkResolverException(Secret, expected)];
        foreach (Exception exception in exceptions)
        {
            var resolver = new RecordingSdkResolverService(_ => throw exception);

            Record.Exception(() => FileSystemProjectInstanceSnapshotValidator.Instance.Validate(
                    key, entry, CreateContext(resolver), out _))
                .ShouldBeSameAs(expected);
            resolver.ResolveCount.ShouldBe(1);
            Record.Exception(() => FileSystemProjectInstanceSnapshotValidator.Instance.Validate(
                    key, entry, CreateContext(resolver)))
                .ShouldBeSameAs(expected);
            resolver.ResolveCount.ShouldBe(2);
        }
    }

    public static TheoryData<Exception> NonRecoverableExceptions =>
    [
        new OperationCanceledException(Secret),
        new BuildAbortedException(Secret),
        new OutOfMemoryException(Secret),
    ];

    private static void AssertFileSystemFailure(EvaluationInputs inputs, string category, string detail, string legacyReason)
    {
        EvaluationInputValidator.IsFileSystemCurrent(inputs, out string? reason, out EvaluationInputValidationFailure failure)
            .ShouldBeFalse();
        failure.ShouldBe(new(category, detail));
        reason.ShouldBe(legacyReason);
        EvaluationInputValidator.IsFileSystemCurrent(inputs, out reason).ShouldBeFalse();
        reason.ShouldBe(legacyReason);
    }

    private static ProjectInstanceSnapshotCacheKey CreateKey() =>
        new(ProjectPath("project.csproj"), "Current", false, null, ProjectLoadSettings.Default, new Dictionary<string, string>());

    private static string ProjectPath(string name) =>
        Path.Combine(Path.GetTempPath(), "snapshot-validation-diagnostics-tests", name);

    private static EvaluationInputs CreateInputs(
        ProjectInstanceSnapshotCacheKey key,
        SdkResult? sdk = null,
        EvaluationInputRecorder? recorder = null)
    {
        recorder ??= new EvaluationInputRecorder();
        if (sdk is not null)
        {
            string path = key.ToEvaluationInputKey().ProjectFullPath;
            recorder.RecordSdkResolution(
                s_sdkReference, sdk, ElementLocation.Create(path, 2, 3), null, path, false, false, true);
        }

        return recorder.Freeze(key.ToEvaluationInputKey());
    }

    private ProjectInstanceSnapshotCacheEntry CreateEntry(TestEnvironment env, EvaluationInputs? inputs)
    {
        ProjectCollection collection = env.CreateProjectCollection().Collection;
        collection.RegisterLogger(new MockLogger(_output));
        using var project = new ProjectRootElementFromString("<Project />", collection);
        ProjectInstanceSnapshot snapshot = ProjectInstanceSnapshot.Create(new ProjectInstance(project.Project));
        return new ProjectInstanceSnapshotCacheEntry(
            snapshot,
            inputs is null ? EmptyProjectInstanceSnapshotValidationData.Instance : new EvaluationInputsSnapshotValidationData(inputs));
    }

    private static SdkResult CreateSdkResult(string? changedField = null) =>
        new(
            s_sdkReference,
            ProjectPath(changedField == nameof(SdkResult.Path) ? Secret : "sdk"),
            changedField == nameof(SdkResult.Version) ? Secret : "1.0",
            warnings: null,
            propertiesToAdd: new Dictionary<string, string>
            {
                ["Property"] = changedField == nameof(SdkResult.PropertiesToAdd) ? Secret : "recorded",
            },
            itemsToAdd: new Dictionary<string, SdkResultItem>
            {
                ["Item"] = new(changedField == nameof(SdkResult.ItemsToAdd) ? Secret : "recorded", null),
            },
            environmentVariablesToAdd: new Dictionary<string, string>
            {
                ["Variable"] = changedField == nameof(SdkResult.EnvironmentVariablesToAdd) ? Secret : "recorded",
            });

    private static ProjectInstanceSnapshotValidationContext CreateContext(
        ISdkResolverService resolver,
        MockLoggingService? loggingService = null) =>
        new(resolver, loggingService ?? new MockLoggingService(), s_buildEventContext, submissionId: 7);

    private static SdkResult ResolveForFreshEvaluation(
        ProjectInstanceSnapshotValidationContext context,
        SdkDependency dependency,
        MockLoggingService? loggingService = null) =>
        context.GetResolverForFreshEvaluation().ResolveSdk(
            7,
            dependency.Reference,
            new EvaluationLoggingContext(loggingService ?? new MockLoggingService(), s_buildEventContext, dependency.Context.ProjectPath),
            dependency.Context.ReferenceLocation,
            dependency.Context.SolutionPath!,
            dependency.Context.ProjectPath,
            dependency.Context.Interactive,
            dependency.Context.IsRunningInVisualStudio,
            dependency.Context.FailOnUnresolvedSdk);

    private sealed class ThrowingFileDependencies(Exception exception)
        : Dictionary<string, FileDependency>, IReadOnlyDictionary<string, FileDependency>
    {
        IEnumerator<KeyValuePair<string, FileDependency>> IEnumerable<KeyValuePair<string, FileDependency>>.GetEnumerator() =>
            throw exception;

        IEnumerator IEnumerable.GetEnumerator() => throw exception;
    }

    private sealed class RecordingSdkResolverService(Func<LoggingContext, SdkResult> resolve) : ISdkResolverService
    {
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
            return resolve(loggingContext);
        }
    }
}
