// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Resources;
using System.Reflection;
using System.Globalization;

namespace Microsoft.Build.Conversion
{
    /// <summary>
    /// This class provides access to the assembly's resources.
    /// </summary>
    /// <owner>SumedhK</owner>
    static internal class AssemblyResources
    {
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
            string resource = resources.GetString(name, CultureInfo.CurrentUICulture);

            if (resource == null)
            {
                resource = sharedResources.GetString(name, CultureInfo.CurrentUICulture);
            }

            return resource;
        }

        // assembly resources
        private static readonly ResourceManager resources = new ResourceManager("Microsoft.Build.Conversion.Core.Strings", Assembly.GetExecutingAssembly());
        // shared resources
        private static readonly ResourceManager sharedResources = new ResourceManager("Microsoft.Build.Conversion.Core.Strings.shared", Assembly.GetExecutingAssembly());
    }
}
