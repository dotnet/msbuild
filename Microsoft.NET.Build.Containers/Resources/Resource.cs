// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Resources;
using System.Runtime.CompilerServices;

[assembly: NeutralResourcesLanguage("en")]
[assembly: InternalsVisibleTo("Test.Microsoft.NET.Build.Containers")]

namespace Microsoft.NET.Build.Containers.Resources
{
    /// <summary>
    /// This class provides access to the assembly's resources.
    /// </summary>
    internal static class Resource
    {
        private static readonly ResourceManager resourceManager = new ResourceManager("Microsoft.NET.Build.Containers.Resources.Strings", typeof(Resource).GetTypeInfo().Assembly);

        /// <summary>
        /// Looks up a resource value for a particular name. Looks in the CurrentUICulture, and if not found, all parent CultureInfos.
        /// </summary>
        /// <param name="name">Name of the resource.</param>
        /// <returns>Localized string or resource name if the resource isn't found.</returns>
        public static string GetString(string name)
        {
            string? resource = resourceManager.GetString(name, CultureInfo.CurrentUICulture);

            Debug.Assert(resource != null, $"Resource with name {name} not found");

            return resource ?? $"<{name}>";
        }

        /// <summary>
        /// Looks up a resource value for a particular name and uses it as format. Looks in the CurrentUICulture, and if not found, all parent CultureInfos.
        /// </summary>
        /// <param name="name">Name of the resource.</param>
        /// <returns>Localized formatted string or resource name if the resource isn't found.</returns>
        public static string FormatString(string name, params object?[] args)
        {
            string? resource = resourceManager.GetString(name, CultureInfo.CurrentUICulture);

            Debug.Assert(resource != null, $"Resource with name {name} not found");

            return resource is null ?
                $"<{name}>" :
                string.Format(resource, args);
        }
    }
}
