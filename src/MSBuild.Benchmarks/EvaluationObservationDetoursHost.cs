// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NETFRAMEWORK && EVALUATION_OBSERVATION_DETOURS
using System.Collections;
using System.Runtime.InteropServices;
using System.Text;
using BuildXL.Processes;
using BuildXL.Utilities.Core;
using static BuildXL.Processes.FileAccessManifest;
using BuildXLFileAccessData = BuildXL.Processes.IDetoursEventListener.FileAccessData;
using BuildXLProcessData = BuildXL.Processes.IDetoursEventListener.ProcessData;

namespace MSBuild.Benchmarks;

internal static class EvaluationObservationDetoursHost
{
    internal const string HostSwitch = "--evaluation-observation-detours-host";
    internal const string DetoursOnlyPathPrefix = "EVALUATION_OBSERVATION_DETOURS_ONLY_PATH|";
    internal const string NativeOnlyPathPrefix = "EVALUATION_OBSERVATION_NATIVE_ONLY_PATH|";

    internal static bool TryRun(List<string> args, out int exitCode)
    {
        if (!args.Remove(HostSwitch))
        {
            exitCode = 0;
            return false;
        }

        string targetExecutable = Decode(TakeValue(args, "--target-executable"));
        string targetArguments = Decode(TakeValue(args, "--target-arguments"));
        string[] comparisonRoots = Decode(TakeValue(args, "--comparison-roots"))
            .Split(['\n'], StringSplitOptions.RemoveEmptyEntries);
        string measurementRoot = Decode(TakeValue(args, "--measurement-root"));
        bool includeNativeOnlyPaths = bool.Parse(
            TakeValue(args, "--include-native-only-paths"));
        string resultFile = Decode(TakeValue(args, "--result-file"));

        if (args.Count != 0)
        {
            throw new ArgumentException($"Unexpected Detours host arguments: {string.Join(" ", args)}");
        }

        exitCode = Run(
            targetExecutable,
            targetArguments,
            comparisonRoots,
            measurementRoot,
            includeNativeOnlyPaths,
            resultFile);
        return true;
    }

    private static int Run(
        string targetExecutable,
        string targetArguments,
        IReadOnlyList<string> comparisonRoots,
        string measurementRoot,
        bool includeNativeOnlyPaths,
        string resultFile)
    {
        if (RuntimeInformation.ProcessArchitecture != Architecture.X64)
        {
            throw new PlatformNotSupportedException(
                $"The Detours broker requires x64, but is running as {RuntimeInformation.ProcessArchitecture}.");
        }

        string hostResultFile = Path.GetTempFileName();
        try
        {
            File.Delete(hostResultFile);
            var listener = new DetoursEventListener(comparisonRoots, measurementRoot);
            listener.SetMessageHandlingFlags(
                MessageHandlingFlags.DebugMessageNotify |
                MessageHandlingFlags.FileAccessNotify |
                MessageHandlingFlags.ProcessDataNotify |
                MessageHandlingFlags.ProcessDetoursStatusNotify);

            SandboxedProcessInfo info = CreateProcessInfo(
                targetExecutable,
                string.Concat(
                    targetArguments,
                    " --result-file ",
                    EvaluationObservationBenchmarkProcess.Quote(hostResultFile)),
                listener);

            using ISandboxedProcess sandboxedProcess =
                SandboxedProcessFactory.StartAsync(info, forceSandboxing: false).GetAwaiter().GetResult();
            SandboxedProcessResult processResult = sandboxedProcess.GetResultAsync().GetAwaiter().GetResult();

            ValidateResult(processResult, listener);
            if (!File.Exists(hostResultFile))
            {
                throw new InvalidOperationException("The detoured evaluation host did not produce a result file.");
            }

            string resultContent = File.ReadAllText(hostResultFile);
            EvaluationObservationBenchmarkResult hostResult =
                EvaluationObservationBenchmarkResult.Parse(resultContent);
            HashSet<string> nativePaths = listener.FilterToComparisonRoots(
                EvaluationObservationNativeMetrics.ParsePaths(resultContent));
            HashSet<string> detoursPaths = listener.GetUniquePaths();
            if (hostResult.NativeReports > 0 && nativePaths.Count == 0)
            {
                throw new InvalidOperationException(
                    "Native observation reports were produced without any sampled filesystem paths.");
            }

            bool comparisonAvailable = hostResult.NativeReports > 0;
            int overlap = 0;
            if (comparisonAvailable)
            {
                foreach (string path in nativePaths)
                {
                    if (detoursPaths.Contains(path))
                    {
                        overlap++;
                    }
                }
            }

            EvaluationObservationBenchmarkResult result = new()
            {
                EvaluationTicks = hostResult.EvaluationTicks,
                AllocatedManagedBytes = hostResult.AllocatedManagedBytes,
                RetainedManagedBytes = hostResult.RetainedManagedBytes,
                PrivateBytes = hostResult.PrivateBytes,
                PeakWorkingSetBytes = hostResult.PeakWorkingSetBytes,
                Gen0Collections = hostResult.Gen0Collections,
                Gen1Collections = hostResult.Gen1Collections,
                Gen2Collections = hostResult.Gen2Collections,
                NativeReports = hostResult.NativeReports,
                NativePathProbes = hostResult.NativePathProbes,
                NativeEnumerations = hostResult.NativeEnumerations,
                NativeMetadataReads = hostResult.NativeMetadataReads,
                NativeFileReads = hostResult.NativeFileReads,
                NativeSemanticObservations = hostResult.NativeSemanticObservations,
                NativeUniquePaths = nativePaths.Count,
                SemanticComparisons = hostResult.SemanticComparisons,
                SemanticImports = hostResult.SemanticImports,
                SemanticProperties = hostResult.SemanticProperties,
                SemanticItems = hostResult.SemanticItems,
                SemanticMetadata = hostResult.SemanticMetadata,
                DetoursAccesses = listener.AccessCount,
                DetoursUniquePaths = detoursPaths.Count,
                NativeDetoursOverlap = comparisonAvailable ? overlap : -1,
                NativeOnlyPaths = comparisonAvailable ? nativePaths.Count - overlap : -1,
                DetoursOnlyPaths = comparisonAvailable ? detoursPaths.Count - overlap : -1,
            };

            StringBuilder serializedResult = new(result.Serialize());
            if (comparisonAvailable)
            {
                if (includeNativeOnlyPaths)
                {
                    foreach (string path in nativePaths.OrderBy(
                        static path => path,
                        StringComparer.OrdinalIgnoreCase))
                    {
                        if (detoursPaths.Contains(path))
                        {
                            continue;
                        }

                        serializedResult.AppendLine();
                        serializedResult.Append(NativeOnlyPathPrefix);
                        serializedResult.Append(EvaluationObservationDetoursRunner.Encode(path));
                    }

                    using StringReader reader = new(resultContent);
                    while (reader.ReadLine() is { } line)
                    {
                        if (!line.StartsWith(
                                EvaluationObservationBenchmarkProtocol.NativeEnumerationPrefix,
                                StringComparison.Ordinal) &&
                            !line.StartsWith(
                                EvaluationObservationBenchmarkProtocol.NativeGlobPrefix,
                                StringComparison.Ordinal))
                        {
                            continue;
                        }

                        serializedResult.AppendLine();
                        serializedResult.Append(line);
                    }
                }

                foreach (string path in detoursPaths.OrderBy(static path => path, StringComparer.OrdinalIgnoreCase))
                {
                    if (nativePaths.Contains(path))
                    {
                        continue;
                    }

                    serializedResult.AppendLine();
                    serializedResult.Append(DetoursOnlyPathPrefix);
                    serializedResult.Append(EvaluationObservationDetoursRunner.Encode(path));
                }
            }

            File.WriteAllText(resultFile, serializedResult.ToString());
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
        finally
        {
            if (File.Exists(hostResultFile))
            {
                File.Delete(hostResultFile);
            }
        }
    }

    private static void ValidateResult(
        SandboxedProcessResult result,
        DetoursEventListener listener)
    {
        if (result.ExitCode != 0 ||
            result.Killed ||
            result.TimedOut ||
            result.HasDetoursInjectionFailures ||
            result.MessageProcessingFailure is not null ||
            !listener.StartMarkerObserved ||
            !listener.StopMarkerObserved ||
            listener.NormalizationFailure is not null ||
            listener.AccessCount == 0)
        {
            string standardError = result.StandardError?.ReadValueAsync().GetAwaiter().GetResult() ?? string.Empty;
            throw new InvalidOperationException(
                $"Detours observation failed. ExitCode={result.ExitCode}, Killed={result.Killed}, " +
                $"TimedOut={result.TimedOut}, InjectionFailures={result.HasDetoursInjectionFailures}, " +
                $"MessageFailure={result.MessageProcessingFailure is not null}, " +
                $"StartMarker={listener.StartMarkerObserved}, StopMarker={listener.StopMarkerObserved}, " +
                $"Accesses={listener.AccessCount}, NormalizationFailure={listener.NormalizationFailure is not null}." +
                $"{Environment.NewLine}{listener.NormalizationFailure}{Environment.NewLine}{standardError}");
        }
    }

    private static SandboxedProcessInfo CreateProcessInfo(
        string executable,
        string arguments,
        DetoursEventListener listener)
    {
        SandboxedProcessInfo info = new(
            fileStorage: null,
            fileName: executable,
            disableConHostSharing: false,
            detoursEventListener: listener,
            createJobObjectForCurrentProcess: false)
        {
            SandboxKind = SandboxKind.Default,
            PipDescription = "MSBuild evaluation observation benchmark",
            PipSemiStableHash = 0,
            Arguments = arguments,
            EnvironmentVariables = CreateEnvironmentVariables(),
            MaxLengthInMemory = 0,
        };

        info.FileAccessManifest.AddScope(
            AbsolutePath.Invalid,
            FileAccessPolicy.MaskNothing,
            FileAccessPolicy.AllowAll | FileAccessPolicy.ReportAccess);
        info.FileAccessManifest.MonitorChildProcesses = true;
        info.FileAccessManifest.IgnoreReparsePoints = true;
        info.FileAccessManifest.UseExtraThreadToDrainNtClose = false;
        info.FileAccessManifest.UseLargeNtClosePreallocatedList = true;
        info.FileAccessManifest.LogProcessData = true;
        info.FileAccessManifest.ReportProcessArgs = true;
        info.FileAccessManifest.NormalizeReadTimestamps = false;
        info.NestedProcessTerminationTimeout = TimeSpan.Zero;

        return info;
    }

    private static BuildParameters.IBuildParameters CreateEnvironmentVariables()
    {
        Dictionary<string, string> variables = new(StringComparer.OrdinalIgnoreCase);
        foreach (DictionaryEntry variable in Environment.GetEnvironmentVariables())
        {
            variables[(string)variable.Key] = (string)variable.Value;
        }

        return BuildParameters.GetFactory().PopulateFromDictionary(variables);
    }

    private static string Decode(string value) =>
        Encoding.UTF8.GetString(Convert.FromBase64String(value));

    private static string TakeValue(List<string> args, string name)
    {
        int index = args.IndexOf(name);
        if (index < 0 || index + 1 >= args.Count)
        {
            throw new ArgumentException($"Missing required Detours host argument '{name}'.");
        }

        string value = args[index + 1];
        args.RemoveAt(index + 1);
        args.RemoveAt(index);
        return value;
    }

    private sealed class DetoursEventListener : IDetoursEventListener
    {
        private readonly string[] _comparisonRoots;
        private readonly string[] _comparisonRootPrefixes;
        private readonly string _startMarker;
        private readonly string _stopMarker;
        private readonly HashSet<string> _uniquePaths = new(StringComparer.OrdinalIgnoreCase);
        private int _accessCount;
        private int _counting;
        private int _startMarkerObserved;
        private int _stopMarkerObserved;
        private string? _normalizationFailure;

        internal DetoursEventListener(
            IReadOnlyList<string> comparisonRoots,
            string measurementRoot)
        {
            string fullMeasurementRoot = Path.GetFullPath(measurementRoot);
            _comparisonRoots = new string[comparisonRoots.Count];
            _comparisonRootPrefixes = new string[comparisonRoots.Count];
            for (int i = 0; i < comparisonRoots.Count; i++)
            {
                string fullRoot = Path.GetFullPath(comparisonRoots[i]);
                _comparisonRoots[i] = fullRoot;
                _comparisonRootPrefixes[i] = EnsureTrailingDirectorySeparator(fullRoot);
            }

            _startMarker = Path.Combine(fullMeasurementRoot, EvaluationObservationBenchmarkProtocol.MeasurementStartMarker);
            _stopMarker = Path.Combine(fullMeasurementRoot, EvaluationObservationBenchmarkProtocol.MeasurementStopMarker);
        }

        internal int AccessCount => Volatile.Read(ref _accessCount);
        internal bool StartMarkerObserved => Volatile.Read(ref _startMarkerObserved) != 0;
        internal bool StopMarkerObserved => Volatile.Read(ref _stopMarkerObserved) != 0;
        internal string? NormalizationFailure => Volatile.Read(ref _normalizationFailure);

        internal HashSet<string> GetUniquePaths()
        {
            lock (_uniquePaths)
            {
                return new HashSet<string>(_uniquePaths, StringComparer.OrdinalIgnoreCase);
            }
        }

        internal HashSet<string> FilterToComparisonRoots(IEnumerable<string> paths)
        {
            HashSet<string> result = new(StringComparer.OrdinalIgnoreCase);
            foreach (string path in paths)
            {
                string? fullPath = TryNormalizePath(path, out bool ignored, out string? failure);
                if (fullPath is null)
                {
                    throw new InvalidOperationException(
                        ignored
                            ? $"Native observation unexpectedly reported pseudo-filesystem path '{path}'."
                            : failure);
                }

                if (IsUnderComparisonRoot(fullPath))
                {
                    result.Add(fullPath);
                }
            }

            return result;
        }

        public override void HandleDebugMessage(DebugData debugData)
        {
        }

        public override void HandleFileAccess(BuildXLFileAccessData fileAccessData)
        {
            string? fullPath = TryNormalizePath(
                fileAccessData.Path,
                out bool ignored,
                out string? failure);
            if (fullPath is null)
            {
                if (!ignored &&
                    failure is not null &&
                    Volatile.Read(ref _counting) != 0)
                {
                    Interlocked.CompareExchange(
                        ref _normalizationFailure,
                        failure,
                        comparand: null);
                }

                return;
            }

            if (string.Equals(fullPath, _startMarker, StringComparison.OrdinalIgnoreCase))
            {
                Volatile.Write(ref _startMarkerObserved, 1);
                Volatile.Write(ref _counting, 1);
                return;
            }

            if (string.Equals(fullPath, _stopMarker, StringComparison.OrdinalIgnoreCase))
            {
                Volatile.Write(ref _stopMarkerObserved, 1);
                Volatile.Write(ref _counting, 0);
                return;
            }

            if (Volatile.Read(ref _counting) == 0 || !IsUnderComparisonRoot(fullPath))
            {
                return;
            }

            Interlocked.Increment(ref _accessCount);
            lock (_uniquePaths)
            {
                _uniquePaths.Add(fullPath);
            }
        }

        public override void HandleProcessData(BuildXLProcessData processData)
        {
        }

        public override void HandleProcessDetouringStatus(ProcessDetouringStatusData data)
        {
        }

        private bool IsUnderComparisonRoot(string fullPath)
        {
            for (int i = 0; i < _comparisonRoots.Length; i++)
            {
                if (string.Equals(
                        fullPath,
                        _comparisonRoots[i],
                        StringComparison.OrdinalIgnoreCase) ||
                    fullPath.StartsWith(
                        _comparisonRootPrefixes[i],
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static string? TryNormalizePath(
            string path,
            out bool ignored,
            out string? failure)
        {
            ignored = false;
            failure = null;
            if (path.StartsWith(@"\\.\pipe\", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith(@"\\?\pipe\", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith(@"\??\pipe\", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith(@"\Device\NamedPipe\", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith(@"\Device\Mailslot\", StringComparison.OrdinalIgnoreCase))
            {
                ignored = true;
                return null;
            }

            if (path.StartsWith(@"\??\GLOBALROOT\", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith(@"\\?\GLOBALROOT\", StringComparison.OrdinalIgnoreCase))
            {
                failure = $"Unsupported GLOBALROOT path '{path}'.";
                return null;
            }

            if (path.StartsWith(@"\??\UNC\", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase))
            {
                path = @"\\" + path.Substring(8);
            }
            else if (path.StartsWith(@"\??\", StringComparison.OrdinalIgnoreCase) ||
                     path.StartsWith(@"\\?\", StringComparison.OrdinalIgnoreCase) ||
                     path.StartsWith(@"\\.\", StringComparison.OrdinalIgnoreCase))
            {
                path = path.Substring(4);
            }

            if (path.StartsWith(@"\Device\", StringComparison.OrdinalIgnoreCase))
            {
                failure = $"Unsupported device path '{path}'.";
                return null;
            }

            if (!IsFullyQualifiedDosPath(path))
            {
                failure = $"Path '{path}' is not a fully qualified DOS or UNC path.";
                return null;
            }

            try
            {
                path = Path.GetFullPath(path.Replace('/', '\\'));
                string root = Path.GetPathRoot(path);
                return string.Equals(path, root, StringComparison.OrdinalIgnoreCase)
                    ? path
                    : path.TrimEnd('\\');
            }
            catch (Exception exception)
            {
                failure = $"Could not normalize '{path}': {exception.GetType().Name}: {exception.Message}";
                return null;
            }
        }

        private static bool IsFullyQualifiedDosPath(string path) =>
            path.StartsWith(@"\\", StringComparison.Ordinal) ||
            (path.Length >= 3 &&
             char.IsLetter(path[0]) &&
             path[1] == ':' &&
             (path[2] == '\\' || path[2] == '/'));

        private static string EnsureTrailingDirectorySeparator(string path)
        {
            return path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                ? path
                : string.Concat(path, Path.DirectorySeparatorChar);
        }
    }
}
#else
namespace MSBuild.Benchmarks;

internal static class EvaluationObservationDetoursHost
{
    internal const string HostSwitch = "--evaluation-observation-detours-host";

    internal static bool TryRun(List<string> args, out int exitCode)
    {
        exitCode = 0;
        return false;
    }
}
#endif
