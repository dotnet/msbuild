// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// THE ASSEMBLY BUILT FROM THIS SOURCE FILE HAS BEEN DEPRECATED FOR YEARS. IT IS BUILT ONLY TO PROVIDE
// BACKWARD COMPATIBILITY FOR API USERS WHO HAVE NOT YET MOVED TO UPDATED APIS. PLEASE DO NOT SEND PULL
// REQUESTS THAT CHANGE THIS FILE WITHOUT FIRST CHECKING WITH THE MAINTAINERS THAT THE FIX IS REQUIRED.

namespace Microsoft.Build.BuildEngine.Shared
{
    /// <summary>
    /// GetDirectories delegate
    /// </summary>
    /// <param name="path">The path to get directories for.</param>
    /// <param name="pattern">The pattern to search for.</param>
    /// <returns>An array of directories.</returns>
    internal delegate string[] GetDirectories(string path, string pattern);
}
