// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel;

namespace Microsoft.Build.Server
{
    /// <summary>
    /// Describes how an <see cref="MSBuildClient"/> execution finished, and — when the build ran on
    /// the server — the exit type the build itself produced.
    /// </summary>
    /// <remarks>
    /// This type is public only so that the MSBuild command-line application can host the MSBuild server;
    /// third-party use is not expected or supported. The server APIs only work to wrap the MSBuild CLI,
    /// so invoke the CLI instead.
    /// </remarks>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class MSBuildClientExitResult
    {
        /// <summary>
        /// The MSBuild client exit type.
        /// Covers different ways MSBuild client execution can finish.
        /// Build errors are not included. The client could finish successfully and the build at the same time could result in a build error.
        /// </summary>
        public MSBuildClientExitType MSBuildClientExitType { get; set; }

        /// <summary>
        /// The build exit type. Possible values: MSBuildApp.ExitType serialized into a string.
        /// This field is null if MSBuild client execution was not successful.
        /// </summary>
        public string? MSBuildAppExitTypeString { get; set; }

        /// <summary>
        /// When this client launched a server child process and that process had already exited
        /// by the time we observed the connection failure, this is its exit code. <c>null</c>
        /// otherwise (server still running, never launched, or its state could not be queried).
        /// Hosts use this to surface "server crashed immediately on launch" to the user instead
        /// of a generic timeout message.
        /// </summary>
        public int? ServerProcessExitCode { get; set; }
    }
}
