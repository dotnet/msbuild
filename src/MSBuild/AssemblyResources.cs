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
        /// <param name="name"></param>
        /// <returns>The resource string, or null if not found.</returns>
        internal static string GetString(string name)
        {
            // NOTE: the ResourceManager.GetString() method is thread-safe
            string resource = s_resources.GetString(name, CultureInfo.CurrentUICulture);

            if (resource == null)
            {
                resource = s_sharedResources.GetString(name, CultureInfo.CurrentUICulture);
            }

            ErrorUtilities.VerifyThrow(resource != null, "Missing resource '{0}'", name);

            return resource;
        }

        // assembly resources
        private static readonly ResourceManager s_resources = new ResourceManager("MSBuild.Strings", typeof(AssemblyResources).GetTypeInfo().Assembly);
        // shared resources
        private static readonly ResourceManager s_sharedResources = new ResourceManager("MSBuild.Strings.shared", typeof(AssemblyResources).GetTypeInfo().Assembly);
    }
}
