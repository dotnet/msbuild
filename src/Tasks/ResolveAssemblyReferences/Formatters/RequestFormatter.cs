// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using MessagePack;
using MessagePack.Formatters;
using Microsoft.Build.Framework;
using Microsoft.Build.Tasks.ResolveAssemblyReferences.Contract;

namespace Microsoft.Build.Tasks.ResolveAssemblyReferences.Formatters
{
    internal sealed class RequestFormatter : IMessagePackFormatter<ResolveAssemblyReferenceRequest>
    {
        internal const int MemberCount = 43;

        public void Serialize(ref MessagePackWriter writer, ResolveAssemblyReferenceRequest value, MessagePackSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNil();
                return;
            }

            IFormatterResolver formatterResolver = options.Resolver;
            writer.WriteArrayHeader(MemberCount);
            formatterResolver.GetFormatter<string[]>().Serialize(ref writer, value.AllowedAssemblyExtensions, options);
            formatterResolver.GetFormatter<string[]>().Serialize(ref writer, value.AllowedRelatedFileExtensions, options);
            writer.Write(value.AppConfigFile);
            formatterResolver.GetFormatter<ITaskItem[]>().Serialize(ref writer, value.Assemblies, options);
            formatterResolver.GetFormatter<ITaskItem[]>().Serialize(ref writer, value.AssemblyFiles, options);
            writer.Write(value.AutoUnify);
            formatterResolver.GetFormatter<string[]>().Serialize(ref writer, value.CandidateAssemblyFiles, options);
            writer.Write(value.CopyLocalDependenciesWhenParentReferenceInGac);
            writer.Write(value.DoNotCopyLocalIfInGac);
            writer.Write(value.FindDependencies);
            writer.Write(value.FindDependenciesOfExternallyResolvedReferences);
            writer.Write(value.FindRelatedFiles);
            writer.Write(value.FindSatellites);
            writer.Write(value.FindSerializationAssemblies);
            formatterResolver.GetFormatter<ITaskItem[]>().Serialize(ref writer, value.FullFrameworkAssemblyTables, options);
            formatterResolver.GetFormatter<string[]>().Serialize(ref writer, value.FullFrameworkFolders, options);
            formatterResolver.GetFormatter<string[]>().Serialize(ref writer, value.FullTargetFrameworkSubsetNames, options);
            writer.Write(value.IgnoreDefaultInstalledAssemblySubsetTables);
            writer.Write(value.IgnoreDefaultInstalledAssemblyTables);
            writer.Write(value.IgnoreTargetFrameworkAttributeVersionMismatch);
            writer.Write(value.IgnoreVersionForFrameworkReferences);
            formatterResolver.GetFormatter<ITaskItem[]>().Serialize(ref writer, value.InstalledAssemblySubsetTables, options);
            formatterResolver.GetFormatter<ITaskItem[]>().Serialize(ref writer, value.InstalledAssemblyTables, options);
            formatterResolver.GetFormatter<string[]>().Serialize(ref writer, value.LatestTargetFrameworkDirectories, options);
            writer.Write(value.ProfileName);
            formatterResolver.GetFormatter<ITaskItem[]>().Serialize(ref writer, value.ResolvedSDKReferences, options);
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
            formatterResolver.GetFormatter<ITaskItem[]>().Serialize(ref writer, value.AssemblyInformationCachePaths, options);
        }

        public ResolveAssemblyReferenceRequest Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil())
            {
                return null;
            }

            options.Security.DepthStep(ref reader);
            IFormatterResolver formatterResolver = options.Resolver;
            int _ = reader.ReadArrayHeader(); // Content starts with this
            ResolveAssemblyReferenceRequest result = new ResolveAssemblyReferenceRequest();

            result.AllowedAssemblyExtensions = formatterResolver.GetFormatter<string[]>().Deserialize(ref reader, options);
            result.AllowedRelatedFileExtensions = formatterResolver.GetFormatter<string[]>().Deserialize(ref reader, options);
            result.AppConfigFile = reader.ReadString();
            result.Assemblies = formatterResolver.GetFormatter<ITaskItem[]>().Deserialize(ref reader, options);
            result.AssemblyFiles = formatterResolver.GetFormatter<ITaskItem[]>().Deserialize(ref reader, options);
            result.AutoUnify = reader.ReadBoolean();
            result.CandidateAssemblyFiles = formatterResolver.GetFormatter<string[]>().Deserialize(ref reader, options);
            result.CopyLocalDependenciesWhenParentReferenceInGac = reader.ReadBoolean();
            result.DoNotCopyLocalIfInGac = reader.ReadBoolean();
            result.FindDependencies = reader.ReadBoolean();
            result.FindDependenciesOfExternallyResolvedReferences = reader.ReadBoolean();
            result.FindRelatedFiles = reader.ReadBoolean();
            result.FindSatellites = reader.ReadBoolean();
            result.FindSerializationAssemblies = reader.ReadBoolean();
            result.FullFrameworkAssemblyTables = formatterResolver.GetFormatter<ITaskItem[]>().Deserialize(ref reader, options);
            result.FullFrameworkFolders = formatterResolver.GetFormatter<string[]>().Deserialize(ref reader, options);
            result.FullTargetFrameworkSubsetNames = formatterResolver.GetFormatter<string[]>().Deserialize(ref reader, options);
            result.IgnoreDefaultInstalledAssemblySubsetTables = reader.ReadBoolean();
            result.IgnoreDefaultInstalledAssemblyTables = reader.ReadBoolean();
            result.IgnoreTargetFrameworkAttributeVersionMismatch = reader.ReadBoolean();
            result.IgnoreVersionForFrameworkReferences = reader.ReadBoolean();
            result.InstalledAssemblySubsetTables = formatterResolver.GetFormatter<ITaskItem[]>().Deserialize(ref reader, options);
            result.InstalledAssemblyTables = formatterResolver.GetFormatter<ITaskItem[]>().Deserialize(ref reader, options);
            result.LatestTargetFrameworkDirectories = formatterResolver.GetFormatter<string[]>().Deserialize(ref reader, options);
            result.ProfileName = reader.ReadString();
            result.ResolvedSDKReferences = formatterResolver.GetFormatter<ITaskItem[]>().Deserialize(ref reader, options);
            result.SearchPaths = formatterResolver.GetFormatter<string[]>().Deserialize(ref reader, options);
            result.Silent = reader.ReadBoolean();
            result.StateFile = reader.ReadString();
            result.SupportsBindingRedirectGeneration = reader.ReadBoolean();
            result.TargetedRuntimeVersion = reader.ReadString();
            result.TargetFrameworkDirectories = formatterResolver.GetFormatter<string[]>().Deserialize(ref reader, options);
            result.TargetFrameworkMoniker = reader.ReadString();
            result.TargetFrameworkMonikerDisplayName = reader.ReadString();
            result.TargetFrameworkSubsets = formatterResolver.GetFormatter<string[]>().Deserialize(ref reader, options);
            result.TargetFrameworkVersion = reader.ReadString();
            result.TargetProcessorArchitecture = reader.ReadString();
            result.UnresolveFrameworkAssembliesFromHigherFrameworks = reader.ReadBoolean();
            result.UseResolveAssemblyReferenceService = reader.ReadBoolean();
            result.WarnOrErrorOnTargetArchitectureMismatch = reader.ReadString();
            result.CurrentPath = reader.ReadString();
            result.AssemblyInformationCacheOutputPath = reader.ReadString();
            result.AssemblyInformationCachePaths = formatterResolver.GetFormatter<ITaskItem[]>().Deserialize(ref reader, options);

            reader.Depth--;
            return result;
        }
    }
}
