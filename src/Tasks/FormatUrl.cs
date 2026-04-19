// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
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
        /// Gets or sets the task execution environment for thread-safe path resolution.
        /// </summary>
        public TaskEnvironment TaskEnvironment { get; set; } = TaskEnvironment.Fallback;

        public string InputUrl { get; set; }

        [Output]
        public string OutputUrl { get; set; }

        public override bool Execute()
        {
            OutputUrl = InputUrl != null ? PathUtil.Format(InputUrl, TaskEnvironment.ProjectDirectory) : String.Empty;
            return true;
        }
    }
}
