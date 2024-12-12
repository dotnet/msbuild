// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Reasons why a resolution might fail.
    /// </summary>
    internal enum NoMatchReason
    {
        /// <summary>
        /// The default state.
        /// </summary>
        Unknown,

        /// <summary>
        /// There was no file found.
        /// </summary>
        FileNotFound,

        /// <summary>
        /// The file was found, but its fusion name didn't match.
        /// </summary>
        FusionNamesDidNotMatch,

        /// <summary>
        /// The file was found, but it didn't have a fusion name.
        /// </summary>
        TargetHadNoFusionName,

        /// <summary>
        /// The file is not in the GAC.
        /// </summary>
        NotInGac,

        /// <summary>
        /// If treated as a filename, the file doesn't exist on disk.
        /// </summary>
        NotAFileNameOnDisk,

        /// <summary>
        /// The processor architecture does not match the targeted processor architecture.
        /// </summary>
        ProcessorArchitectureDoesNotMatch
    }
}
