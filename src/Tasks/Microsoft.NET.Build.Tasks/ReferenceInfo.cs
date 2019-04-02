// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Build.Framework;

namespace Microsoft.NET.Build.Tasks
{
    internal class ReferenceInfo
    {
        public string Name { get; }
        public string Version { get; }
        public string FullPath { get; }
        public string FileName => Path.GetFileName(FullPath);

        public string PackageName { get; }
        public string PackageVersion { get; }
        public string PathInPackage { get; }

        private List<ResourceAssemblyInfo> _resourceAssemblies;
        public IEnumerable<ResourceAssemblyInfo> ResourceAssemblies
        {
            get { return _resourceAssemblies; }
        }

        private ReferenceInfo(string name, string version, string fullPath,
            string packageName, string packageVersion, string pathInPackage)
        {
            Name = name;
            Version = version;
            FullPath = fullPath;
            PackageName = packageName;
            PackageVersion = packageVersion;
            PathInPackage = pathInPackage;

            _resourceAssemblies = new List<ResourceAssemblyInfo>();
        }

        public static IEnumerable<ReferenceInfo> CreateReferenceInfos(IEnumerable<ITaskItem> referencePaths)
        {
            List<ReferenceInfo> referenceInfos = new List<ReferenceInfo>();
            foreach (ITaskItem referencePath in referencePaths)
            {
                referenceInfos.Add(CreateReferenceInfo(referencePath));
            }

            return referenceInfos;
        }

        public static IEnumerable<ReferenceInfo> CreateDirectReferenceInfos(
            IEnumerable<ITaskItem> referencePaths,
            IEnumerable<ITaskItem> referenceSatellitePaths)
        {
            IEnumerable<ITaskItem> directReferencePaths = referencePaths
                .Where(r => r.HasMetadataValue("CopyLocal", "true") &&
                            r.HasMetadataValue("ReferenceSourceTarget", "ResolveAssemblyReference") &&
                            !IsNuGetReference(r));

            return CreateFilteredReferenceInfos(directReferencePaths, referenceSatellitePaths);
        }

        private static bool IsNuGetReference(ITaskItem reference)
        {
            return !string.IsNullOrEmpty(reference.GetMetadata("NuGetSourceType")) &&
                   reference.GetMetadata("NuGetIsFrameworkReference") != "true";
        }

        public static IEnumerable<ReferenceInfo> CreateDependencyReferenceInfos(
            IEnumerable<ITaskItem> referenceDependencyPaths,
            IEnumerable<ITaskItem> referenceSatellitePaths)
        {
            IEnumerable<ITaskItem> indirectReferencePaths = referenceDependencyPaths
                .Where(r => r.HasMetadataValue("CopyLocal", "true") &&
                            !IsNuGetReference(r));

            return CreateFilteredReferenceInfos(indirectReferencePaths, referenceSatellitePaths);
        }

        private static IEnumerable<ReferenceInfo> CreateFilteredReferenceInfos(
            IEnumerable<ITaskItem> referencePaths,
            IEnumerable<ITaskItem> referenceSatellitePaths)
        {
            Dictionary<string, ReferenceInfo> directReferences = new Dictionary<string, ReferenceInfo>();

            foreach (ITaskItem referencePath in referencePaths)
            {
                ReferenceInfo referenceInfo = CreateReferenceInfo(referencePath);
                directReferences.Add(referenceInfo.FullPath, referenceInfo);
            }

            foreach (ITaskItem referenceSatellitePath in referenceSatellitePaths)
            {
                string originalItemSpec = referenceSatellitePath.GetMetadata("OriginalItemSpec");
                if (!string.IsNullOrEmpty(originalItemSpec))
                {
                    ReferenceInfo referenceInfo;
                    if (directReferences.TryGetValue(originalItemSpec, out referenceInfo))
                    {
                        ResourceAssemblyInfo resourceAssemblyInfo =
                            ResourceAssemblyInfo.CreateFromReferenceSatellitePath(referenceSatellitePath);
                        referenceInfo._resourceAssemblies.Add(resourceAssemblyInfo);
                    }
                }
            }

            return directReferences.Values;
        }

        internal static ReferenceInfo CreateReferenceInfo(ITaskItem referencePath)
        {
            string fullPath = referencePath.ItemSpec;
            string name = Path.GetFileNameWithoutExtension(fullPath);
            string version = GetVersion(referencePath);

            var packageName = referencePath.GetMetadata(MetadataKeys.NuGetPackageId);
            if (string.IsNullOrEmpty(packageName))
            {
                packageName = referencePath.GetMetadata(MetadataKeys.PackageName);
            }

            var packageVersion = referencePath.GetMetadata(MetadataKeys.NuGetPackageVersion);
            if (string.IsNullOrEmpty(packageVersion))
            {
                packageVersion = referencePath.GetMetadata(MetadataKeys.PackageVersion);
            }

            var pathInPackage = referencePath.GetMetadata(MetadataKeys.PathInPackage);

            return new ReferenceInfo(name, version, fullPath,
                packageName, packageVersion, pathInPackage);
        }

        private static string GetVersion(ITaskItem referencePath)
        {
            string version = referencePath.GetMetadata("Version");

            if (string.IsNullOrEmpty(version))
            {
                string fusionName = referencePath.GetMetadata("FusionName");
                if (!string.IsNullOrEmpty(fusionName))
                {
                    AssemblyName assemblyName = new AssemblyName(fusionName);
                    version = assemblyName.Version.ToString();
                }

                if (string.IsNullOrEmpty(version))
                {
                    // Use 0.0.0.0 as placeholder, if we can't find a version any
                    // other way
                    version = "0.0.0.0";
                }
            }

            return version;
        }
    }
}
