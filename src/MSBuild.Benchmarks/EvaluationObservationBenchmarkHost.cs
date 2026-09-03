// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Globalization;
using System.Text;
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
        bool observationEnabled = bool.Parse(TakeValue(args, "--observation-enabled"));
        EvaluationObservationBenchmarkScenario scenario =
            (EvaluationObservationBenchmarkScenario)Enum.Parse(
                typeof(EvaluationObservationBenchmarkScenario),
                TakeValue(args, "--scenario"),
                ignoreCase: false);

        if (args.Count != 0)
        {
            throw new ArgumentException($"Unexpected benchmark host arguments: {string.Join(" ", args)}");
        }

        ValidateIndependentEvaluationEnvironment();
        VerifyRepresentativeEquivalence(projectPath, scenario);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

#if !NETFRAMEWORK
        long allocatedBytesBefore = GC.GetTotalAllocatedBytes(precise: false);
#endif
        EvaluationObservationNativeMetrics nativeMetrics = new();
        Stopwatch stopwatch = Stopwatch.StartNew();
        using (EvaluationObservationNativeBridge.Enable(
            observationEnabled,
            observationEnabled ? nativeMetrics : null))
        {
            for (int i = 0; i < iterations; i++)
            {
                Evaluate(projectPath, scenario, captureRepresentativeState: false);
            }
        }

        stopwatch.Stop();

#if NETFRAMEWORK
        long allocatedManagedBytes = 0;
#else
        long allocatedManagedBytes = GC.GetTotalAllocatedBytes(precise: false) - allocatedBytesBefore;
#endif
        EvaluationObservationBenchmarkResult result = new()
        {
            EvaluationTicks = stopwatch.ElapsedTicks,
            AllocatedManagedBytes = allocatedManagedBytes,
            NativeReports = nativeMetrics.Reports,
            NativeObservations = nativeMetrics.Observations,
        };

        if (!observationEnabled && result.NativeReports != 0)
        {
            throw new InvalidOperationException("The disabled benchmark unexpectedly produced observation reports.");
        }

        if (observationEnabled && result.NativeReports != iterations)
        {
            throw new InvalidOperationException(
                $"The enabled benchmark expected {iterations} reports but observed {result.NativeReports}.");
        }

        Console.WriteLine(result.Serialize());
        exitCode = 0;
        return true;
    }

    private static void VerifyRepresentativeEquivalence(
        string projectPath,
        EvaluationObservationBenchmarkScenario scenario)
    {
        string disabledState;
        using (EvaluationObservationNativeBridge.Enable(enabled: false, metrics: null))
        {
            disabledState = Evaluate(projectPath, scenario, captureRepresentativeState: true)!;
        }

        EvaluationObservationNativeMetrics metrics = new();
        string enabledState;
        using (EvaluationObservationNativeBridge.Enable(enabled: true, metrics))
        {
            enabledState = Evaluate(projectPath, scenario, captureRepresentativeState: true)!;
        }

        if (metrics.Reports != 1 || !string.Equals(disabledState, enabledState, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Observation changed the representative evaluated state used by the benchmark.");
        }
    }

    private static string? Evaluate(
        string projectPath,
        EvaluationObservationBenchmarkScenario scenario,
        bool captureRepresentativeState)
    {
        using ProjectCollection collection = new();
        ProjectInstance project = ProjectInstance.FromFile(projectPath, new ProjectOptions
        {
            ProjectCollection = collection,
            LoadSettings = ProjectLoadSettings.RecordDuplicateButNotCircularImports,
        });

        int expectedCompileCount =
            scenario == EvaluationObservationBenchmarkScenario.GlobHeavy ? 2_000 : 200;
        if (project.GetPropertyValue("RequestedProperty") != "ImportedValue" ||
            project.GetItems("Compile").Count != expectedCompileCount)
        {
            throw new InvalidOperationException("Evaluation benchmark project produced unexpected state.");
        }

        string importedEnvironment = project.GetPropertyValue("ImportedEnvironment");
        if (scenario == EvaluationObservationBenchmarkScenario.AmbientAndSdk)
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
        else if (importedEnvironment.Length != 0)
        {
            throw new InvalidOperationException("Non-ambient evaluation benchmark unexpectedly imported ambient state.");
        }

        if (!captureRepresentativeState)
        {
            return null;
        }

        return CaptureRepresentativeState(project, projectPath, scenario);
    }

    private static string CaptureRepresentativeState(
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
                $"Evaluation benchmark expected {expectedImportCount} imports including two duplicate fixture imports.");
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

        string escapedItemState = string.Empty;
        foreach (ProjectItemInstance escapedItem in escapedItems)
        {
            if (((IItem)escapedItem).EvaluatedIncludeEscaped != "semi%3Bcolon" ||
                ((IItem)escapedItem).GetMetadataValueEscaped("EscapedMetadata") != "metadata%3Bvalue")
            {
                throw new InvalidOperationException(
                    "Evaluation benchmark did not preserve escaped item or metadata values.");
            }

            escapedItemState = string.Concat(
                ((IItem)escapedItem).EvaluatedIncludeEscaped,
                "|",
                ((IItem)escapedItem).GetMetadataValueEscaped("EscapedMetadata"));
        }

        StringBuilder orderedState = new();
        int orderedIndex = 0;
        foreach (ProjectItemInstance item in project.GetItems("Ordered"))
        {
            string expectedInclude = orderedIndex == 1 ? "second" : "first";
            string expectedPosition = (orderedIndex + 1).ToString(CultureInfo.InvariantCulture);
            string expectedOverride = orderedIndex == 1 ? "item" : "default";
            if (orderedIndex >= 3 ||
                item.EvaluatedInclude != expectedInclude ||
                item.GetMetadataValue("Position") != expectedPosition ||
                item.GetMetadataValue("Inherited") != "definition" ||
                item.GetMetadataValue("Override") != expectedOverride)
            {
                throw new InvalidOperationException(
                    "Evaluation benchmark project did not preserve item order, duplicates, or metadata.");
            }

            orderedState.Append(item.EvaluatedInclude);
            orderedState.Append('|');
            orderedState.Append(item.GetMetadataValue("Position"));
            orderedState.Append('|');
            orderedState.Append(item.GetMetadataValue("Inherited"));
            orderedState.Append('|');
            orderedState.Append(item.GetMetadataValue("Override"));
            orderedState.AppendLine();
            orderedIndex++;
        }

        if (orderedIndex != 3)
        {
            throw new InvalidOperationException(
                "Evaluation benchmark project did not preserve item order, duplicates, or metadata.");
        }

        int metadataCount = 0;
        foreach (ProjectItemInstance item in project.Items)
        {
            foreach (ProjectMetadataInstance _ in item.Metadata)
            {
                metadataCount++;
            }
        }

        StringBuilder state = new();
        state.AppendLine(project.Properties.Count.ToString(CultureInfo.InvariantCulture));
        state.AppendLine(project.Items.Count.ToString(CultureInfo.InvariantCulture));
        state.AppendLine(metadataCount.ToString(CultureInfo.InvariantCulture));
        for (int i = 0; i < imports.Count; i++)
        {
            state.AppendLine(imports[i]);
        }

        state.AppendLine(project.GetPropertyValue("RequestedProperty"));
        state.AppendLine(((IProperty)escapedProperty).EvaluatedValueEscaped);
        state.AppendLine(project.GetPropertyValue("ImportedEnvironment"));
        state.AppendLine(project.GetPropertyValue("LiveEnvironment"));
        state.AppendLine(project.GetPropertyValue("Settings"));
        state.AppendLine(project.GetPropertyValue("Above"));
        state.AppendLine(project.GetPropertyValue("Volatile"));
        state.AppendLine(escapedItemState);
        state.Append(orderedState);
        return state.ToString();
    }

    private static void ValidateIndependentEvaluationEnvironment()
    {
        if (Traits.Instance.CacheFileExistence ||
            Traits.Instance.MSBuildCacheFileEnumerations)
        {
            throw new InvalidOperationException(
                "The benchmark requires the process-wide file-existence and enumeration caches to be disabled.");
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
}
