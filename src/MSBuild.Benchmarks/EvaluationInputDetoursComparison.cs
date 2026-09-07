// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if EVALUATION_INPUT_DETOURS
using System.Collections;
using System.Runtime.InteropServices;
using BuildXL.Processes;
using BuildXL.Utilities.Core;
using static BuildXL.Processes.FileAccessManifest;

namespace MSBuild.Benchmarks;

/// <summary>
/// Runs <see cref="EvaluationInputRecordHost"/> under BuildXL Detours and compares every file system path the process
/// touched during evaluation with the paths the recorder wrote down. A touched path is explained when it was recorded, or
/// when it was only probed or enumerated and its parent directory was recorded, since a recorded directory's timestamp covers
/// its membership but not the content of its files. Everything else is printed for attribution, reads separately. Opt-in: build with <c>-p:EnableEvaluationInputDetours=true</c> on Windows x64.
/// </summary>
internal static class EvaluationInputDetoursComparison
{
    internal const string Switch = "--evaluation-input-detours";
    private const string RecordingVariable = "MSBUILDRECORDEVALUATIONINPUTS";

    internal static bool TryRun(List<string> args, out int exitCode)
    {
        if (!args.Remove(Switch))
        {
            exitCode = 0;
            return false;
        }

        string project = Path.GetFullPath(HarnessArguments.Take(args, "--project"));
        Dictionary<string, string> globalProperties = HarnessArguments.TakeGlobalProperties(args);
        HarnessArguments.ExpectEmpty(args);

        if (RuntimeInformation.ProcessArchitecture != Architecture.X64)
        {
            throw new PlatformNotSupportedException($"Detours requires x64, but this process is {RuntimeInformation.ProcessArchitecture}.");
        }

        string resultFile = Path.GetTempFileName();
        try
        {
            var listener = new AccessListener(Path.GetDirectoryName(project)!);
            listener.SetMessageHandlingFlags(
                MessageHandlingFlags.DebugMessageNotify |
                MessageHandlingFlags.FileAccessNotify |
                MessageHandlingFlags.ProcessDataNotify |
                MessageHandlingFlags.ProcessDetoursStatusNotify);

            SandboxedProcessInfo info = CreateProcessInfo(ChildArguments(project, globalProperties, resultFile), listener);
            using ISandboxedProcess process = SandboxedProcessFactory.StartAsync(info, forceSandboxing: false).GetAwaiter().GetResult();
            SandboxedProcessResult processResult = process.GetResultAsync().GetAwaiter().GetResult();
            Validate(processResult, listener);

            Dictionary<string, string> recorded = ReadRecorded(resultFile, out string summary);
            Dictionary<string, bool> touched = listener.Paths;
            int overlap = 0;
            int owned = 0;
            List<string> unexplainedProbes = [];
            List<string> unexplainedReads = [];
            foreach (KeyValuePair<string, bool> access in touched)
            {
                string path = access.Key;
                bool readContent = access.Value;
                if (recorded.ContainsKey(path))
                {
                    overlap++;
                }
                else if (!readContent && recorded.TryGetValue(Path.GetDirectoryName(path) ?? string.Empty, out string? parentKind) && parentKind == "Directory")
                {
                    // A probe or enumeration under a recorded directory is covered by that directory's timestamp; a read is not.
                    owned++;
                }
                else
                {
                    (readContent ? unexplainedReads : unexplainedProbes).Add(path);
                }
            }

            unexplainedProbes.Sort(StringComparer.OrdinalIgnoreCase);
            unexplainedReads.Sort(StringComparer.OrdinalIgnoreCase);
            Console.WriteLine(
                $"EVALUATION_INPUT_DETOURS|Accesses={listener.AccessCount}|TouchedPaths={touched.Count}|RecordedPaths={recorded.Count}" +
                $"|Overlap={overlap}|OwnedByRecordedDirectory={owned}|RecordedOnly={recorded.Count - overlap}" +
                $"|DetoursOnly={unexplainedProbes.Count + unexplainedReads.Count}|DetoursOnlyReads={unexplainedReads.Count}|{summary}");
            foreach (string path in unexplainedProbes)
            {
                Console.WriteLine("DETOURS_ONLY|" + path);
            }

            foreach (string path in unexplainedReads)
            {
                Console.WriteLine("DETOURS_ONLY_READ|" + path);
            }

            foreach (KeyValuePair<string, string> entry in recorded)
            {
                if (!touched.ContainsKey(entry.Key))
                {
                    Console.WriteLine($"RECORDED_ONLY|{entry.Value}|{entry.Key}");
                }
            }

            exitCode = 0;
            return true;
        }
        finally
        {
            File.Delete(resultFile);
        }
    }

    private static string ChildArguments(string project, Dictionary<string, string> globalProperties, string resultFile)
    {
        var arguments = new System.Text.StringBuilder();
        arguments.Append(HarnessArguments.Quote(typeof(EvaluationInputDetoursComparison).Assembly.Location))
            .Append(' ').Append(EvaluationInputRecordHost.Switch)
            .Append(" --project ").Append(HarnessArguments.Quote(project))
            .Append(" --result-file ").Append(HarnessArguments.Quote(resultFile));
        foreach (KeyValuePair<string, string> property in globalProperties)
        {
            arguments.Append(" --global-property ").Append(HarnessArguments.Quote($"{property.Key}={property.Value}"));
        }

        return arguments.ToString();
    }

    private static SandboxedProcessInfo CreateProcessInfo(string arguments, AccessListener listener)
    {
        var info = new SandboxedProcessInfo(
            fileStorage: null,
            fileName: Environment.ProcessPath ?? throw new InvalidOperationException("The host executable path is unknown."),
            disableConHostSharing: false,
            detoursEventListener: listener,
            createJobObjectForCurrentProcess: false)
        {
            SandboxKind = SandboxKind.Default,
            PipDescription = "MSBuild evaluation input recording",
            PipSemiStableHash = 0,
            Arguments = arguments,
            EnvironmentVariables = ChildEnvironment(),
            MaxLengthInMemory = 0,
        };

        info.FileAccessManifest.AddScope(AbsolutePath.Invalid, FileAccessPolicy.MaskNothing, FileAccessPolicy.AllowAll | FileAccessPolicy.ReportAccess);
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

    private static BuildParameters.IBuildParameters ChildEnvironment()
    {
        Dictionary<string, string> variables = new(StringComparer.OrdinalIgnoreCase);
        foreach (DictionaryEntry variable in Environment.GetEnvironmentVariables())
        {
            variables[(string)variable.Key] = (string)variable.Value!;
        }

        variables[RecordingVariable] = "1";
        return BuildParameters.GetFactory().PopulateFromDictionary(variables);
    }

    private static void Validate(SandboxedProcessResult result, AccessListener listener)
    {
        if (result.ExitCode != 0 || result.Killed || result.TimedOut || result.HasDetoursInjectionFailures ||
            result.MessageProcessingFailure is not null || !listener.StartObserved || !listener.StopObserved || listener.AccessCount == 0)
        {
            string standardError = result.StandardError?.ReadValueAsync().GetAwaiter().GetResult() ?? string.Empty;
            string standardOutput = result.StandardOutput?.ReadValueAsync().GetAwaiter().GetResult() ?? string.Empty;
            throw new InvalidOperationException(
                $"Detours run failed. ExitCode={result.ExitCode} Killed={result.Killed} TimedOut={result.TimedOut} " +
                $"InjectionFailures={result.HasDetoursInjectionFailures} MessageFailure={result.MessageProcessingFailure is not null} " +
                $"Start={listener.StartObserved} Stop={listener.StopObserved} Accesses={listener.AccessCount}" +
                $"{Environment.NewLine}{standardOutput}{Environment.NewLine}{standardError}");
        }
    }

    private static Dictionary<string, string> ReadRecorded(string resultFile, out string summary)
    {
        Dictionary<string, string> recorded = new(StringComparer.OrdinalIgnoreCase);
        summary = string.Empty;
        foreach (string line in File.ReadLines(resultFile))
        {
            if (line.StartsWith(EvaluationInputRecordHost.RecordedPrefix, StringComparison.Ordinal))
            {
                string[] parts = line.Substring(EvaluationInputRecordHost.RecordedPrefix.Length).Split('|');
                recorded[EvaluationInputRecordHost.Decode(parts[1])] = parts[0];
            }
            else if (line.StartsWith(EvaluationInputRecordHost.SummaryPrefix, StringComparison.Ordinal))
            {
                summary = line.Substring(EvaluationInputRecordHost.SummaryPrefix.Length);
            }
        }

        if (summary.Length == 0)
        {
            throw new InvalidOperationException("The record host did not write a summary.");
        }

        return recorded;
    }

    /// <summary>
    /// Collects every normalized path the sandboxed process touched between the start and stop marker probes.
    /// </summary>
    private sealed class AccessListener : IDetoursEventListener
    {
        private readonly string _startMarker;
        private readonly string _stopMarker;
        // Path to whether any access read or wrote its content rather than probing or enumerating it.
        private readonly Dictionary<string, bool> _paths = new(StringComparer.OrdinalIgnoreCase);
        private int _accessCount;
        private volatile bool _counting;
        private volatile bool _startObserved;
        private volatile bool _stopObserved;

        internal AccessListener(string measurementRoot)
        {
            _startMarker = Path.Combine(measurementRoot, EvaluationInputRecordHost.StartMarker);
            _stopMarker = Path.Combine(measurementRoot, EvaluationInputRecordHost.StopMarker);
        }

        internal int AccessCount => Volatile.Read(ref _accessCount);

        internal bool StartObserved => _startObserved;

        internal bool StopObserved => _stopObserved;

        internal Dictionary<string, bool> Paths
        {
            get
            {
                lock (_paths)
                {
                    return new Dictionary<string, bool>(_paths, StringComparer.OrdinalIgnoreCase);
                }
            }
        }

        public override void HandleFileAccess(IDetoursEventListener.FileAccessData fileAccessData)
        {
            string? path = Normalize(fileAccessData.Path);
            if (path is null)
            {
                return;
            }

            if (string.Equals(path, _startMarker, StringComparison.OrdinalIgnoreCase))
            {
                _startObserved = true;
                _counting = true;
                return;
            }

            if (string.Equals(path, _stopMarker, StringComparison.OrdinalIgnoreCase))
            {
                _stopObserved = true;
                _counting = false;
                return;
            }

            if (!_counting)
            {
                return;
            }

            bool readContent = (fileAccessData.RequestedAccess & (RequestedAccess.Read | RequestedAccess.Write)) != 0;
            Interlocked.Increment(ref _accessCount);
            lock (_paths)
            {
                _paths[path] = readContent || (_paths.TryGetValue(path, out bool earlier) && earlier);
            }
        }

        public override void HandleDebugMessage(DebugData debugData)
        {
        }

        public override void HandleProcessData(IDetoursEventListener.ProcessData processData)
        {
        }

        public override void HandleProcessDetouringStatus(ProcessDetouringStatusData data)
        {
        }

        /// <summary>
        /// Turns a Detours path into a full DOS path; pipes, devices, and other non-file paths return null.
        /// </summary>
        private static string? Normalize(string path)
        {
            if (path.StartsWith(@"\??\UNC\", StringComparison.OrdinalIgnoreCase) || path.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase))
            {
                path = @"\\" + path.Substring(8);
            }
            else if (path.StartsWith(@"\??\", StringComparison.Ordinal) || path.StartsWith(@"\\?\", StringComparison.Ordinal) || path.StartsWith(@"\\.\", StringComparison.Ordinal))
            {
                path = path.Substring(4);
            }

            bool dosPath = path.Length >= 3 && char.IsLetter(path[0]) && path[1] == ':' && (path[2] == '\\' || path[2] == '/');
            if (!dosPath && !path.StartsWith(@"\\", StringComparison.Ordinal))
            {
                return null;
            }

            try
            {
                path = Path.GetFullPath(path.Replace('/', '\\'));
                return string.Equals(path, Path.GetPathRoot(path), StringComparison.OrdinalIgnoreCase) ? path : path.TrimEnd('\\');
            }
            catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
            {
                return null;
            }
        }
    }
}
#else
namespace MSBuild.Benchmarks;

internal static class EvaluationInputDetoursComparison
{
    internal const string Switch = "--evaluation-input-detours";

    internal static bool TryRun(List<string> args, out int exitCode)
    {
        if (args.Contains(Switch))
        {
            throw new PlatformNotSupportedException("Build with -p:EnableEvaluationInputDetours=true on Windows x64 to use the Detours comparison.");
        }

        exitCode = 0;
        return false;
    }
}
#endif
