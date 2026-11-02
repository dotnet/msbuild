// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Framework;

namespace Microsoft.Build.AotValidation;

/// <summary>
/// Runs an in-process MSBuild build for the harness's build tests.
/// </summary>
/// <remarks>
/// In-process MSBuild builds go through the process-global <c>BuildManager.DefaultBuildManager</c>, which
/// allows only one build at a time; the MTP test host runs tests in parallel, so builds are serialized
/// here with a process-wide lock.
/// </remarks>
internal static class InProcBuild
{
    private static readonly object s_buildLock = new();

    /// <summary>
    /// Builds <paramref name="target"/> of <paramref name="project"/> in-process with the given logger,
    /// serialized against any other harness build.
    /// </summary>
    /// <remarks>
    /// <see cref="Project.Build(string, System.Collections.Generic.IEnumerable{ILogger})"/> is annotated
    /// <see cref="RequiresUnreferencedCodeAttribute"/> because the build entry points can load loggers and
    /// project cache plugins by reflecting over assemblies named at run time. The harness passes a
    /// pre-constructed <see cref="ILogger"/> and configures no project cache plugins, so that reflective
    /// path is never taken; the registered-task execution path under test is itself reflection-free. Making
    /// the build entry points fully trim-clean (gating the reflective logger/plugin loading behind a feature
    /// switch) is separate, larger work outside this task-registration change.
    /// </remarks>
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:RequiresUnreferencedCode",
        Justification = "The build is invoked with a pre-constructed ILogger and no project cache plugins, so the "
            + "reflective logger/plugin-loading path that annotates Project.Build is not exercised. The task-execution "
            + "path under test is reflection-free (host-registered tasks).")]
    public static bool Run(Project project, string target, ILogger logger)
    {
        lock (s_buildLock)
        {
            return project.Build(target, [logger]);
        }
    }
}
