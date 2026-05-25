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

            // On Windows, whitespace-only paths are not valid file system paths and were rejected
            // historically by Path.GetFullPath inside PathUtil.Resolve. After migrating to resolve
            // relative paths against the project directory, "projectDir\ " would otherwise be
            // silently trimmed by Path.GetFullPath and absolutize to the project directory itself,
            // masking the input error.
            //
            // Log a clear, localized error AND rethrow the historical ArgumentException so any
            // existing caller relying on the exception contract from Path.GetFullPath(" ") still
            // observes it. Whitespace remains valid on Unix where it is a legal filename character.
            if (InputUrl.Length > 0 && NativeMethodsShared.IsWindows && string.IsNullOrWhiteSpace(InputUrl))
            {
                Log.LogErrorWithCodeFromResources("FormatUrl.WhitespaceInputUrlNotAllowedOnWindows", InputUrl);
                Path.GetFullPath(InputUrl);
            }

            OutputUrl = PathUtil.Format(InputUrl, TaskEnvironment.ProjectDirectory);
            return true;
        }
    }
}
