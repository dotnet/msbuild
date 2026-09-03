// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace MSBuild.Benchmarks;

internal static class EvaluationObservationDetoursRunner
{
#if NETFRAMEWORK && EVALUATION_OBSERVATION_DETOURS
    private const int BrokerTimeoutMilliseconds = 120_000;
    private static int s_reportedPathDiff;
#endif

    internal static EvaluationObservationBenchmarkResult Run(
        string executable,
        string arguments,
        IReadOnlyList<string> comparisonRoots,
        string measurementRoot,
        bool includeNativeOnlyPaths)
    {
#if NETFRAMEWORK && EVALUATION_OBSERVATION_DETOURS
        if (RuntimeInformation.ProcessArchitecture != Architecture.X64)
        {
            throw new PlatformNotSupportedException(
                $"The Detours observer benchmark requires x64, but is running as {RuntimeInformation.ProcessArchitecture}.");
        }

        string resultFile = Path.GetTempFileName();
        try
        {
            File.Delete(resultFile);
            string brokerArguments = string.Join(
                " ",
                EvaluationObservationDetoursHost.HostSwitch,
                "--target-executable",
                Encode(executable),
                "--target-arguments",
                Encode(arguments),
                "--comparison-roots",
                Encode(string.Join("\n", comparisonRoots)),
                "--measurement-root",
                Encode(measurementRoot),
                "--include-native-only-paths",
                includeNativeOnlyPaths.ToString(),
                "--result-file",
                Encode(resultFile));

            ProcessStartInfo startInfo = new(executable, brokerArguments)
            {
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
            };

            using Process process = Process.Start(startInfo) ??
                throw new InvalidOperationException($"Could not start Detours benchmark broker '{executable}'.");
            Task<string> standardOutput = process.StandardOutput.ReadToEndAsync();
            Task<string> standardError = process.StandardError.ReadToEndAsync();

            if (!process.WaitForExit(BrokerTimeoutMilliseconds))
            {
                process.Kill();
                process.WaitForExit();
                throw new TimeoutException($"Detours benchmark broker exceeded {BrokerTimeoutMilliseconds} ms.");
            }

            Task.WaitAll(standardOutput, standardError);
            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"Detours benchmark broker exited with code {process.ExitCode}.{Environment.NewLine}" +
                    $"{standardOutput.Result}{Environment.NewLine}{standardError.Result}");
            }

            if (!File.Exists(resultFile))
            {
                throw new InvalidOperationException(
                    $"Detours benchmark broker did not produce a result.{Environment.NewLine}" +
                    $"{standardOutput.Result}{Environment.NewLine}{standardError.Result}");
            }

            string resultContent = File.ReadAllText(resultFile);
            if (Interlocked.Exchange(ref s_reportedPathDiff, 1) == 0)
            {
                WritePathDiff(resultContent);
            }

            return EvaluationObservationBenchmarkResult.Parse(resultContent);
        }
        finally
        {
            if (File.Exists(resultFile))
            {
                File.Delete(resultFile);
            }
        }
#else
        throw new PlatformNotSupportedException("The Detours observer benchmark requires .NET Framework on Windows.");
#endif
    }

    internal static string Encode(string value) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(value));

#if NETFRAMEWORK && EVALUATION_OBSERVATION_DETOURS
    private static void WritePathDiff(string content)
    {
        using StringReader reader = new(content);
        while (reader.ReadLine() is { } line)
        {
            string outputPrefix;
            string encodedPath;
            if (line.StartsWith(
                EvaluationObservationDetoursHost.DetoursOnlyPathPrefix,
                StringComparison.Ordinal))
            {
                outputPrefix = EvaluationObservationDetoursHost.DetoursOnlyPathPrefix;
                encodedPath = line.Substring(outputPrefix.Length);
            }
            else if (line.StartsWith(
                EvaluationObservationDetoursHost.NativeOnlyPathPrefix,
                StringComparison.Ordinal))
            {
                outputPrefix = EvaluationObservationDetoursHost.NativeOnlyPathPrefix;
                encodedPath = line.Substring(outputPrefix.Length);
            }
            else if (line.StartsWith(
                EvaluationObservationBenchmarkProtocol.NativeEnumerationPrefix,
                StringComparison.Ordinal))
            {
                outputPrefix = EvaluationObservationBenchmarkProtocol.NativeEnumerationPrefix;
                encodedPath = line.Substring(outputPrefix.Length);
            }
            else if (line.StartsWith(
                EvaluationObservationBenchmarkProtocol.NativeGlobPrefix,
                StringComparison.Ordinal))
            {
                outputPrefix = EvaluationObservationBenchmarkProtocol.NativeGlobPrefix;
                encodedPath = line.Substring(outputPrefix.Length);
            }
            else
            {
                continue;
            }

            string path = Encoding.UTF8.GetString(Convert.FromBase64String(encodedPath));
            Console.WriteLine(string.Concat(outputPrefix, path));
        }
    }
#endif
}
