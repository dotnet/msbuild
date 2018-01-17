// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.Reflection;
using System.Resources;

namespace Microsoft.Build.Shared
{
    /// <summary>
    /// This class provides access to the assembly's resources.
    /// </summary>
    internal static class AssemblyResources
    {
        internal static ResourceManager PrimaryResources { get; } = new ResourceManager("NuGet.MSBuildSdkResolver.Strings", typeof(AssemblyResources).GetTypeInfo().Assembly);

        /// <summary>
        /// Loads the specified resource string.
        /// </summary>
        /// <remarks>This method is thread-safe.</remarks>
        /// <param name="name">The name of the resource.</param>
        /// <returns>The resource string, or null if not found.</returns>
        internal static string GetString(string name)
        {
            // NOTE: the ResourceManager.GetString() method is thread-safe
            string resource = PrimaryResources.GetString(name, CultureInfo.CurrentUICulture);

            ErrorUtilities.VerifyThrow(resource != null, "Missing resource '{0}'", name);

            return resource;
        }
    }
}
