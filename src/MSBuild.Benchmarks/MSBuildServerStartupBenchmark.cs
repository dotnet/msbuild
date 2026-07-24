// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Globalization;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;

namespace MSBuild.Benchmarks;

/// <summary>
/// Measures an end-to-end cold server-backed build. Each invocation uses a unique handshake salt and disables
/// node reuse so it cannot reuse an existing server and the server exits after the build.
/// </summary>
public class MSBuildServerStartupBenchmark
{
    private const string MSBuildPathEnvironmentVariable = "MSBUILD_SERVER_BENCHMARK_PATH";

    private string _msbuildPath = null!;
    private string _projectPath = null!;
    private string _processIdPath = null!;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _msbuildPath = Environment.GetEnvironmentVariable(MSBuildPathEnvironmentVariable)
            ?? throw new InvalidOperationException(
                $"Set {MSBuildPathEnvironmentVariable} to the MSBuild executable produced by the same build as this benchmark.");

        if (!File.Exists(_msbuildPath))
        {
            throw new FileNotFoundException("The MSBuild server benchmark executable was not found.", _msbuildPath);
        }

        _projectPath = Path.Combine(Path.GetTempPath(), $"msbuild-server-benchmark-{Guid.NewGuid():N}.proj");
        _processIdPath = Path.ChangeExtension(_projectPath, ".pid");
        File.WriteAllText(
            _projectPath,
            """
            <Project>
              <Target Name="Build">
                <WriteLinesToFile
                    File="$(BenchmarkProcessIdFile)"
                    Lines="$([System.Diagnostics.Process]::GetCurrentProcess().Id)"
                    Overwrite="true" />
              </Target>
            </Project>
            """);
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        File.Delete(_projectPath);
        File.Delete(_processIdPath);
    }

    [Benchmark]
    public void ColdServerBuild() => RunBuild();

    private void RunBuild()
    {
        File.Delete(_processIdPath);

        ProcessStartInfo startInfo = new()
        {
            FileName = _msbuildPath,
            Arguments = $"\"{_projectPath}\" -nologo -verbosity:quiet -mt -nodeReuse:false -p:BenchmarkProcessIdFile=\"{_processIdPath}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        startInfo.Environment["MSBUILDUSESERVER"] = "1";
        startInfo.Environment["MSBUILDNODEHANDSHAKESALT"] = Guid.NewGuid().ToString("N");
        startInfo.Environment["MSBUILDENABLEALLPROPERTYFUNCTIONS"] = "1";

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start MSBuild.");
        Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
        Task<string> errorTask = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        string output = outputTask.GetAwaiter().GetResult();
        string error = errorTask.GetAwaiter().GetResult();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"MSBuild exited with code {process.ExitCode}.{Environment.NewLine}{output}{Environment.NewLine}{error}");
        }

        int buildProcessId = int.Parse(File.ReadAllText(_processIdPath), CultureInfo.InvariantCulture);
        if (buildProcessId == process.Id)
        {
            throw new InvalidOperationException("MSBuild fell back to an in-process build instead of using the server.");
        }
    }
}
