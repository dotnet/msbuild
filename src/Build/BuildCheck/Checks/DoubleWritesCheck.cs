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
using Microsoft.Build.Shared;
using static Microsoft.Build.Experimental.BuildCheck.TaskInvocationCheckData;

#if FEATURE_MSIOREDIST
using Path = Microsoft.IO.Path;
#endif

namespace Microsoft.Build.Experimental.BuildCheck.Checks;

internal sealed class DoubleWritesCheck : Check
{
    public static CheckRule SupportedRule = new CheckRule(
        "BC0102",
        "DoubleWrites",
        ResourceUtilities.GetResourceString("BuildCheck_BC0102_Title")!,
        ResourceUtilities.GetResourceString("BuildCheck_BC0102_MessageFmt")!,
        new CheckConfiguration() { Severity = CheckResultSeverity.Warning });

    public override string FriendlyName => "MSBuild.DoubleWritesCheck";

    public override IReadOnlyList<CheckRule> SupportedRules { get; } = [SupportedRule];

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

    private void TaskInvocationAction(BuildCheckDataContext<TaskInvocationCheckData> context)
    {
        // This check uses a hard-coded list of tasks known to write files.
        switch (context.Data.TaskName)
        {
            case "Csc":
            case "Vbc":
            case "Fsc": CheckCompilerTask(context); break;
            case "Copy": CheckCopyTask(context); break;
        }
    }

    private void CheckCompilerTask(BuildCheckDataContext<TaskInvocationCheckData> context)
    {
        var taskParameters = context.Data.Parameters;

        // Compiler tasks have several parameters representing files being written.
        CheckParameter("OutputAssembly");
        CheckParameter("OutputRefAssembly");
        CheckParameter("DocumentationFile");
        CheckParameter("PdbFile");

        void CheckParameter(string parameterName)
        {
            if (taskParameters.TryGetValue(parameterName, out TaskParameter? taskParameter))
            {
                string outputPath = taskParameter.EnumerateStringValues().FirstOrDefault() ?? "";
                CheckWrite(context, outputPath);
            }
        }
    }

    private void CheckCopyTask(BuildCheckDataContext<TaskInvocationCheckData> context)
    {
        var taskParameters = context.Data.Parameters;

        // The destination is specified as either DestinationFolder or DestinationFiles.
        if (taskParameters.TryGetValue("SourceFiles", out TaskParameter? sourceFiles) &&
            taskParameters.TryGetValue("DestinationFolder", out TaskParameter? destinationFolder))
        {
            string destinationFolderPath = destinationFolder.EnumerateStringValues().FirstOrDefault() ?? "";
            foreach (string sourceFilePath in sourceFiles.EnumerateStringValues())
            {
                CheckWrite(context, Path.Combine(destinationFolderPath, Path.GetFileName(sourceFilePath)));
            }
        }
        else if (taskParameters.TryGetValue("DestinationFiles", out TaskParameter? destinationFiles))
        {
            foreach (string destinationFilePath in destinationFiles.EnumerateStringValues())
            {
                CheckWrite(context, destinationFilePath);
            }
        }
    }

    private void CheckWrite(BuildCheckDataContext<TaskInvocationCheckData> context, string fileBeingWritten)
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
