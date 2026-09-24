// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;
using Microsoft.Build.Definition;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Evaluation.Context;
using Microsoft.Build.Execution;

namespace MSBuild.Benchmarks;

/// <summary>
/// Child process of <see cref="EvaluationInputDetoursComparison"/>. Evaluates one project with input recording on and
/// writes the recorded paths to a result file. The parent runs it inside the Detours sandbox and observes its file accesses
/// between the two marker probes.
/// </summary>
internal static class EvaluationInputRecordHost
{
    internal const string Switch = "--evaluation-input-record-host";
    internal const string StartMarker = ".evaluation-input-measure-start";
    internal const string StopMarker = ".evaluation-input-measure-stop";
    internal const string RecordedPrefix = "RECORDED|";
    internal const string SummaryPrefix = "SUMMARY|";

    internal static bool TryRun(List<string> args, out int exitCode)
    {
        if (!args.Remove(Switch))
        {
            exitCode = 0;
            return false;
        }

        string project = Path.GetFullPath(HarnessArguments.Take(args, "--project"));
        string resultFile = HarnessArguments.Take(args, "--result-file");
        Dictionary<string, string> globalProperties = HarnessArguments.TakeGlobalProperties(args);
        HarnessArguments.ExpectEmpty(args);

        string measurementRoot = Path.GetDirectoryName(project)!;
        using var collection = new ProjectCollection(globalProperties);
        var options = new ProjectOptions { ProjectCollection = collection, GlobalProperties = globalProperties };

        _ = File.Exists(Path.Combine(measurementRoot, StartMarker));
        ProjectInstance instance = ProjectInstance.FromFile(project, options);
        _ = File.Exists(Path.Combine(measurementRoot, StopMarker));

        EvaluationInputs inputs = instance.EvaluationInputs
            ?? throw new InvalidOperationException("Recording is off; the parent must set MSBUILDRECORDEVALUATIONINPUTS=1.");

        var result = new StringBuilder();
        foreach (KeyValuePair<string, FileDependency> file in inputs.Files)
        {
            result.Append(RecordedPrefix).Append(file.Value.Kind).Append('|').AppendLine(Encode(file.Key));
        }

        result.Append(SummaryPrefix)
            .Append("NonCacheable=").Append(inputs.NonCacheable)
            .Append("|EnvironmentReads=").Append(inputs.EnvironmentReads.Count)
            .Append("|SdkResolutions=").Append(inputs.SdkResolutions.Length)
            .Append("|RegistryReads=").Append(inputs.RegistryReads.Length)
            .Append("|Detail=").AppendLine(Encode(inputs.NonCacheableDetail ?? string.Empty));
        File.WriteAllText(resultFile, result.ToString());

        exitCode = 0;
        return true;
    }

    internal static string Encode(string value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value));

    internal static string Decode(string value) => Encoding.UTF8.GetString(Convert.FromBase64String(value));
}
