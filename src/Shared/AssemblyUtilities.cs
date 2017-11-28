// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Globalization;
using System.Linq;
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
        private static PropertyInfo s_assemblylocationProperty;
        private static MethodInfo s_cultureInfoGetCultureMethod;

#if !FEATURE_CULTUREINFO_GETCULTURES
        private static Lazy<CultureInfo[]> s_validCultures = new Lazy<CultureInfo[]>(() => GetValidCultures(), true);
#endif

#if !CLR2COMPATIBILITY
        private static Lazy<Assembly> s_entryAssembly = new Lazy<Assembly>(() => GetEntryAssembly());
        public static Assembly EntryAssembly => s_entryAssembly.Value;
#else
        public static Assembly EntryAssembly = GetEntryAssembly();
#endif

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
#if CLR2COMPATIBILITY
            return (AssemblyName) assemblyNameToClone.Clone();
#else

            // NOTE: In large projects, this is called a lot. Avoid calling AssemblyName.Clone
            // because it clones the Version property (which is immutable) and the PublicKey property
            // and the PublicKeyToken property.
            //
            // While the array themselves are mutable - throughout MSBuild they are only ever
            // read from.
            AssemblyName name = new AssemblyName();
            name.Name = assemblyNameToClone.Name;
            name.SetPublicKey(assemblyNameToClone.GetPublicKey());
            name.SetPublicKeyToken(assemblyNameToClone.GetPublicKeyToken());
            name.Version = assemblyNameToClone.Version;
            name.Flags = assemblyNameToClone.Flags;
            name.ProcessorArchitecture = assemblyNameToClone.ProcessorArchitecture;

#if !RUNTIME_TYPE_NETCORE
            name.CultureInfo = assemblyNameToClone.CultureInfo;
            name.HashAlgorithm = assemblyNameToClone.HashAlgorithm;
            name.VersionCompatibility = assemblyNameToClone.VersionCompatibility;
            name.CodeBase = assemblyNameToClone.CodeBase;
            name.KeyPair = assemblyNameToClone.KeyPair;
            name.VersionCompatibility = assemblyNameToClone.VersionCompatibility;
#elif !MONO
            // Setting the culture name creates a new CultureInfo, leading to many allocations. Only set CultureName when the CultureInfo member is not available.
            // CultureName not available on Mono
            name.CultureName = assemblyNameToClone.CultureName;
#endif

            return name;
#endif

        }

        public static bool CultureInfoHasGetCultures()
        {
            return s_cultureInfoGetCultureMethod != null;
        }

        public static CultureInfo[] GetAllCultures()
        {
#if FEATURE_CULTUREINFO_GETCULTURES
            return CultureInfo.GetCultures(CultureTypes.AllCultures);
#else
            Initialize();

            if (!CultureInfoHasGetCultures())
            {
                throw new NotSupportedException("CultureInfo does not have the method GetCultures");
            }

            return s_validCultures.Value;
#endif
        }

        /// <summary>
        /// Initialize static fields. Doesn't need to be thread safe.
        /// </summary>
        private static void Initialize()
        {
            if (s_initialized) return;

            s_assemblylocationProperty = typeof(Assembly).GetProperty("Location", typeof(string));
            s_cultureInfoGetCultureMethod = typeof(CultureInfo).GetMethod("GetCultures");

            s_initialized = true;
        }

        private static Assembly GetEntryAssembly()
        {
#if FEATURE_ASSEMBLY_GETENTRYASSEMBLY
            return System.Reflection.Assembly.GetEntryAssembly();
#else
            var getEntryAssembly = typeof(Assembly).GetMethod("GetEntryAssembly");

            ErrorUtilities.VerifyThrowInternalNull(getEntryAssembly, "Assembly does not have the method GetEntryAssembly");

            return (Assembly) getEntryAssembly.Invoke(null, Array.Empty<object>());
#endif
        }

#if !FEATURE_CULTUREINFO_GETCULTURES
        private static CultureInfo[] GetValidCultures()
        {
            var cultureTypesType = s_cultureInfoGetCultureMethod?.GetParameters().FirstOrDefault()?.ParameterType;

            ErrorUtilities.VerifyThrow(cultureTypesType != null &&
                                       cultureTypesType.Name == "CultureTypes" &&
                                       Enum.IsDefined(cultureTypesType, "AllCultures"),
                                       "GetCulture is expected to accept CultureTypes.AllCultures");

            var allCulturesEnumValue = Enum.Parse(cultureTypesType, "AllCultures", true);

            var cultures = s_cultureInfoGetCultureMethod.Invoke(null, new[] {allCulturesEnumValue}) as CultureInfo[];

            ErrorUtilities.VerifyThrowInternalNull(cultures, "CultureInfo.GetCultures should work if all reflection checks pass");

            return cultures;
        }
#endif
    }
}
