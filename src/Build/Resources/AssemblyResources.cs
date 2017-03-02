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
        /// A slot for msbuild.exe to add a resource manager over its own resources, that can also be consulted.
        /// </summary>
        private static ResourceManager s_msbuildExeResourceManager;

        /// <summary>
        /// The internals of the Engine are exposed to MSBuild.exe, so they must share the same AssemblyResources class and 
        /// ResourceUtilities class that uses it. To make this possible, MSBuild.exe registers its resources here and they are
        /// normally consulted last. This assumes that there are no duplicated resource ID's between the Engine and MSBuild.exe.
        /// (Actually there are currently two: LoggerCreationError and LoggerNotFoundError.
        /// We can't change the resource ID's this late in the cycle and we sometimes want to load the MSBuild.exe ones,
        /// because they're a little different. So for that purpose we call GetStringLookingInMSBuildExeResourcesFirst() )
        /// </summary>
        internal static void RegisterMSBuildExeResources(ResourceManager manager)
        {
            ErrorUtilities.VerifyThrow(s_msbuildExeResourceManager == null, "Only one extra resource manager");

            s_msbuildExeResourceManager = manager;
        }

        /// <summary>
        /// Loads the specified resource string, either from the assembly's primary resources, or its shared resources.
        /// </summary>
        /// <remarks>This method is thread-safe.</remarks>
        /// <param name="name"></param>
        /// <returns>The resource string, or null if not found.</returns>
        internal static string GetString(string name)
        {
            // NOTE: the ResourceManager.GetString() method is thread-safe
            string resource = GetStringFromEngineResources(name);

            if (resource == null)
            {
                resource = GetStringFromMSBuildExeResources(name);
            }

            return resource;
        }

        /// <summary>
        /// Loads the specified resource string.
        /// </summary>
        /// <returns>The resource string, or null if not found.</returns>
        internal static string GetStringLookingInMSBuildExeResourcesFirst(string name)
        {
            string resource = GetStringFromMSBuildExeResources(name);

            if (resource == null)
            {
                resource = GetStringFromEngineResources(name);
            }

            return resource;
        }

        /// <summary>
        /// Loads the specified resource string, from the Engine or else Shared resources.
        /// </summary>
        /// <returns>The resource string, or null if not found.</returns>
        private static string GetStringFromEngineResources(string name)
        {
            string resource = s_resources.GetString(name, CultureInfo.CurrentUICulture);

            if (resource == null)
            {
                resource = s_sharedResources.GetString(name, CultureInfo.CurrentUICulture);
            }

            ErrorUtilities.VerifyThrow(resource != null, "Missing resource '{0}'", name);

            return resource;
        }

        /// <summary>
        /// Loads the specified resource string, from the MSBuild.exe resources.
        /// </summary>
        /// <returns>The resource string, or null if not found.</returns>
        private static string GetStringFromMSBuildExeResources(string name)
        {
            string resource = null;

            if (s_msbuildExeResourceManager != null)
            {
                // Try MSBuild.exe's resources
                resource = s_msbuildExeResourceManager.GetString(name, CultureInfo.CurrentUICulture);
            }

            return resource;
        }

        internal static ResourceManager PrimaryResources
        {
            get { return s_resources; }
        }

        internal static ResourceManager SharedResources
        {
            get { return s_sharedResources; }
        }

        // assembly resources
        private static readonly ResourceManager s_resources = new ResourceManager("Microsoft.Build.Strings", typeof(AssemblyResources).GetTypeInfo().Assembly);
        // shared resources
        private static readonly ResourceManager s_sharedResources = new ResourceManager("Microsoft.Build.Strings.shared", typeof(AssemblyResources).GetTypeInfo().Assembly);
    }
}
