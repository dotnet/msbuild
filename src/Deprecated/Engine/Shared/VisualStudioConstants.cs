// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.Build.BuildEngine.Shared
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
        internal const int CurrentVisualStudioSolutionFileVersion = 11;

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
