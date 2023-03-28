// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// THE ASSEMBLY BUILT FROM THIS SOURCE FILE HAS BEEN DEPRECATED FOR YEARS. IT IS BUILT ONLY TO PROVIDE
// BACKWARD COMPATIBILITY FOR API USERS WHO HAVE NOT YET MOVED TO UPDATED APIS. PLEASE DO NOT SEND PULL
// REQUESTS THAT CHANGE THIS FILE WITHOUT FIRST CHECKING WITH THE MAINTAINERS THAT THE FIX IS REQUIRED.

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
