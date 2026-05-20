// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Tasks.Deployment.ManifestUtilities;

#nullable disable

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Formats a url by canonicalizing it (i.e. " " -> "%20") and transforming "localhost" to "machinename".
    /// </summary>
    [MSBuildMultiThreadableTask]
    public sealed class FormatUrl : TaskExtension, IMultiThreadableTask
    {
        /// <summary>
        /// Gets or sets the task execution environment used to resolve relative paths against
        /// the project directory instead of the process current working directory.
        /// </summary>
        public TaskEnvironment TaskEnvironment { get; set; } = TaskEnvironment.Fallback;

        public string InputUrl { get; set; }

        [Output]
        public string OutputUrl { get; set; }

        public override bool Execute()
        {
            if (InputUrl == null)
            {
                OutputUrl = String.Empty;
                return true;
            }

            // Before the migration to multithreaded execution, PathUtil.Resolve called
            // Path.GetFullPath(InputUrl) which, on Windows, throws ArgumentException for
            // whitespace-only paths. After migrating to resolve relative paths against the
            // project directory via AbsolutePath, Path.Combine(projectDir, " ") yields a
            // path like "projectDir\ " whose trailing whitespace is silently trimmed by
            // Path.GetFullPath inside AbsolutePath, masking the historical error.
            //
            // To preserve a 1:1 replication of the original Windows-only behavior, force
            // the same ArgumentException Path.GetFullPath would have raised. Whitespace
            // remains valid on Unix where it is a legal filename character.
            if (InputUrl.Length > 0 && NativeMethodsShared.IsWindows && string.IsNullOrWhiteSpace(InputUrl))
            {
                Path.GetFullPath(InputUrl);
            }

            OutputUrl = PathUtil.Format(InputUrl, TaskEnvironment.ProjectDirectory);
            return true;
        }
    }
}
