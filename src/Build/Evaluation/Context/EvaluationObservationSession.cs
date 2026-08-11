// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared.FileSystem;

#nullable disable

namespace Microsoft.Build.Evaluation.Context
{
    [Flags]
    internal enum EvaluationObservationReason
    {
        None = 0,
        PrototypeCategoriesIncomplete = 1 << 0,
        FileContentReadsNotImplemented = 1 << 1,
        AmbiguousNegativeProbe = 1 << 2,
        ConflictingObservation = 1 << 3,
        PartialEnumeration = 1 << 4,
        ExternalOperationFailure = 1 << 5,
        UnverifiableFileRead = 1 << 6,
        UnversionedSharedCache = 1 << 7,
        UnversionedFileExistenceCache = 1 << 8,
        UnversionedGlobCache = 1 << 9,
        UnversionedDirectoryCache = 1 << 10,
        ProjectXmlContentNotObserved = 1 << 11,
        UnversionedProjectRootElementCache = 1 << 12,
        UnversionedSdkResolverCache = 1 << 13,
        IncompleteEvaluationStage = 1 << 14,
        UnrootedPath = 1 << 15,
    }

    internal enum EvaluationPathKind
    {
        File,
        Directory,
        FileOrDirectory,
    }

    internal enum EvaluationEnumerationKind
    {
        Files,
        Directories,
        FilesAndDirectories,
    }

    internal enum EvaluationEnumerationCompletion
    {
        Complete,
        Partial,
        Failure,
    }

    internal enum EvaluationMetadataKind
    {
        Attributes,
        LastWriteTimeUtc,
    }

    internal readonly struct EvaluationPathProbeObservation
    {
        internal EvaluationPathProbeObservation(string path, EvaluationPathKind kind, bool exists)
        {
            Path = path;
            Kind = kind;
            Exists = exists;
        }

        internal string Path { get; }
        internal EvaluationPathKind Kind { get; }
        internal bool Exists { get; }
    }

    internal readonly struct EvaluationDirectoryEnumerationObservation
    {
        internal EvaluationDirectoryEnumerationObservation(
            string path,
            string searchPattern,
            SearchOption searchOption,
            EvaluationEnumerationKind kind,
            string[] entries,
            EvaluationEnumerationCompletion completion)
        {
            Path = path;
            SearchPattern = searchPattern;
            SearchOption = searchOption;
            Kind = kind;
            Entries = entries;
            Completion = completion;
        }

        internal string Path { get; }
        internal string SearchPattern { get; }
        internal SearchOption SearchOption { get; }
        internal EvaluationEnumerationKind Kind { get; }
        internal string[] Entries { get; }
        internal EvaluationEnumerationCompletion Completion { get; }
    }

    internal readonly struct EvaluationMetadataObservation
    {
        internal EvaluationMetadataObservation(string path, EvaluationMetadataKind kind, long value)
        {
            Path = path;
            Kind = kind;
            Value = value;
        }

        internal string Path { get; }
        internal EvaluationMetadataKind Kind { get; }
        internal long Value { get; }
    }

    internal readonly struct EvaluationFileReadObservation
    {
        internal EvaluationFileReadObservation(string path, string contentHash, bool isVerifiable)
        {
            Path = path;
            ContentHash = contentHash;
            IsVerifiable = isVerifiable;
        }

        internal string Path { get; }
        internal string ContentHash { get; }
        internal bool IsVerifiable { get; }
    }

    internal sealed class EvaluationObservationReport
    {
        internal EvaluationObservationReport(
            int evaluationId,
            string projectPath,
            bool evaluationSucceeded,
            EvaluationObservationReason reasons,
            EvaluationPathProbeObservation[] pathProbes,
            EvaluationDirectoryEnumerationObservation[] directoryEnumerations,
            EvaluationMetadataObservation[] metadataReads,
            EvaluationFileReadObservation[] fileReads)
        {
            EvaluationId = evaluationId;
            ProjectPath = projectPath;
            EvaluationSucceeded = evaluationSucceeded;
            Reasons = reasons;
            PathProbes = pathProbes;
            DirectoryEnumerations = directoryEnumerations;
            MetadataReads = metadataReads;
            FileReads = fileReads;
        }

        internal int EvaluationId { get; }
        internal string ProjectPath { get; }
        internal bool EvaluationSucceeded { get; }
        internal EvaluationObservationReason Reasons { get; }
        internal EvaluationPathProbeObservation[] PathProbes { get; }
        internal EvaluationDirectoryEnumerationObservation[] DirectoryEnumerations { get; }
        internal EvaluationMetadataObservation[] MetadataReads { get; }
        internal EvaluationFileReadObservation[] FileReads { get; }
        internal bool ReadyForCacheHits => false;
    }

    internal sealed class EvaluationObservationSession
    {
        private const string ObservationEnvironmentVariable = "MSBUILDPROTOTYPEEVALUATIONOBSERVATION";

        private static readonly bool s_enabled =
            Environment.GetEnvironmentVariable(ObservationEnvironmentVariable) == "1";

        private static readonly object s_testLock = new();
        private static TestConfiguration s_testConfiguration;

        private readonly int _evaluationId;
        private readonly string _projectPath;
        private readonly ConcurrentDictionary<PathProbeKey, bool> _pathProbes = new();
        private readonly ConcurrentDictionary<EnumerationKey, EvaluationDirectoryEnumerationObservation> _directoryEnumerations = new();
        private readonly ConcurrentDictionary<MetadataKey, long> _metadataReads = new();
        private readonly ConcurrentDictionary<string, EvaluationFileReadObservation> _fileReads =
            new(FileUtilities.PathComparer);
        private readonly object _observationLock = new();

        private int _reasons;
        private int _completed;
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
            _reasons = (int)(EvaluationObservationReason.PrototypeCategoriesIncomplete |
                EvaluationObservationReason.FileContentReadsNotImplemented |
                EvaluationObservationReason.ProjectXmlContentNotObserved |
                EvaluationObservationReason.UnversionedProjectRootElementCache);
            _projectPath = NormalizePath(projectPath);
            _testConfiguration = testConfiguration;

            if (sharingPolicy == EvaluationContext.SharingPolicy.Shared)
            {
                AddReason(EvaluationObservationReason.UnversionedSharedCache);
            }

            if (sharingPolicy != EvaluationContext.SharingPolicy.Isolated)
            {
                AddReason(EvaluationObservationReason.UnversionedSdkResolverCache);
            }

            if (evaluationStage != ProjectEvaluationStage.Full)
            {
                AddReason(EvaluationObservationReason.IncompleteEvaluationStage);
            }

            if (Traits.Instance.CacheFileExistence)
            {
                AddReason(EvaluationObservationReason.UnversionedFileExistenceCache);
            }

            if (Traits.Instance.MSBuildCacheFileEnumerations)
            {
                AddReason(EvaluationObservationReason.UnversionedGlobCache);
            }

            if (hasDirectoryCache)
            {
                AddReason(EvaluationObservationReason.UnversionedDirectoryCache);
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

        internal static EvaluationObservationSession CreateForTests(int evaluationId = 1)
        {
            return new EvaluationObservationSession(
                evaluationId,
                projectPath: null,
                ProjectEvaluationStage.Full,
                EvaluationContext.SharingPolicy.Isolated,
                hasDirectoryCache: false,
                testConfiguration: null);
        }

        internal static IDisposable TestOnlyConfigure(
            bool enabled,
            Action<EvaluationObservationReport> reportCreated = null)
        {
            var configuration = new TestConfiguration(enabled, reportCreated);
            lock (s_testLock)
            {
                Assumed.Null(s_testConfiguration, "A test observation scope is already active.");
                Volatile.Write(ref s_testConfiguration, configuration);
            }

            return new TestScope(configuration);
        }

        internal bool IsCompleted => Volatile.Read(ref _completed) != 0;

        internal int TestOnlyRetainedObservationCount
        {
            get
            {
                lock (_observationLock)
                {
                    return _pathProbes.Count +
                        _directoryEnumerations.Count +
                        _metadataReads.Count +
                        _fileReads.Count;
                }
            }
        }

        internal void RecordProbe(string path, EvaluationPathKind kind, bool exists)
        {
            try
            {
                lock (_observationLock)
                {
                    if (IsCompleted)
                    {
                        return;
                    }

                    var key = new PathProbeKey(NormalizePath(path), kind);
                    if (!_pathProbes.TryAdd(key, exists) &&
                        _pathProbes.TryGetValue(key, out bool priorResult) &&
                        priorResult != exists)
                    {
                        AddReason(EvaluationObservationReason.ConflictingObservation);
                    }

                    if (!exists)
                    {
                        AddReason(EvaluationObservationReason.AmbiguousNegativeProbe);
                    }
                }
            }
            catch (Exception ex) when (!ExceptionHandling.IsCriticalException(ex))
            {
                AddReason(EvaluationObservationReason.ExternalOperationFailure);
            }
        }

        internal void RecordEnumeration(
            string path,
            string searchPattern,
            SearchOption searchOption,
            EvaluationEnumerationKind kind,
            IReadOnlyList<string> entries,
            EvaluationEnumerationCompletion completion)
        {
            try
            {
                lock (_observationLock)
                {
                    if (IsCompleted)
                    {
                        return;
                    }

                    string[] entrySnapshot = new string[entries.Count];
                    for (int i = 0; i < entries.Count; i++)
                    {
                        entrySnapshot[i] = entries[i];
                    }

                    var key = new EnumerationKey(NormalizePath(path), searchPattern ?? "*", searchOption, kind);
                    var observation = new EvaluationDirectoryEnumerationObservation(
                        key.Path,
                        key.SearchPattern,
                        key.SearchOption,
                        key.Kind,
                        entrySnapshot,
                        completion);

                    if (!_directoryEnumerations.TryAdd(key, observation) &&
                        _directoryEnumerations.TryGetValue(key, out EvaluationDirectoryEnumerationObservation priorObservation) &&
                        !EnumerationResultsEqual(priorObservation, observation))
                    {
                        AddReason(EvaluationObservationReason.ConflictingObservation);
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
                AddReason(EvaluationObservationReason.ExternalOperationFailure);
            }
        }

        internal void RecordMetadata(string path, EvaluationMetadataKind kind, long value)
        {
            try
            {
                lock (_observationLock)
                {
                    if (IsCompleted)
                    {
                        return;
                    }

                    var key = new MetadataKey(NormalizePath(path), kind);
                    if (!_metadataReads.TryAdd(key, value) &&
                        _metadataReads.TryGetValue(key, out long priorValue) &&
                        priorValue != value)
                    {
                        AddReason(EvaluationObservationReason.ConflictingObservation);
                    }
                }
            }
            catch (Exception ex) when (!ExceptionHandling.IsCriticalException(ex))
            {
                AddReason(EvaluationObservationReason.ExternalOperationFailure);
            }
        }

        internal void RecordFileRead(string path, string contentHash, bool isVerifiable)
        {
            try
            {
                lock (_observationLock)
                {
                    if (IsCompleted)
                    {
                        return;
                    }

                    string normalizedPath = NormalizePath(path);
                    var observation = new EvaluationFileReadObservation(normalizedPath, contentHash, isVerifiable);

                    if (!_fileReads.TryAdd(normalizedPath, observation) &&
                        _fileReads.TryGetValue(normalizedPath, out EvaluationFileReadObservation priorObservation))
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
                            _fileReads[normalizedPath] = observation;
                        }
                    }

                    if (!isVerifiable)
                    {
                        AddReason(EvaluationObservationReason.UnverifiableFileRead);
                    }
                }
            }
            catch (Exception ex) when (!ExceptionHandling.IsCriticalException(ex))
            {
                AddReason(EvaluationObservationReason.ExternalOperationFailure);
            }
        }

        internal void RecordOperationFailure()
        {
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
                    report = new EvaluationObservationReport(
                        _evaluationId,
                        _projectPath,
                        evaluationSucceeded,
                        (EvaluationObservationReason)Volatile.Read(ref _reasons),
                        CreatePathProbeSnapshot(),
                        CreateEnumerationSnapshot(),
                        CreateMetadataSnapshot(),
                        CreateFileReadSnapshot());
                }
                catch (Exception ex) when (!ExceptionHandling.IsCriticalException(ex))
                {
                    report = new EvaluationObservationReport(
                        _evaluationId,
                        _projectPath,
                        evaluationSucceeded,
                        (EvaluationObservationReason)Volatile.Read(ref _reasons) |
                            EvaluationObservationReason.ExternalOperationFailure,
                        [],
                        [],
                        [],
                        []);
                }

                _pathProbes.Clear();
                _directoryEnumerations.Clear();
                _metadataReads.Clear();
                _fileReads.Clear();
                testConfiguration = Interlocked.Exchange(ref _testConfiguration, null);
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
            using SHA256 sha256 = SHA256.Create();
            return Convert.ToBase64String(sha256.ComputeHash(content));
        }

        private static string ComputeHash(string content)
        {
            return ComputeHash(Encoding.UTF8.GetBytes(content));
        }

        internal static string ComputeTextHash(string content) => ComputeHash(content);
        internal static string ComputeBytesHash(byte[] content) => ComputeHash(content);

        private string NormalizePath(string path)
        {
            if (string.IsNullOrEmpty(path) || Path.IsPathRooted(path))
            {
                return string.IsNullOrEmpty(path) ? path : FileUtilities.GetFullPathNoThrow(path);
            }

            AddReason(EvaluationObservationReason.UnrootedPath);
            return path;
        }

        private static bool EnumerationResultsEqual(
            EvaluationDirectoryEnumerationObservation left,
            EvaluationDirectoryEnumerationObservation right)
        {
            if (left.Completion != right.Completion || left.Entries.Length != right.Entries.Length)
            {
                return false;
            }

            for (int i = 0; i < left.Entries.Length; i++)
            {
                if (!FileUtilities.PathComparer.Equals(left.Entries[i], right.Entries[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private EvaluationPathProbeObservation[] CreatePathProbeSnapshot()
        {
            var observations = new List<EvaluationPathProbeObservation>(_pathProbes.Count);
            foreach (KeyValuePair<PathProbeKey, bool> observation in _pathProbes)
            {
                observations.Add(new EvaluationPathProbeObservation(
                    observation.Key.Path,
                    observation.Key.Kind,
                    observation.Value));
            }

            EvaluationPathProbeObservation[] snapshot = observations.ToArray();
            Array.Sort(snapshot, static (left, right) =>
            {
                int pathComparison = FileUtilities.PathComparer.Compare(left.Path, right.Path);
                return pathComparison != 0 ? pathComparison : left.Kind.CompareTo(right.Kind);
            });
            return snapshot;
        }

        private EvaluationDirectoryEnumerationObservation[] CreateEnumerationSnapshot()
        {
            var observations = new List<EvaluationDirectoryEnumerationObservation>(_directoryEnumerations.Count);
            foreach (EvaluationDirectoryEnumerationObservation observation in _directoryEnumerations.Values)
            {
                observations.Add(observation);
            }

            EvaluationDirectoryEnumerationObservation[] snapshot = observations.ToArray();
            Array.Sort(snapshot, static (left, right) =>
            {
                int pathComparison = FileUtilities.PathComparer.Compare(left.Path, right.Path);
                if (pathComparison != 0)
                {
                    return pathComparison;
                }

                int patternComparison = string.Compare(left.SearchPattern, right.SearchPattern, StringComparison.Ordinal);
                return patternComparison != 0 ? patternComparison : left.Kind.CompareTo(right.Kind);
            });
            return snapshot;
        }

        private EvaluationMetadataObservation[] CreateMetadataSnapshot()
        {
            var observations = new List<EvaluationMetadataObservation>(_metadataReads.Count);
            foreach (KeyValuePair<MetadataKey, long> observation in _metadataReads)
            {
                observations.Add(new EvaluationMetadataObservation(
                    observation.Key.Path,
                    observation.Key.Kind,
                    observation.Value));
            }

            EvaluationMetadataObservation[] snapshot = observations.ToArray();
            Array.Sort(snapshot, static (left, right) =>
            {
                int pathComparison = FileUtilities.PathComparer.Compare(left.Path, right.Path);
                return pathComparison != 0 ? pathComparison : left.Kind.CompareTo(right.Kind);
            });
            return snapshot;
        }

        private EvaluationFileReadObservation[] CreateFileReadSnapshot()
        {
            var observations = new List<EvaluationFileReadObservation>(_fileReads.Count);
            foreach (EvaluationFileReadObservation observation in _fileReads.Values)
            {
                observations.Add(observation);
            }

            EvaluationFileReadObservation[] snapshot = observations.ToArray();
            Array.Sort(snapshot, static (left, right) => FileUtilities.PathComparer.Compare(left.Path, right.Path));
            return snapshot;
        }

        private void AddReason(EvaluationObservationReason reason)
        {
            int priorValue;
            int newValue;
            do
            {
                priorValue = Volatile.Read(ref _reasons);
                newValue = priorValue | (int)reason;
            }
            while (Interlocked.CompareExchange(ref _reasons, newValue, priorValue) != priorValue);
        }

        private readonly struct PathProbeKey : IEquatable<PathProbeKey>
        {
            internal PathProbeKey(string path, EvaluationPathKind kind)
            {
                Path = path;
                Kind = kind;
            }

            internal string Path { get; }
            internal EvaluationPathKind Kind { get; }

            public bool Equals(PathProbeKey other)
            {
                return Kind == other.Kind && FileUtilities.PathComparer.Equals(Path, other.Path);
            }

            public override bool Equals(object obj) => obj is PathProbeKey other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    return (FileUtilities.PathComparer.GetHashCode(Path) * 397) ^ (int)Kind;
                }
            }
        }

        private readonly struct EnumerationKey : IEquatable<EnumerationKey>
        {
            internal EnumerationKey(
                string path,
                string searchPattern,
                SearchOption searchOption,
                EvaluationEnumerationKind kind)
            {
                Path = path;
                SearchPattern = searchPattern;
                SearchOption = searchOption;
                Kind = kind;
            }

            internal string Path { get; }
            internal string SearchPattern { get; }
            internal SearchOption SearchOption { get; }
            internal EvaluationEnumerationKind Kind { get; }

            public bool Equals(EnumerationKey other)
            {
                return SearchOption == other.SearchOption &&
                    Kind == other.Kind &&
                    FileUtilities.PathComparer.Equals(Path, other.Path) &&
                    string.Equals(SearchPattern, other.SearchPattern, StringComparison.Ordinal);
            }

            public override bool Equals(object obj) => obj is EnumerationKey other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    int hashCode = FileUtilities.PathComparer.GetHashCode(Path);
                    hashCode = (hashCode * 397) ^ StringComparer.Ordinal.GetHashCode(SearchPattern);
                    hashCode = (hashCode * 397) ^ (int)SearchOption;
                    return (hashCode * 397) ^ (int)Kind;
                }
            }
        }

        private readonly struct MetadataKey : IEquatable<MetadataKey>
        {
            internal MetadataKey(string path, EvaluationMetadataKind kind)
            {
                Path = path;
                Kind = kind;
            }

            internal string Path { get; }
            internal EvaluationMetadataKind Kind { get; }

            public bool Equals(MetadataKey other)
            {
                return Kind == other.Kind && FileUtilities.PathComparer.Equals(Path, other.Path);
            }

            public override bool Equals(object obj) => obj is MetadataKey other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    return (FileUtilities.PathComparer.GetHashCode(Path) * 397) ^ (int)Kind;
                }
            }
        }

        private sealed class TestConfiguration
        {
            internal TestConfiguration(bool enabled, Action<EvaluationObservationReport> reportCreated)
            {
                Enabled = enabled;
                ReportCreated = reportCreated;
            }

            internal bool Enabled { get; }
            internal Action<EvaluationObservationReport> ReportCreated { get; }
            internal Exception ReportException;
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
        private readonly EvaluationObservationSession _session;

        internal RecordingFileSystem(IFileSystem inner, EvaluationObservationSession session)
        {
            _inner = inner;
            _session = session;
        }

        public TextReader ReadFile(string path)
        {
            if (_session.IsCompleted)
            {
                return _inner.ReadFile(path);
            }

            try
            {
                TextReader reader = _inner.ReadFile(path);
                _session.RecordFileRead(path, contentHash: null, isVerifiable: false);
                return reader;
            }
            catch (Exception ex) when (!ExceptionHandling.IsCriticalException(ex))
            {
                _session.RecordOperationFailure();
                throw;
            }
        }

        public Stream GetFileStream(string path, FileMode mode, FileAccess access, FileShare share)
        {
            if (_session.IsCompleted)
            {
                return _inner.GetFileStream(path, mode, access, share);
            }

            try
            {
                Stream stream = _inner.GetFileStream(path, mode, access, share);
                if ((access & FileAccess.Read) != 0)
                {
                    _session.RecordFileRead(path, contentHash: null, isVerifiable: false);
                }

                return stream;
            }
            catch (Exception ex) when (!ExceptionHandling.IsCriticalException(ex))
            {
                _session.RecordOperationFailure();
                throw;
            }
        }

        public string ReadFileAllText(string path)
        {
            if (_session.IsCompleted)
            {
                return _inner.ReadFileAllText(path);
            }

            try
            {
                string content = _inner.ReadFileAllText(path);
                try
                {
                    _session.RecordFileRead(path, EvaluationObservationSession.ComputeTextHash(content), isVerifiable: true);
                }
                catch (Exception ex) when (!ExceptionHandling.IsCriticalException(ex))
                {
                    _session.RecordOperationFailure();
                }

                return content;
            }
            catch (Exception ex) when (!ExceptionHandling.IsCriticalException(ex))
            {
                _session.RecordOperationFailure();
                throw;
            }
        }

        public byte[] ReadFileAllBytes(string path)
        {
            if (_session.IsCompleted)
            {
                return _inner.ReadFileAllBytes(path);
            }

            try
            {
                byte[] content = _inner.ReadFileAllBytes(path);
                try
                {
                    _session.RecordFileRead(path, EvaluationObservationSession.ComputeBytesHash(content), isVerifiable: true);
                }
                catch (Exception ex) when (!ExceptionHandling.IsCriticalException(ex))
                {
                    _session.RecordOperationFailure();
                }

                return content;
            }
            catch (Exception ex) when (!ExceptionHandling.IsCriticalException(ex))
            {
                _session.RecordOperationFailure();
                throw;
            }
        }

        public IEnumerable<string> EnumerateFiles(
            string path,
            string searchPattern = "*",
            SearchOption searchOption = SearchOption.TopDirectoryOnly)
        {
            if (_session.IsCompleted)
            {
                return _inner.EnumerateFiles(path, searchPattern, searchOption);
            }

            return RecordEnumeration(
                path,
                searchPattern,
                searchOption,
                EvaluationEnumerationKind.Files,
                static (fileSystem, p, pattern, option) => fileSystem.EnumerateFiles(p, pattern, option));
        }

        public IEnumerable<string> EnumerateDirectories(
            string path,
            string searchPattern = "*",
            SearchOption searchOption = SearchOption.TopDirectoryOnly)
        {
            if (_session.IsCompleted)
            {
                return _inner.EnumerateDirectories(path, searchPattern, searchOption);
            }

            return RecordEnumeration(
                path,
                searchPattern,
                searchOption,
                EvaluationEnumerationKind.Directories,
                static (fileSystem, p, pattern, option) => fileSystem.EnumerateDirectories(p, pattern, option));
        }

        public IEnumerable<string> EnumerateFileSystemEntries(
            string path,
            string searchPattern = "*",
            SearchOption searchOption = SearchOption.TopDirectoryOnly)
        {
            if (_session.IsCompleted)
            {
                return _inner.EnumerateFileSystemEntries(path, searchPattern, searchOption);
            }

            return RecordEnumeration(
                path,
                searchPattern,
                searchOption,
                EvaluationEnumerationKind.FilesAndDirectories,
                static (fileSystem, p, pattern, option) => fileSystem.EnumerateFileSystemEntries(p, pattern, option));
        }

        public FileAttributes GetAttributes(string path)
        {
            if (_session.IsCompleted)
            {
                return _inner.GetAttributes(path);
            }

            try
            {
                FileAttributes attributes = _inner.GetAttributes(path);
                _session.RecordMetadata(path, EvaluationMetadataKind.Attributes, (long)attributes);
                return attributes;
            }
            catch (Exception ex) when (!ExceptionHandling.IsCriticalException(ex))
            {
                _session.RecordOperationFailure();
                throw;
            }
        }

        public DateTime GetLastWriteTimeUtc(string path)
        {
            if (_session.IsCompleted)
            {
                return _inner.GetLastWriteTimeUtc(path);
            }

            try
            {
                DateTime timestamp = _inner.GetLastWriteTimeUtc(path);
                _session.RecordMetadata(path, EvaluationMetadataKind.LastWriteTimeUtc, timestamp.Ticks);
                return timestamp;
            }
            catch (Exception ex) when (!ExceptionHandling.IsCriticalException(ex))
            {
                _session.RecordOperationFailure();
                throw;
            }
        }

        public bool DirectoryExists(string path)
        {
            if (_session.IsCompleted)
            {
                return _inner.DirectoryExists(path);
            }

            try
            {
                bool exists = _inner.DirectoryExists(path);
                _session.RecordProbe(path, EvaluationPathKind.Directory, exists);
                return exists;
            }
            catch (Exception ex) when (!ExceptionHandling.IsCriticalException(ex))
            {
                _session.RecordOperationFailure();
                throw;
            }
        }

        public bool FileExists(string path)
        {
            if (_session.IsCompleted)
            {
                return _inner.FileExists(path);
            }

            try
            {
                bool exists = _inner.FileExists(path);
                _session.RecordProbe(path, EvaluationPathKind.File, exists);
                return exists;
            }
            catch (Exception ex) when (!ExceptionHandling.IsCriticalException(ex))
            {
                _session.RecordOperationFailure();
                throw;
            }
        }

        public bool FileOrDirectoryExists(string path)
        {
            if (_session.IsCompleted)
            {
                return _inner.FileOrDirectoryExists(path);
            }

            try
            {
                bool exists = _inner.FileOrDirectoryExists(path);
                _session.RecordProbe(path, EvaluationPathKind.FileOrDirectory, exists);
                return exists;
            }
            catch (Exception ex) when (!ExceptionHandling.IsCriticalException(ex))
            {
                _session.RecordOperationFailure();
                throw;
            }
        }

        private IEnumerable<string> RecordEnumeration(
            string path,
            string searchPattern,
            SearchOption searchOption,
            EvaluationEnumerationKind kind,
            Func<IFileSystem, string, string, SearchOption, IEnumerable<string>> enumerate)
        {
            IEnumerable<string> entries;
            try
            {
                entries = enumerate(_inner, path, searchPattern, searchOption);
            }
            catch (Exception ex) when (!ExceptionHandling.IsCriticalException(ex))
            {
                _session.RecordEnumeration(
                    path,
                    searchPattern,
                    searchOption,
                    kind,
                    [],
                    EvaluationEnumerationCompletion.Failure);
                throw;
            }

            return RecordEnumerationIterator(path, searchPattern, searchOption, kind, entries);
        }

        private IEnumerable<string> RecordEnumerationIterator(
            string path,
            string searchPattern,
            SearchOption searchOption,
            EvaluationEnumerationKind kind,
            IEnumerable<string> entries)
        {
            var observedEntries = new List<string>();
            EvaluationEnumerationCompletion completion = EvaluationEnumerationCompletion.Partial;
            IEnumerator<string> enumerator = null;

            try
            {
                try
                {
                    enumerator = entries.GetEnumerator();
                }
                catch (Exception ex) when (!ExceptionHandling.IsCriticalException(ex))
                {
                    completion = EvaluationEnumerationCompletion.Failure;
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
                        throw;
                    }

                    if (!hasNext)
                    {
                        completion = EvaluationEnumerationCompletion.Complete;
                        yield break;
                    }

                    string entry = enumerator.Current;
                    observedEntries.Add(entry);
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
                    throw;
                }
                finally
                {
                    _session.RecordEnumeration(
                        path,
                        searchPattern,
                        searchOption,
                        kind,
                        observedEntries,
                        completion);
                }
            }
        }
    }
}
