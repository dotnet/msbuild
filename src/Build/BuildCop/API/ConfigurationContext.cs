// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace Microsoft.Build.Experimental.BuildCop;

/// <summary>
/// Holder of an optional configuration from .editorconfig file (not recognized by infrastructure)
/// </summary>
public class ConfigurationContext
{
    public static ConfigurationContext Null { get; } = new();

    public IReadOnlyDictionary<string, string>? ConfigurationData { get; init; }
}
