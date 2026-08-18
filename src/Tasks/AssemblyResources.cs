// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using System.Reflection;
using System.Resources;

#nullable disable

namespace Microsoft.Build.Shared
{
    /// <summary>
    /// This class provides access to the assembly's resources.
    /// </summary>
    internal static class AssemblyResources
    {
        /// <summary>
        /// Loads the specified resource string, either from the assembly's primary resources, or its shared resources.
        /// </summary>
        /// <remarks>This method is thread-safe.</remarks>
        /// <returns>The resource string, or null if not found.</returns>
        internal static string GetString(string name)
        {
            // NOTE: the ResourceManager.GetString() method is thread-safe
            string resource = PrimaryResources.GetString(name, CultureInfo.CurrentUICulture) ?? SharedResources.GetString(name, CultureInfo.CurrentUICulture);

            ErrorUtilities.VerifyThrow(resource != null, "Missing resource '{0}'", name);

            return resource;
        }

        /// <summary>
        /// Loads the specified resource string using invariant culture, either from the assembly's primary resources, or its shared resources.
        /// This method is useful for generating code that should be culture-independent for reproducible builds.
        /// </summary>
        /// <remarks>This method is thread-safe.</remarks>
        /// <returns>The resource string, or null if not found.</returns>
        internal static string GetInvariantString(string name)
        {
            // NOTE: the ResourceManager.GetString() method is thread-safe
            string resource = PrimaryResources.GetString(name, CultureInfo.InvariantCulture) ?? SharedResources.GetString(name, CultureInfo.InvariantCulture);

            ErrorUtilities.VerifyThrow(resource != null, "Missing resource '{0}'", name);

            return resource;
        }

        /// <summary>
        /// Gets the assembly's primary resources i.e. the resources exclusively owned by this assembly.
        /// </summary>
        /// <remarks>This property is thread-safe.</remarks>
        /// <value>ResourceManager for primary resources.</value>
        internal static ResourceManager PrimaryResources { get; } = new ResourceManager("Microsoft.Build.Tasks.Core.Strings", typeof(AssemblyResources).GetTypeInfo().Assembly);

        /// <summary>
        /// Gets the assembly's shared resources i.e. the resources this assembly shares with other assemblies.
        /// </summary>
        /// <remarks>This property is thread-safe.</remarks>
        /// <value>ResourceManager for shared resources.</value>
        internal static ResourceManager SharedResources { get; } = new ResourceManager("Microsoft.Build.Tasks.Core.Strings.shared", typeof(AssemblyResources).GetTypeInfo().Assembly);

        // assembly resources
        // shared resources
    }
}
