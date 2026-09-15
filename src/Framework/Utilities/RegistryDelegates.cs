// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.Win32;

namespace Microsoft.Build.Shared
{
    /// <summary>
    /// Given a registry hive and a request view open the base key for that registry location.
    /// </summary>
    internal delegate RegistryKey OpenBaseKey(RegistryHive hive, RegistryView view);

    /// <summary>
    /// Simplified registry access delegate. Given a baseKey and a subKey, get all of the subkey
    /// names.
    /// </summary>
    /// <param name="baseKey">The base registry key.</param>
    /// <param name="subKey">The subkey</param>
    /// <returns>An enumeration of strings.</returns>
    internal delegate IEnumerable<string> GetRegistrySubKeyNames(RegistryKey baseKey, string subKey);

    /// <summary>
    /// Simplified registry access delegate. Given a baseKey and subKey, get the default value
    /// of the subKey.
    /// </summary>
    /// <param name="baseKey">The base registry key.</param>
    /// <param name="subKey">The subkey</param>
    /// <returns>A string containing the default value.</returns>
    internal delegate string GetRegistrySubKeyDefaultValue(RegistryKey baseKey, string subKey);
}
