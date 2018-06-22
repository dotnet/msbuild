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
            string resource = PrimaryResources.GetString(name, CultureInfo.CurrentUICulture)
                ?? SharedResources.GetString(name, CultureInfo.CurrentUICulture);

            ErrorUtilities.VerifyThrow(resource != null, "Missing resource '{0}'", name);

            return resource;
        }

        /// <summary>
        /// Gets the assembly's primary resources i.e. the resources exclusively owned by this assembly.
        /// </summary>
        /// <remarks>This property is thread-safe.</remarks>
        /// <value>ResourceManager for primary resources.</value>
        internal static ResourceManager PrimaryResources { get; } = new ResourceManager("Microsoft.Build.Utilities.Core.Strings", typeof(AssemblyResources).GetTypeInfo().Assembly);

        /// <summary>
        /// Gets the assembly's shared resources i.e. the resources this assembly shares with other assemblies.
        /// </summary>
        /// <remarks>This property is thread-safe.</remarks>
        /// <value>ResourceManager for shared resources.</value>
        internal static ResourceManager SharedResources { get; } = new ResourceManager("Microsoft.Build.Utilities.Core.Strings.shared", typeof(AssemblyResources).GetTypeInfo().Assembly);

        /// <summary>
        /// Formats the given string using the variable arguments passed in. The current thread's culture is used for formatting.
        /// </summary>
        /// <remarks>This method is thread-safe.</remarks>
        /// <param name="unformatted">The string to format.</param>
        /// <param name="args">Arguments for formatting.</param>
        /// <returns>The formatted string.</returns>
        internal static string FormatString(string unformatted, params object[] args)
        {
            ErrorUtilities.VerifyThrowArgumentNull(unformatted, nameof(unformatted));

            return ResourceUtilities.FormatString(unformatted, args);
        }

        /// <summary>
        /// Loads the specified resource string and optionally formats it using the given arguments. The current thread's culture
        /// is used for formatting.
        /// </summary>
        /// <remarks>
        /// 1) This method requires the owner task to have registered its resources either via the Task (or TaskMarshalByRef) base
        ///    class constructor, or the Task.TaskResources (or AppDomainIsolatedTask.TaskResources) property.
        /// 2) This method is thread-safe.
        /// </remarks>
        /// <param name="resourceName">The name of the string resource to load.</param>
        /// <param name="args">Optional arguments for formatting the loaded string.</param>
        /// <returns>The formatted string.</returns>
        internal static string FormatResourceString(string resourceName, params object[] args)
        {
            ErrorUtilities.VerifyThrowArgumentNull(resourceName, nameof(resourceName));

            // NOTE: the ResourceManager.GetString() method is thread-safe
            string resourceString = GetString(resourceName);

            return FormatString(resourceString, args);
        }
    }
}
