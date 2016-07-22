// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Reflection;

namespace Microsoft.Build.Shared
{
    /// <summary>
    /// This class contains common reflection tasks
    /// </summary>
    internal static class AssemblyUtilities
    {
        public static string GetAssemblyLocation(Assembly assembly)
        {
#if FEATURE_ASSEMBLY_LOCATION
            return assembly.Location;
#else
            // Assembly.Location is only available in .netstandard1.5, but MSBuild needs to target 1.3.
            // use reflection to access the property

            var locationProperty = assembly.GetType().GetProperty("Location", typeof(string));

            if (locationProperty == null)
            {
                throw new NotSupportedException("Type Assembly does not have the Location property");
            }

            return (string)locationProperty.GetValue(assembly);
#endif
        }

#if CLR2COMPATIBILITY
        /// <summary>
        /// Shim for the lack of <see cref="System.Reflection.IntrospectionExtensions.GetTypeInfo"/> in .NET 3.5.
        /// </summary>
        public static Type GetTypeInfo(this Type t)
        {
            return t;
        }
#endif
    }
}
