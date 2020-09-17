// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using MessagePack;
using MessagePack.Formatters;
using Microsoft.Build.Tasks.ResolveAssemblyReferences.Contract;

namespace Microsoft.Build.Tasks.ResolveAssemblyReferences.Formatters
{
    internal sealed class RequestFormatter : IMessagePackFormatter<ResolveAssemblyReferenceRequest>
    {
        public void Serialize(ref MessagePackWriter writer, ResolveAssemblyReferenceRequest value, MessagePackSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNil();
                return;
            }

            IFormatterResolver formatterResolver = options.Resolver;
            writer.WriteArrayHeader(43);
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
            writer.Write(value.AssemblyInformationCacheOutputPath);
            formatterResolver.GetFormatter<ReadOnlyTaskItem[]>().Serialize(ref writer, value.AssemblyInformationCachePaths, options);
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
            ResolveAssemblyReferenceRequest result = new ResolveAssemblyReferenceRequest();

            for (int i = 0; i < length; i++)
            {
                int key = i;

                switch (key)
                {
                    case 0:
                        result.AllowedAssemblyExtensions = formatterResolver.GetFormatter<string[]>().Deserialize(ref reader, options);
                        break;
                    case 1:
                        result.AllowedRelatedFileExtensions = formatterResolver.GetFormatter<string[]>().Deserialize(ref reader, options);
                        break;
                    case 2:
                        result.AppConfigFile = reader.ReadString();
                        break;
                    case 3:
                        result.Assemblies = formatterResolver.GetFormatter<ReadOnlyTaskItem[]>().Deserialize(ref reader, options);
                        break;
                    case 4:
                        result.AssemblyFiles = formatterResolver.GetFormatter<ReadOnlyTaskItem[]>().Deserialize(ref reader, options);
                        break;
                    case 5:
                        result.AutoUnify = reader.ReadBoolean();
                        break;
                    case 6:
                        result.CandidateAssemblyFiles = formatterResolver.GetFormatter<string[]>().Deserialize(ref reader, options);
                        break;
                    case 7:
                        result.CopyLocalDependenciesWhenParentReferenceInGac = reader.ReadBoolean();
                        break;
                    case 8:
                        result.DoNotCopyLocalIfInGac = reader.ReadBoolean();
                        break;
                    case 9:
                        result.FindDependencies = reader.ReadBoolean();
                        break;
                    case 10:
                        result.FindDependenciesOfExternallyResolvedReferences = reader.ReadBoolean();
                        break;
                    case 11:
                        result.FindRelatedFiles = reader.ReadBoolean();
                        break;
                    case 12:
                        result.FindSatellites = reader.ReadBoolean();
                        break;
                    case 13:
                        result.FindSerializationAssemblies = reader.ReadBoolean();
                        break;
                    case 14:
                        result.FullFrameworkAssemblyTables = formatterResolver.GetFormatter<ReadOnlyTaskItem[]>().Deserialize(ref reader, options);
                        break;
                    case 15:
                        result.FullFrameworkFolders = formatterResolver.GetFormatter<string[]>().Deserialize(ref reader, options);
                        break;
                    case 16:
                        result.FullTargetFrameworkSubsetNames = formatterResolver.GetFormatter<string[]>().Deserialize(ref reader, options);
                        break;
                    case 17:
                        result.IgnoreDefaultInstalledAssemblySubsetTables = reader.ReadBoolean();
                        break;
                    case 18:
                        result.IgnoreDefaultInstalledAssemblyTables = reader.ReadBoolean();
                        break;
                    case 19:
                        result.IgnoreTargetFrameworkAttributeVersionMismatch = reader.ReadBoolean();
                        break;
                    case 20:
                        result.IgnoreVersionForFrameworkReferences = reader.ReadBoolean();
                        break;
                    case 21:
                        result.InstalledAssemblySubsetTables = formatterResolver.GetFormatter<ReadOnlyTaskItem[]>().Deserialize(ref reader, options);
                        break;
                    case 22:
                        result.InstalledAssemblyTables = formatterResolver.GetFormatter<ReadOnlyTaskItem[]>().Deserialize(ref reader, options);
                        break;
                    case 23:
                        result.LatestTargetFrameworkDirectories = formatterResolver.GetFormatter<string[]>().Deserialize(ref reader, options);
                        break;
                    case 24:
                        result.ProfileName = reader.ReadString();
                        break;
                    case 25:
                        result.ResolvedSDKReferences = formatterResolver.GetFormatter<ReadOnlyTaskItem[]>().Deserialize(ref reader, options);
                        break;
                    case 26:
                        result.SearchPaths = formatterResolver.GetFormatter<string[]>().Deserialize(ref reader, options);
                        break;
                    case 27:
                        result.Silent = reader.ReadBoolean();
                        break;
                    case 28:
                        result.StateFile = reader.ReadString();
                        break;
                    case 29:
                        result.SupportsBindingRedirectGeneration = reader.ReadBoolean();
                        break;
                    case 30:
                        result.TargetedRuntimeVersion = reader.ReadString();
                        break;
                    case 31:
                        result.TargetFrameworkDirectories = formatterResolver.GetFormatter<string[]>().Deserialize(ref reader, options);
                        break;
                    case 32:
                        result.TargetFrameworkMoniker = reader.ReadString();
                        break;
                    case 33:
                        result.TargetFrameworkMonikerDisplayName = reader.ReadString();
                        break;
                    case 34:
                        result.TargetFrameworkSubsets = formatterResolver.GetFormatter<string[]>().Deserialize(ref reader, options);
                        break;
                    case 35:
                        result.TargetFrameworkVersion = reader.ReadString();
                        break;
                    case 36:
                        result.TargetProcessorArchitecture = reader.ReadString();
                        break;
                    case 37:
                        result.UnresolveFrameworkAssembliesFromHigherFrameworks = reader.ReadBoolean();
                        break;
                    case 38:
                        result.UseResolveAssemblyReferenceService = reader.ReadBoolean();
                        break;
                    case 39:
                        result.WarnOrErrorOnTargetArchitectureMismatch = reader.ReadString();
                        break;
                    case 40:
                        result.CurrentPath = reader.ReadString();
                        break;
                    case 41:
                        result.AssemblyInformationCacheOutputPath = reader.ReadString();
                        break;
                    case 42:
                        result.AssemblyInformationCachePaths = formatterResolver.GetFormatter<ReadOnlyTaskItem[]>().Deserialize(ref reader, options);
                        break;
                    default:
                        reader.Skip();
                        break;
                }
            }

            reader.Depth--;
            return result;
        }
    }
}
