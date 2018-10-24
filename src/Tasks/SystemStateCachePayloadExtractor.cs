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
        private class SystemStateCachePayloadExtractor
        {
            internal static SystemStateCachePayload ExtractSystemStateCachePayload(Dictionary<string, FileState> cache)
            {
                var cachePayload = new FileStatePayload[cache.Count];
                int cachePayloadCount = 0;

                foreach (KeyValuePair<string, FileState> pathWithFileState in cache)
                {
                    string path = pathWithFileState.Key;
                    FileState fileState = pathWithFileState.Value;
                    cachePayload[cachePayloadCount] = ExtractFileStatePayload(fileState, path);
                    cachePayloadCount++;
                }

                return new SystemStateCachePayload { InstanceLocalFileStateCache = cachePayload };
            }

            private static FileStatePayload ExtractFileStatePayload(FileState fileState, string path)
            {
                DateTimePayload lastModifiedPayload = ExtractLastModifiedPayload(fileState.LastModified);
                AssemblyNameExtensionPayload assemblyPayload = ExtractAssemblyPayload(fileState.Assembly);
                AssemblyNameExtensionPayload[] dependenciesPayload = ExtractDependenciesPayload(fileState.dependencies);
                FrameworkNamePayload frameworkName = ExtractFrameworkNamePayload(fileState.frameworkName);

                return new FileStatePayload
                {
                    Path = path,
                    LastModified = lastModifiedPayload,
                    Assembly = assemblyPayload,
                    Dependencies = dependenciesPayload,
                    ScatterFiles = fileState.scatterFiles,
                    RuntimeVersion = fileState.RuntimeVersion,
                    FrameworkName = frameworkName
                };
            }

            private static DateTimePayload ExtractLastModifiedPayload(DateTime lastModified) =>
                new DateTimePayload
                {
                    Ticks = lastModified.Ticks,
                    Kind = lastModified.Kind
                };

            private static FrameworkNamePayload ExtractFrameworkNamePayload(FrameworkName frameworkName)
            {
                if (frameworkName == null)
                {
                    return null;
                }

                VersionPayload versionPayload = ExtractVersionPayload(frameworkName.Version);

                return new FrameworkNamePayload
                {
                    Version = versionPayload,
                    Identifier = frameworkName.Identifier,
                    Profile = frameworkName.Profile
                };
            }

            private static AssemblyNameExtensionPayload[] ExtractDependenciesPayload(AssemblyNameExtension[] dependencies)
            {
                if (dependencies == null)
                {
                    return null;
                }

                var dependenciesPayload = new AssemblyNameExtensionPayload[dependencies.Length];

                for (int i = 0; i < dependencies.Length; i++)
                {
                    dependenciesPayload[i] = ExtractAssemblyPayload(dependencies[i]);
                }

                return dependenciesPayload;
            }

            private static AssemblyNameExtensionPayload ExtractAssemblyPayload(AssemblyNameExtension assembly)
            {
                if (assembly == null)
                {
                    return null;
                }

                AssemblyNameExtensionState assemblyNameState = assembly.GetState();
                AssemblyNamePayload asAssemblyNamePayload = ExtractAsAssemblyNamePayload(assemblyNameState.AsAssemblyName);
                AssemblyNameExtensionPayload[] remappedFromPayload = ExtractRemappedFromPayload(assemblyNameState.RemappedFrom);

                return new AssemblyNameExtensionPayload
                {
                    AsAssemblyName = asAssemblyNamePayload,
                    AsString = assemblyNameState.AsString,
                    IsSimpleName = assemblyNameState.IsSimpleName,
                    HasProcessorArchitectureInFusionName = assemblyNameState.HasProcessorArchitectureInFusionName,
                    Immutable = assemblyNameState.Immutable,
                    RemappedFrom = remappedFromPayload
                };
            }

            private static AssemblyNameExtensionPayload[] ExtractRemappedFromPayload(HashSet<AssemblyNameExtension> remappedFrom)
            {
                if (remappedFrom == null)
                {
                    return null;
                }

                var remappedFromPayload = new AssemblyNameExtensionPayload[remappedFrom.Count];
                int remappedFromPayloadCount = 0;

                foreach (AssemblyNameExtension assemblyName in remappedFrom)
                {
                    remappedFromPayload[remappedFromPayloadCount] = ExtractAssemblyPayload(assemblyName);
                    remappedFromPayloadCount++;
                }

                return remappedFromPayload;
            }

            private static AssemblyNamePayload ExtractAsAssemblyNamePayload(AssemblyName asAssemblyName)
            {
                if (asAssemblyName == null)
                {
                    return null;
                }

                VersionPayload versionPayload = ExtractVersionPayload(asAssemblyName.Version);
                CultureInfoPayload cultureInfoPayload = ExtractCultureInfoPayload(asAssemblyName.CultureInfo);
                StrongNameKeyPairPayload keyPairPayload = ExtractKeyPairPayload(asAssemblyName.KeyPair);

                return new AssemblyNamePayload
                {
                    Name = asAssemblyName.Name,
                    PublicKey = asAssemblyName.GetPublicKey(),
                    PublicKeyToken = asAssemblyName.GetPublicKeyToken(),
                    Version = versionPayload,
                    Flags = asAssemblyName.Flags,
                    ProcessorArchitecture = asAssemblyName.ProcessorArchitecture,
                    CultureInfo = cultureInfoPayload,
                    HashAlgorithm = asAssemblyName.HashAlgorithm,
                    VersionCompatibility = asAssemblyName.VersionCompatibility,
                    CodeBase = asAssemblyName.CodeBase,
                    KeyPair = keyPairPayload
                };
            }

            private static VersionPayload ExtractVersionPayload(Version version)
            {
                return version == null
                    ? null
                    : new VersionPayload
                    {
                        Major = version.Major,
                        Minor = version.Minor,
                        Build = version.Build,
                        Revision = version.Revision
                    };
            }

            private static CultureInfoPayload ExtractCultureInfoPayload(CultureInfo info)
            {
                return info == null ? null : new CultureInfoPayload { LCID = info.LCID };
            }

            private static StrongNameKeyPairPayload ExtractKeyPairPayload(StrongNameKeyPair keyPair)
            {
                return keyPair == null
                    ? null
                    : new StrongNameKeyPairPayload
                    {
                        PublicKey = keyPair.PublicKey
                    };
            }
        }

    }
}
