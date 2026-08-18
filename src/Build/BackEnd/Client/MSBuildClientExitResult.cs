// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Experimental
{
    /// <summary>
    /// Enumeration of the various ways in which the MSBuildClient execution can exit.
    /// </summary>
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
