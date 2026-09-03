// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Globalization;
using Microsoft.Build.Collections;
using Microsoft.Build.Definition;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

namespace MSBuild.Benchmarks;

internal static class EvaluationObservationBenchmarkHost
{
    private const string HostSwitch = "--evaluation-observation-host";

    internal static bool TryRun(List<string> args, out int exitCode)
    {
        if (!args.Remove(HostSwitch))
        {
            exitCode = 0;
            return false;
        }

        string projectPath = TakeValue(args, "--project");
        int iterations = int.Parse(TakeValue(args, "--iterations"), CultureInfo.InvariantCulture);
        EvaluationObservationBenchmarkMode mode = (EvaluationObservationBenchmarkMode)Enum.Parse(
            typeof(EvaluationObservationBenchmarkMode),
            TakeValue(args, "--mode"),
            ignoreCase: false);
        EvaluationObservationBenchmarkScenario scenario =
            (EvaluationObservationBenchmarkScenario)Enum.Parse(
                typeof(EvaluationObservationBenchmarkScenario),
                TakeValue(args, "--scenario"),
                ignoreCase: false);
        string measurementRoot = TakeValue(args, "--measurement-root");
        Dictionary<string, string> globalProperties = TakeGlobalProperties(args);
        string? resultFile = TryTakeValue(args, "--result-file");

        if (args.Count != 0)
        {
            throw new ArgumentException($"Unexpected benchmark host arguments: {string.Join(" ", args)}");
        }

        bool externalProject = scenario == EvaluationObservationBenchmarkScenario.ExternalProject;
        ValidateIndependentEvaluationEnvironment();
        bool nativeEnabled = (mode & EvaluationObservationBenchmarkMode.Native) != 0;
        bool retainObservationDetails = (mode & EvaluationObservationBenchmarkMode.Detours) != 0;
        EvaluationObservationSemanticSummary semanticSummary = default;
        if (!externalProject)
        {
            EvaluationObservationSemanticSnapshot.ValidateDifferenceDetection();
            semanticSummary = VerifySemanticEquivalence(
                projectPath,
                scenario,
                globalProperties,
                retainObservationDetails);
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        long managedMemoryBefore = GC.GetTotalMemory(forceFullCollection: false);
#if !NETFRAMEWORK
        long allocatedBytesBefore = GC.GetTotalAllocatedBytes(precise: false);
#endif
        int gen0Before = GC.CollectionCount(0);
        int gen1Before = GC.CollectionCount(1);
        int gen2Before = GC.CollectionCount(2);
        EvaluationObservationNativeMetrics nativeMetrics = new();
        _ = File.Exists(Path.Combine(measurementRoot, EvaluationObservationBenchmarkProtocol.MeasurementStartMarker));
        Stopwatch stopwatch = Stopwatch.StartNew();
        using (EvaluationObservationNativeBridge.Enable(
            nativeEnabled,
            nativeMetrics,
            collectPaths: (mode & EvaluationObservationBenchmarkMode.Detours) != 0))
        {
            for (int i = 0; i < iterations; i++)
            {
                Evaluate(projectPath, scenario, globalProperties);
            }
        }

        stopwatch.Stop();
        _ = File.Exists(Path.Combine(measurementRoot, EvaluationObservationBenchmarkProtocol.MeasurementStopMarker));

        long managedMemoryAfter = GC.GetTotalMemory(forceFullCollection: true);
#if NETFRAMEWORK
        long allocatedManagedBytes = 0;
#else
        long allocatedManagedBytes = GC.GetTotalAllocatedBytes(precise: false) - allocatedBytesBefore;
#endif
        using Process process = Process.GetCurrentProcess();
        process.Refresh();

        EvaluationObservationBenchmarkResult result = new()
        {
            EvaluationTicks = stopwatch.ElapsedTicks,
            AllocatedManagedBytes = allocatedManagedBytes,
            RetainedManagedBytes = Math.Max(0, managedMemoryAfter - managedMemoryBefore),
            PrivateBytes = process.PrivateMemorySize64,
            PeakWorkingSetBytes = process.PeakWorkingSet64,
            Gen0Collections = GC.CollectionCount(0) - gen0Before,
            Gen1Collections = GC.CollectionCount(1) - gen1Before,
            Gen2Collections = GC.CollectionCount(2) - gen2Before,
            NativeReports = nativeMetrics.Reports,
            NativePathProbes = nativeMetrics.PathProbes,
            NativeEnumerations = nativeMetrics.Enumerations,
            NativeMetadataReads = nativeMetrics.MetadataReads,
            NativeFileReads = nativeMetrics.FileReads,
            NativeSemanticObservations = nativeMetrics.SemanticObservations,
            NativeUniquePaths = nativeMetrics.UniquePathCount,
            SemanticComparisons = semanticSummary.ComparisonCount,
            SemanticImports = semanticSummary.ImportCount,
            SemanticProperties = semanticSummary.PropertyCount,
            SemanticItems = semanticSummary.ItemCount,
            SemanticMetadata = semanticSummary.MetadataCount,
        };

        if (!externalProject && result.SemanticComparisons != 1)
        {
            throw new InvalidOperationException(
                $"The benchmark expected one semantic comparison but observed {result.SemanticComparisons}.");
        }

        if (mode == EvaluationObservationBenchmarkMode.Baseline && result.NativeReports != 0)
        {
            throw new InvalidOperationException("The baseline benchmark unexpectedly produced native observation reports.");
        }

        if (nativeEnabled && result.NativeReports != iterations)
        {
            throw new InvalidOperationException(
                $"The native benchmark expected {iterations} reports but observed {result.NativeReports}.");
        }

        string serializedResult = result.Serialize();
        Console.WriteLine(serializedResult);
        if (resultFile is not null)
        {
            File.WriteAllText(
                resultFile,
                string.Concat(serializedResult, Environment.NewLine, nativeMetrics.SerializePaths()));
        }

        exitCode = 0;
        return true;
    }

    private static EvaluationObservationSemanticSummary VerifySemanticEquivalence(
        string projectPath,
        EvaluationObservationBenchmarkScenario scenario,
        IReadOnlyDictionary<string, string> globalProperties,
        bool retainObservationDetails)
    {
        EvaluationObservationSemanticSnapshot referenceSnapshot;
        using (EvaluationObservationNativeBridge.Enable(enabled: false, metrics: null, collectPaths: false))
        {
            referenceSnapshot = EvaluateAndCapture(projectPath, scenario, globalProperties);
        }

        EvaluationObservationNativeMetrics metrics = new();
        EvaluationObservationSemanticSnapshot observedSnapshot;
        using (EvaluationObservationNativeBridge.Enable(
            enabled: true,
            metrics,
            collectPaths: retainObservationDetails))
        {
            observedSnapshot = EvaluateAndCapture(projectPath, scenario, globalProperties);
        }

        if (metrics.Reports != 1 ||
            (retainObservationDetails && metrics.UniquePathCount == 0))
        {
            throw new InvalidOperationException(
                "Semantic verification did not exercise the expected native observation path.");
        }

        int comparisonCount = 0;
        referenceSnapshot.AssertEquivalent(observedSnapshot);
        comparisonCount++;
        return observedSnapshot.GetSummary(comparisonCount);
    }

    private static void Evaluate(
        string projectPath,
        EvaluationObservationBenchmarkScenario scenario,
        IReadOnlyDictionary<string, string> globalProperties)
    {
        _ = Evaluate(projectPath, scenario, globalProperties, captureSemanticState: false);
    }

    private static EvaluationObservationSemanticSnapshot EvaluateAndCapture(
        string projectPath,
        EvaluationObservationBenchmarkScenario scenario,
        IReadOnlyDictionary<string, string> globalProperties) =>
        Evaluate(projectPath, scenario, globalProperties, captureSemanticState: true)!;

    private static EvaluationObservationSemanticSnapshot? Evaluate(
        string projectPath,
        EvaluationObservationBenchmarkScenario scenario,
        IReadOnlyDictionary<string, string> globalProperties,
        bool captureSemanticState)
    {
        using ProjectCollection collection = new();
        Dictionary<string, string> projectGlobalProperties =
            new(MSBuildNameIgnoreCaseComparer.Default);
        foreach (KeyValuePair<string, string> property in globalProperties)
        {
            projectGlobalProperties.Add(property.Key, property.Value);
        }

        ProjectInstance project = ProjectInstance.FromFile(projectPath, new ProjectOptions
        {
            ProjectCollection = collection,
            GlobalProperties = projectGlobalProperties,
            LoadSettings = ProjectLoadSettings.RecordDuplicateButNotCircularImports,
        });

        bool externalProject = scenario == EvaluationObservationBenchmarkScenario.ExternalProject;
        if (!externalProject &&
            (project.GetPropertyValue("RequestedProperty") != "ImportedValue" ||
             project.GetItems("Compile").Count == 0))
        {
            throw new InvalidOperationException("Evaluation benchmark project produced unexpected state.");
        }

        string importedEnvironment = project.GetPropertyValue("ImportedEnvironment");
        if (!externalProject && scenario == EvaluationObservationBenchmarkScenario.AmbientAndSdk)
        {
            if (importedEnvironment != "benchmark-environment-value" ||
                project.GetPropertyValue("LiveEnvironment") != importedEnvironment ||
                project.GetPropertyValue("Settings") != "settings-value" ||
                string.IsNullOrEmpty(project.GetPropertyValue("Above")) ||
                project.GetPropertyValue("Volatile") != "Utc")
            {
                throw new InvalidOperationException("Ambient evaluation benchmark project produced unexpected state.");
            }
        }
        else if (!externalProject && importedEnvironment.Length != 0)
        {
            throw new InvalidOperationException("Non-ambient evaluation benchmark unexpectedly imported ambient state.");
        }

        if (captureSemanticState && !externalProject)
        {
            ValidateSemanticFixture(project, projectPath, scenario);
            ValidateOrderedItems(project.GetItems("Ordered"));
        }

        if (!captureSemanticState)
        {
            return null;
        }

        EvaluationObservationSemanticSnapshot snapshot =
            EvaluationObservationSemanticSnapshot.Capture(project);
        ValidateSnapshotCardinality(project, scenario, snapshot.GetSummary());
        return snapshot;
    }

    private static Dictionary<string, string> TakeGlobalProperties(List<string> args)
    {
        Dictionary<string, string> properties = new(MSBuildNameIgnoreCaseComparer.Default);
        int index;
        while ((index = args.IndexOf("--global-property")) >= 0)
        {
            if (index + 1 >= args.Count)
            {
                throw new ArgumentException("Missing value for '--global-property'.");
            }

            string assignment = args[index + 1];
            args.RemoveAt(index + 1);
            args.RemoveAt(index);
            int separator = assignment.IndexOf('=');
            if (separator <= 0)
            {
                throw new ArgumentException(
                    $"Global property '{assignment}' must use the form Name=Value.");
            }

            properties.Add(
                assignment.Substring(0, separator),
                assignment.Substring(separator + 1));
        }

        return properties;
    }

    private static void ValidateSemanticFixture(
        ProjectInstance project,
        string projectPath,
        EvaluationObservationBenchmarkScenario scenario)
    {
        IReadOnlyList<string> imports = project.ImportPathsIncludingDuplicates;
        string importedProjectPath = Path.Combine(Path.GetDirectoryName(projectPath)!, "imported.props");
        int importedProjectCount = 0;
        for (int i = 0; i < imports.Count; i++)
        {
            if (string.Equals(imports[i], importedProjectPath, FileUtilities.PathComparison))
            {
                importedProjectCount++;
            }
        }

        int expectedImportCount =
            scenario == EvaluationObservationBenchmarkScenario.AmbientAndSdk ? 4 : 2;
        if (imports.Count != expectedImportCount || importedProjectCount != 2)
        {
            throw new InvalidOperationException(
                $"Evaluation benchmark expected {expectedImportCount} imports including two duplicate fixture imports, " +
                $"but observed {imports.Count} imports including {importedProjectCount} fixture imports.");
        }

        ProjectPropertyInstance? escapedProperty = project.GetProperty("EscapedProperty");
        ICollection<ProjectItemInstance> escapedItems = project.GetItems("Escaped");
        if (escapedProperty is null ||
            ((IProperty)escapedProperty).EvaluatedValueEscaped != "property%3Bvalue" ||
            escapedItems.Count != 1)
        {
            throw new InvalidOperationException(
                "Evaluation benchmark did not preserve the escaped property or item fixture.");
        }

        foreach (ProjectItemInstance escapedItem in escapedItems)
        {
            if (((IItem)escapedItem).EvaluatedIncludeEscaped != "semi%3Bcolon" ||
                ((IItem)escapedItem).GetMetadataValueEscaped("EscapedMetadata") != "metadata%3Bvalue")
            {
                throw new InvalidOperationException(
                    "Evaluation benchmark did not preserve escaped item or metadata values.");
            }
        }
    }

    private static void ValidateSnapshotCardinality(
        ProjectInstance project,
        EvaluationObservationBenchmarkScenario scenario,
        EvaluationObservationSemanticSummary summary)
    {
        int metadataCount = 0;
        foreach (ProjectItemInstance item in project.Items)
        {
            foreach (ProjectMetadataInstance _ in item.Metadata)
            {
                metadataCount++;
            }
        }

        int expectedImportCount =
            scenario == EvaluationObservationBenchmarkScenario.AmbientAndSdk ? 4 : 2;
        if (summary.ImportCount != expectedImportCount ||
            summary.PropertyCount == 0 ||
            summary.ItemCount < 5 ||
            summary.MetadataCount < 10 ||
            summary.ImportCount != project.ImportPathsIncludingDuplicates.Count ||
            summary.PropertyCount != project.Properties.Count ||
            summary.ItemCount != project.Items.Count ||
            summary.MetadataCount != metadataCount)
        {
            throw new InvalidOperationException(
                "Semantic snapshot cardinalities did not match the evaluated project.");
        }
    }

    private static void ValidateOrderedItems(ICollection<ProjectItemInstance> orderedItems)
    {
        int index = 0;
        foreach (ProjectItemInstance item in orderedItems)
        {
            string expectedInclude = index == 1 ? "second" : "first";
            string expectedPosition = (index + 1).ToString(CultureInfo.InvariantCulture);
            string expectedOverride = index == 1 ? "item" : "default";
            if (index >= 3 ||
                item.EvaluatedInclude != expectedInclude ||
                item.GetMetadataValue("Position") != expectedPosition ||
                item.GetMetadataValue("Inherited") != "definition" ||
                item.GetMetadataValue("Override") != expectedOverride)
            {
                throw new InvalidOperationException(
                    "Evaluation benchmark project did not preserve item order, duplicates, or metadata.");
            }

            index++;
        }

        if (index != 3)
        {
            throw new InvalidOperationException(
                "Evaluation benchmark project did not preserve item order, duplicates, or metadata.");
        }
    }

    private static void ValidateIndependentEvaluationEnvironment()
    {
        if (Traits.Instance.CacheFileExistence ||
            Traits.Instance.MSBuildCacheFileEnumerations)
        {
            throw new InvalidOperationException(
                "Semantic verification requires the process-wide MsBuildCacheFileExistence and " +
                "MsBuildCacheFileEnumerations caches to be disabled.");
        }
    }

    private static string TakeValue(List<string> args, string name)
    {
        int index = args.IndexOf(name);
        if (index < 0 || index + 1 >= args.Count)
        {
            throw new ArgumentException($"Missing required benchmark host argument '{name}'.");
        }

        string value = args[index + 1];
        args.RemoveAt(index + 1);
        args.RemoveAt(index);
        return value;
    }

    private static string? TryTakeValue(List<string> args, string name)
    {
        int index = args.IndexOf(name);
        if (index < 0)
        {
            return null;
        }

        if (index + 1 >= args.Count)
        {
            throw new ArgumentException($"Missing value for benchmark host argument '{name}'.");
        }

        string value = args[index + 1];
        args.RemoveAt(index + 1);
        args.RemoveAt(index);
        return value;
    }
}
