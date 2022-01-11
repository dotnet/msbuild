// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// Class to encapsulate state that was stored in BuildEnvironmentHelper.
    /// </summary>
    /// <remarks>
    /// This should be deleted when BuildEnvironmentHelper can be moved into Framework.
    /// </remarks>
    internal static class BuildEnvironmentState
    {
        internal static bool s_runningInVisualStudio = false;
        internal static bool s_runningTests = false;
    }
}
