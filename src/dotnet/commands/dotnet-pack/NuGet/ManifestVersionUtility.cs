// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Linq;
using System.Reflection;

namespace NuGet
{
    internal static class ManifestVersionUtility
    {
        public const int DefaultVersion = 1;
        public const int SemverVersion = 3;
        public const int TargetFrameworkSupportForDependencyContentsAndToolsVersion = 4;
        public const int TargetFrameworkSupportForReferencesVersion = 5;
        public const int XdtTransformationVersion = 6;
        // Note that this version should change from 7 to 8 when the PackageType
        // schema is merged into here
        public const int ServiceableVersion = 7;

        public static int GetManifestVersion(ManifestMetadata metadata)
        {
            return Math.Max(GetVersionFromObject(metadata), GetMaxVersionFromMetadata(metadata));
        }

        private static int GetMaxVersionFromMetadata(ManifestMetadata metadata)
        {
            // Important: always add newer version checks at the top
            if (metadata.Serviceable)
            {
                return ServiceableVersion;
            }

            bool referencesHasTargetFramework =
              metadata.PackageAssemblyReferences != null &&
              metadata.PackageAssemblyReferences.Any(r => r.TargetFramework != null);

            if (referencesHasTargetFramework)
            {
                return TargetFrameworkSupportForReferencesVersion;
            }

            bool dependencyHasTargetFramework =
                metadata.DependencySets != null &&
                metadata.DependencySets.Any(d => d.TargetFramework != null);

            if (dependencyHasTargetFramework)
            {
                return TargetFrameworkSupportForDependencyContentsAndToolsVersion;
            }

            if (metadata.Version.IsPrerelease)
            {
                return SemverVersion;
            }

            return DefaultVersion;
        }

        private static int GetVersionFromObject(object obj)
        {
            // all public, gettable, non-static properties
            return obj?.GetType()
                       .GetRuntimeProperties()
                       .Where(prop => prop.GetMethod != null && prop.GetMethod.IsPublic && !prop.GetMethod.IsStatic)
                       .Select(prop => GetVersionFromPropertyInfo(obj, prop))
                       .Max()
                      ?? DefaultVersion;
        }

        private static int GetVersionFromPropertyInfo(object obj, PropertyInfo property)
        {
            var value = property.GetValue(obj, index: null);
            if (value == null)
            {
                return DefaultVersion;
            }

            int? version = GetPropertyVersion(property);
            if (!version.HasValue)
            {
                return DefaultVersion;
            }

            var stringValue = value as string;
            if (stringValue != null)
            {
                if (!string.IsNullOrEmpty(stringValue))
                {
                    return version.Value;
                }

                return DefaultVersion;
            }

            // For all other object types a null check would suffice.
            return version.Value;
        }

        private static int VisitList(IEnumerable list)
        {
            int version = DefaultVersion;

            foreach (var item in list)
            {
                version = Math.Max(version, GetVersionFromObject(item));
            }

            return version;
        }

        private static int? GetPropertyVersion(PropertyInfo property)
        {
            var attribute = property.GetCustomAttribute<ManifestVersionAttribute>();
            return attribute?.Version;
        }
    }
}
