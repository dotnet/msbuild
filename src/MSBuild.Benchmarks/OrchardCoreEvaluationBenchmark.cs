// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Microsoft.Build.Definition;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;

namespace MSBuild.Benchmarks;

/// <summary>
/// Measures repeated <c>-getItem</c>/<c>-getProperty</c>-style evaluations of an Orchard Core
/// project. Full evaluation models the behavior before partial evaluation, while the Properties
/// and Items stages model the optimized paths.
/// </summary>
[MemoryDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class OrchardCoreEvaluationBenchmark
{
    internal const string ProjectPathEnvironmentVariable = "MSBUILD_BENCHMARK_ORCHARDCORE_PROJECT";

    private const int EvaluationCount = 100;
    private const string ItemType = "PackageReference";
    private const string PropertyName = "TargetFrameworks";

    private string _projectPath = null!;
    private string _sdkPath = null!;
    private Dictionary<string, string> _toolsetProperties = null!;
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
                $"Specify an Orchard Core project with --orchard-core-project or the {ProjectPathEnvironmentVariable} environment variable.");
        }

        _projectPath = Path.GetFullPath(projectPath);
        if (!File.Exists(_projectPath))
        {
            throw new FileNotFoundException("The Orchard Core benchmark project does not exist.", _projectPath);
        }

        _sdkPath = FindDotNetSdkPath();
        string sdksPath = Path.Combine(_sdkPath, "Sdks");

        _originalMSBuildSDKsPath = Environment.GetEnvironmentVariable("MSBuildSDKsPath");
        _originalMSBuildExtensionsPath = Environment.GetEnvironmentVariable("MSBuildExtensionsPath");
        _originalMSBuildEnableWorkloadResolver = Environment.GetEnvironmentVariable("MSBuildEnableWorkloadResolver");

        try
        {
            Environment.SetEnvironmentVariable("MSBuildSDKsPath", sdksPath);
            Environment.SetEnvironmentVariable("MSBuildExtensionsPath", _sdkPath);
            Environment.SetEnvironmentVariable("MSBuildEnableWorkloadResolver", "false");

            _toolsetProperties = new Dictionary<string, string>
            {
                ["MSBuildExtensionsPath"] = _sdkPath,
                ["MSBuildExtensionsPath32"] = _sdkPath,
                ["MSBuildExtensionsPath64"] = _sdkPath,
                ["MSBuildSDKsPath"] = sdksPath,
            };

            Console.WriteLine($"Using .NET SDK: {_sdkPath}");
            ValidateEvaluationStages();
        }
        catch
        {
            RestoreEnvironment();
            throw;
        }
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        RestoreEnvironment();
    }

    private void RestoreEnvironment()
    {
        Environment.SetEnvironmentVariable("MSBuildSDKsPath", _originalMSBuildSDKsPath);
        Environment.SetEnvironmentVariable("MSBuildExtensionsPath", _originalMSBuildExtensionsPath);
        Environment.SetEnvironmentVariable("MSBuildEnableWorkloadResolver", _originalMSBuildEnableWorkloadResolver);
    }

    [Benchmark(Baseline = true, OperationsPerInvoke = EvaluationCount)]
    [BenchmarkCategory("GetProperty")]
    public int FullEvaluation_GetProperty()
        => EvaluateAndGetProperty(ProjectEvaluationStage.Full);

    [Benchmark(OperationsPerInvoke = EvaluationCount)]
    [BenchmarkCategory("GetProperty")]
    public int PartialEvaluation_GetProperty()
        => EvaluateAndGetProperty(ProjectEvaluationStage.Properties);

    [Benchmark(Baseline = true, OperationsPerInvoke = EvaluationCount)]
    [BenchmarkCategory("GetItems")]
    public int FullEvaluation_GetItems()
        => EvaluateAndGetItems(ProjectEvaluationStage.Full);

    [Benchmark(OperationsPerInvoke = EvaluationCount)]
    [BenchmarkCategory("GetItems")]
    public int PartialEvaluation_GetItems()
        => EvaluateAndGetItems(ProjectEvaluationStage.Items);

    private int EvaluateAndGetItems(ProjectEvaluationStage stage)
    {
        int checksum = 0;
        for (int i = 0; i < EvaluationCount; i++)
        {
            using ProjectCollection collection = CreateProjectCollection();
            ProjectInstance project = Evaluate(stage, collection);
            checksum += project.GetItems(ItemType).Count;
        }

        return checksum;
    }

    private int EvaluateAndGetProperty(ProjectEvaluationStage stage)
    {
        int checksum = 0;
        for (int i = 0; i < EvaluationCount; i++)
        {
            using ProjectCollection collection = CreateProjectCollection();
            ProjectInstance project = Evaluate(stage, collection);
            checksum += project.GetPropertyValue(PropertyName).Length;
        }

        return checksum;
    }

    private ProjectCollection CreateProjectCollection()
    {
        ProjectCollection collection = new();
        collection.RemoveAllToolsets();
        collection.AddToolset(new Toolset("Current", _sdkPath, _toolsetProperties, collection, null));
        collection.DefaultToolsVersion = "Current";
        return collection;
    }

    private ProjectInstance Evaluate(ProjectEvaluationStage stage, ProjectCollection collection)
        => ProjectInstance.FromFile(_projectPath, new ProjectOptions
        {
            EvaluationStage = stage,
            ProjectCollection = collection,
            ToolsVersion = "Current",
        });

    private void ValidateEvaluationStages()
    {
        string fullProperty;
        string[] fullItems;
        using (ProjectCollection collection = CreateProjectCollection())
        {
            ProjectInstance project = Evaluate(ProjectEvaluationStage.Full, collection);
            fullProperty = project.GetPropertyValue(PropertyName);
            fullItems = GetItemSignatures(project);
        }

        if (string.IsNullOrWhiteSpace(fullProperty))
        {
            throw new InvalidOperationException(
                $"The Orchard Core project must define a non-empty {PropertyName} property.");
        }

        if (fullItems.Length == 0)
        {
            throw new InvalidOperationException(
                $"The Orchard Core project must define at least one {ItemType} item.");
        }

        using (ProjectCollection collection = CreateProjectCollection())
        {
            string partialProperty = Evaluate(ProjectEvaluationStage.Properties, collection)
                .GetPropertyValue(PropertyName);
            if (!string.Equals(fullProperty, partialProperty, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Partial property evaluation produced a different {PropertyName} value.");
            }
        }

        using (ProjectCollection collection = CreateProjectCollection())
        {
            string[] partialItems = GetItemSignatures(Evaluate(ProjectEvaluationStage.Items, collection));
            if (!fullItems.SequenceEqual(partialItems, StringComparer.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Partial item evaluation produced different {ItemType} items or metadata.");
            }
        }
    }

    private static string[] GetItemSignatures(ProjectInstance project)
    {
        ICollection<ProjectItemInstance> items = project.GetItems(ItemType);
        var signatures = new string[items.Count];
        int index = 0;
        foreach (ProjectItemInstance item in items)
        {
            var signature = new System.Text.StringBuilder(item.EvaluatedInclude);
            foreach (string metadataName in item.MetadataNames.OrderBy(name => name, StringComparer.OrdinalIgnoreCase))
            {
                signature.Append('\0')
                    .Append(metadataName)
                    .Append('=')
                    .Append(item.GetMetadataValue(metadataName));
            }

            signatures[index++] = signature.ToString();
        }

        return signatures;
    }

    private static string FindDotNetSdkPath()
    {
        string? configuredSdksPath = Environment.GetEnvironmentVariable("MSBuildSDKsPath");
        if (!string.IsNullOrWhiteSpace(configuredSdksPath))
        {
            string normalizedSdksPath = Path.GetFullPath(configuredSdksPath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string configuredSdkPath = Directory.GetParent(normalizedSdksPath)?.FullName
                ?? throw new InvalidOperationException($"Invalid MSBuildSDKsPath: {configuredSdksPath}");

            if (IsSdkPath(configuredSdkPath))
            {
                return configuredSdkPath;
            }
        }

        var dotNetRoots = new List<string>();
        var seenRoots = new HashSet<string>(StringComparer.Ordinal);
        AddDotNetRoot(dotNetRoots, seenRoots, Environment.GetEnvironmentVariable("DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR"));
        AddDotNetRoot(dotNetRoots, seenRoots, Environment.GetEnvironmentVariable("DOTNET_ROOT"));
        AddDotNetRoot(dotNetRoots, seenRoots, Path.GetDirectoryName(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")));
        AddDotNetRoot(dotNetRoots, seenRoots, Path.GetDirectoryName(Environment.ProcessPath));

        string runtimeDirectory = System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory();
        AddDotNetRoot(
            dotNetRoots,
            seenRoots,
            Path.GetFullPath(Path.Combine(runtimeDirectory, "..", "..", "..")));

        string? path = Environment.GetEnvironmentVariable("PATH");
        if (!string.IsNullOrEmpty(path))
        {
            foreach (string pathEntry in path.Split(Path.PathSeparator))
            {
                string trimmedEntry = pathEntry.Trim();
                if (File.Exists(Path.Combine(trimmedEntry, OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet")))
                {
                    AddDotNetRoot(dotNetRoots, seenRoots, trimmedEntry);
                }
            }
        }

        foreach (string dotNetRoot in dotNetRoots)
        {
            string? bestSdk = FindBestSdk(dotNetRoot);
            if (bestSdk is not null)
            {
                return bestSdk;
            }
        }

        throw new InvalidOperationException(
            "Could not locate a .NET SDK. Set MSBuildSDKsPath or run the benchmark with dotnet.");
    }

    private static void AddDotNetRoot(List<string> roots, HashSet<string> seenRoots, string? path)
    {
        if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
        {
            string fullPath = Path.GetFullPath(path);
            if (seenRoots.Add(fullPath))
            {
                roots.Add(fullPath);
            }
        }
    }

    private static string? FindBestSdk(string dotNetRoot)
    {
        string sdkRoot = Path.Combine(dotNetRoot, "sdk");
        if (!Directory.Exists(sdkRoot))
        {
            return null;
        }

        string? bestSdk = null;
        foreach (string candidate in Directory.EnumerateDirectories(sdkRoot))
        {
            if (IsSdkPath(candidate) && (bestSdk is null || CompareSdkVersions(candidate, bestSdk) > 0))
            {
                bestSdk = candidate;
            }
        }

        return bestSdk;
    }

    private static int CompareSdkVersions(string leftPath, string rightPath)
    {
        string left = Path.GetFileName(leftPath);
        string right = Path.GetFileName(rightPath);
        string leftNumeric = left.Split('-')[0];
        string rightNumeric = right.Split('-')[0];

        bool leftParsed = Version.TryParse(leftNumeric, out Version? leftVersion);
        bool rightParsed = Version.TryParse(rightNumeric, out Version? rightVersion);
        if (leftParsed && rightParsed)
        {
            int versionComparison = leftVersion!.CompareTo(rightVersion);
            if (versionComparison != 0)
            {
                return versionComparison;
            }

            bool leftIsPrerelease = left.Length != leftNumeric.Length;
            bool rightIsPrerelease = right.Length != rightNumeric.Length;
            if (leftIsPrerelease != rightIsPrerelease)
            {
                return leftIsPrerelease ? -1 : 1;
            }

            if (leftIsPrerelease)
            {
                int prereleaseComparison = ComparePrerelease(
                    left.Substring(leftNumeric.Length + 1),
                    right.Substring(rightNumeric.Length + 1));
                if (prereleaseComparison != 0)
                {
                    return prereleaseComparison;
                }
            }
        }
        else if (leftParsed != rightParsed)
        {
            return leftParsed ? 1 : -1;
        }

        return string.CompareOrdinal(left, right);
    }

    private static int ComparePrerelease(string left, string right)
    {
        string[] leftParts = left.Split('.');
        string[] rightParts = right.Split('.');
        int commonLength = Math.Min(leftParts.Length, rightParts.Length);
        for (int i = 0; i < commonLength; i++)
        {
            bool leftNumeric = int.TryParse(leftParts[i], out int leftNumber);
            bool rightNumeric = int.TryParse(rightParts[i], out int rightNumber);
            int comparison;
            if (leftNumeric && rightNumeric)
            {
                comparison = leftNumber.CompareTo(rightNumber);
            }
            else if (leftNumeric != rightNumeric)
            {
                comparison = leftNumeric ? -1 : 1;
            }
            else
            {
                comparison = string.CompareOrdinal(leftParts[i], rightParts[i]);
            }

            if (comparison != 0)
            {
                return comparison;
            }
        }

        return leftParts.Length.CompareTo(rightParts.Length);
    }

    private static bool IsSdkPath(string path)
        => File.Exists(Path.Combine(path, "Current", "Microsoft.Common.props")) &&
           File.Exists(Path.Combine(path, "Sdks", "Microsoft.NET.Sdk", "Sdk", "Sdk.props"));
}
