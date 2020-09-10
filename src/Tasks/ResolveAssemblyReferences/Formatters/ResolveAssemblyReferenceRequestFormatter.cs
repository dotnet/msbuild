using System;
using System.Buffers;
using MessagePack;
using MessagePack.Formatters;
using Microsoft.Build.Tasks.ResolveAssemblyReferences.Contract;

namespace Microsoft.Build.Tasks.ResolveAssemblyReferences.Formatters
{
    internal sealed class ResolveAssemblyReferenceRequestFormatter : IMessagePackFormatter<ResolveAssemblyReferenceRequest>
    {
        public void Serialize(ref MessagePackWriter writer, ResolveAssemblyReferenceRequest value, MessagePackSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNil();
                return;
            }

            IFormatterResolver formatterResolver = options.Resolver;
            writer.WriteArrayHeader(41);
            formatterResolver.GetFormatter<string[]>().Serialize(ref writer, value.AllowedAssemblyExtensions, options);
            formatterResolver.GetFormatter<string[]>().Serialize(ref writer, value.AllowedRelatedFileExtensions, options);
            writer.Write(value.AppConfigFile);
            formatterResolver.GetFormatter<ReadOnlyTaskItem[]>().Serialize(ref writer, value.Assemblies, options);
            formatterResolver.GetFormatter<ReadOnlyTaskItem[]>().Serialize(ref writer, value.AssemblyFiles, options);
            writer.Write(value.AutoUnify);
            formatterResolver.GetFormatter<string[]>().Serialize(ref writer, value.CandidateAssemblyFiles, options);
            writer.Write(value.CopyLocalDependenciesWhenParentReferenceInGac);
            writer.Write(value.DoNotCopyLocalIfInGac);
            writer.Write(value.FindDependencies);
            writer.Write(value.FindDependenciesOfExternallyResolvedReferences);
            writer.Write(value.FindRelatedFiles);
            writer.Write(value.FindSatellites);
            writer.Write(value.FindSerializationAssemblies);
            formatterResolver.GetFormatter<ReadOnlyTaskItem[]>().Serialize(ref writer, value.FullFrameworkAssemblyTables, options);
            formatterResolver.GetFormatter<string[]>().Serialize(ref writer, value.FullFrameworkFolders, options);
            formatterResolver.GetFormatter<string[]>().Serialize(ref writer, value.FullTargetFrameworkSubsetNames, options);
            writer.Write(value.IgnoreDefaultInstalledAssemblySubsetTables);
            writer.Write(value.IgnoreDefaultInstalledAssemblyTables);
            writer.Write(value.IgnoreTargetFrameworkAttributeVersionMismatch);
            writer.Write(value.IgnoreVersionForFrameworkReferences);
            formatterResolver.GetFormatter<ReadOnlyTaskItem[]>().Serialize(ref writer, value.InstalledAssemblySubsetTables, options);
            formatterResolver.GetFormatter<ReadOnlyTaskItem[]>().Serialize(ref writer, value.InstalledAssemblyTables, options);
            formatterResolver.GetFormatter<string[]>().Serialize(ref writer, value.LatestTargetFrameworkDirectories, options);
            writer.Write(value.ProfileName);
            formatterResolver.GetFormatter<ReadOnlyTaskItem[]>().Serialize(ref writer, value.ResolvedSDKReferences, options);
            formatterResolver.GetFormatter<string[]>().Serialize(ref writer, value.SearchPaths, options);
            writer.Write(value.Silent);
            writer.Write(value.StateFile);
            writer.Write(value.SupportsBindingRedirectGeneration);
            writer.Write(value.TargetedRuntimeVersion);
            formatterResolver.GetFormatter<string[]>().Serialize(ref writer, value.TargetFrameworkDirectories, options);
            writer.Write(value.TargetFrameworkMoniker);
            writer.Write(value.TargetFrameworkMonikerDisplayName);
            formatterResolver.GetFormatter<string[]>().Serialize(ref writer, value.TargetFrameworkSubsets, options);
            writer.Write(value.TargetFrameworkVersion);
            writer.Write(value.TargetProcessorArchitecture);
            writer.Write(value.UnresolveFrameworkAssembliesFromHigherFrameworks);
            writer.Write(value.UseResolveAssemblyReferenceService);
            writer.Write(value.WarnOrErrorOnTargetArchitectureMismatch);
            writer.Write(value.CurrentPath);
        }

        public ResolveAssemblyReferenceRequest Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil())
            {
                return null;
            }

            options.Security.DepthStep(ref reader);
            IFormatterResolver formatterResolver = options.Resolver;
            int length = reader.ReadArrayHeader();
            string[] allowedAssemblyExtensions = default;
            string[] allowedRelatedFileExtensions = default;
            string appConfigFile = default;
            ReadOnlyTaskItem[] assemblies = default;
            ReadOnlyTaskItem[] assemblyFiles = default;
            bool autoUnify = default;
            string[] candidateAssemblyFiles = default;
            bool copyLocalDependenciesWhenParentReferenceInGac = default;
            bool doNotCopyLocalIfInGac = default;
            bool findDependencies = default;
            bool findDependenciesOfExternallyResolvedReferences = default;
            bool findRelatedFiles = default;
            bool findSatellites = default;
            bool findSerializationAssemblies = default;
            ReadOnlyTaskItem[] fullFrameworkAssemblyTables = default;
            string[] fullFrameworkFolders = default;
            string[] fullTargetFrameworkSubsetNames = default;
            bool ignoreDefaultInstalledAssemblySubsetTables = default;
            bool ignoreDefaultInstalledAssemblyTables = default;
            bool ignoreTargetFrameworkAttributeVersionMismatch = default;
            bool ignoreVersionForFrameworkReferences = default;
            ReadOnlyTaskItem[] installedAssemblySubsetTables = default;
            ReadOnlyTaskItem[] installedAssemblyTables = default;
            string[] latestTargetFrameworkDirectories = default;
            string profileName = default;
            ReadOnlyTaskItem[] resolvedSDKReferences = default;
            string[] searchPaths = default;
            bool silent = default;
            string stateFile = default;
            bool supportsBindingRedirectGeneration = default;
            string targetedRuntimeVersion = default;
            string[] targetFrameworkDirectories = default;
            string targetFrameworkMoniker = default;
            string targetFrameworkMonikerDisplayName = default;
            string[] targetFrameworkSubsets = default;
            string targetFrameworkVersion = default;
            string targetProcessorArchitecture = default;
            bool unresolveFrameworkAssembliesFromHigherFrameworks = default;
            bool useResolveAssemblyReferenceService = default;
            string warnOrErrorOnTargetArchitectureMismatch = default;
            string currentPath = default;

            for (int i = 0; i < length; i++)
            {
                int key = i;

                switch (key)
                {
                    case 0:
                        allowedAssemblyExtensions = formatterResolver.GetFormatter<string[]>().Deserialize(ref reader, options);
                        break;
                    case 1:
                        allowedRelatedFileExtensions = formatterResolver.GetFormatter<string[]>().Deserialize(ref reader, options);
                        break;
                    case 2:
                        appConfigFile = reader.ReadString();
                        break;
                    case 3:
                        assemblies = formatterResolver.GetFormatter<ReadOnlyTaskItem[]>().Deserialize(ref reader, options);
                        break;
                    case 4:
                        assemblyFiles = formatterResolver.GetFormatter<ReadOnlyTaskItem[]>().Deserialize(ref reader, options);
                        break;
                    case 5:
                        autoUnify = reader.ReadBoolean();
                        break;
                    case 6:
                        candidateAssemblyFiles = formatterResolver.GetFormatter<string[]>().Deserialize(ref reader, options);
                        break;
                    case 7:
                        copyLocalDependenciesWhenParentReferenceInGac = reader.ReadBoolean();
                        break;
                    case 8:
                        doNotCopyLocalIfInGac = reader.ReadBoolean();
                        break;
                    case 9:
                        findDependencies = reader.ReadBoolean();
                        break;
                    case 10:
                        findDependenciesOfExternallyResolvedReferences = reader.ReadBoolean();
                        break;
                    case 11:
                        findRelatedFiles = reader.ReadBoolean();
                        break;
                    case 12:
                        findSatellites = reader.ReadBoolean();
                        break;
                    case 13:
                        findSerializationAssemblies = reader.ReadBoolean();
                        break;
                    case 14:
                        fullFrameworkAssemblyTables = formatterResolver.GetFormatter<ReadOnlyTaskItem[]>().Deserialize(ref reader, options);
                        break;
                    case 15:
                        fullFrameworkFolders = formatterResolver.GetFormatter<string[]>().Deserialize(ref reader, options);
                        break;
                    case 16:
                        fullTargetFrameworkSubsetNames = formatterResolver.GetFormatter<string[]>().Deserialize(ref reader, options);
                        break;
                    case 17:
                        ignoreDefaultInstalledAssemblySubsetTables = reader.ReadBoolean();
                        break;
                    case 18:
                        ignoreDefaultInstalledAssemblyTables = reader.ReadBoolean();
                        break;
                    case 19:
                        ignoreTargetFrameworkAttributeVersionMismatch = reader.ReadBoolean();
                        break;
                    case 20:
                        ignoreVersionForFrameworkReferences = reader.ReadBoolean();
                        break;
                    case 21:
                        installedAssemblySubsetTables = formatterResolver.GetFormatter<ReadOnlyTaskItem[]>().Deserialize(ref reader, options);
                        break;
                    case 22:
                        installedAssemblyTables = formatterResolver.GetFormatter<ReadOnlyTaskItem[]>().Deserialize(ref reader, options);
                        break;
                    case 23:
                        latestTargetFrameworkDirectories = formatterResolver.GetFormatter<string[]>().Deserialize(ref reader, options);
                        break;
                    case 24:
                        profileName = reader.ReadString();
                        break;
                    case 25:
                        resolvedSDKReferences = formatterResolver.GetFormatter<ReadOnlyTaskItem[]>().Deserialize(ref reader, options);
                        break;
                    case 26:
                        searchPaths = formatterResolver.GetFormatter<string[]>().Deserialize(ref reader, options);
                        break;
                    case 27:
                        silent = reader.ReadBoolean();
                        break;
                    case 28:
                        stateFile = reader.ReadString();
                        break;
                    case 29:
                        supportsBindingRedirectGeneration = reader.ReadBoolean();
                        break;
                    case 30:
                        targetedRuntimeVersion = reader.ReadString();
                        break;
                    case 31:
                        targetFrameworkDirectories = formatterResolver.GetFormatter<string[]>().Deserialize(ref reader, options);
                        break;
                    case 32:
                        targetFrameworkMoniker = reader.ReadString();
                        break;
                    case 33:
                        targetFrameworkMonikerDisplayName = reader.ReadString();
                        break;
                    case 34:
                        targetFrameworkSubsets = formatterResolver.GetFormatter<string[]>().Deserialize(ref reader, options);
                        break;
                    case 35:
                        targetFrameworkVersion = reader.ReadString();
                        break;
                    case 36:
                        targetProcessorArchitecture = reader.ReadString();
                        break;
                    case 37:
                        unresolveFrameworkAssembliesFromHigherFrameworks = reader.ReadBoolean();
                        break;
                    case 38:
                        useResolveAssemblyReferenceService = reader.ReadBoolean();
                        break;
                    case 39:
                        warnOrErrorOnTargetArchitectureMismatch = reader.ReadString();
                        break;
                    case 40:
                        currentPath = reader.ReadString();
                        break;
                    default:
                        reader.Skip();
                        break;
                }
            }

            ResolveAssemblyReferenceRequest result = new ResolveAssemblyReferenceRequest
            {
                AllowedAssemblyExtensions = allowedAssemblyExtensions,
                AllowedRelatedFileExtensions = allowedRelatedFileExtensions,
                AppConfigFile = appConfigFile,
                Assemblies = assemblies,
                AssemblyFiles = assemblyFiles,
                AutoUnify = autoUnify,
                CandidateAssemblyFiles = candidateAssemblyFiles,
                CopyLocalDependenciesWhenParentReferenceInGac = copyLocalDependenciesWhenParentReferenceInGac,
                DoNotCopyLocalIfInGac = doNotCopyLocalIfInGac,
                FindDependencies = findDependencies,
                FindDependenciesOfExternallyResolvedReferences = findDependenciesOfExternallyResolvedReferences,
                FindRelatedFiles = findRelatedFiles,
                FindSatellites = findSatellites,
                FindSerializationAssemblies = findSerializationAssemblies,
                FullFrameworkAssemblyTables = fullFrameworkAssemblyTables,
                FullFrameworkFolders = fullFrameworkFolders,
                FullTargetFrameworkSubsetNames = fullTargetFrameworkSubsetNames,
                IgnoreDefaultInstalledAssemblySubsetTables = ignoreDefaultInstalledAssemblySubsetTables,
                IgnoreDefaultInstalledAssemblyTables = ignoreDefaultInstalledAssemblyTables,
                IgnoreTargetFrameworkAttributeVersionMismatch = ignoreTargetFrameworkAttributeVersionMismatch,
                IgnoreVersionForFrameworkReferences = ignoreVersionForFrameworkReferences,
                InstalledAssemblySubsetTables = installedAssemblySubsetTables,
                InstalledAssemblyTables = installedAssemblyTables,
                LatestTargetFrameworkDirectories = latestTargetFrameworkDirectories,
                ProfileName = profileName,
                ResolvedSDKReferences = resolvedSDKReferences,
                SearchPaths = searchPaths,
                Silent = silent,
                StateFile = stateFile,
                SupportsBindingRedirectGeneration = supportsBindingRedirectGeneration,
                TargetedRuntimeVersion = targetedRuntimeVersion,
                TargetFrameworkDirectories = targetFrameworkDirectories,
                TargetFrameworkMoniker = targetFrameworkMoniker,
                TargetFrameworkMonikerDisplayName = targetFrameworkMonikerDisplayName,
                TargetFrameworkSubsets = targetFrameworkSubsets,
                TargetFrameworkVersion = targetFrameworkVersion,
                TargetProcessorArchitecture = targetProcessorArchitecture,
                UnresolveFrameworkAssembliesFromHigherFrameworks = unresolveFrameworkAssembliesFromHigherFrameworks,
                UseResolveAssemblyReferenceService = useResolveAssemblyReferenceService,
                WarnOrErrorOnTargetArchitectureMismatch = warnOrErrorOnTargetArchitectureMismatch,
                CurrentPath = currentPath
            };
            reader.Depth--;
            return result;
        }
    }
}
