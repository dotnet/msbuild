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
        // True when the cached method info objects have been set.
        private static bool s_initialized;

        // Cached method info
        private static MethodInfo s_assemblyNameCloneMethod;
        private static PropertyInfo s_assemblylocationProperty;

        public static string GetAssemblyLocation(Assembly assembly)
        {
#if FEATURE_ASSEMBLY_LOCATION
            return assembly.Location;
#else
            // Assembly.Location is only available in .netstandard1.5, but MSBuild needs to target 1.3.
            // use reflection to access the property
            Initialize();

            if (s_assemblylocationProperty == null)
            {
                throw new NotSupportedException("Type Assembly does not have the Location property");
            }

            return (string)s_assemblylocationProperty.GetValue(assembly);
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

        public static AssemblyName CloneIfPossible(this AssemblyName assemblyNameToClone)
        {
#if FEATURE_ASSEMBLYNAME_CLONE
            return (AssemblyName) assemblyNameToClone.Clone();
#else

            Initialize();

            if (s_assemblyNameCloneMethod == null)
            {
                return new AssemblyName(assemblyNameToClone.FullName);
            }

            // Try to Invoke the Clone method via reflection. If the method exists (it will on .NET
            // Core 2.0 or later) use that result, otherwise use new AssemblyName(FullName).
            return (AssemblyName) s_assemblyNameCloneMethod.Invoke(assemblyNameToClone, null) ??
                   new AssemblyName(assemblyNameToClone.FullName);
#endif
        }

        /// <summary>
        /// Initialize static fields. Doesn't need to be thread safe.
        /// </summary>
        private static void Initialize()
        {
            if (s_initialized) return;

            s_assemblyNameCloneMethod = typeof(AssemblyName).GetMethod("Clone");
            s_assemblylocationProperty = typeof(Assembly).GetProperty("Location", typeof(string));
            s_initialized = true;
        }
    }
}
