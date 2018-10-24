using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Runtime.Versioning;

using Microsoft.Build.Shared;

namespace Microsoft.Build.Tasks
{
    internal sealed partial class SystemState
    {
        private class SystemStateCacheHydrator
        {
            internal static Dictionary<string, FileState> HydrateSystemStateCache(SystemStateCachePayload cachePayload)
            {
                var cache = new Dictionary<string, FileState>(StringComparer.OrdinalIgnoreCase);

                foreach (FileStatePayload fileStatePayload in cachePayload.InstanceLocalFileStateCache)
                {
                    cache[fileStatePayload.Path] = HydrateFileState(fileStatePayload);
                }

                return cache;
            }

            private static FileState HydrateFileState(FileStatePayload fileStatePayload)
            {
                DateTime lastModified = HydrateLastModified(fileStatePayload.LastModified);
                AssemblyNameExtension assembly = HydrateAssembly(fileStatePayload.Assembly);
                AssemblyNameExtension[] dependencies = HydrateDependencies(fileStatePayload.Dependencies);
                FrameworkName frameworkName = HydrateFrameworkName(fileStatePayload.FrameworkName);

                return new FileState(lastModified)
                {
                    Assembly = assembly,
                    dependencies = dependencies,
                    scatterFiles = fileStatePayload.ScatterFiles,
                    runtimeVersion = fileStatePayload.RuntimeVersion,
                    frameworkName = frameworkName
                };
            }

            private static DateTime HydrateLastModified(DateTimePayload lastModifiedPayload) =>
                new DateTime(lastModifiedPayload.Ticks, lastModifiedPayload.Kind);

            private static FrameworkName HydrateFrameworkName(FrameworkNamePayload frameworkNamePayload)
            {
                if (frameworkNamePayload == null)
                {
                    return null;
                }

                Version version = HydrateVersion(frameworkNamePayload.Version);

                return new FrameworkName(frameworkNamePayload.Identifier, version, frameworkNamePayload.Profile);
            }

            private static AssemblyNameExtension[] HydrateDependencies(AssemblyNameExtensionPayload[] dependenciesPayload)
            {
                if (dependenciesPayload == null)
                {
                    return null;
                }

                var dependencies = new AssemblyNameExtension[dependenciesPayload.Length];

                for (int i = 0; i < dependenciesPayload.Length; i++)
                {
                    dependencies[i] = HydrateAssembly(dependenciesPayload[i]);
                }

                return dependencies;
            }

            private static AssemblyNameExtension HydrateAssembly(AssemblyNameExtensionPayload assemblyPayload)
            {
                if (assemblyPayload == null)
                {
                    return null;
                }

                AssemblyName asAssemblyName = HydrateAsAssemblyName(assemblyPayload.AsAssemblyName);
                HashSet<AssemblyNameExtension> remappedFrom = HydrateRemappedFrom(assemblyPayload.RemappedFrom);
                var assemblyNameState = new AssemblyNameExtensionState
                {
                    AsAssemblyName = asAssemblyName,
                    AsString = assemblyPayload.AsString,
                    IsSimpleName = assemblyPayload.IsSimpleName,
                    HasProcessorArchitectureInFusionName = assemblyPayload.HasProcessorArchitectureInFusionName,
                    Immutable = assemblyPayload.Immutable,
                    RemappedFrom = remappedFrom
                };

                return new AssemblyNameExtension(assemblyNameState);
            }

            private static HashSet<AssemblyNameExtension> HydrateRemappedFrom(AssemblyNameExtensionPayload[] remappedFromPayload)
            {
                if (remappedFromPayload == null)
                {
                    return null;
                }

                var remappedFrom = new HashSet<AssemblyNameExtension>();

                foreach (AssemblyNameExtensionPayload assemblyNamePayload in remappedFromPayload)
                {
                    AssemblyNameExtension assembly = HydrateAssembly(assemblyNamePayload);
                    remappedFrom.Add(assembly);
                }

                return remappedFrom;
            }

            private static AssemblyName HydrateAsAssemblyName(AssemblyNamePayload asAssemblyNamePayload)
            {
                if (asAssemblyNamePayload == null)
                {
                    return null;
                }

                Version version = HydrateVersion(asAssemblyNamePayload.Version);
                CultureInfo cultureInfo = HydrateCultureInfo(asAssemblyNamePayload.CultureInfo);
                StrongNameKeyPair keyPair = HydrateKeyPair(asAssemblyNamePayload.KeyPair);

                var asAssemblyName = new AssemblyName
                {
                    Name = asAssemblyNamePayload.Name,
                    Version = version,
                    Flags = asAssemblyNamePayload.Flags,
                    ProcessorArchitecture = asAssemblyNamePayload.ProcessorArchitecture,
                    CultureInfo = cultureInfo,
                    HashAlgorithm = asAssemblyNamePayload.HashAlgorithm,
                    VersionCompatibility = asAssemblyNamePayload.VersionCompatibility,
                    CodeBase = asAssemblyNamePayload.CodeBase,
                    KeyPair = keyPair
                };
                asAssemblyName.SetPublicKey(asAssemblyNamePayload.PublicKey);
                asAssemblyName.SetPublicKeyToken(asAssemblyNamePayload.PublicKeyToken);

                return asAssemblyName;
            }

            private static Version HydrateVersion(VersionPayload versionPayload)
            {
                if (versionPayload == null)
                {
                    return null;
                }
                if (versionPayload.Build < 0 && versionPayload.Revision < 0)
                {
                    return new Version(versionPayload.Major, versionPayload.Minor);
                }
                if (versionPayload.Revision < 0)
                {
                    return new Version(versionPayload.Major, versionPayload.Minor, versionPayload.Build);
                }

                return new Version
                (
                    versionPayload.Major,
                    versionPayload.Minor,
                    versionPayload.Build,
                    versionPayload.Revision
                );
            }

            private static CultureInfo HydrateCultureInfo(CultureInfoPayload infoPayload)
            {
                return infoPayload == null ? null : new CultureInfo(infoPayload.LCID);
            }

            private static StrongNameKeyPair HydrateKeyPair(StrongNameKeyPairPayload keyPairPayload)
            {
                return keyPairPayload == null ? null : new StrongNameKeyPair(keyPairPayload.PublicKey);
            }
        }
    }
}
