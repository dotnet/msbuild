// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Microsoft.Build.Construction;
using Microsoft.Build.Framework;
using Microsoft.Build.ObjectModelRemoting;
using Microsoft.Build.Shared.FileSystem;
using SdkResult = Microsoft.Build.BackEnd.SdkResolution.SdkResult;
using SdkResolverCacheIdentity = Microsoft.Build.BackEnd.SdkResolution.SdkResolverCacheIdentity;

#nullable disable

namespace Microsoft.Build.Evaluation.Context
{
    internal sealed class EvaluationObservationSession : IEvaluationInputObserver
    {
        private const string ObservationEnvironmentVariable = "MSBUILDPROTOTYPEEVALUATIONOBSERVATION";
        private const int ObservationSchemaVersion = 17;
        private const int PropertyFunctionClassificationVersion = 1;
#if NET
        private const int SupportedEnumerationOptionsPropertyCount = 8;
#endif

        [ThreadStatic]
        private static EvaluationObservationSession s_current;

        private static readonly bool s_enabled =
            Environment.GetEnvironmentVariable(ObservationEnvironmentVariable) == "1";
#if NET
        private static readonly int s_enumerationOptionsPropertyCount =
            typeof(EnumerationOptions).GetProperties().Length;
        private static readonly bool s_enumerationOptionsShapeSupported =
            s_enumerationOptionsPropertyCount == SupportedEnumerationOptionsPropertyCount;
#endif
        private static readonly ConditionalWeakTable<ProjectRootElement, ProjectSourceHashCache> s_projectSourceHashes = new();
        private static readonly string s_defaultFileSystemProvider =
            FileSystems.Default.GetType().AssemblyQualifiedName;
        private static readonly HashSet<string> s_knownPureIntrinsicMembers = new(StringComparer.OrdinalIgnoreCase)
        {
            "Add",
            "Subtract",
            "Multiply",
            "Divide",
            "Modulo",
            "Escape",
            "Unescape",
            "BitwiseOr",
            "BitwiseAnd",
            "BitwiseXor",
            "BitwiseNot",
            "LeftShift",
            "RightShift",
            "RightShiftUnsigned",
            "ValueOrDefault",
            "ConvertToBase64",
            "ConvertFromBase64",
            "StableStringHash",
            "EnsureTrailingSlash",
            "VersionEquals",
            "VersionNotEquals",
            "VersionGreaterThan",
            "VersionGreaterThanOrEquals",
            "VersionLessThan",
            "VersionLessThanOrEquals",
            "GetTargetFrameworkIdentifier",
            "GetTargetFrameworkVersion",
            "IsTargetFrameworkCompatible",
            "GetTargetPlatformIdentifier",
            "GetTargetPlatformVersion",
            "FilterTargetFrameworks",
            "SubstringByAsciiChars",
        };
        private static readonly HashSet<string> s_knownPurePathMembers = new(StringComparer.OrdinalIgnoreCase)
        {
            "ChangeExtension",
            "Combine",
            "EndsInDirectorySeparator",
            "GetDirectoryName",
            "GetExtension",
            "GetFileName",
            "GetFileNameWithoutExtension",
            "GetInvalidFileNameChars",
            "GetInvalidPathChars",
            "GetPathRoot",
            "HasExtension",
            "IsPathFullyQualified",
            "IsPathRooted",
            "Join",
            "TrimEndingDirectorySeparator",
        };
        private static readonly HashSet<string> s_fileMetadataMembers = new(StringComparer.OrdinalIgnoreCase)
        {
            "GetAttributes",
            "GetCreationTime",
            "GetCreationTimeUtc",
            "GetLastAccessTime",
            "GetLastAccessTimeUtc",
            "GetLastWriteTime",
            "GetLastWriteTimeUtc",
        };
        private static readonly HashSet<string> s_directoryMetadataMembers = new(StringComparer.OrdinalIgnoreCase)
        {
            "GetLastAccessTime",
            "GetLastAccessTimeUtc",
            "GetLastWriteTime",
            "GetLastWriteTimeUtc",
        };
        private static readonly HashSet<string> s_fileSystemInfoMetadataMembers = new(StringComparer.OrdinalIgnoreCase)
        {
            "Attributes",
            "CreationTime",
            "CreationTimeUtc",
            "Exists",
            "LastAccessTime",
            "LastAccessTimeUtc",
            "LastWriteTime",
            "LastWriteTimeUtc",
            "Length",
            "LinkTarget",
        };
        private static readonly HashSet<string> s_fileSystemInfoPathMembers = new(StringComparer.OrdinalIgnoreCase)
        {
            "Directory",
            "DirectoryName",
            "Extension",
            "FullName",
            "Name",
            "Parent",
            "Root",
        };

        private static readonly object s_testLock = new();
        private static TestConfiguration s_testConfiguration;

        private readonly int _evaluationId;
        private readonly string _projectPath;
        private readonly bool _allPropertyFunctionsEnabled;
        private readonly bool _retainDetails;
        private Dictionary<PathProbeKey, EvaluationPathProbeObservation> _pathProbes = new();
        private Dictionary<EnumerationKey, EvaluationDirectoryEnumerationObservation> _directoryEnumerations = new();
        private Dictionary<MetadataKey, EvaluationMetadataObservation> _metadataReads = new();
        private Dictionary<FileReadKey, EvaluationFileReadObservation> _fileReads = new();
        private EvaluationRequestObservation _request;
        private Dictionary<string, EvaluationProjectSourceObservation> _projectSources = new(FileUtilities.PathComparer);
        private Dictionary<string, EvaluationGlobObservation> _globs = new(StringComparer.Ordinal);
        private Dictionary<string, EvaluationSearchObservation> _searches = new(StringComparer.Ordinal);
        private Dictionary<EnvironmentKey, EvaluationEnvironmentObservation> _environment = new();
        private Dictionary<string, EvaluationExternalInputObservation> _externalInputs = new(StringComparer.Ordinal);
        private Dictionary<string, EvaluationPropertyFunctionObservation> _propertyFunctions = new(StringComparer.Ordinal);
        private List<EvaluationSdkResolutionObservation> _sdkResolutions = [];
        private Dictionary<string, EvaluationTaskRegistrationObservation> _taskRegistrations = new(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, EvaluationSideEffectObservation> _sideEffects = new(StringComparer.Ordinal);
        private List<EvaluationOperationFailureObservation> _operationFailures = [];
        private readonly object _observationLock = new();

        private long _reasons;
        private long _observedCategories;
        private long _incompleteCategories;
        private long _unsupportedCategories;
        private int _completed;
        private int _propertyFunctionInvocationId;
        private int _incompleteEnumerationIdentity;
        private int _suppressDirectoryEnumerations;
        private TestConfiguration _testConfiguration;

        private EvaluationObservationSession(
            int evaluationId,
            string projectPath,
            ProjectEvaluationStage evaluationStage,
            EvaluationContext.SharingPolicy sharingPolicy,
            bool hasDirectoryCache,
            TestConfiguration testConfiguration)
        {
            _evaluationId = evaluationId;
            _reasons = 0;
            _projectPath = NormalizePath(projectPath);
            _testConfiguration = testConfiguration;
            _allPropertyFunctionsEnabled = FeatureSwitches.EnableAllPropertyFunctions;
            _retainDetails = testConfiguration?.RetainDetails ?? false;
            MarkCategory(EvaluationObservationCategory.Request, EvaluationObservationCategoryState.Observed);

            if (_allPropertyFunctionsEnabled)
            {
                AddReason(EvaluationObservationReason.AllPropertyFunctionsEnabled);
                MarkCategory(EvaluationObservationCategory.PropertyFunction, EvaluationObservationCategoryState.Unsupported);
            }

            if (sharingPolicy == EvaluationContext.SharingPolicy.Shared)
            {
                AddReason(EvaluationObservationReason.UnversionedSharedCache);
                MarkCategory(EvaluationObservationCategory.SharedCache, EvaluationObservationCategoryState.Incomplete);
            }

            if (evaluationStage != ProjectEvaluationStage.Full)
            {
                AddReason(EvaluationObservationReason.IncompleteEvaluationStage);
            }

            if (Traits.Instance.CacheFileExistence)
            {
                AddReason(EvaluationObservationReason.UnversionedFileExistenceCache);
                MarkCategory(EvaluationObservationCategory.SharedCache, EvaluationObservationCategoryState.Incomplete);
            }

            if (Traits.Instance.MSBuildCacheFileEnumerations)
            {
                AddReason(EvaluationObservationReason.UnversionedGlobCache);
                MarkCategory(EvaluationObservationCategory.SharedCache, EvaluationObservationCategoryState.Incomplete);
            }

            if (hasDirectoryCache)
            {
                AddReason(EvaluationObservationReason.UnversionedDirectoryCache);
                MarkCategory(EvaluationObservationCategory.CustomProvider, EvaluationObservationCategoryState.Incomplete);
            }
        }

        internal static EvaluationObservationSession TryCreate(
            int evaluationId,
            string projectPath,
            ProjectEvaluationStage evaluationStage,
            EvaluationContext.SharingPolicy sharingPolicy,
            bool hasDirectoryCache)
        {
            TestConfiguration testConfiguration = Volatile.Read(ref s_testConfiguration);
            bool enabled = testConfiguration?.Enabled ?? s_enabled;

            return enabled
                ? new EvaluationObservationSession(
                    evaluationId,
                    projectPath,
                    evaluationStage,
                    sharingPolicy,
                    hasDirectoryCache,
                    testConfiguration)
                : null;
        }

        internal static void ReportProjectLoadFailure(
            int evaluationId,
            string projectPath,
            ProjectEvaluationStage evaluationStage,
            EvaluationContext.SharingPolicy sharingPolicy,
            EvaluationProjectSourceLoadCapture sourceLoadCapture)
        {
            try
            {
                if (sourceLoadCapture?.Failure is null)
                {
                    return;
                }

                EvaluationObservationSession session = TryCreate(
                    evaluationId,
                    projectPath,
                    evaluationStage,
                    sharingPolicy,
                    hasDirectoryCache: false);
                if (session is null)
                {
                    return;
                }

                session.MarkCategory(
                    EvaluationObservationCategory.Request,
                    EvaluationObservationCategoryState.Incomplete);
                session.RecordProjectSourceFailure(projectPath, sourceLoadCapture);
                session.Complete(evaluationSucceeded: false);
            }
            catch (Exception ex) when (!ExceptionHandling.IsCriticalException(ex))
            {
                // Preserve the project-load exception.
            }
        }

        internal static EvaluationObservationSession CreateForTests(int evaluationId = 1)
        {
            return new EvaluationObservationSession(
                evaluationId,
                projectPath: null,
                ProjectEvaluationStage.Full,
                EvaluationContext.SharingPolicy.Isolated,
                hasDirectoryCache: false,
                testConfiguration: new TestConfiguration(
                    enabled: true,
                    reportCreated: null,
                    retainDetails: true));
        }

        internal static EvaluationObservationSession Current => s_current;
        internal bool ShouldRecordDirectoryEnumeration => Volatile.Read(ref _suppressDirectoryEnumerations) == 0;
        bool IEvaluationInputObserver.RetainDetails => _retainDetails;

        internal static bool IsEnabled
        {
            get
            {
                TestConfiguration testConfiguration = Volatile.Read(ref s_testConfiguration);
                return testConfiguration?.Enabled ?? s_enabled;
            }
        }

        internal IDisposable Enter()
        {
            EvaluationObservationSession previous = s_current;
            s_current = this;
            return new CurrentScope(previous, EvaluationInputObserver.Enter(this));
        }

        internal static DirectoryEnumerationSuppressionScope SuppressDirectoryEnumerations()
        {
            EvaluationObservationSession session = s_current;
            if (session is not null)
            {
                Interlocked.Increment(ref session._suppressDirectoryEnumerations);
            }

            return new DirectoryEnumerationSuppressionScope(session);
        }

        void IEvaluationInputObserver.RecordPathProbe(
            string path,
            EvaluationPathProbeKind kind,
            bool exists)
        {
            RecordProbe(path, ConvertPathKind(kind), exists);
        }

        void IEvaluationInputObserver.RecordAmbiguousPathProbe(
            string path,
            EvaluationPathProbeKind kind)
        {
            RecordProbe(path, ConvertPathKind(kind), exists: false);
            AddReason(EvaluationObservationReason.AmbiguousNegativeProbe);
        }

        void IEvaluationInputObserver.RecordItemMetadata(
            string itemSpec,
            string modifier,
            string baseDirectory,
            string value)
        {
            RecordItemMetadata(itemSpec, modifier, baseDirectory, value);
        }

        void IEvaluationInputObserver.RecordPathAdjustment(
            string value,
            string baseDirectory,
            string result)
        {
            RecordExternalInput(
                EvaluationExternalInputKind.Ambient,
                "UnixPathAdjustment",
                string.Concat(value, "|Base=", baseDirectory),
                result);
        }

        void IEvaluationInputObserver.RecordPathResolution(
            string operation,
            string firstInput,
            string secondInput,
            string firstResult,
            string secondResult)
        {
            RecordExternalInput(
                EvaluationExternalInputKind.Ambient,
                string.Concat("MSBuild::", operation, ".PathResolution"),
                string.Concat("First=", firstInput, "\0Second=", secondInput),
                string.Concat("First=", firstResult, "\0Second=", secondResult));
        }

        void IEvaluationInputObserver.RecordSearch(
            string kind,
            string request,
            IReadOnlyList<string> candidates,
            int candidateCount,
            string candidatesFingerprint,
            string selected)
        {
            string[] selectedPaths = string.IsNullOrEmpty(selected) ? [] : [selected];
            RecordSearch(
                kind,
                request,
                _retainDetails ? CopyStrings(candidates) : [],
                candidateCount,
                candidatesFingerprint,
                selectedPaths,
                selectedPaths.Length,
                ComputeStringSequenceHash(selectedPaths),
                complete: true);
        }

        internal static IDisposable TestOnlyConfigure(
            bool enabled,
            Action<EvaluationObservationReport> reportCreated = null,
            bool retainDetails = true)
        {
            var configuration = new TestConfiguration(enabled, reportCreated, retainDetails);
            lock (s_testLock)
            {
                Assumed.Null(s_testConfiguration, "A test observation scope is already active.");
                Volatile.Write(ref s_testConfiguration, configuration);
            }

            return new TestScope(configuration);
        }

        internal bool IsCompleted => Volatile.Read(ref _completed) != 0;
        internal bool RetainDetails => _retainDetails;

        internal int TestOnlyRetainedObservationCount
        {
            get
            {
                lock (_observationLock)
                {
                    return (_pathProbes?.Count ?? 0) +
                        (_directoryEnumerations?.Count ?? 0) +
                        (_metadataReads?.Count ?? 0) +
                        (_fileReads?.Count ?? 0) +
                        (_request is null ? 0 : 1) +
                        (_projectSources?.Count ?? 0) +
                        (_globs?.Count ?? 0) +
                        (_searches?.Count ?? 0) +
                        (_environment?.Count ?? 0) +
                        (_externalInputs?.Count ?? 0) +
                        (_propertyFunctions?.Count ?? 0) +
                        (_sdkResolutions?.Count ?? 0) +
                        (_taskRegistrations?.Count ?? 0) +
                        (_sideEffects?.Count ?? 0) +
                        (_operationFailures?.Count ?? 0);
                }
            }
        }

        internal EvaluationObservationReason TestOnlyReasons =>
            (EvaluationObservationReason)Volatile.Read(ref _reasons);

        internal bool TestOnlyObservationCollectionsDetached
        {
            get
            {
                lock (_observationLock)
                {
                    return _pathProbes is null &&
                        _directoryEnumerations is null &&
                        _metadataReads is null &&
                        _fileReads is null &&
                        _request is null &&
                        _projectSources is null &&
                        _globs is null &&
                        _searches is null &&
                        _environment is null &&
                        _externalInputs is null &&
                        _propertyFunctions is null &&
                        _sdkResolutions is null &&
                        _taskRegistrations is null &&
                        _sideEffects is null &&
                        _operationFailures is null;
                }
            }
        }

        internal void RecordRequest(EvaluationRequestObservation request)
        {
            if (request is null)
            {
                return;
            }

            lock (_observationLock)
            {
                if (IsCompleted)
                {
                    return;
                }

                if (_request is not null)
                {
                    AddReason(EvaluationObservationReason.ConflictingObservation);
                    return;
                }

                _request = request;
            }
        }

        internal void RecordProjectSource(ProjectRootElement source, EvaluationProjectSourceRole role)
        {
            if (source is null)
            {
                return;
            }

            MarkCategory(EvaluationObservationCategory.ProjectSource, EvaluationObservationCategoryState.Observed);
            Record(
                () =>
                {
                    ProjectRootElementLink link = source.RootLink;
                    string path = source.FullPath is null
                        ? string.Concat(
                            "inmemory://",
                            RuntimeHelpers.GetHashCode(source).ToString("x", CultureInfo.InvariantCulture))
                        : NormalizePath(source.FullPath);
                    string provider = link is not null
                        ? link.GetType().AssemblyQualifiedName
                        : source.EvaluationObservationSourceKind;
                    string sourceHash = source.EvaluationObservationSourceHash;
                    DateTime? lastWriteTimeUtc =
                        source.EvaluationObservationSourceKind == "Disk"
                            ? source.EvaluationObservationLastWriteTimeUtc
                            : null;
                    bool timestampWasStableDuringRead =
                        source.EvaluationObservationSourceTimestampStable;
                    string hash;
                    if (link is not null)
                    {
                        hash = null;
                    }
                    else
                    {
                        try
                        {
                            hash = sourceHash ?? GetProjectSourceHash(source);
                        }
                        catch (Exception ex) when (!ExceptionHandling.IsCriticalException(ex))
                        {
                            AddReason(EvaluationObservationReason.ProjectXmlContentNotObserved);
                            hash = null;
                        }
                    }

                    var observation = new EvaluationProjectSourceObservation(
                        role,
                        EvaluationProjectSourceOutcome.Parsed,
                        path,
                        source.Version,
                        hash,
                        link is not null
                            ? EvaluationContentHashKind.Unknown
                            : sourceHash is not null
                            ? EvaluationContentHashKind.RawBytes
                            : EvaluationContentHashKind.ParsedXml,
                        source.Encoding?.WebName,
                        provider,
                        lastWriteTimeUtc.HasValue,
                        lastWriteTimeUtc?.Ticks ?? 0,
                        timestampWasStableDuringRead);
                    string key = string.Concat(((int)role).ToString(CultureInfo.InvariantCulture), "\0", path ?? source.GetHashCode().ToString(CultureInfo.InvariantCulture));

                    bool hadPriorObservation = _projectSources.TryGetValue(
                        key,
                        out EvaluationProjectSourceObservation prior);
                    if (hadPriorObservation &&
                        (prior.Outcome != observation.Outcome ||
                         prior.Version != observation.Version ||
                         !string.Equals(prior.ContentHash, observation.ContentHash, StringComparison.Ordinal) ||
                         prior.HasLastWriteTimeUtc != observation.HasLastWriteTimeUtc ||
                         prior.LastWriteTimeUtcTicks != observation.LastWriteTimeUtcTicks ||
                         prior.TimestampWasStableDuringRead != observation.TimestampWasStableDuringRead))
                    {
                        AddReason(EvaluationObservationReason.ConflictingObservation);
                    }
                    else
                    {
                        _projectSources[key] = observation;
                    }

                    if (!timestampWasStableDuringRead)
                    {
                        AddReason(EvaluationObservationReason.ProjectSourceChangedDuringRead);
                    }

                    if (source.FullPath is not null && hash is not null)
                    {
                        bool hasRawSourceHash = sourceHash is not null;
                        RecordFileRead(
                            path,
                            hash,
                            isVerifiable: hasRawSourceHash,
                            hashKind: hasRawSourceHash
                                ? EvaluationContentHashKind.RawBytes
                                : EvaluationContentHashKind.ParsedXml,
                            provider: provider);
                    }

                    if (link is null &&
                        source.FullPath is not null &&
                        sourceHash is null)
                    {
                        AddReason(EvaluationObservationReason.ParsedProjectSourceOnly);
                        AddReason(EvaluationObservationReason.UnversionedProjectRootElementCache);
                    }

                    if (source.EvaluationObservationSourceKind is "XmlReader" or "Document" or "Unknown")
                    {
                        AddReason(EvaluationObservationReason.UnversionedSourceProvider);
                    }
                });
        }

        internal void RecordProjectSourceFailure(
            string path,
            EvaluationProjectSourceLoadCapture sourceLoadCapture)
        {
            try
            {
                RecordProjectSourceFailureCore(path, sourceLoadCapture);
            }
            catch (Exception ex) when (!ExceptionHandling.IsCriticalException(ex))
            {
                AddReason(EvaluationObservationReason.ObservationIncomplete);
            }
        }

        private void RecordProjectSourceFailureCore(
            string path,
            EvaluationProjectSourceLoadCapture sourceLoadCapture)
        {
            if (string.IsNullOrEmpty(path) || sourceLoadCapture is null)
            {
                return;
            }

            const string provider = "Disk";
            string normalizedPath = NormalizePath(path);
            EvaluationProjectSourceRole role =
                FileUtilities.PathComparer.Equals(_projectPath, normalizedPath)
                    ? EvaluationProjectSourceRole.Root
                    : EvaluationProjectSourceRole.Import;
            EvaluationProjectSourceOutcome outcome =
                sourceLoadCapture.Outcome == EvaluationProjectSourceOutcome.Parsed
                    ? EvaluationProjectSourceOutcome.ParseFailure
                    : sourceLoadCapture.Outcome;
            var observation = new EvaluationProjectSourceObservation(
                role,
                outcome,
                normalizedPath,
                0,
                sourceLoadCapture.ContentHash,
                sourceLoadCapture.ContentHash is null
                    ? EvaluationContentHashKind.Unknown
                    : EvaluationContentHashKind.RawBytes,
                sourceLoadCapture.Encoding,
                provider,
                sourceLoadCapture.HasLastWriteTimeUtc,
                sourceLoadCapture.LastWriteTimeUtcTicks,
                sourceLoadCapture.TimestampWasStableDuringRead);
            string key = string.Concat(
                ((int)role).ToString(CultureInfo.InvariantCulture),
                "\0",
                normalizedPath);

            MarkCategory(
                EvaluationObservationCategory.ProjectSource,
                EvaluationObservationCategoryState.Incomplete);
            Record(
                () =>
                {
                    if (_projectSources.TryGetValue(
                            key,
                            out EvaluationProjectSourceObservation prior) &&
                        (prior.Outcome != observation.Outcome ||
                         prior.Version != observation.Version ||
                         !string.Equals(prior.ContentHash, observation.ContentHash, StringComparison.Ordinal) ||
                         prior.HasLastWriteTimeUtc != observation.HasLastWriteTimeUtc ||
                         prior.LastWriteTimeUtcTicks != observation.LastWriteTimeUtcTicks ||
                         prior.TimestampWasStableDuringRead != observation.TimestampWasStableDuringRead))
                    {
                        AddReason(EvaluationObservationReason.ConflictingObservation);
                    }
                    else
                    {
                        _projectSources[key] = observation;
                    }

                    if (!observation.TimestampWasStableDuringRead)
                    {
                        AddReason(EvaluationObservationReason.ProjectSourceChangedDuringRead);
                    }
                });

            if (observation.ContentHash is not null)
            {
                RecordFileRead(
                    normalizedPath,
                    observation.ContentHash,
                    isVerifiable: true,
                    hashKind: EvaluationContentHashKind.RawBytes,
                    provider: provider);
            }
            else
            {
                MarkCategory(
                    EvaluationObservationCategory.FileContent,
                    EvaluationObservationCategoryState.Incomplete);
                AddReason(EvaluationObservationReason.ProjectXmlContentNotObserved);
            }

            if (sourceLoadCapture.ContentCaptureFailed)
            {
                AddReason(EvaluationObservationReason.ObservationIncomplete);
            }

            RecordOperationFailure(
                EvaluationObservationCategory.ProjectSource,
                outcome == EvaluationProjectSourceOutcome.LoadFailure
                    ? "ProjectSource.Load"
                    : "ProjectSource.Parse",
                normalizedPath,
                provider,
                sourceLoadCapture.Failure);
        }

        internal void RecordGlob(
            string role,
            string directory,
            string include,
            IReadOnlyList<string> excludes,
            IReadOnlyList<string> results,
            bool resultsEscaped,
            bool wasLazy,
            bool driveEnumerating,
            string failure)
        {
            MarkCategory(
                EvaluationObservationCategory.Glob,
                failure is null
                    ? EvaluationObservationCategoryState.Observed
                    : EvaluationObservationCategoryState.Incomplete);
            Record(
                () =>
                {
                    string[] excludeSnapshot = _retainDetails ? CopyStrings(excludes) : [];
                    int excludeCount = excludes?.Count ?? 0;
                    string excludesFingerprint = ComputeStringSequenceHash(excludes);
                    string[] resultSnapshot = _retainDetails ? CopyStrings(results) : [];
                    int resultCount = results?.Count ?? 0;
                    string resultsFingerprint = ComputeStringSequenceHash(results);
                    string normalizedDirectory = NormalizePath(directory);
                    var observation = new EvaluationGlobObservation(
                        role,
                        normalizedDirectory,
                        include,
                        excludeSnapshot,
                        excludeCount,
                        excludesFingerprint,
                        resultSnapshot,
                        resultCount,
                        resultsFingerprint,
                        resultsEscaped,
                        wasLazy,
                        driveEnumerating,
                        failure);
                    string key = string.Concat(
                        role,
                        "\0",
                        normalizedDirectory,
                        "\0",
                        include,
                        "\0",
                        excludesFingerprint);

                    if (_globs.TryGetValue(key, out EvaluationGlobObservation prior) &&
                        !GlobResultsEqual(prior, observation))
                    {
                        AddReason(EvaluationObservationReason.ConflictingObservation);
                    }
                    else
                    {
                        _globs[key] = observation;
                    }

                    if (failure is not null)
                    {
                        AddReason(EvaluationObservationReason.ExternalOperationFailure);
                    }
                });
        }

        internal void RecordSearch(
            string kind,
            string request,
            IReadOnlyList<string> candidates,
            string selected,
            bool complete)
        {
            RecordSearch(
                kind,
                request,
                candidates,
                string.IsNullOrEmpty(selected) ? [] : [selected],
                complete);
        }

        internal void RecordSearch(
            string kind,
            string request,
            IReadOnlyList<string> candidates,
            IReadOnlyList<string> selectedPaths,
            bool complete)
        {
            RecordSearch(
                kind,
                request,
                _retainDetails ? CopyStrings(candidates) : [],
                candidates?.Count ?? 0,
                ComputeStringSequenceHash(candidates),
                CopyStrings(selectedPaths),
                selectedPaths?.Count ?? 0,
                ComputeStringSequenceHash(selectedPaths),
                complete);
        }

        private void RecordSearch(
            string kind,
            string request,
            string[] candidates,
            int candidateCount,
            string candidatesFingerprint,
            string[] selectedPaths,
            int selectedPathCount,
            string selectedPathsFingerprint,
            bool complete)
        {
            MarkCategory(
                EvaluationObservationCategory.Search,
                complete
                    ? EvaluationObservationCategoryState.Observed
                    : EvaluationObservationCategoryState.Incomplete);
            Record(
                () =>
                {
                    var observation = new EvaluationSearchObservation(
                        kind,
                        request,
                        candidates,
                        candidateCount,
                        candidatesFingerprint,
                        selectedPaths,
                        selectedPathCount,
                        selectedPathsFingerprint,
                        complete);
                    string key = string.Concat(kind, "\0", request);

                    if (_searches.TryGetValue(key, out EvaluationSearchObservation prior) &&
                        (prior.CandidateCount != candidateCount ||
                         prior.SelectedPathCount != selectedPathCount ||
                         !string.Equals(prior.CandidatesFingerprint, candidatesFingerprint, StringComparison.Ordinal) ||
                         !string.Equals(prior.SelectedPathsFingerprint, selectedPathsFingerprint, StringComparison.Ordinal)))
                    {
                        AddReason(EvaluationObservationReason.ConflictingObservation);
                    }
                    else
                    {
                        _searches[key] = observation;
                    }

                    if (!complete)
                    {
                        AddReason(EvaluationObservationReason.OpaqueExternalInput);
                    }
                });
        }

        internal void RecordEnvironment(
            string name,
            EvaluationEnvironmentSource source,
            bool present,
            string value)
        {
            if (string.IsNullOrEmpty(name))
            {
                return;
            }

            MarkCategory(
                source is EvaluationEnvironmentSource.Imported or
                    EvaluationEnvironmentSource.MissingImported or
                    EvaluationEnvironmentSource.SdkInjected
                    ? EvaluationObservationCategory.ImportedEnvironment
                    : EvaluationObservationCategory.LiveEnvironment,
                EvaluationObservationCategoryState.Observed);
            Record(
                () =>
                {
                    var key = new EnvironmentKey(source, name);
                    var observation = new EvaluationEnvironmentObservation(name, source, present, value);
                    if (_environment.TryGetValue(key, out EvaluationEnvironmentObservation prior) &&
                        (prior.Present != present || !string.Equals(prior.Value, value, StringComparison.Ordinal)))
                    {
                        AddReason(EvaluationObservationReason.ConflictingObservation);
                    }
                    else
                    {
                        _environment[key] = observation;
                    }
                });
        }

        internal void RecordExternalInput(
            EvaluationExternalInputKind kind,
            string operation,
            string request,
            object result)
        {
            RecordExternalInputCore(kind, operation, request, SerializeValue(result));
        }

        private void RecordExternalInputCore(
            EvaluationExternalInputKind kind,
            string operation,
            string request,
            string serializedResult)
        {
            MarkCategory(
                kind switch
                {
                    EvaluationExternalInputKind.Registry => EvaluationObservationCategory.Registry,
                    EvaluationExternalInputKind.Toolset => EvaluationObservationCategory.Toolset,
                    EvaluationExternalInputKind.Sdk => EvaluationObservationCategory.SdkResolution,
                    EvaluationExternalInputKind.Search => EvaluationObservationCategory.Search,
                    EvaluationExternalInputKind.Environment => EvaluationObservationCategory.LiveEnvironment,
                    _ => EvaluationObservationCategory.Request,
                },
                EvaluationObservationCategoryState.Observed);
            Record(
                () =>
                {
                    string key = string.Concat(((int)kind).ToString(CultureInfo.InvariantCulture), "\0", operation, "\0", request);
                    var observation = new EvaluationExternalInputObservation(kind, operation, request, serializedResult);
                    if (_externalInputs.TryGetValue(key, out EvaluationExternalInputObservation prior) &&
                        !string.Equals(prior.Result, serializedResult, StringComparison.Ordinal))
                    {
                        AddReason(EvaluationObservationReason.ConflictingObservation);
                    }
                    else
                    {
                        _externalInputs[key] = observation;
                    }
                });
        }

        internal void RecordItemMetadata(
            string itemSpec,
            string metadataName,
            string baseDirectory,
            string value)
        {
            if (metadataName is "FullPath" or "RootDir" or "RelativeDir" or "Directory")
            {
                RecordExternalInputCore(
                    EvaluationExternalInputKind.Ambient,
                    string.Concat("ItemMetadata::", metadataName),
                    string.Concat("ItemSpec=", itemSpec, "\0Base=", baseDirectory),
                    value);
                return;
            }

            EvaluationMetadataKind kind = metadataName switch
            {
                "ModifiedTime" => EvaluationMetadataKind.ItemModifiedTime,
                "CreatedTime" => EvaluationMetadataKind.ItemCreatedTime,
                "AccessedTime" => EvaluationMetadataKind.ItemAccessedTime,
                _ => EvaluationMetadataKind.PropertyFunction,
            };

            RecordMetadata(itemSpec, kind, value, baseDirectory, metadataName);
        }

        internal void RecordPropertyFunction(
            Type receiverType,
            string member,
            object instance,
            object[] arguments,
            object result,
            bool succeeded = true,
            string pathBaseDirectory = null,
            object[] usageArguments = null)
        {
            try
            {
                RecordPropertyFunctionCore(
                    receiverType,
                    member,
                    instance,
                    arguments,
                    result,
                    succeeded,
                    pathBaseDirectory,
                    usageArguments);
            }
            catch (Exception ex) when (!ExceptionHandling.IsCriticalException(ex))
            {
                AddReason(EvaluationObservationReason.ObservationIncomplete);
            }
        }

        private void RecordPropertyFunctionCore(
            Type receiverType,
            string member,
            object instance,
            object[] arguments,
            object result,
            bool succeeded,
            string pathBaseDirectory,
            object[] usageArguments)
        {
            EvaluationPropertyFunctionEffect effects = ClassifyPropertyFunction(receiverType, member);
            if (succeeded && effects == EvaluationPropertyFunctionEffect.Pure)
            {
                return;
            }

            string[] serializedArguments = SerializeArguments(usageArguments ?? arguments);
            string serializedResult =
                !succeeded
                    ? "<failed>"
                    : (effects & EvaluationPropertyFunctionEffect.FileContent) != 0 &&
                        (effects & EvaluationPropertyFunctionEffect.SideEffect) == 0
                    ? "<file-content>"
                    : (effects & EvaluationPropertyFunctionEffect.DirectoryEnumeration) != 0
                        ? "<directory-enumeration>"
                        : SerializeValue(result);
            string receiverName = receiverType?.FullName ?? instance?.GetType().FullName ?? "<unknown>";
            string instanceIdentity = instance is FileSystemInfo fileSystemInfo
                ? fileSystemInfo.FullName
                : SerializeValue(instance);

            if (result is IEnumerable and not string and not ICollection)
            {
                effects |= EvaluationPropertyFunctionEffect.OpaqueUnsupported;
            }

            MarkCategory(
                EvaluationObservationCategory.PropertyFunction,
                (effects & EvaluationPropertyFunctionEffect.OpaqueUnsupported) != 0
                    ? EvaluationObservationCategoryState.Unsupported
                    : EvaluationObservationCategoryState.Observed);

            Record(
                () =>
                {
                    bool uniqueInvocation =
                        (effects & (EvaluationPropertyFunctionEffect.Volatile | EvaluationPropertyFunctionEffect.SideEffect)) != 0;
                    string key = string.Concat(
                        receiverName,
                        "\0",
                        member,
                        "\0",
                        instanceIdentity,
                        "\0",
                        string.Join("\0", serializedArguments),
                        "\0",
                        succeeded ? "success" : "failure",
                        uniqueInvocation
                            ? string.Concat("\0", Interlocked.Increment(ref _propertyFunctionInvocationId).ToString(CultureInfo.InvariantCulture))
                            : string.Empty);
                    var observation = new EvaluationPropertyFunctionObservation(
                        receiverName,
                        member,
                        instanceIdentity,
                        effects,
                        serializedArguments,
                        serializedResult,
                        succeeded);
                    if (_propertyFunctions.TryGetValue(key, out EvaluationPropertyFunctionObservation prior) &&
                        !string.Equals(prior.Result, serializedResult, StringComparison.Ordinal))
                    {
                        AddReason(EvaluationObservationReason.ConflictingObservation);
                    }
                    else
                    {
                        _propertyFunctions[key] = observation;
                    }

                    if ((effects & EvaluationPropertyFunctionEffect.Volatile) != 0)
                    {
                        AddReason(EvaluationObservationReason.UnsupportedVolatileInput);
                        MarkCategory(EvaluationObservationCategory.VolatileOrSideEffect, EvaluationObservationCategoryState.Unsupported);
                    }

                    if ((effects & EvaluationPropertyFunctionEffect.SideEffect) != 0)
                    {
                        AddReason(EvaluationObservationReason.EvaluationSideEffect);
                        MarkCategory(EvaluationObservationCategory.VolatileOrSideEffect, EvaluationObservationCategoryState.Unsupported);
                    }

                    if ((effects & EvaluationPropertyFunctionEffect.OpaqueUnsupported) != 0)
                    {
                        AddReason(EvaluationObservationReason.UnclassifiedPropertyFunction);
                    }
                });

            if (succeeded)
            {
                RecordTypedPropertyFunction(
                    receiverType,
                    member,
                    instance,
                    arguments,
                    result,
                    effects,
                    serializedArguments,
                    serializedResult,
                    pathBaseDirectory);
            }
        }

        internal void RecordSdkResolution(
            int submissionId,
            SdkReference sdk,
            SdkResult result,
            bool fromCache,
            SdkResolverCacheIdentity cacheIdentity,
            string projectPath,
            string solutionPath,
            bool interactive,
            bool isRunningInVisualStudio,
            bool failOnUnresolvedSdk,
            ElementLocation referenceLocation)
        {
            if (sdk is null)
            {
                return;
            }

            MarkCategory(EvaluationObservationCategory.SdkResolution, EvaluationObservationCategoryState.Observed);
            if (!cacheIdentity.CacheEnabled)
            {
                AddReason(EvaluationObservationReason.SdkResolutionWithoutCacheLifetime);
            }

            Record(
                () =>
                {
                    _sdkResolutions.Add(new EvaluationSdkResolutionObservation(
                        submissionId,
                        sdk.Name,
                        sdk.Version,
                        sdk.MinimumVersion,
                        projectPath,
                        solutionPath,
                        interactive,
                        isRunningInVisualStudio,
                        failOnUnresolvedSdk,
                        referenceLocation?.File,
                        referenceLocation?.Line ?? 0,
                        referenceLocation?.Column ?? 0,
                        cacheIdentity,
                        result?.Success ?? false,
                        result?.Path,
                        result?.Version,
                        fromCache,
                        CopyStrings(result?.AdditionalPaths),
                        CreateNamedValueSnapshot(result?.PropertiesToAdd, "SdkProperty"),
                        CreateSdkItemSnapshot(result?.ItemsToAdd),
                        CreateNamedValueSnapshot(result?.EnvironmentVariablesToAdd, "SdkEnvironment"),
                        CopyStrings(result?.Warnings),
                        CopyStrings(result?.Errors)));
                });
        }

        internal void RecordSdkRequest(
            int submissionId,
            SdkReference sdk,
            string projectPath,
            string solutionPath,
            bool interactive,
            bool isRunningInVisualStudio,
            bool failOnUnresolvedSdk,
            ElementLocation referenceLocation)
        {
            if (sdk is null)
            {
                return;
            }

            RecordExternalInputCore(
                EvaluationExternalInputKind.Sdk,
                "SdkRequest",
                string.Concat(
                    "Submission=", submissionId.ToString(CultureInfo.InvariantCulture),
                    "\0Name=", sdk.Name,
                    "\0Version=", sdk.Version,
                    "\0MinimumVersion=", sdk.MinimumVersion,
                    "\0Project=", projectPath,
                    "\0Solution=", solutionPath,
                    "\0Location=", referenceLocation?.File,
                    ":", (referenceLocation?.Line ?? 0).ToString(CultureInfo.InvariantCulture),
                    ":", (referenceLocation?.Column ?? 0).ToString(CultureInfo.InvariantCulture)),
                string.Concat(
                    "Interactive=", interactive.ToString(CultureInfo.InvariantCulture),
                    ";VisualStudio=", isRunningInVisualStudio.ToString(CultureInfo.InvariantCulture),
                    ";FailOnUnresolved=", failOnUnresolvedSdk.ToString(CultureInfo.InvariantCulture)));
        }

        internal void RecordTaskRegistration(
            string taskName,
            string taskFactory,
            string assemblyFile,
            string assemblyName,
            string runtime,
            string architecture,
            bool isOverride)
        {
            MarkCategory(EvaluationObservationCategory.TaskRegistration, EvaluationObservationCategoryState.Observed);
            Record(
                () =>
                {
                    var observation = new EvaluationTaskRegistrationObservation(
                        taskName,
                        taskFactory,
                        NormalizePath(assemblyFile),
                        assemblyName,
                        runtime,
                        architecture,
                        isOverride);
                    string key = string.Concat(taskName, "\0", taskFactory, "\0", observation.AssemblyFile, "\0", assemblyName);
                    _taskRegistrations[key] = observation;
                });
        }

        internal void RecordSideEffect(string kind, string identity, object value)
        {
            RecordSideEffectCore(kind, identity, SerializeValue(value));
        }

        private void RecordSideEffectCore(string kind, string identity, string serializedValue)
        {
            MarkCategory(EvaluationObservationCategory.VolatileOrSideEffect, EvaluationObservationCategoryState.Unsupported);
            Record(
                () =>
                {
                    string key = string.Concat(kind, "\0", identity);
                    _sideEffects[key] = new EvaluationSideEffectObservation(kind, identity, serializedValue);
                    AddReason(EvaluationObservationReason.EvaluationSideEffect);
                });
        }

        internal void RecordProbe(
            string path,
            EvaluationPathKind kind,
            bool exists,
            string provider = null,
            string baseDirectory = null)
        {
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            MarkCategory(EvaluationObservationCategory.PathProbe, EvaluationObservationCategoryState.Observed);
            try
            {
                lock (_observationLock)
                {
                    if (IsCompleted)
                    {
                        return;
                    }

                    var key = new PathProbeKey(
                        NormalizePath(path, baseDirectory),
                        kind,
                        provider ?? s_defaultFileSystemProvider);
                    var observation = new EvaluationPathProbeObservation(
                        key.Path,
                        key.Kind,
                        exists,
                        key.Provider);
                    if (_pathProbes.TryGetValue(key, out EvaluationPathProbeObservation priorObservation))
                    {
                        if (priorObservation.Exists != exists)
                        {
                            AddReason(EvaluationObservationReason.ConflictingObservation);
                        }
                    }
                    else
                    {
                        _pathProbes.Add(key, observation);
                    }
                }
            }
            catch (Exception ex) when (!ExceptionHandling.IsCriticalException(ex))
            {
                AddReason(EvaluationObservationReason.ObservationIncomplete);
            }
        }

        internal void RecordEnumeration(
            string path,
            string searchPattern,
            SearchOption searchOption,
            EvaluationEnumerationKind kind,
            IReadOnlyList<string> entries,
            EvaluationEnumerationCompletion completion,
            string provider = null,
            string optionsIdentity = null,
            string baseDirectory = null)
        {
            string[] retainedEntries = _retainDetails && entries is { Count: > 0 }
                ? new string[entries.Count]
                : [];
            var entriesHasher = new EvaluationInputFingerprintBuilder();
            int entryCount = entries?.Count ?? 0;
            for (int i = 0; i < entryCount; i++)
            {
                string normalizedEntry = NormalizePath(entries[i], baseDirectory);
                entriesHasher.Add(normalizedEntry);
                if (_retainDetails)
                {
                    retainedEntries[i] = normalizedEntry;
                }
            }

            RecordEnumerationCore(
                path,
                searchPattern,
                searchOption,
                kind,
                retainedEntries,
                entryCount,
                entriesHasher.Complete(),
                completion,
                provider,
                optionsIdentity,
                baseDirectory);
        }

        internal void RecordEnumeration(
            string path,
            string searchPattern,
            SearchOption searchOption,
            EvaluationEnumerationKind kind,
            string[] entries,
            int entryCount,
            string entriesHash,
            EvaluationEnumerationCompletion completion,
            string provider = null,
            string optionsIdentity = null,
            string baseDirectory = null)
        {
            RecordEnumerationCore(
                path,
                searchPattern,
                searchOption,
                kind,
                entries,
                entryCount,
                entriesHash,
                completion,
                provider,
                optionsIdentity,
                baseDirectory);
        }

        private void RecordEnumerationCore(
            string path,
            string searchPattern,
            SearchOption searchOption,
            EvaluationEnumerationKind kind,
            string[] entries,
            int entryCount,
            string entriesHash,
            EvaluationEnumerationCompletion completion,
            string provider,
            string optionsIdentity,
            string baseDirectory)
        {
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            MarkCategory(
                EvaluationObservationCategory.DirectoryEnumeration,
                completion == EvaluationEnumerationCompletion.Complete
                    ? EvaluationObservationCategoryState.Observed
                    : EvaluationObservationCategoryState.Incomplete);
            try
            {
                lock (_observationLock)
                {
                    if (IsCompleted)
                    {
                        return;
                    }

                    int incompleteIdentity = completion == EvaluationEnumerationCompletion.Complete
                        ? 0
                        : ++_incompleteEnumerationIdentity;
                    var key = new EnumerationKey(
                        NormalizePath(path, baseDirectory),
                        searchPattern ?? "*",
                        searchOption,
                        kind,
                        provider ?? s_defaultFileSystemProvider,
                        optionsIdentity,
                        incompleteIdentity);
                    var observation = new EvaluationDirectoryEnumerationObservation(
                        key.Path,
                        key.SearchPattern,
                        key.SearchOption,
                        key.Kind,
                        entries,
                        entryCount,
                        entriesHash,
                        key.Provider,
                        completion,
                        key.OptionsIdentity);

                    if (_directoryEnumerations.TryGetValue(key, out EvaluationDirectoryEnumerationObservation priorObservation))
                    {
                        if (!EnumerationResultsEqual(priorObservation, observation))
                        {
                            AddReason(EvaluationObservationReason.ConflictingObservation);
                        }
                    }
                    else
                    {
                        _directoryEnumerations.Add(key, observation);
                    }

                    if (completion != EvaluationEnumerationCompletion.Complete)
                    {
                        AddReason(completion == EvaluationEnumerationCompletion.Failure
                            ? EvaluationObservationReason.ExternalOperationFailure
                            : EvaluationObservationReason.PartialEnumeration);
                    }
                }
            }
            catch (Exception ex) when (!ExceptionHandling.IsCriticalException(ex))
            {
                AddReason(EvaluationObservationReason.ObservationIncomplete);
            }
        }

        internal void RecordMetadata(
            string path,
            EvaluationMetadataKind kind,
            long value,
            string provider = null,
            string baseDirectory = null)
        {
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            MarkCategory(EvaluationObservationCategory.FileMetadata, EvaluationObservationCategoryState.Observed);
            try
            {
                string normalizedPath = NormalizePath(path, baseDirectory);
                RecordMetadataCore(
                    normalizedPath,
                    kind,
                    new EvaluationMetadataObservation(
                        normalizedPath,
                        kind,
                        value,
                        provider ?? s_defaultFileSystemProvider));
            }
            catch (Exception ex) when (!ExceptionHandling.IsCriticalException(ex))
            {
                AddReason(EvaluationObservationReason.ObservationIncomplete);
            }
        }

        internal void RecordMetadata(
            string path,
            EvaluationMetadataKind kind,
            string value,
            string baseDirectory,
            string operation = null,
            string provider = null)
        {
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            MarkCategory(EvaluationObservationCategory.FileMetadata, EvaluationObservationCategoryState.Observed);
            try
            {
                string normalizedBaseDirectory = NormalizePath(baseDirectory);
                string normalizedPath = NormalizePath(path, normalizedBaseDirectory);
                RecordMetadataCore(
                    normalizedPath,
                    kind,
                    new EvaluationMetadataObservation(
                        normalizedPath,
                        kind,
                        value,
                        normalizedBaseDirectory,
                        operation,
                        provider ?? s_defaultFileSystemProvider));
            }
            catch (Exception ex) when (!ExceptionHandling.IsCriticalException(ex))
            {
                AddReason(EvaluationObservationReason.ObservationIncomplete);
            }
        }

        private void RecordMetadataCore(
            string path,
            EvaluationMetadataKind kind,
            EvaluationMetadataObservation observation)
        {
            try
            {
                lock (_observationLock)
                {
                    if (IsCompleted)
                    {
                        return;
                    }

                    var key = new MetadataKey(
                        path,
                        kind,
                        observation.Operation,
                        observation.BaseDirectory,
                        observation.Provider);
                    if (_metadataReads.TryGetValue(key, out EvaluationMetadataObservation priorValue))
                    {
                        if (priorValue.Value != observation.Value ||
                            !string.Equals(priorValue.TextValue, observation.TextValue, StringComparison.Ordinal) ||
                            !FileUtilities.PathComparer.Equals(priorValue.BaseDirectory, observation.BaseDirectory))
                        {
                            AddReason(EvaluationObservationReason.ConflictingObservation);
                        }
                    }
                    else
                    {
                        _metadataReads.Add(key, observation);
                    }
                }
            }
            catch (Exception ex) when (!ExceptionHandling.IsCriticalException(ex))
            {
                AddReason(EvaluationObservationReason.ObservationIncomplete);
            }
        }

        internal void RecordFileRead(
            string path,
            string contentHash,
            bool isVerifiable,
            EvaluationContentHashKind hashKind = EvaluationContentHashKind.Unknown,
            string provider = null,
            string baseDirectory = null)
        {
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            MarkCategory(
                EvaluationObservationCategory.FileContent,
                isVerifiable
                    ? EvaluationObservationCategoryState.Observed
                    : EvaluationObservationCategoryState.Incomplete);
            try
            {
                lock (_observationLock)
                {
                    if (IsCompleted)
                    {
                        return;
                    }

                    string normalizedPath = NormalizePath(path, baseDirectory);
                    string actualProvider = provider ?? s_defaultFileSystemProvider;
                    var key = new FileReadKey(normalizedPath, hashKind, actualProvider);
                    var observation = new EvaluationFileReadObservation(
                        normalizedPath,
                        contentHash,
                        isVerifiable,
                        hashKind,
                        actualProvider);

                    if (_fileReads.TryGetValue(key, out EvaluationFileReadObservation priorObservation))
                    {
                        if (priorObservation.IsVerifiable && observation.IsVerifiable)
                        {
                            if (!string.Equals(priorObservation.ContentHash, observation.ContentHash, StringComparison.Ordinal))
                            {
                                AddReason(EvaluationObservationReason.ConflictingObservation);
                            }
                        }
                        else if (!priorObservation.IsVerifiable && observation.IsVerifiable)
                        {
                            _fileReads[key] = observation;
                        }
                    }
                    else
                    {
                        _fileReads.Add(key, observation);
                    }

                    if (!isVerifiable)
                    {
                        AddReason(EvaluationObservationReason.UnverifiableFileRead);
                    }
                }
            }
            catch (Exception ex) when (!ExceptionHandling.IsCriticalException(ex))
            {
                AddReason(EvaluationObservationReason.ObservationIncomplete);
            }
        }

        internal void RecordOperationFailure(
            EvaluationObservationCategory category,
            string operation,
            string path,
            string provider,
            Exception exception,
            string baseDirectory = null,
            EvaluationObservationCategoryState categoryState = EvaluationObservationCategoryState.Incomplete)
        {
            try
            {
                exception ??= new InvalidOperationException("The operation failed without exception details.");
                RecordOperationFailureCore(
                    category,
                    operation,
                    path,
                    provider,
                    exception.GetType().FullName,
                    exception.HResult,
                    exception.Message,
                    baseDirectory,
                    categoryState);
            }
            catch (Exception ex) when (!ExceptionHandling.IsCriticalException(ex))
            {
                MarkOperationFailureWithoutDetails(category, categoryState);
                AddReason(EvaluationObservationReason.ObservationIncomplete);
            }
        }

        internal void RecordOperationFailure(
            EvaluationObservationCategory category,
            string operation,
            string path,
            string provider,
            string exceptionType,
            int hResult,
            string message,
            string baseDirectory = null,
            EvaluationObservationCategoryState categoryState = EvaluationObservationCategoryState.Incomplete)
        {
            try
            {
                RecordOperationFailureCore(
                    category,
                    operation,
                    path,
                    provider,
                    exceptionType,
                    hResult,
                    message,
                    baseDirectory,
                    categoryState);
            }
            catch (Exception ex) when (!ExceptionHandling.IsCriticalException(ex))
            {
                MarkOperationFailureWithoutDetails(category, categoryState);
                AddReason(EvaluationObservationReason.ObservationIncomplete);
            }
        }

        internal void RecordPropertyFunctionFailure(
            Type receiverType,
            string member,
            object instance,
            object[] arguments,
            string pathBaseDirectory,
            Exception exception)
        {
            try
            {
                EvaluationPropertyFunctionEffect effects = ClassifyPropertyFunction(receiverType, member);
                bool hasPathInput = TryGetPropertyFunctionPath(
                    receiverType,
                    member,
                    instance,
                    arguments,
                    out string path);
                EvaluationObservationCategory category =
                    !hasPathInput
                        ? EvaluationObservationCategory.PropertyFunction
                        : (effects & EvaluationPropertyFunctionEffect.FileContent) != 0
                            ? EvaluationObservationCategory.FileContent
                            : (effects & EvaluationPropertyFunctionEffect.PathProbe) != 0
                                ? EvaluationObservationCategory.PathProbe
                                : (effects & EvaluationPropertyFunctionEffect.FileMetadata) != 0
                                    ? EvaluationObservationCategory.FileMetadata
                                    : (effects & EvaluationPropertyFunctionEffect.DirectoryEnumeration) != 0
                                        ? EvaluationObservationCategory.DirectoryEnumeration
                                        : EvaluationObservationCategory.PropertyFunction;
                RecordOperationFailure(
                    category,
                    string.Concat(receiverType?.FullName ?? instance?.GetType().FullName ?? "<unknown>", "::", member),
                    path,
                    hasPathInput ? s_defaultFileSystemProvider : null,
                    exception,
                    pathBaseDirectory);
            }
            catch (Exception ex) when (!ExceptionHandling.IsCriticalException(ex))
            {
                MarkOperationFailureWithoutDetails(
                    EvaluationObservationCategory.PropertyFunction,
                    EvaluationObservationCategoryState.Incomplete);
                AddReason(EvaluationObservationReason.ObservationIncomplete);
            }
        }

        private void RecordOperationFailureCore(
            EvaluationObservationCategory category,
            string operation,
            string path,
            string provider,
            string exceptionType,
            int hResult,
            string message,
            string baseDirectory,
            EvaluationObservationCategoryState categoryState)
        {
            MarkCategory(category, categoryState);
            MarkCategory(EvaluationObservationCategory.Completion, EvaluationObservationCategoryState.Incomplete);
            lock (_observationLock)
            {
                if (IsCompleted)
                {
                    return;
                }

                _operationFailures.Add(new EvaluationOperationFailureObservation(
                    category,
                    operation,
                    NormalizePath(path, baseDirectory),
                    provider,
                    exceptionType ?? "<unknown>",
                    hResult,
                    message));
                AddReason(EvaluationObservationReason.ExternalOperationFailure);
            }
        }

        private void MarkOperationFailureWithoutDetails(
            EvaluationObservationCategory category,
            EvaluationObservationCategoryState categoryState)
        {
            MarkCategory(category, categoryState);
            MarkCategory(EvaluationObservationCategory.Completion, EvaluationObservationCategoryState.Incomplete);
            lock (_observationLock)
            {
                if (!IsCompleted)
                {
                    AddReason(EvaluationObservationReason.ExternalOperationFailure);
                }
            }
        }

        internal EvaluationObservationReport Complete(bool evaluationSucceeded)
        {
            MarkCategory(EvaluationObservationCategory.Completion, EvaluationObservationCategoryState.Observed);
            EvaluationObservationReport report;
            TestConfiguration testConfiguration;
            lock (_observationLock)
            {
                if (IsCompleted)
                {
                    return null;
                }

                Volatile.Write(ref _completed, 1);
                try
                {
                    try
                    {
                        report = new EvaluationObservationReport(
                            _evaluationId,
                            _projectPath,
                            evaluationSucceeded,
                            (EvaluationObservationReason)Volatile.Read(ref _reasons),
                            ObservationSchemaVersion,
                            PropertyFunctionClassificationVersion,
                            CreateCategorySnapshot(),
                            _request,
                            _projectSources.Values,
                            _pathProbes.Values,
                            _directoryEnumerations.Values,
                            _metadataReads.Values,
                            _fileReads.Values,
                            _globs.Values,
                            _searches.Values,
                            _environment.Values,
                            _externalInputs.Values,
                            _propertyFunctions.Values,
                            _sdkResolutions,
                            _taskRegistrations.Values,
                            _sideEffects.Values,
                            _operationFailures);
                    }
                    catch (Exception ex) when (!ExceptionHandling.IsCriticalException(ex))
                    {
                        report = new EvaluationObservationReport(
                            _evaluationId,
                            _projectPath,
                            evaluationSucceeded,
                            (EvaluationObservationReason)Volatile.Read(ref _reasons) |
                                EvaluationObservationReason.ObservationIncomplete,
                            ObservationSchemaVersion,
                            PropertyFunctionClassificationVersion,
                            CreateCategorySnapshot(),
                            null,
                            [],
                            [],
                            [],
                            [],
                            [],
                            [],
                            [],
                            [],
                            [],
                            [],
                            [],
                            [],
                            [],
                            []);
                    }
                }
                finally
                {
                    // Successful reports own the populated collections. On fallback, the
                    // collections are discarded. In both cases the completed session must
                    // release them because evaluator objects can retain the session.
                    _pathProbes = null;
                    _directoryEnumerations = null;
                    _metadataReads = null;
                    _fileReads = null;
                    _request = null;
                    _projectSources = null;
                    _globs = null;
                    _searches = null;
                    _environment = null;
                    _externalInputs = null;
                    _propertyFunctions = null;
                    _sdkResolutions = null;
                    _taskRegistrations = null;
                    _sideEffects = null;
                    _operationFailures = null;
                    testConfiguration = Interlocked.Exchange(ref _testConfiguration, null);
                }
            }

            try
            {
                testConfiguration?.ReportCreated?.Invoke(report);
            }
            catch (Exception ex) when (!ExceptionHandling.IsCriticalException(ex))
            {
                Interlocked.CompareExchange(ref testConfiguration.ReportException, ex, null);
            }

            return report;
        }

        private static string ComputeHash(byte[] content)
        {
#if NET
            return Convert.ToBase64String(SHA256.HashData(content));
#else
            using SHA256 sha256 = SHA256.Create();
            return Convert.ToBase64String(sha256.ComputeHash(content));
#endif
        }

        private static string ComputeHash(string content)
        {
            return ComputeHash(Encoding.UTF8.GetBytes(content));
        }

        internal static string ComputeTextHash(string content) => ComputeHash(content);
        internal static string ComputeBytesHash(byte[] content) => ComputeHash(content);

        private static string GetProjectSourceHash(ProjectRootElement source)
        {
            ProjectSourceHashCache cache = s_projectSourceHashes.GetValue(
                source,
                static _ => new ProjectSourceHashCache());
            lock (cache)
            {
                int version = source.Version;
                if (cache.Version != version || cache.ContentHash is null)
                {
                    cache.Version = version;
                    cache.ContentHash = ComputeTextHash(source.RawXml);
                }

                return cache.ContentHash;
            }
        }

        internal string NormalizePath(string path, string baseDirectory = null)
        {
            if (string.IsNullOrEmpty(path))
            {
                return path;
            }

            if (FileUtilities.IsPathFullyQualifiedNoThrow(path))
            {
                return FileUtilities.NormalizePathForObservation(
                    FileUtilities.GetFullPathNoThrow(path));
            }

            if (!string.IsNullOrEmpty(baseDirectory))
            {
                return FileUtilities.NormalizePathForObservation(
                    FileUtilities.GetFullPathNoThrow(path, baseDirectory));
            }

            AddReason(EvaluationObservationReason.UnrootedPath);
            return path;
        }

        private static bool EnumerationResultsEqual(
            EvaluationDirectoryEnumerationObservation left,
            EvaluationDirectoryEnumerationObservation right)
        {
            return left.Completion == right.Completion &&
                left.EntryCount == right.EntryCount &&
                string.Equals(left.EntriesHash, right.EntriesHash, StringComparison.Ordinal);
        }

        private void Record(Action action)
        {
            try
            {
                lock (_observationLock)
                {
                    if (!IsCompleted)
                    {
                        action();
                    }
                }
            }
            catch (Exception ex) when (!ExceptionHandling.IsCriticalException(ex))
            {
                AddReason(EvaluationObservationReason.ObservationIncomplete);
            }
        }

        private static StringComparer GetEnvironmentNameComparer(EvaluationEnvironmentSource source)
        {
            return source != EvaluationEnvironmentSource.LiveProcess || NativeMethodsShared.IsWindows
                ? StringComparer.OrdinalIgnoreCase
                : StringComparer.Ordinal;
        }

        internal void MarkReason(EvaluationObservationReason reason) => AddReason(reason);

        private static EvaluationPathKind ConvertPathKind(EvaluationPathProbeKind kind)
        {
            return kind switch
            {
                EvaluationPathProbeKind.File => EvaluationPathKind.File,
                EvaluationPathProbeKind.Directory => EvaluationPathKind.Directory,
                EvaluationPathProbeKind.FileOrDirectory => EvaluationPathKind.FileOrDirectory,
                _ => Assumed.Unreachable<EvaluationPathKind>(),
            };
        }

        private void RecordTypedPropertyFunction(
            Type receiverType,
            string member,
            object instance,
            object[] arguments,
            object result,
            EvaluationPropertyFunctionEffect effects,
            string[] serializedArguments,
            string serializedResult,
            string pathBaseDirectory)
        {
            string receiverName = receiverType?.FullName;
            string firstArgument = arguments is { Length: > 0 } ? arguments[0]?.ToString() : null;
            bool hasPathInput = TryGetPropertyFunctionPath(
                receiverType,
                member,
                instance,
                arguments,
                out string pathInput);
            string firstPath = hasPathInput
                ? NormalizePath(pathInput, pathBaseDirectory)
                : firstArgument;
            string serializedRequest = string.Join("|", serializedArguments);

            if (receiverName == typeof(Environment).FullName)
            {
                if (string.Equals(member, nameof(Environment.GetEnvironmentVariable), StringComparison.OrdinalIgnoreCase))
                {
                    string value = result as string;
                    RecordEnvironment(firstArgument, EvaluationEnvironmentSource.LiveProcess, value is not null, value);
                }
                else
                {
                    RecordExternalInputCore(
                        EvaluationExternalInputKind.Environment,
                        string.Concat(receiverName, "::", member),
                        serializedRequest,
                        serializedResult);
                }

                return;
            }

            if (receiverName == typeof(System.IO.File).FullName)
            {
                if (string.Equals(member, nameof(System.IO.File.ReadAllText), StringComparison.OrdinalIgnoreCase) &&
                    result is string text)
                {
                    RecordFileRead(
                        firstPath,
                        ComputeTextHash(text),
                        isVerifiable: true,
                        hashKind: EvaluationContentHashKind.DecodedText);
                }
                else if (string.Equals(member, nameof(System.IO.File.ReadAllBytes), StringComparison.OrdinalIgnoreCase) &&
                    result is byte[] bytes)
                {
                    RecordFileRead(
                        firstPath,
                        ComputeBytesHash(bytes),
                        isVerifiable: true,
                        hashKind: EvaluationContentHashKind.RawBytes);
                }
                else if (string.Equals(member, "ReadAllLines", StringComparison.OrdinalIgnoreCase) &&
                    result is string[] lines)
                {
                    RecordFileRead(
                        firstPath,
                        ComputeStringSequenceHash(lines),
                        isVerifiable: true,
                        hashKind: EvaluationContentHashKind.DecodedTextSequence);
                }
                else if (string.Equals(member, nameof(System.IO.File.Exists), StringComparison.OrdinalIgnoreCase) &&
                    result is bool fileExists)
                {
                    RecordProbe(firstPath, EvaluationPathKind.File, fileExists);
                }
                else
                {
                    RecordMetadata(
                        firstPath,
                        EvaluationMetadataKind.PropertyFunction,
                        serializedResult,
                        null,
                        string.Concat(receiverName, "::", member));
                }

                return;
            }

            if (receiverName == typeof(System.IO.Directory).FullName)
            {
                if (string.Equals(member, nameof(System.IO.Directory.Exists), StringComparison.OrdinalIgnoreCase) &&
                    result is bool directoryExists)
                {
                    RecordProbe(firstPath, EvaluationPathKind.Directory, directoryExists);
                }
                else if ((effects & EvaluationPropertyFunctionEffect.DirectoryEnumeration) != 0 &&
                         result is ICollection collection)
                {
                    var entries = new List<string>(collection.Count);
                    foreach (object entry in collection)
                    {
                        entries.Add(entry?.ToString());
                    }

                    bool requestIsComplete = TryGetDirectoryEnumerationRequest(
                        isStatic: true,
                        member,
                        arguments,
                        out string searchPattern,
                        out SearchOption searchOption,
                        out string optionsIdentity);
                    RecordEnumeration(
                        firstPath,
                        searchPattern,
                        searchOption,
                        GetEnumerationKind(member),
                        entries,
                        requestIsComplete
                            ? EvaluationEnumerationCompletion.Complete
                            : EvaluationEnumerationCompletion.Partial,
                        optionsIdentity: optionsIdentity,
                        baseDirectory: pathBaseDirectory);
                }
                else if ((effects & EvaluationPropertyFunctionEffect.Ambient) != 0)
                {
                    RecordExternalInputCore(
                        EvaluationExternalInputKind.Ambient,
                        string.Concat(receiverName, "::", member),
                        string.Concat("Arguments=", serializedRequest, "\0Base=", pathBaseDirectory),
                        serializedResult);
                }
                else
                {
                    RecordMetadata(
                        firstPath,
                        EvaluationMetadataKind.PropertyFunction,
                        serializedResult,
                        null,
                        string.Concat(receiverName, "::", member));
                }

                return;
            }

            if (receiverName == typeof(System.IO.Path).FullName)
            {
                if (string.Equals(member, "Exists", StringComparison.OrdinalIgnoreCase) &&
                    result is bool exists)
                {
                    RecordProbe(firstPath, EvaluationPathKind.FileOrDirectory, exists);
                }
                else if ((effects & EvaluationPropertyFunctionEffect.Ambient) != 0)
                {
                    RecordExternalInputCore(
                        EvaluationExternalInputKind.Ambient,
                        string.Concat(receiverName, "::", member),
                        string.Concat("Arguments=", serializedRequest, "\0Base=", pathBaseDirectory),
                        serializedResult);
                }

                return;
            }

            if (receiverType == typeof(IntrinsicFunctions))
            {
                if (string.Equals(member, "FileExists", StringComparison.OrdinalIgnoreCase) &&
                    result is bool fileExists)
                {
                    RecordProbe(firstPath, EvaluationPathKind.File, fileExists);
                }
                else if (string.Equals(member, "DirectoryExists", StringComparison.OrdinalIgnoreCase) &&
                         result is bool directoryExists)
                {
                    RecordProbe(firstPath, EvaluationPathKind.Directory, directoryExists);
                }
                else if (string.Equals(member, "GetPathOfFileAbove", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(member, "GetDirectoryNameOfFileAbove", StringComparison.OrdinalIgnoreCase))
                {
                    // The ordered candidate list is recorded by FileUtilities at the actual search seam.
                }
                else if (member.StartsWith("GetRegistryValue", StringComparison.OrdinalIgnoreCase))
                {
                    RecordExternalInputCore(
                        EvaluationExternalInputKind.Registry,
                        member,
                        serializedRequest,
                        serializedResult);
                }
                else if (string.Equals(member, "RegisterBuildCheck", StringComparison.OrdinalIgnoreCase))
                {
                    RecordSideEffectCore("RegisterBuildCheck", firstArgument, serializedResult);
                }
                else if ((effects & EvaluationPropertyFunctionEffect.Ambient) != 0)
                {
                    RecordExternalInputCore(
                        EvaluationExternalInputKind.Ambient,
                        member,
                        serializedRequest,
                        serializedResult);
                }

                return;
            }

            if (receiverName == "Microsoft.Build.Utilities.ToolLocationHelper")
            {
                RecordExternalInputCore(
                    EvaluationExternalInputKind.Toolset,
                    string.Concat(receiverName, "::", member),
                    serializedRequest,
                    serializedResult);
                MarkReason(EvaluationObservationReason.UnversionedToolLocationHelperCache);
                return;
            }

            if (instance is FileSystemInfo fileSystemInfo)
            {
                if ((effects & EvaluationPropertyFunctionEffect.DirectoryEnumeration) != 0 &&
                    result is ICollection collection)
                {
                    List<string> entries = [];
                    foreach (object entry in collection)
                    {
                        entries.Add(entry is FileSystemInfo resultInfo ? resultInfo.FullName : entry?.ToString());
                    }

                    bool requestIsComplete = TryGetDirectoryEnumerationRequest(
                        isStatic: false,
                        member,
                        arguments,
                        out string searchPattern,
                        out SearchOption searchOption,
                        out string optionsIdentity);
                    RecordEnumeration(
                        fileSystemInfo.FullName,
                        searchPattern,
                        searchOption,
                        GetEnumerationKind(member),
                        entries,
                        requestIsComplete
                            ? EvaluationEnumerationCompletion.Complete
                            : EvaluationEnumerationCompletion.Partial,
                        optionsIdentity: optionsIdentity);
                }
                else if ((effects & EvaluationPropertyFunctionEffect.FileMetadata) != 0)
                {
                    RecordMetadata(
                        fileSystemInfo.FullName,
                        EvaluationMetadataKind.PropertyFunction,
                        serializedResult,
                        null,
                        string.Concat(receiverName, "::", member));
                }
                else if ((effects & EvaluationPropertyFunctionEffect.Ambient) != 0)
                {
                    RecordExternalInputCore(
                        EvaluationExternalInputKind.Ambient,
                        string.Concat(receiverName, "::", member),
                        string.Concat("Instance=", fileSystemInfo.FullName, "\0Arguments=", serializedRequest),
                        serializedResult);
                }

                return;
            }

            if ((effects & EvaluationPropertyFunctionEffect.Registry) != 0)
            {
                RecordExternalInputCore(
                    EvaluationExternalInputKind.Registry,
                    string.Concat(receiverName, "::", member),
                    serializedRequest,
                    serializedResult);
            }
            else if ((effects & EvaluationPropertyFunctionEffect.Ambient) != 0)
            {
                RecordExternalInputCore(
                    EvaluationExternalInputKind.Ambient,
                    string.Concat(receiverName, "::", member),
                    serializedRequest,
                    serializedResult);
            }

            if ((effects & EvaluationPropertyFunctionEffect.SideEffect) != 0)
            {
                RecordSideEffectCore(
                    string.Concat(receiverName, "::", member),
                    firstArgument,
                    serializedResult);
            }
        }

        private static bool TryGetPropertyFunctionPath(
            Type receiverType,
            string member,
            object instance,
            object[] arguments,
            out string path)
        {
            if (instance is FileSystemInfo fileSystemInfo)
            {
                path = fileSystemInfo.FullName;
                return true;
            }

            string receiverName = receiverType?.FullName;
            bool hasPathInput =
                receiverName == typeof(System.IO.File).FullName ||
                receiverName == typeof(System.IO.Directory).FullName ||
                (receiverName == typeof(System.IO.Path).FullName &&
                    string.Equals(member, "Exists", StringComparison.OrdinalIgnoreCase)) ||
                (receiverType == typeof(IntrinsicFunctions) &&
                    (string.Equals(member, "FileExists", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(member, "DirectoryExists", StringComparison.OrdinalIgnoreCase)));
            path = hasPathInput && arguments is { Length: > 0 }
                ? arguments[0]?.ToString()
                : null;
            return hasPathInput;
        }

        private EvaluationPropertyFunctionEffect ClassifyPropertyFunction(
            Type receiverType,
            string member)
        {
            if (_allPropertyFunctionsEnabled)
            {
                return EvaluationPropertyFunctionEffect.OpaqueUnsupported;
            }

            string receiverName = receiverType?.FullName;
            if (receiverType == typeof(IntrinsicFunctions))
            {
                if (member.StartsWith("GetRegistryValue", StringComparison.OrdinalIgnoreCase))
                {
                    return EvaluationPropertyFunctionEffect.Registry;
                }

                if (string.Equals(member, "FileExists", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(member, "DirectoryExists", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(member, "GetPathOfFileAbove", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(member, "GetDirectoryNameOfFileAbove", StringComparison.OrdinalIgnoreCase))
                {
                    return EvaluationPropertyFunctionEffect.PathProbe;
                }

                if (string.Equals(member, "DoesTaskHostExist", StringComparison.OrdinalIgnoreCase))
                {
                    return EvaluationPropertyFunctionEffect.PathProbe | EvaluationPropertyFunctionEffect.Ambient;
                }

                if (string.Equals(member, "RegisterBuildCheck", StringComparison.OrdinalIgnoreCase))
                {
                    return EvaluationPropertyFunctionEffect.FileContent | EvaluationPropertyFunctionEffect.SideEffect;
                }

                if (member.StartsWith("GetCurrentToolsDirectory", StringComparison.OrdinalIgnoreCase) ||
                    member.StartsWith("GetToolsDirectory", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(member, "GetMSBuildSDKsPath", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(member, "GetVsInstallRoot", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(member, "GetProgramFiles32", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(member, "GetMSBuildExtensionsPath", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(member, "IsRunningFromVisualStudio", StringComparison.OrdinalIgnoreCase))
                {
                    return EvaluationPropertyFunctionEffect.Ambient;
                }

                if (string.Equals(member, "NormalizePath", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(member, "NormalizeDirectory", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(member, "MakeRelative", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(member, "AreFeaturesEnabled", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(member, "CheckFeatureAvailability", StringComparison.OrdinalIgnoreCase) ||
                    member.StartsWith("IsOs", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(member, "IsOSPlatform", StringComparison.OrdinalIgnoreCase))
                {
                    return EvaluationPropertyFunctionEffect.Ambient;
                }

                return IsKnownPureIntrinsic(member)
                    ? EvaluationPropertyFunctionEffect.Pure
                    : EvaluationPropertyFunctionEffect.OpaqueUnsupported;
            }

            if (receiverName == typeof(Environment).FullName)
            {
                return IsVolatileEnvironmentMember(member)
                    ? EvaluationPropertyFunctionEffect.Volatile
                    : EvaluationPropertyFunctionEffect.Environment | EvaluationPropertyFunctionEffect.Ambient;
            }

            if (receiverName == typeof(System.IO.File).FullName)
            {
                if (IsMutatingFileSystemMember(member))
                {
                    return EvaluationPropertyFunctionEffect.SideEffect |
                        EvaluationPropertyFunctionEffect.OpaqueUnsupported;
                }

                if (member.StartsWith("ReadAll", StringComparison.OrdinalIgnoreCase))
                {
                    return EvaluationPropertyFunctionEffect.FileContent;
                }

                if (string.Equals(member, nameof(System.IO.File.Exists), StringComparison.OrdinalIgnoreCase))
                {
                    return EvaluationPropertyFunctionEffect.PathProbe;
                }

                if (s_fileMetadataMembers.Contains(member))
                {
                    return EvaluationPropertyFunctionEffect.FileMetadata;
                }

                return EvaluationPropertyFunctionEffect.OpaqueUnsupported;
            }

            if (receiverName == typeof(System.IO.Directory).FullName)
            {
                if (IsMutatingFileSystemMember(member))
                {
                    return EvaluationPropertyFunctionEffect.SideEffect |
                        EvaluationPropertyFunctionEffect.OpaqueUnsupported;
                }

                if (string.Equals(member, nameof(System.IO.Directory.Exists), StringComparison.OrdinalIgnoreCase))
                {
                    return EvaluationPropertyFunctionEffect.PathProbe;
                }

                if (string.Equals(member, nameof(System.IO.Directory.GetFiles), StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(member, nameof(System.IO.Directory.GetDirectories), StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(member, "GetFileSystemEntries", StringComparison.OrdinalIgnoreCase) ||
                    member.StartsWith("Enumerate", StringComparison.OrdinalIgnoreCase))
                {
                    return EvaluationPropertyFunctionEffect.DirectoryEnumeration;
                }

                if (string.Equals(member, nameof(System.IO.Directory.GetParent), StringComparison.OrdinalIgnoreCase))
                {
                    return EvaluationPropertyFunctionEffect.Ambient;
                }

                if (s_directoryMetadataMembers.Contains(member))
                {
                    return EvaluationPropertyFunctionEffect.FileMetadata;
                }

                return EvaluationPropertyFunctionEffect.OpaqueUnsupported;
            }

            if (receiverName is "System.IO.FileInfo" or "System.IO.DirectoryInfo" or "System.IO.FileSystemInfo")
            {
                if (member.StartsWith("Enumerate", StringComparison.OrdinalIgnoreCase))
                {
                    return EvaluationPropertyFunctionEffect.DirectoryEnumeration |
                        EvaluationPropertyFunctionEffect.OpaqueUnsupported;
                }

                if (member.StartsWith("GetFiles", StringComparison.OrdinalIgnoreCase) ||
                    member.StartsWith("GetDirectories", StringComparison.OrdinalIgnoreCase) ||
                    member.StartsWith("GetFileSystemInfos", StringComparison.OrdinalIgnoreCase))
                {
                    return EvaluationPropertyFunctionEffect.DirectoryEnumeration;
                }

                if (member.StartsWith("Open", StringComparison.OrdinalIgnoreCase) ||
                    member.StartsWith("Create", StringComparison.OrdinalIgnoreCase) ||
                    member.StartsWith("Append", StringComparison.OrdinalIgnoreCase) ||
                    member.StartsWith("Delete", StringComparison.OrdinalIgnoreCase) ||
                    member.StartsWith("Move", StringComparison.OrdinalIgnoreCase) ||
                    member.StartsWith("Copy", StringComparison.OrdinalIgnoreCase) ||
                    member.StartsWith("Replace", StringComparison.OrdinalIgnoreCase))
                {
                    return EvaluationPropertyFunctionEffect.SideEffect |
                        EvaluationPropertyFunctionEffect.OpaqueUnsupported;
                }

                if (s_fileSystemInfoPathMembers.Contains(member))
                {
                    return EvaluationPropertyFunctionEffect.Ambient;
                }

                return s_fileSystemInfoMetadataMembers.Contains(member)
                    ? EvaluationPropertyFunctionEffect.FileMetadata
                    : EvaluationPropertyFunctionEffect.OpaqueUnsupported;
            }

            if (receiverName == typeof(System.IO.Path).FullName)
            {
                if (string.Equals(member, "Exists", StringComparison.OrdinalIgnoreCase))
                {
                    return EvaluationPropertyFunctionEffect.PathProbe;
                }

                if (string.Equals(member, nameof(System.IO.Path.GetTempFileName), StringComparison.OrdinalIgnoreCase))
                {
                    return EvaluationPropertyFunctionEffect.Volatile | EvaluationPropertyFunctionEffect.SideEffect;
                }

                if (string.Equals(member, nameof(System.IO.Path.GetRandomFileName), StringComparison.OrdinalIgnoreCase))
                {
                    return EvaluationPropertyFunctionEffect.Volatile;
                }

                if (string.Equals(member, nameof(System.IO.Path.GetTempPath), StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(member, nameof(System.IO.Path.GetFullPath), StringComparison.OrdinalIgnoreCase))
                {
                    return EvaluationPropertyFunctionEffect.Ambient;
                }

                return s_knownPurePathMembers.Contains(member)
                    ? EvaluationPropertyFunctionEffect.Pure
                    : EvaluationPropertyFunctionEffect.OpaqueUnsupported;
            }

            if (receiverName == typeof(DateTime).FullName ||
                receiverName == typeof(DateTimeOffset).FullName)
            {
                if (string.Equals(member, "Now", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(member, "UtcNow", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(member, "Today", StringComparison.OrdinalIgnoreCase))
                {
                    return EvaluationPropertyFunctionEffect.Volatile;
                }

                return EvaluationPropertyFunctionEffect.Ambient;
            }

            if (receiverName == typeof(Guid).FullName &&
                string.Equals(member, nameof(Guid.NewGuid), StringComparison.OrdinalIgnoreCase))
            {
                return EvaluationPropertyFunctionEffect.Volatile;
            }

            if (receiverName == typeof(Guid).FullName)
            {
                return EvaluationPropertyFunctionEffect.Pure;
            }

            if (receiverName == typeof(char).FullName)
            {
                return member.StartsWith("ToLower", StringComparison.OrdinalIgnoreCase) ||
                    member.StartsWith("ToUpper", StringComparison.OrdinalIgnoreCase)
                    ? EvaluationPropertyFunctionEffect.Ambient
                    : EvaluationPropertyFunctionEffect.Pure;
            }

            if (IsNumericPropertyFunctionType(receiverType))
            {
                return IsCultureSensitiveNumericMember(member)
                    ? EvaluationPropertyFunctionEffect.Ambient
                    : EvaluationPropertyFunctionEffect.Pure;
            }

            if (receiverName == typeof(Convert).FullName)
            {
                return EvaluationPropertyFunctionEffect.Ambient;
            }

            if (receiverName == typeof(TimeSpan).FullName)
            {
                return IsCultureSensitiveNumericMember(member)
                    ? EvaluationPropertyFunctionEffect.Ambient
                    : EvaluationPropertyFunctionEffect.Pure;
            }

            if (receiverName == typeof(string).FullName)
            {
                return IsCultureSensitiveStringMember(member)
                    ? EvaluationPropertyFunctionEffect.Ambient
                    : EvaluationPropertyFunctionEffect.Pure;
            }

            if (receiverName == typeof(StringComparer).FullName)
            {
                return member.StartsWith("CurrentCulture", StringComparison.OrdinalIgnoreCase)
                    ? EvaluationPropertyFunctionEffect.Ambient
                    : EvaluationPropertyFunctionEffect.Pure;
            }

            if (receiverName == typeof(CultureInfo).FullName)
            {
                return EvaluationPropertyFunctionEffect.Ambient;
            }

            if (receiverName == "Microsoft.Build.Utilities.ToolLocationHelper")
            {
                return EvaluationPropertyFunctionEffect.FileContent |
                    EvaluationPropertyFunctionEffect.Registry |
                    EvaluationPropertyFunctionEffect.Ambient;
            }

            if (receiverName == typeof(RuntimeInformation).FullName ||
                receiverName == typeof(OSPlatform).FullName ||
                receiverName is "System.OperatingSystem" or "Microsoft.Build.Framework.OperatingSystem")
            {
                return EvaluationPropertyFunctionEffect.Ambient;
            }

            if (IsKnownPurePropertyFunctionType(receiverType))
            {
                return EvaluationPropertyFunctionEffect.Pure;
            }

            return EvaluationPropertyFunctionEffect.OpaqueUnsupported;
        }

        private static bool IsKnownPureIntrinsic(string member)
        {
            return s_knownPureIntrinsicMembers.Contains(member);
        }

        private static EvaluationEnumerationKind GetEnumerationKind(string member)
        {
            if (member.IndexOf("FileSystem", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return EvaluationEnumerationKind.FilesAndDirectories;
            }

            return member.IndexOf("Directories", StringComparison.OrdinalIgnoreCase) >= 0
                ? EvaluationEnumerationKind.Directories
                : EvaluationEnumerationKind.Files;
        }

        private static bool TryGetDirectoryEnumerationRequest(
            bool isStatic,
            string member,
            object[] arguments,
            out string searchPattern,
            out SearchOption searchOption,
            out string optionsIdentity)
        {
            searchPattern = "*";
            object option = null;
            bool shapeIsSupported;
            int argumentCount = arguments?.Length ?? 0;

            if (isStatic)
            {
                shapeIsSupported = argumentCount is 1 or 2 or 3;
                if (argumentCount >= 2)
                {
                    if (arguments[1] is string pattern)
                    {
                        searchPattern = pattern;
                    }
                    else
                    {
                        shapeIsSupported = false;
                        if (IsEnumerationOption(arguments[1]))
                        {
                            option = arguments[1];
                        }
                        else
                        {
                            searchPattern = SerializeValue(arguments[1]) ?? "<null>";
                        }
                    }
                }

                if (argumentCount >= 3)
                {
                    if (option is null || IsEnumerationOption(arguments[2]))
                    {
                        option = arguments[2];
                    }
                }
            }
            else
            {
                shapeIsSupported = argumentCount is 0 or 1 or 2;
                if (argumentCount >= 1)
                {
                    if (arguments[0] is string pattern)
                    {
                        searchPattern = pattern;
                    }
                    else
                    {
                        shapeIsSupported = false;
                        if (IsEnumerationOption(arguments[0]))
                        {
                            option = arguments[0];
                        }
                        else
                        {
                            searchPattern = SerializeValue(arguments[0]) ?? "<null>";
                        }
                    }
                }

                if (argumentCount >= 2)
                {
                    if (option is null || IsEnumerationOption(arguments[1]))
                    {
                        option = arguments[1];
                    }
                }
            }

            if (!shapeIsSupported && !IsEnumerationOption(option) && arguments is not null)
            {
                for (int i = isStatic ? 1 : 0; i < arguments.Length; i++)
                {
                    if (IsEnumerationOption(arguments[i]))
                    {
                        option = arguments[i];
                        break;
                    }
                }
            }

            bool optionsAreSupported = TryGetEnumerationOptionsIdentity(
                option,
                out searchOption,
                out optionsIdentity);
            if (!shapeIsSupported)
            {
                optionsIdentity = string.Concat(
                    optionsIdentity,
                    "\0UnsupportedArgumentShape=",
                    isStatic ? "Static:" : "Instance:",
                    member,
                    ":",
                    argumentCount.ToString(CultureInfo.InvariantCulture),
                    "\0Arguments=",
                    string.Join(";", SerializeArguments(arguments)));
            }

            return shapeIsSupported && optionsAreSupported;
        }

        private static bool IsEnumerationOption(object argument)
        {
            return argument is SearchOption ||
                string.Equals(
                    argument?.GetType().FullName,
                    "System.IO.EnumerationOptions",
                    StringComparison.Ordinal);
        }

        private static bool TryGetEnumerationOptionsIdentity(
            object option,
            out SearchOption searchOption,
            out string optionsIdentity)
        {
            searchOption = SearchOption.TopDirectoryOnly;
            optionsIdentity = "Default";
            if (option is null)
            {
                return true;
            }

            if (option is SearchOption typedSearchOption)
            {
                searchOption = typedSearchOption;
                optionsIdentity = string.Concat(
                    nameof(SearchOption),
                    ":",
                    ((int)typedSearchOption).ToString(CultureInfo.InvariantCulture));
                return typedSearchOption is SearchOption.TopDirectoryOnly or SearchOption.AllDirectories;
            }

            Type optionType = option.GetType();
#if NET
            if (option is EnumerationOptions enumerationOptions)
            {
                searchOption = enumerationOptions.RecurseSubdirectories
                    ? SearchOption.AllDirectories
                    : SearchOption.TopDirectoryOnly;
                optionsIdentity = string.Concat(
                    optionType.FullName,
                    "\0",
                    nameof(EnumerationOptions.AttributesToSkip),
                    "=",
                    ((int)enumerationOptions.AttributesToSkip).ToString(CultureInfo.InvariantCulture),
                    "\0",
                    nameof(EnumerationOptions.BufferSize),
                    "=",
                    enumerationOptions.BufferSize.ToString(CultureInfo.InvariantCulture),
                    "\0",
                    nameof(EnumerationOptions.IgnoreInaccessible),
                    "=",
                    enumerationOptions.IgnoreInaccessible ? "True" : "False",
                    "\0",
                    nameof(EnumerationOptions.MatchCasing),
                    "=",
                    ((int)enumerationOptions.MatchCasing).ToString(CultureInfo.InvariantCulture),
                    "\0",
                    nameof(EnumerationOptions.MatchType),
                    "=",
                    ((int)enumerationOptions.MatchType).ToString(CultureInfo.InvariantCulture),
                    "\0",
                    nameof(EnumerationOptions.MaxRecursionDepth),
                    "=",
                    enumerationOptions.MaxRecursionDepth.ToString(CultureInfo.InvariantCulture),
                    "\0",
                    nameof(EnumerationOptions.RecurseSubdirectories),
                    "=",
                    enumerationOptions.RecurseSubdirectories ? "True" : "False",
                    "\0",
                    nameof(EnumerationOptions.ReturnSpecialDirectories),
                    "=",
                    enumerationOptions.ReturnSpecialDirectories ? "True" : "False");
                if (!s_enumerationOptionsShapeSupported)
                {
                    optionsIdentity = string.Concat(
                        optionsIdentity,
                        "\0UnsupportedPropertyCount=",
                        s_enumerationOptionsPropertyCount.ToString(CultureInfo.InvariantCulture));
                }

                return s_enumerationOptionsShapeSupported;
            }
#endif

            if (!string.Equals(
                    optionType.FullName,
                    "System.IO.EnumerationOptions",
                    StringComparison.Ordinal))
            {
                optionsIdentity = string.Concat("<unsupported:", optionType.FullName, ">");
                return false;
            }

            optionsIdentity = string.Concat("<unsupported-target:", optionType.FullName, ">");
            return false;
        }

        private static bool IsNumericPropertyFunctionType(Type type)
        {
            return type?.IsPrimitive == true ||
                type == typeof(decimal);
        }

        private static bool IsCultureSensitiveNumericMember(string member)
        {
            return member.StartsWith("Parse", StringComparison.OrdinalIgnoreCase) ||
                member.StartsWith("TryParse", StringComparison.OrdinalIgnoreCase) ||
                member.StartsWith("ToString", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsMutatingFileSystemMember(string member)
        {
            return member.StartsWith("Write", StringComparison.OrdinalIgnoreCase) ||
                member.StartsWith("Append", StringComparison.OrdinalIgnoreCase) ||
                member.StartsWith("Create", StringComparison.OrdinalIgnoreCase) ||
                member.StartsWith("Delete", StringComparison.OrdinalIgnoreCase) ||
                member.StartsWith("Move", StringComparison.OrdinalIgnoreCase) ||
                member.StartsWith("Copy", StringComparison.OrdinalIgnoreCase) ||
                member.StartsWith("Replace", StringComparison.OrdinalIgnoreCase) ||
                member.StartsWith("Set", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsCultureSensitiveStringMember(string member)
        {
            return member.StartsWith("Compare", StringComparison.OrdinalIgnoreCase) ||
                member.StartsWith("EndsWith", StringComparison.OrdinalIgnoreCase) ||
                member.StartsWith("IndexOf", StringComparison.OrdinalIgnoreCase) ||
                member.StartsWith("LastIndexOf", StringComparison.OrdinalIgnoreCase) ||
                member.StartsWith("Format", StringComparison.OrdinalIgnoreCase) ||
                member.StartsWith("StartsWith", StringComparison.OrdinalIgnoreCase) ||
                member.StartsWith("ToLower", StringComparison.OrdinalIgnoreCase) ||
                member.StartsWith("ToUpper", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsVolatileEnvironmentMember(string member)
        {
            return string.Equals(member, nameof(Environment.TickCount), StringComparison.OrdinalIgnoreCase) ||
                string.Equals(member, nameof(Environment.WorkingSet), StringComparison.OrdinalIgnoreCase) ||
                string.Equals(member, nameof(Environment.StackTrace), StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsKnownPurePropertyFunctionType(Type type)
        {
            if (type is null)
            {
                return false;
            }

            return type.IsEnum ||
                type == typeof(decimal) ||
                type == typeof(Enum) ||
                type == typeof(Math) ||
                type == typeof(TimeSpan) ||
                type == typeof(Version) ||
                type == typeof(Uri) ||
                type == typeof(UriBuilder) ||
                type.FullName == "System.Text.RegularExpressions.Regex";
        }

        private static string[] SerializeArguments(object[] arguments)
        {
            if (arguments is null || arguments.Length == 0)
            {
                return [];
            }

            string[] result = new string[arguments.Length];
            for (int i = 0; i < arguments.Length; i++)
            {
                result[i] = SerializeValue(arguments[i]);
            }

            return result;
        }

        private static string SerializeValue(object value)
        {
            if (value is null)
            {
                return null;
            }

            if (value is string stringValue)
            {
                return stringValue;
            }

            if (value is byte[] bytes)
            {
                return ComputeBytesHash(bytes);
            }

            if (value is DateTime dateTime)
            {
                return dateTime.ToString("O", CultureInfo.InvariantCulture);
            }

            if (value is DateTimeOffset dateTimeOffset)
            {
                return dateTimeOffset.ToString("O", CultureInfo.InvariantCulture);
            }

            if (value is IDictionary dictionary)
            {
                List<string> entries = [];
                foreach (DictionaryEntry entry in dictionary)
                {
                    entries.Add(string.Concat(SerializeValue(entry.Key), "=", SerializeValue(entry.Value)));
                }

                entries.Sort(StringComparer.Ordinal);
                return string.Join(";", entries);
            }

            if (value is ICollection collection)
            {
                List<string> entries = [];
                foreach (object entry in collection)
                {
                    entries.Add(SerializeValue(entry));
                }

                return string.Join(";", entries);
            }

            return value is IFormattable formattable
                ? formattable.ToString(null, CultureInfo.InvariantCulture)
                : value.ToString();
        }

        private static string[] CopyStrings(IReadOnlyList<string> values)
        {
            if (values is null || values.Count == 0)
            {
                return [];
            }

            string[] snapshot = new string[values.Count];
            for (int i = 0; i < values.Count; i++)
            {
                snapshot[i] = values[i];
            }

            return snapshot;
        }

        private static string[] CopyStrings(IEnumerable<string> values)
        {
            if (values is null)
            {
                return [];
            }

            List<string> snapshot = values is ICollection<string> collection
                ? new List<string>(collection.Count)
                : [];
            foreach (string value in values)
            {
                snapshot.Add(value);
            }

            return snapshot.ToArray();
        }

        private static EvaluationNamedValueObservation[] CreateNamedValueSnapshot(
            IDictionary<string, string> values,
            string source)
        {
            if (values is null || values.Count == 0)
            {
                return [];
            }

            var snapshot = new EvaluationNamedValueObservation[values.Count];
            int index = 0;
            foreach (KeyValuePair<string, string> value in values)
            {
                snapshot[index++] = new EvaluationNamedValueObservation(
                    value.Key,
                    value.Value,
                    source);
            }

            return snapshot;
        }

        private static EvaluationSdkItemObservation[] CreateSdkItemSnapshot(
            IDictionary<string, SdkResultItem> items)
        {
            if (items is null || items.Count == 0)
            {
                return [];
            }

            var snapshot = new EvaluationSdkItemObservation[items.Count];
            int index = 0;
            foreach (KeyValuePair<string, SdkResultItem> item in items)
            {
                snapshot[index++] = new EvaluationSdkItemObservation(
                    item.Key,
                    item.Value?.ItemSpec,
                    CreateNamedValueSnapshot(item.Value?.Metadata, "SdkItemMetadata"));
            }

            return snapshot;
        }

        private static string ComputeStringSequenceHash(IReadOnlyList<string> values)
        {
            var hasher = new EvaluationInputFingerprintBuilder();
            if (values is not null)
            {
                for (int i = 0; i < values.Count; i++)
                {
                    hasher.Add(values[i]);
                }
            }

            return hasher.Complete();
        }

        private static bool GlobResultsEqual(
            EvaluationGlobObservation left,
            EvaluationGlobObservation right)
        {
            return left.WasLazy == right.WasLazy &&
                left.DriveEnumerating == right.DriveEnumerating &&
                left.ResultsEscaped == right.ResultsEscaped &&
                left.ExcludeCount == right.ExcludeCount &&
                string.Equals(left.ExcludesFingerprint, right.ExcludesFingerprint, StringComparison.Ordinal) &&
                left.ResultCount == right.ResultCount &&
                string.Equals(left.ResultsFingerprint, right.ResultsFingerprint, StringComparison.Ordinal) &&
                string.Equals(left.Failure, right.Failure, StringComparison.Ordinal);
        }

        private void MarkCategory(
            EvaluationObservationCategory category,
            EvaluationObservationCategoryState state)
        {
            long mask = 1L << (int)category;
            switch (state)
            {
                case EvaluationObservationCategoryState.Observed:
                    SetCategoryBit(ref _observedCategories, mask);
                    break;
                case EvaluationObservationCategoryState.Incomplete:
                    SetCategoryBit(ref _incompleteCategories, mask);
                    break;
                case EvaluationObservationCategoryState.Unsupported:
                    SetCategoryBit(ref _unsupportedCategories, mask);
                    break;
            }
        }

        private static void SetCategoryBit(ref long field, long mask)
        {
            long priorValue;
            long newValue;
            do
            {
                priorValue = Volatile.Read(ref field);
                if ((priorValue & mask) != 0)
                {
                    return;
                }

                newValue = priorValue | mask;
            }
            while (Interlocked.CompareExchange(ref field, newValue, priorValue) != priorValue);
        }

        private EvaluationCategoryObservation[] CreateCategorySnapshot()
        {
            EvaluationObservationCategory[] categories =
                (EvaluationObservationCategory[])Enum.GetValues(typeof(EvaluationObservationCategory));
            var result = new EvaluationCategoryObservation[categories.Length];
            long observed = Volatile.Read(ref _observedCategories);
            long incomplete = Volatile.Read(ref _incompleteCategories);
            long unsupported = Volatile.Read(ref _unsupportedCategories);

            for (int i = 0; i < categories.Length; i++)
            {
                EvaluationObservationCategory category = categories[i];
                long mask = 1L << (int)category;
                EvaluationObservationCategoryState state =
                    (unsupported & mask) != 0
                        ? EvaluationObservationCategoryState.Unsupported
                        : (incomplete & mask) != 0
                            ? EvaluationObservationCategoryState.Incomplete
                            : (observed & mask) != 0
                                ? EvaluationObservationCategoryState.Observed
                                : EvaluationObservationCategoryState.NotExercised;
                result[i] = new EvaluationCategoryObservation(
                    category,
                    GetCategoryCoverage(category),
                    state);
            }

            return result;
        }

        private static EvaluationObservationCoverage GetCategoryCoverage(
            EvaluationObservationCategory category)
        {
            return category == EvaluationObservationCategory.Completion
                ? EvaluationObservationCoverage.Complete
                : EvaluationObservationCoverage.Partial;
        }

        private void AddReason(EvaluationObservationReason reason)
        {
            long priorValue;
            long newValue;
            do
            {
                priorValue = Volatile.Read(ref _reasons);
                newValue = priorValue | (long)reason;
            }
            while (Interlocked.CompareExchange(ref _reasons, newValue, priorValue) != priorValue);

            switch (reason)
            {
                case EvaluationObservationReason.AllPropertyFunctionsEnabled:
                case EvaluationObservationReason.UnclassifiedPropertyFunction:
                    MarkCategory(EvaluationObservationCategory.PropertyFunction, EvaluationObservationCategoryState.Unsupported);
                    break;
                case EvaluationObservationReason.UnsupportedVolatileInput:
                case EvaluationObservationReason.EvaluationSideEffect:
                    MarkCategory(EvaluationObservationCategory.VolatileOrSideEffect, EvaluationObservationCategoryState.Unsupported);
                    break;
                case EvaluationObservationReason.UnversionedToolsetInputs:
                case EvaluationObservationReason.UnversionedToolLocationHelperCache:
                    MarkCategory(EvaluationObservationCategory.Toolset, EvaluationObservationCategoryState.Incomplete);
                    break;
                case EvaluationObservationReason.UnversionedCustomProvider:
                case EvaluationObservationReason.UnversionedDirectoryCache:
                    MarkCategory(EvaluationObservationCategory.CustomProvider, EvaluationObservationCategoryState.Incomplete);
                    break;
                case EvaluationObservationReason.UnversionedSharedCache:
                case EvaluationObservationReason.UnversionedFileExistenceCache:
                case EvaluationObservationReason.UnversionedGlobCache:
                    MarkCategory(EvaluationObservationCategory.SharedCache, EvaluationObservationCategoryState.Incomplete);
                    break;
                case EvaluationObservationReason.ProjectXmlContentNotObserved:
                case EvaluationObservationReason.UnversionedProjectRootElementCache:
                case EvaluationObservationReason.UnversionedSourceProvider:
                case EvaluationObservationReason.ParsedProjectSourceOnly:
                case EvaluationObservationReason.ProjectSourceChangedDuringRead:
                    MarkCategory(EvaluationObservationCategory.ProjectSource, EvaluationObservationCategoryState.Incomplete);
                    break;
                case EvaluationObservationReason.UnverifiableFileRead:
                    MarkCategory(EvaluationObservationCategory.FileContent, EvaluationObservationCategoryState.Incomplete);
                    break;
                case EvaluationObservationReason.AmbiguousNegativeProbe:
                case EvaluationObservationReason.UnrootedPath:
                    MarkCategory(EvaluationObservationCategory.PathProbe, EvaluationObservationCategoryState.Incomplete);
                    break;
                case EvaluationObservationReason.PartialEnumeration:
                    MarkCategory(EvaluationObservationCategory.DirectoryEnumeration, EvaluationObservationCategoryState.Incomplete);
                    break;
                case EvaluationObservationReason.SdkResolutionWithoutCacheLifetime:
                    MarkCategory(EvaluationObservationCategory.SdkResolution, EvaluationObservationCategoryState.Incomplete);
                    break;
                case EvaluationObservationReason.IncompleteEvaluationStage:
                case EvaluationObservationReason.ParserConfigurationProvenanceUnavailable:
                    MarkCategory(EvaluationObservationCategory.Request, EvaluationObservationCategoryState.Incomplete);
                    break;
                case EvaluationObservationReason.ConflictingObservation:
                    MarkCategory(EvaluationObservationCategory.Completion, EvaluationObservationCategoryState.Incomplete);
                    break;
                case EvaluationObservationReason.ExternalOperationFailure:
                case EvaluationObservationReason.OpaqueExternalInput:
                case EvaluationObservationReason.ObservationIncomplete:
                    MarkCategory(EvaluationObservationCategory.Completion, EvaluationObservationCategoryState.Incomplete);
                    break;
            }
        }

        private readonly struct PathProbeKey : IEquatable<PathProbeKey>
        {
            internal PathProbeKey(string path, EvaluationPathKind kind, string provider)
            {
                Path = path;
                Kind = kind;
                Provider = provider;
            }

            internal string Path { get; }
            internal EvaluationPathKind Kind { get; }
            internal string Provider { get; }

            public bool Equals(PathProbeKey other)
            {
                return Kind == other.Kind &&
                    FileUtilities.PathComparer.Equals(Path, other.Path) &&
                    string.Equals(Provider, other.Provider, StringComparison.Ordinal);
            }

            public override bool Equals(object obj) => obj is PathProbeKey other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    int hashCode = (FileUtilities.PathComparer.GetHashCode(Path) * 397) ^ (int)Kind;
                    return (hashCode * 397) ^ StringComparer.Ordinal.GetHashCode(Provider);
                }
            }
        }

        private readonly struct EnvironmentKey : IEquatable<EnvironmentKey>
        {
            internal EnvironmentKey(EvaluationEnvironmentSource source, string name)
            {
                Source = source;
                Name = name;
            }

            private EvaluationEnvironmentSource Source { get; }
            private string Name { get; }

            public bool Equals(EnvironmentKey other)
            {
                return Source == other.Source &&
                    GetEnvironmentNameComparer(Source).Equals(Name, other.Name);
            }

            public override bool Equals(object obj) => obj is EnvironmentKey other && Equals(other);

            public override int GetHashCode()
            {
                return ((int)Source * 397) ^
                    GetEnvironmentNameComparer(Source).GetHashCode(Name);
            }
        }

        private readonly struct EnumerationKey : IEquatable<EnumerationKey>
        {
            internal EnumerationKey(
                string path,
                string searchPattern,
                SearchOption searchOption,
                EvaluationEnumerationKind kind,
                string provider,
                string optionsIdentity,
                int incompleteIdentity)
            {
                Path = path;
                SearchPattern = searchPattern;
                SearchOption = searchOption;
                Kind = kind;
                Provider = provider;
                OptionsIdentity = optionsIdentity;
                IncompleteIdentity = incompleteIdentity;
            }

            internal string Path { get; }
            internal string SearchPattern { get; }
            internal SearchOption SearchOption { get; }
            internal EvaluationEnumerationKind Kind { get; }
            internal string Provider { get; }
            internal string OptionsIdentity { get; }
            internal int IncompleteIdentity { get; }

            public bool Equals(EnumerationKey other)
            {
                return SearchOption == other.SearchOption &&
                    Kind == other.Kind &&
                    FileUtilities.PathComparer.Equals(Path, other.Path) &&
                    string.Equals(SearchPattern, other.SearchPattern, StringComparison.Ordinal) &&
                    string.Equals(Provider, other.Provider, StringComparison.Ordinal) &&
                    string.Equals(OptionsIdentity, other.OptionsIdentity, StringComparison.Ordinal) &&
                    IncompleteIdentity == other.IncompleteIdentity;
            }

            public override bool Equals(object obj) => obj is EnumerationKey other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    int hashCode = FileUtilities.PathComparer.GetHashCode(Path);
                    hashCode = (hashCode * 397) ^ StringComparer.Ordinal.GetHashCode(SearchPattern);
                    hashCode = (hashCode * 397) ^ (int)SearchOption;
                    hashCode = (hashCode * 397) ^ (int)Kind;
                    hashCode = (hashCode * 397) ^ StringComparer.Ordinal.GetHashCode(Provider);
                    hashCode = (hashCode * 397) ^ (OptionsIdentity is null
                        ? 0
                        : StringComparer.Ordinal.GetHashCode(OptionsIdentity));
                    return (hashCode * 397) ^ IncompleteIdentity;
                }
            }
        }

        private readonly struct MetadataKey : IEquatable<MetadataKey>
        {
            internal MetadataKey(
                string path,
                EvaluationMetadataKind kind,
                string operation,
                string baseDirectory,
                string provider)
            {
                Path = path;
                Kind = kind;
                Operation = operation;
                BaseDirectory = baseDirectory;
                Provider = provider;
            }

            internal string Path { get; }
            internal EvaluationMetadataKind Kind { get; }
            internal string Operation { get; }
            internal string BaseDirectory { get; }
            internal string Provider { get; }

            public bool Equals(MetadataKey other)
            {
                return Kind == other.Kind &&
                    FileUtilities.PathComparer.Equals(Path, other.Path) &&
                    string.Equals(Operation, other.Operation, StringComparison.Ordinal) &&
                    FileUtilities.PathComparer.Equals(BaseDirectory, other.BaseDirectory) &&
                    string.Equals(Provider, other.Provider, StringComparison.Ordinal);
            }

            public override bool Equals(object obj) => obj is MetadataKey other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    int hashCode = (FileUtilities.PathComparer.GetHashCode(Path) * 397) ^ (int)Kind;
                    hashCode = (hashCode * 397) ^ (Operation is null ? 0 : StringComparer.Ordinal.GetHashCode(Operation));
                    hashCode = (hashCode * 397) ^ (BaseDirectory is null ? 0 : FileUtilities.PathComparer.GetHashCode(BaseDirectory));
                    return (hashCode * 397) ^ StringComparer.Ordinal.GetHashCode(Provider);
                }
            }
        }

        private readonly struct FileReadKey : IEquatable<FileReadKey>
        {
            internal FileReadKey(
                string path,
                EvaluationContentHashKind hashKind,
                string provider)
            {
                Path = path;
                HashKind = hashKind;
                Provider = provider;
            }

            internal string Path { get; }
            internal EvaluationContentHashKind HashKind { get; }
            internal string Provider { get; }

            public bool Equals(FileReadKey other)
            {
                return HashKind == other.HashKind &&
                    FileUtilities.PathComparer.Equals(Path, other.Path) &&
                    string.Equals(Provider, other.Provider, StringComparison.Ordinal);
            }

            public override bool Equals(object obj) => obj is FileReadKey other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    int hashCode = (FileUtilities.PathComparer.GetHashCode(Path) * 397) ^ (int)HashKind;
                    return (hashCode * 397) ^ StringComparer.Ordinal.GetHashCode(Provider);
                }
            }
        }

        private sealed class TestConfiguration
        {
            internal TestConfiguration(
                bool enabled,
                Action<EvaluationObservationReport> reportCreated,
                bool retainDetails)
            {
                Enabled = enabled;
                ReportCreated = reportCreated;
                RetainDetails = retainDetails;
            }

            internal bool Enabled { get; }
            internal Action<EvaluationObservationReport> ReportCreated { get; }
            internal bool RetainDetails { get; }
            internal Exception ReportException;
        }

        private sealed class ProjectSourceHashCache
        {
            internal int Version = -1;
            internal string ContentHash;
        }

        private sealed class CurrentScope : IDisposable
        {
            private readonly EvaluationObservationSession _previous;
            private readonly IDisposable _frameworkScope;
            private int _disposed;

            internal CurrentScope(
                EvaluationObservationSession previous,
                IDisposable frameworkScope)
            {
                _previous = previous;
                _frameworkScope = frameworkScope;
            }

            public void Dispose()
            {
                if (Interlocked.Exchange(ref _disposed, 1) == 0)
                {
                    try
                    {
                        _frameworkScope.Dispose();
                    }
                    finally
                    {
                        s_current = _previous;
                    }
                }
            }
        }

        internal readonly struct DirectoryEnumerationSuppressionScope : IDisposable
        {
            private readonly EvaluationObservationSession _session;

            internal DirectoryEnumerationSuppressionScope(EvaluationObservationSession session)
            {
                _session = session;
            }

            public void Dispose()
            {
                if (_session is not null)
                {
                    Interlocked.Decrement(ref _session._suppressDirectoryEnumerations);
                }
            }
        }

        private sealed class TestScope : IDisposable
        {
            private readonly TestConfiguration _configuration;
            private int _disposed;

            internal TestScope(TestConfiguration configuration)
            {
                _configuration = configuration;
            }

            public void Dispose()
            {
                if (Interlocked.Exchange(ref _disposed, 1) != 0)
                {
                    return;
                }

                Exception reportException;
                lock (s_testLock)
                {
                    Assumed.True(
                        ReferenceEquals(s_testConfiguration, _configuration),
                        "The active test observation scope changed unexpectedly.");
                    Volatile.Write(ref s_testConfiguration, null);
                    reportException = _configuration.ReportException;
                }

                if (reportException is not null)
                {
                    throw new InvalidOperationException(
                        "The test evaluation-observation callback failed.",
                        reportException);
                }
            }
        }
    }

    internal sealed class RecordingFileSystem : IFileSystem
    {
        private readonly IFileSystem _inner;
        private readonly string _providerIdentity;
        private readonly EvaluationObservationSession _session;

        internal RecordingFileSystem(IFileSystem inner, EvaluationObservationSession session)
        {
            _inner = inner;
            _providerIdentity = inner.GetType().AssemblyQualifiedName;
            _session = session;
        }

        private string CaptureBaseDirectory(string path)
        {
            if (string.IsNullOrEmpty(path) || FileUtilities.IsPathFullyQualifiedNoThrow(path))
            {
                return null;
            }

            try
            {
                return Directory.GetCurrentDirectory();
            }
            catch (Exception ex) when (!ExceptionHandling.IsCriticalException(ex))
            {
                _session.MarkReason(EvaluationObservationReason.ObservationIncomplete);
                return null;
            }
        }

        public TextReader ReadFile(string path)
        {
            if (_session.IsCompleted)
            {
                return _inner.ReadFile(path);
            }

            string baseDirectory = CaptureBaseDirectory(path);
            try
            {
                TextReader reader = _inner.ReadFile(path);
                _session.RecordFileRead(
                    path,
                    contentHash: null,
                    isVerifiable: false,
                    provider: _providerIdentity,
                    baseDirectory: baseDirectory);
                return reader;
            }
            catch (Exception ex) when (!ExceptionHandling.IsCriticalException(ex))
            {
                _session.RecordOperationFailure(
                    EvaluationObservationCategory.FileContent,
                    nameof(IFileSystem.ReadFile),
                    path,
                    _providerIdentity,
                    ex,
                    baseDirectory);
                throw;
            }
        }

        public Stream GetFileStream(string path, FileMode mode, FileAccess access, FileShare share)
        {
            if (_session.IsCompleted)
            {
                return _inner.GetFileStream(path, mode, access, share);
            }

            string baseDirectory = CaptureBaseDirectory(path);
            try
            {
                Stream stream = _inner.GetFileStream(path, mode, access, share);
                if ((access & FileAccess.Read) != 0)
                {
                    _session.RecordFileRead(
                        path,
                        contentHash: null,
                        isVerifiable: false,
                        provider: _providerIdentity,
                        baseDirectory: baseDirectory);
                }

                if ((access & FileAccess.Write) != 0)
                {
                    _session.RecordSideEffect(
                        "WritableFileStream",
                        _session.NormalizePath(path, baseDirectory),
                        string.Concat(
                            "Provider=",
                            _providerIdentity,
                            "\0Mode=",
                            mode,
                            "\0Access=",
                            access,
                            "\0Share=",
                            share));
                }

                return stream;
            }
            catch (Exception ex) when (!ExceptionHandling.IsCriticalException(ex))
            {
                EvaluationObservationCategory category =
                    access == FileAccess.Read
                        ? EvaluationObservationCategory.FileContent
                        : EvaluationObservationCategory.VolatileOrSideEffect;
                _session.RecordOperationFailure(
                    category,
                    nameof(IFileSystem.GetFileStream),
                    path,
                    _providerIdentity,
                    ex,
                    baseDirectory,
                    category == EvaluationObservationCategory.VolatileOrSideEffect
                        ? EvaluationObservationCategoryState.Unsupported
                        : EvaluationObservationCategoryState.Incomplete);
                throw;
            }
        }

        public string ReadFileAllText(string path)
        {
            if (_session.IsCompleted)
            {
                return _inner.ReadFileAllText(path);
            }

            string baseDirectory = CaptureBaseDirectory(path);
            try
            {
                string content = _inner.ReadFileAllText(path);
                try
                {
                    _session.RecordFileRead(
                        path,
                        EvaluationObservationSession.ComputeTextHash(content),
                        isVerifiable: true,
                        hashKind: EvaluationContentHashKind.DecodedText,
                        provider: _providerIdentity,
                        baseDirectory: baseDirectory);
                }
                catch (Exception ex) when (!ExceptionHandling.IsCriticalException(ex))
                {
                    _session.MarkReason(EvaluationObservationReason.ObservationIncomplete);
                }

                return content;
            }
            catch (Exception ex) when (!ExceptionHandling.IsCriticalException(ex))
            {
                _session.RecordOperationFailure(
                    EvaluationObservationCategory.FileContent,
                    nameof(IFileSystem.ReadFileAllText),
                    path,
                    _providerIdentity,
                    ex,
                    baseDirectory);
                throw;
            }
        }

        public byte[] ReadFileAllBytes(string path)
        {
            if (_session.IsCompleted)
            {
                return _inner.ReadFileAllBytes(path);
            }

            string baseDirectory = CaptureBaseDirectory(path);
            try
            {
                byte[] content = _inner.ReadFileAllBytes(path);
                try
                {
                    _session.RecordFileRead(
                        path,
                        EvaluationObservationSession.ComputeBytesHash(content),
                        isVerifiable: true,
                        hashKind: EvaluationContentHashKind.RawBytes,
                        provider: _providerIdentity,
                        baseDirectory: baseDirectory);
                }
                catch (Exception ex) when (!ExceptionHandling.IsCriticalException(ex))
                {
                    _session.MarkReason(EvaluationObservationReason.ObservationIncomplete);
                }

                return content;
            }
            catch (Exception ex) when (!ExceptionHandling.IsCriticalException(ex))
            {
                _session.RecordOperationFailure(
                    EvaluationObservationCategory.FileContent,
                    nameof(IFileSystem.ReadFileAllBytes),
                    path,
                    _providerIdentity,
                    ex,
                    baseDirectory);
                throw;
            }
        }

        public IEnumerable<string> EnumerateFiles(
            string path,
            string searchPattern = "*",
            SearchOption searchOption = SearchOption.TopDirectoryOnly)
        {
            if (_session.IsCompleted || !_session.ShouldRecordDirectoryEnumeration)
            {
                return _inner.EnumerateFiles(path, searchPattern, searchOption);
            }

            return RecordEnumeration(
                path,
                searchPattern,
                searchOption,
                EvaluationEnumerationKind.Files,
                nameof(IFileSystem.EnumerateFiles),
                static (fileSystem, p, pattern, option) => fileSystem.EnumerateFiles(p, pattern, option));
        }

        public IEnumerable<string> EnumerateDirectories(
            string path,
            string searchPattern = "*",
            SearchOption searchOption = SearchOption.TopDirectoryOnly)
        {
            if (_session.IsCompleted || !_session.ShouldRecordDirectoryEnumeration)
            {
                return _inner.EnumerateDirectories(path, searchPattern, searchOption);
            }

            return RecordEnumeration(
                path,
                searchPattern,
                searchOption,
                EvaluationEnumerationKind.Directories,
                nameof(IFileSystem.EnumerateDirectories),
                static (fileSystem, p, pattern, option) => fileSystem.EnumerateDirectories(p, pattern, option));
        }

        public IEnumerable<string> EnumerateFileSystemEntries(
            string path,
            string searchPattern = "*",
            SearchOption searchOption = SearchOption.TopDirectoryOnly)
        {
            if (_session.IsCompleted || !_session.ShouldRecordDirectoryEnumeration)
            {
                return _inner.EnumerateFileSystemEntries(path, searchPattern, searchOption);
            }

            return RecordEnumeration(
                path,
                searchPattern,
                searchOption,
                EvaluationEnumerationKind.FilesAndDirectories,
                nameof(IFileSystem.EnumerateFileSystemEntries),
                static (fileSystem, p, pattern, option) => fileSystem.EnumerateFileSystemEntries(p, pattern, option));
        }

        public FileAttributes GetAttributes(string path)
        {
            if (_session.IsCompleted)
            {
                return _inner.GetAttributes(path);
            }

            string baseDirectory = CaptureBaseDirectory(path);
            try
            {
                FileAttributes attributes = _inner.GetAttributes(path);
                _session.RecordMetadata(
                    path,
                    EvaluationMetadataKind.Attributes,
                    (long)attributes,
                    _providerIdentity,
                    baseDirectory);
                return attributes;
            }
            catch (Exception ex) when (!ExceptionHandling.IsCriticalException(ex))
            {
                _session.RecordOperationFailure(
                    EvaluationObservationCategory.FileMetadata,
                    nameof(IFileSystem.GetAttributes),
                    path,
                    _providerIdentity,
                    ex,
                    baseDirectory);
                throw;
            }
        }

        public DateTime GetLastWriteTimeUtc(string path)
        {
            if (_session.IsCompleted)
            {
                return _inner.GetLastWriteTimeUtc(path);
            }

            string baseDirectory = CaptureBaseDirectory(path);
            try
            {
                DateTime timestamp = _inner.GetLastWriteTimeUtc(path);
                _session.RecordMetadata(
                    path,
                    EvaluationMetadataKind.LastWriteTimeUtc,
                    timestamp.Ticks,
                    _providerIdentity,
                    baseDirectory);
                return timestamp;
            }
            catch (Exception ex) when (!ExceptionHandling.IsCriticalException(ex))
            {
                _session.RecordOperationFailure(
                    EvaluationObservationCategory.FileMetadata,
                    nameof(IFileSystem.GetLastWriteTimeUtc),
                    path,
                    _providerIdentity,
                    ex,
                    baseDirectory);
                throw;
            }
        }

        public bool DirectoryExists(string path)
        {
            if (_session.IsCompleted)
            {
                return _inner.DirectoryExists(path);
            }

            string baseDirectory = CaptureBaseDirectory(path);
            try
            {
                bool exists = _inner.DirectoryExists(path);
                _session.RecordProbe(path, EvaluationPathKind.Directory, exists, _providerIdentity, baseDirectory);
                return exists;
            }
            catch (Exception ex) when (!ExceptionHandling.IsCriticalException(ex))
            {
                _session.RecordOperationFailure(
                    EvaluationObservationCategory.PathProbe,
                    nameof(IFileSystem.DirectoryExists),
                    path,
                    _providerIdentity,
                    ex,
                    baseDirectory);
                throw;
            }
        }

        public bool FileExists(string path)
        {
            if (_session.IsCompleted)
            {
                return _inner.FileExists(path);
            }

            string baseDirectory = CaptureBaseDirectory(path);
            try
            {
                bool exists = _inner.FileExists(path);
                _session.RecordProbe(path, EvaluationPathKind.File, exists, _providerIdentity, baseDirectory);
                return exists;
            }
            catch (Exception ex) when (!ExceptionHandling.IsCriticalException(ex))
            {
                _session.RecordOperationFailure(
                    EvaluationObservationCategory.PathProbe,
                    nameof(IFileSystem.FileExists),
                    path,
                    _providerIdentity,
                    ex,
                    baseDirectory);
                throw;
            }
        }

        public bool FileOrDirectoryExists(string path)
        {
            if (_session.IsCompleted)
            {
                return _inner.FileOrDirectoryExists(path);
            }

            string baseDirectory = CaptureBaseDirectory(path);
            try
            {
                bool exists = _inner.FileOrDirectoryExists(path);
                _session.RecordProbe(path, EvaluationPathKind.FileOrDirectory, exists, _providerIdentity, baseDirectory);
                return exists;
            }
            catch (Exception ex) when (!ExceptionHandling.IsCriticalException(ex))
            {
                _session.RecordOperationFailure(
                    EvaluationObservationCategory.PathProbe,
                    nameof(IFileSystem.FileOrDirectoryExists),
                    path,
                    _providerIdentity,
                    ex,
                    baseDirectory);
                throw;
            }
        }

        private IEnumerable<string> RecordEnumeration(
            string path,
            string searchPattern,
            SearchOption searchOption,
            EvaluationEnumerationKind kind,
            string operation,
            Func<IFileSystem, string, string, SearchOption, IEnumerable<string>> enumerate)
        {
            return RecordEnumerationIterator(path, searchPattern, searchOption, kind, operation, enumerate);
        }

        private IEnumerable<string> RecordEnumerationIterator(
            string path,
            string searchPattern,
            SearchOption searchOption,
            EvaluationEnumerationKind kind,
            string operation,
            Func<IFileSystem, string, string, SearchOption, IEnumerable<string>> enumerate)
        {
            string baseDirectory = CaptureBaseDirectory(path);
            List<string> observedEntries = _session.RetainDetails ? [] : null;
            var entriesHasher = new EvaluationInputFingerprintBuilder();
            int entryCount = 0;
            EvaluationEnumerationCompletion completion = EvaluationEnumerationCompletion.Partial;
            IEnumerator<string> enumerator = null;

            try
            {
                IEnumerable<string> entries;
                try
                {
                    entries = enumerate(_inner, path, searchPattern, searchOption);
                    enumerator = entries.GetEnumerator();
                }
                catch (Exception ex) when (!ExceptionHandling.IsCriticalException(ex))
                {
                    completion = EvaluationEnumerationCompletion.Failure;
                    _session.RecordOperationFailure(
                        EvaluationObservationCategory.DirectoryEnumeration,
                        operation,
                        path,
                        _providerIdentity,
                        ex,
                        baseDirectory);
                    throw;
                }

                while (true)
                {
                    bool hasNext;
                    try
                    {
                        hasNext = enumerator.MoveNext();
                    }
                    catch (Exception ex) when (!ExceptionHandling.IsCriticalException(ex))
                    {
                        completion = EvaluationEnumerationCompletion.Failure;
                        _session.RecordOperationFailure(
                            EvaluationObservationCategory.DirectoryEnumeration,
                            operation,
                            path,
                            _providerIdentity,
                            ex,
                            baseDirectory);
                        throw;
                    }

                    if (!hasNext)
                    {
                        completion = EvaluationEnumerationCompletion.Complete;
                        yield break;
                    }

                    string entry = enumerator.Current;
                    string normalizedEntry = _session.NormalizePath(entry, baseDirectory);
                    entryCount++;
                    entriesHasher.Add(normalizedEntry);
                    observedEntries?.Add(normalizedEntry);
                    yield return entry;
                }
            }
            finally
            {
                try
                {
                    enumerator?.Dispose();
                }
                catch (Exception ex) when (!ExceptionHandling.IsCriticalException(ex))
                {
                    completion = EvaluationEnumerationCompletion.Failure;
                    _session.RecordOperationFailure(
                        EvaluationObservationCategory.DirectoryEnumeration,
                        operation,
                        path,
                        _providerIdentity,
                        ex,
                        baseDirectory);
                    throw;
                }
                finally
                {
                    _session.RecordEnumeration(
                        path,
                        searchPattern,
                        searchOption,
                        kind,
                        observedEntries?.ToArray() ?? [],
                        entryCount,
                        entriesHasher.Complete(),
                        completion,
                        _providerIdentity,
                        baseDirectory: baseDirectory);
                }
            }
        }
    }
}
