// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BenchmarkDotNet.Attributes;
using Microsoft.Build.CommandLine;

namespace MSBuild.OrchardCore.Benchmarks;

/// <summary>
/// Measures repeated <c>-getItem</c> and <c>-getProperty</c> command-line queries on an Orchard
/// Core project. The benchmark deliberately invokes the command-line implementation so the same
/// source measures full evaluation before the partial-evaluation optimization and partial
/// evaluation after it.
/// </summary>
[MemoryDiagnoser]
public class OrchardCoreEvaluationBenchmark
{
    internal const string DotNetSdkPathEnvironmentVariable = "MSBUILD_BENCHMARK_DOTNET_SDK_PATH";
    internal const string ProjectPathEnvironmentVariable = "MSBUILD_BENCHMARK_ORCHARDCORE_PROJECT";

    private const int EvaluationCount = 100;
    private const string ItemType = "PackageReference";
    private const string PropertyName = "TargetFrameworks";

    private string[] _getItemsArguments = null!;
    private string[] _getPropertyArguments = null!;
    private TextWriter _originalOut = null!;
    private TextWriter _originalError = null!;
    private string? _originalMSBuildSDKsPath;
    private string? _originalMSBuildExtensionsPath;
    private string? _originalMSBuildEnableWorkloadResolver;

    [GlobalSetup]
    public void GlobalSetup()
    {
        string? projectPath = Environment.GetEnvironmentVariable(ProjectPathEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(projectPath))
        {
            throw new InvalidOperationException(
                "The Orchard Core project path was not passed to the benchmark process.");
        }

        projectPath = Path.GetFullPath(projectPath);
        if (!File.Exists(projectPath))
        {
            throw new FileNotFoundException("The Orchard Core benchmark project does not exist.", projectPath);
        }

        string? sdkPath = Environment.GetEnvironmentVariable(DotNetSdkPathEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(sdkPath))
        {
            throw new InvalidOperationException(
                "The .NET SDK path was not passed to the benchmark process.");
        }

        string sdksPath = Path.Combine(sdkPath, "Sdks");
        string nuGetTargetsPath = Path.Combine(sdkPath, "NuGet.targets");

        _originalMSBuildSDKsPath = Environment.GetEnvironmentVariable("MSBuildSDKsPath");
        _originalMSBuildExtensionsPath = Environment.GetEnvironmentVariable("MSBuildExtensionsPath");
        _originalMSBuildEnableWorkloadResolver = Environment.GetEnvironmentVariable("MSBuildEnableWorkloadResolver");
        _originalOut = Console.Out;
        _originalError = Console.Error;

        try
        {
            Environment.SetEnvironmentVariable("MSBuildSDKsPath", sdksPath);
            Environment.SetEnvironmentVariable("MSBuildExtensionsPath", sdkPath);
            Environment.SetEnvironmentVariable("MSBuildEnableWorkloadResolver", "false");

            _getPropertyArguments = CreateArguments(
                projectPath,
                $"-getProperty:{PropertyName}",
                nuGetTargetsPath);
            _getItemsArguments = CreateArguments(
                projectPath,
                $"-getItem:{ItemType}",
                nuGetTargetsPath);

            ValidateQuery(_getPropertyArguments, output => !string.IsNullOrWhiteSpace(output));
            ValidateQuery(
                _getItemsArguments,
                output =>
                    output.Contains(ItemType, StringComparison.OrdinalIgnoreCase) &&
                    !output.Contains($"\"{ItemType}\": []", StringComparison.OrdinalIgnoreCase));

            Console.SetOut(TextWriter.Null);
            Console.SetError(TextWriter.Null);
        }
        catch
        {
            RestoreProcessState();
            throw;
        }
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        RestoreProcessState();
    }

    [Benchmark(OperationsPerInvoke = EvaluationCount)]
    public int GetProperty()
        => ExecuteRepeatedly(_getPropertyArguments);

    [Benchmark(OperationsPerInvoke = EvaluationCount)]
    public int GetItems()
        => ExecuteRepeatedly(_getItemsArguments);

    private static string[] CreateArguments(
        string projectPath,
        string query,
        string nuGetTargetsPath)
        =>
        [
            "MSBuild.exe",
            projectPath,
            query,
            "-nologo",
            "-noAutoResponse",
            "-tl:off",
            "-p:MSBuildEnableWorkloadResolver=false",
            $"-p:NuGetRestoreTargets={nuGetTargetsPath}",
        ];

    private static int ExecuteRepeatedly(string[] arguments)
    {
        int checksum = 0;
        for (int i = 0; i < EvaluationCount; i++)
        {
            MSBuildApp.ExitType result = MSBuildApp.Execute(arguments);
            if (result != MSBuildApp.ExitType.Success)
            {
                throw new InvalidOperationException($"MSBuild query failed with exit type {result}.");
            }

            checksum++;
        }

        return checksum;
    }

    private static void ValidateQuery(string[] arguments, Func<string, bool> isExpectedOutput)
    {
        using StringWriter output = new();
        TextWriter originalOut = Console.Out;
        TextWriter originalError = Console.Error;
        Console.SetOut(output);
        Console.SetError(output);
        MSBuildApp.ExitType result;
        try
        {
            result = MSBuildApp.Execute(arguments);
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }

        if (result != MSBuildApp.ExitType.Success)
        {
            throw new InvalidOperationException(
                $"The Orchard Core query failed:{Environment.NewLine}{output}");
        }

        if (!isExpectedOutput(output.ToString()))
        {
            throw new InvalidOperationException(
                "The Orchard Core query did not produce the expected output.");
        }
    }

    private void RestoreProcessState()
    {
        if (_originalOut is not null)
        {
            Console.SetOut(_originalOut);
        }

        if (_originalError is not null)
        {
            Console.SetError(_originalError);
        }

        Environment.SetEnvironmentVariable("MSBuildSDKsPath", _originalMSBuildSDKsPath);
        Environment.SetEnvironmentVariable("MSBuildExtensionsPath", _originalMSBuildExtensionsPath);
        Environment.SetEnvironmentVariable("MSBuildEnableWorkloadResolver", _originalMSBuildEnableWorkloadResolver);
    }
}
