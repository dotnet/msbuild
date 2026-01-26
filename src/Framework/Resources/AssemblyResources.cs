// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using System.Reflection;
using System.Resources;

#nullable disable

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// This class provides access to the Framework assembly's resources.
    /// </summary>
    internal static class FrameworkResources
    {
        /// <summary>
        /// Loads the specified resource string from the Framework assembly's resources.
        /// </summary>
        /// <remarks>This method is thread-safe.</remarks>
        /// <param name="name">The resource name.</param>
        /// <returns>The resource string, or null if not found.</returns>
        internal static string GetString(string name)
        {
            // NOTE: the ResourceManager.GetString() method is thread-safe
            string resource = s_resources.GetString(name, CultureInfo.CurrentUICulture);

            FrameworkErrorUtilities.VerifyThrow(resource != null, "Missing resource '{0}'");

            return resource;
        }

        /// <summary>
        /// Gets the assembly's primary resources.
        /// </summary>
        /// <remarks>This property is thread-safe.</remarks>
        internal static ResourceManager PrimaryResources => s_resources;

        // assembly resources
        private static readonly ResourceManager s_resources = new ResourceManager("Microsoft.Build.Framework.Strings", typeof(FrameworkResources).GetTypeInfo().Assembly);
    }
}
