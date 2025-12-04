// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.Build.Shared
{
    /// <summary>
    /// Shared Visual Studio related constants
    /// </summary>
    internal static class VisualStudioConstants
    {
        /// <summary>
        /// This is the version number of the most recent solution file format
        /// we will read. It will be the version number used in solution files
        /// by the latest version of Visual Studio.
        /// </summary>
        internal const int CurrentVisualStudioSolutionFileVersion = 12; // VS11

        /// <summary>
        /// This is the version number of the latest version of Visual Studio.
        /// </summary>
        /// <remarks>
        /// We use it for the version of the VC PIA we try to load and to find
        /// Visual Studio registry hive that we use to find where vcbuild.exe might be.
        /// </remarks>
        internal const string CurrentVisualStudioVersion = "10.0";
    }
}
