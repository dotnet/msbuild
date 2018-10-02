// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Resources;
using System.Reflection;
using System.Globalization;

namespace Microsoft.Build.BuildEngine.Shared
{
    /// <summary>
    /// This class provides access to the assembly's resources.
    /// </summary>
    /// <owner>SumedhK</owner>
    static internal class AssemblyResources
    {
        /// <summary>
        /// A slot for msbuild.exe to add a resource manager over its own resources, that can also be consulted.
        /// </summary>
        private static ResourceManager msbuildExeResourceManager;

        /// <summary>
        /// The internals of the Engine are exposed to MSBuild.exe, so they must share the same AssemblyResources class and 
        /// ResourceUtilities class that uses it. To make this possible, MSBuild.exe registers its resources here and they are
        /// normally consulted last. This assumes that there are no duplicated resource ID's between the Engine and MSBuild.exe.
        /// (Actually there are currently two: LoggerCreationError and LoggerNotFoundError.
        /// We can't change the resource ID's this late in the cycle (UNDONE) and we sometimes want to load the MSBuild.exe ones,
        /// because they're a little different. So for that purpose we call GetStringLookingInMSBuildExeResourcesFirst() )
        /// </summary>
        internal static void RegisterMSBuildExeResources(ResourceManager manager)
        {
            ErrorUtilities.VerifyThrow(msbuildExeResourceManager == null, "Only one extra resource manager");

            msbuildExeResourceManager = manager;
        }

        /// <summary>
        /// Loads the specified resource string, either from the assembly's primary resources, or its shared resources.
        /// </summary>
        /// <remarks>This method is thread-safe.</remarks>
        /// <owner>SumedhK</owner>
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
            string resource = resources.GetString(name, CultureInfo.CurrentUICulture);

            if (resource == null)
            {
                resource = sharedResources.GetString(name, CultureInfo.CurrentUICulture);
            }

            return resource;
        }

        /// <summary>
        /// Loads the specified resource string, from the MSBuild.exe resources.
        /// </summary>
        /// <returns>The resource string, or null if not found.</returns>
        private static string GetStringFromMSBuildExeResources(string name)
        {
            string resource = null;

            if (msbuildExeResourceManager != null)
            {
                // Try MSBuild.exe's resources
                resource = msbuildExeResourceManager.GetString(name, CultureInfo.CurrentUICulture);
            }
            
            return resource;
        }

        // assembly resources
        private static readonly ResourceManager resources = new ResourceManager("Microsoft.Build.Engine.Strings", Assembly.GetExecutingAssembly());
        // shared resources
        private static readonly ResourceManager sharedResources = new ResourceManager("Microsoft.Build.Engine.Strings.shared", Assembly.GetExecutingAssembly());
    }
}
