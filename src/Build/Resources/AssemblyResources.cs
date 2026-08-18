// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using System.Resources;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Shared;

/// <summary>
///  This class provides access to the assembly's resources.
/// </summary>
internal static class AssemblyResources
{
    /// <summary>
    ///  Gets the assembly's primary resources, i.e. the resources exclusively owned by this assembly.
    /// </summary>
    internal static ResourceManager PrimaryResources { get; } = new ResourceManager("Microsoft.Build.Strings", typeof(AssemblyResources).Assembly);

    /// <summary>
    ///  Gets the assembly's shared resources, i.e. the resources this assembly shares with other assemblies.
    /// </summary>
    internal static ResourceManager SharedResources => Framework.Resources.SR.ResourceManager;

    /// <summary>
    ///  Loads the specified resource string, either from the assembly's primary resources, or its shared resources.
    /// </summary>
    /// <param name="name">The name of the resource to load.</param>
    /// <param name="culture">
    ///  The culture to use when looking up the resource. If <see langword="null"/>, the current UI culture is used.
    /// </param>
    /// <returns>
    ///  The resource string.
    /// </returns>
    /// <exception cref="InternalErrorException">Thrown if the resource is not found.</exception>
    internal static string GetString(string name, CultureInfo? culture = null)
    {
        culture ??= CultureInfo.CurrentUICulture;
        string? resource = PrimaryResources.GetString(name, culture) ??
                           SharedResources.GetString(name, culture);

        Assumed.NotNull(resource, $"Missing resource '{name}'");

        return resource;
    }
}
