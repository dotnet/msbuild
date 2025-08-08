// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Build.Experimental.BuildCheck.Infrastructure;
using Microsoft.Build.Construction;
using Microsoft.Build.Experimental.BuildCheck;
using static Microsoft.Build.Experimental.BuildCheck.TaskInvocationAnalysisData;

#if FEATURE_MSIOREDIST
using Path = Microsoft.IO.Path;
#endif

namespace Microsoft.Build.Experimental.BuildCheck.Analyzers;

internal sealed class DoubleWritesAnalyzer : BuildAnalyzer
{
    public static BuildAnalyzerRule SupportedRule = new BuildAnalyzerRule("BC0102", "DoubleWrites",
        "Two tasks should not write the same file",
        "Tasks {0} and {1} from projects {2} and {3} write the same file: {4}.",
        new BuildAnalyzerConfiguration() { Severity = BuildAnalyzerResultSeverity.Warning, IsEnabled = true });

    public override string FriendlyName => "MSBuild.DoubleWritesAnalyzer";

    public override IReadOnlyList<BuildAnalyzerRule> SupportedRules { get; } = [SupportedRule];

    public override void Initialize(ConfigurationContext configurationContext)
    {
        /* This is it - no custom configuration */
    }

    public override void RegisterActions(IBuildCheckRegistrationContext registrationContext)
    {
        registrationContext.RegisterTaskInvocationAction(TaskInvocationAction);
    }

    /// <summary>
    /// Contains the first project file + task that wrote the given file during the build.
    /// </summary>
    private readonly Dictionary<string, (string projectFilePath, string taskName)> _filesWritten = new(StringComparer.CurrentCultureIgnoreCase);

    private void TaskInvocationAction(BuildCheckDataContext<TaskInvocationAnalysisData> context)
    {
        // This analyzer uses a hard-coded list of tasks known to write files.
        switch (context.Data.TaskName)
        {
            case "Csc":
            case "Vbc":
            case "Fsc": AnalyzeCompilerTask(context); break;
            case "Copy": AnalyzeCopyTask(context); break;
        }
    }

    private void AnalyzeCompilerTask(BuildCheckDataContext<TaskInvocationAnalysisData> context)
    {
        var taskParameters = context.Data.Parameters;

        // Compiler tasks have several parameters representing files being written.
        AnalyzeParameter("OutputAssembly");
        AnalyzeParameter("OutputRefAssembly");
        AnalyzeParameter("DocumentationFile");
        AnalyzeParameter("PdbFile");

        void AnalyzeParameter(string parameterName)
        {
            if (taskParameters.TryGetValue(parameterName, out TaskParameter? taskParameter))
            {
                string outputPath = taskParameter.EnumerateStringValues().FirstOrDefault() ?? "";
                AnalyzeWrite(context, outputPath);
            }
        }
    }

    private void AnalyzeCopyTask(BuildCheckDataContext<TaskInvocationAnalysisData> context)
    {
        var taskParameters = context.Data.Parameters;

        // The destination is specified as either DestinationFolder or DestinationFiles.
        if (taskParameters.TryGetValue("SourceFiles", out TaskParameter? sourceFiles) &&
            taskParameters.TryGetValue("DestinationFolder", out TaskParameter? destinationFolder))
        {
            string destinationFolderPath = destinationFolder.EnumerateStringValues().FirstOrDefault() ?? "";
            foreach (string sourceFilePath in sourceFiles.EnumerateStringValues())
            {
                AnalyzeWrite(context, Path.Combine(destinationFolderPath, Path.GetFileName(sourceFilePath)));
            }
        }
        else if (taskParameters.TryGetValue("DestinationFiles", out TaskParameter? destinationFiles))
        {
            foreach (string destinationFilePath in destinationFiles.EnumerateStringValues())
            {
                AnalyzeWrite(context, destinationFilePath);
            }
        }
    }

    private void AnalyzeWrite(BuildCheckDataContext<TaskInvocationAnalysisData> context, string fileBeingWritten)
    {
        if (!string.IsNullOrEmpty(fileBeingWritten))
        {
            // Absolutize the path. Note that if a path used during a build is relative, it is relative to the directory
            // of the project being built, regardless of the project/import in which it occurs.
            fileBeingWritten = Path.GetFullPath(fileBeingWritten, context.Data.ProjectFileDirectory);

            if (_filesWritten.TryGetValue(fileBeingWritten, out (string projectFilePath, string taskName) existingEntry))
            {
                context.ReportResult(BuildCheckResult.Create(
                    SupportedRule,
                    context.Data.TaskInvocationLocation,
                    context.Data.TaskName,
                    existingEntry.taskName,
                    Path.GetFileName(context.Data.ProjectFilePath),
                    Path.GetFileName(existingEntry.projectFilePath),
                    fileBeingWritten));
            }
            else
            {
                _filesWritten.Add(fileBeingWritten, (context.Data.ProjectFilePath, context.Data.TaskName));
            }
        }
   }
}
