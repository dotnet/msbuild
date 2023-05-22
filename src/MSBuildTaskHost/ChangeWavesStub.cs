// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Shared
{
    /// <summary>
    /// Stub implementation of ChangeWaves, that always returns true for AreFeaturesEnabled.
    /// It is used to stub out the real ChangeWaves class, which is not available in the TaskHost.
    /// </summary>
    internal static class ChangeWaves
    {
        internal static readonly Version Wave17_8 = new Version(17, 8);

        internal static bool AreFeaturesEnabled(Version wave) => true;
    }
}
