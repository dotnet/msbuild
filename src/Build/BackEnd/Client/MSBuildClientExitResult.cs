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
    }
}
