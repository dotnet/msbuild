// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Build.Internal;

/// <summary>
/// Aggregates MSBuild trimmer feature switches.
/// </summary>
/// <remarks>
/// Each property is annotated with <see cref="FeatureSwitchDefinitionAttribute"/> and mapped to an
/// AppContext switch. When <c>Microsoft.Build</c> is trimmed (for example when publishing a trimmed
/// application that embeds it), the trimmer can substitute a constant value for the property and
/// remove the statically unreachable code it guards. The default value used during trimming is
/// declared with a matching <c>RuntimeHostConfigurationOption</c> item in Microsoft.Build.csproj.
/// New feature switches should be added here so they can be discovered and configured in one place.
/// </remarks>
internal static class FeatureSwitches
{
    /// <summary>
    /// When <see langword="false"/> (the default in a trimmed application), property-function
    /// receiver types are restricted to the curated allowlist in <c>AvailableStaticMethods</c>,
    /// all of which are statically known and preserved. When <see langword="true"/>
    /// (<c>MSBUILDENABLEALLPROPERTYFUNCTIONS=1</c> or the matching AppContext switch), MSBuild
    /// additionally probes assemblies at runtime to resolve arbitrary receiver types - reflection
    /// that is incompatible with trimming. This is modeled as a trimmer feature switch: under
    /// trimming the property is substituted with a constant <see langword="false"/>, removing the
    /// probing path; at run time (untrimmed) the environment variable and AppContext switch are
    /// read fresh so the setting stays dynamically settable.
    /// </summary>
    [FeatureSwitchDefinition("Microsoft.Build.EnableAllPropertyFunctions")]
    internal static bool EnableAllPropertyFunctions =>
        (AppContext.TryGetSwitch("Microsoft.Build.EnableAllPropertyFunctions", out bool isEnabled) && isEnabled)
        || Environment.GetEnvironmentVariable("MSBUILDENABLEALLPROPERTYFUNCTIONS") == "1";

    /// <summary>
    /// Whether instance property-function calls are limited to a curated set of receiver types.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When enabled, instance "dotting in" is restricted to a curated set of receiver types (see
    /// <see cref="Evaluation.PropertyFunctionReceiver"/>), so the members reachable by reflection are
    /// predictable and statically known. When disabled, any public instance member except <c>GetType</c>
    /// is callable, preserving the historical behavior.
    /// </para>
    /// <para>
    /// The untrimmed default is <see langword="false"/>; under trimming the constant is substituted
    /// <see langword="true"/> so the unrestricted branch is removed, keeping the property-function path
    /// trim compatible. This switch is set only through its AppContext switch; it has no environment
    /// variable.
    /// </para>
    /// </remarks>
    [FeatureSwitchDefinition("Microsoft.Build.RestrictPropertyFunctionReceivers")]
    internal static bool RestrictPropertyFunctionReceivers =>
        AppContext.TryGetSwitch("Microsoft.Build.RestrictPropertyFunctionReceivers", out bool isEnabled) && isEnabled;
}
