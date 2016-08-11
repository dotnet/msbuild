// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Resources;
using System.Reflection;
using System.Globalization;

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

        /// <summary>
        /// Gets the assembly's primary resources i.e. the resources exclusively owned by this assembly.
        /// </summary>
        /// <remarks>This property is thread-safe.</remarks>
        /// <value>ResourceManager for primary resources.</value>
        internal static ResourceManager PrimaryResources
        {
            get
            {
                return s_resources;
            }
        }

        /// <summary>
        /// Gets the assembly's shared resources i.e. the resources this assembly shares with other assemblies.
        /// </summary>
        /// <remarks>This property is thread-safe.</remarks>
        /// <value>ResourceManager for shared resources.</value>
        internal static ResourceManager SharedResources
        {
            get
            {
                return s_sharedResources;
            }
        }

        // assembly resources
        private static readonly ResourceManager s_resources = new ResourceManager("Microsoft.Build.Tasks.Core.Strings", typeof(AssemblyResources).GetTypeInfo().Assembly);
        // shared resources
        private static readonly ResourceManager s_sharedResources = new ResourceManager("Microsoft.Build.Tasks.Core.Strings.shared", typeof(AssemblyResources).GetTypeInfo().Assembly);
    }
}
