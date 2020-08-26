// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using MessagePack;
using System.IO;

namespace Microsoft.Build.Tasks.ResolveAssemblyReferences.Contract
{
    // MessagePack requires transported objects to be public
    [MessagePackObject]
    public sealed class ResolveAssemblyReferenceRequest
    {
        public ResolveAssemblyReferenceRequest() { }
        internal ResolveAssemblyReferenceRequest(ResolveAssemblyReferenceTaskInput input)
        {
            AllowedAssemblyExtensions = input.AllowedAssemblyExtensions;
            AllowedRelatedFileExtensions = input.AllowedRelatedFileExtensions;
            AppConfigFile = input.AppConfigFile;
            Assemblies = ReadOnlyTaskItem.CreateArray(input.Assemblies);
            AssemblyFiles = ReadOnlyTaskItem.CreateArray(input.AssemblyFiles);
            AutoUnify = input.AutoUnify;
            CandidateAssemblyFiles = input.CandidateAssemblyFiles;
            CopyLocalDependenciesWhenParentReferenceInGac = input.CopyLocalDependenciesWhenParentReferenceInGac;
            DoNotCopyLocalIfInGac = input.DoNotCopyLocalIfInGac;
            FindDependencies = input.FindDependencies;
            FindDependenciesOfExternallyResolvedReferences = input.FindDependenciesOfExternallyResolvedReferences;
            FindRelatedFiles = input.FindRelatedFiles;
            FindSatellites = input.FindSatellites;
            FindSerializationAssemblies = input.FindSerializationAssemblies;
            FullFrameworkAssemblyTables = ReadOnlyTaskItem.CreateArray(input.FullFrameworkAssemblyTables);
            FullFrameworkFolders = input.FullFrameworkFolders;
            FullTargetFrameworkSubsetNames = input.FullTargetFrameworkSubsetNames;
            IgnoreDefaultInstalledAssemblySubsetTables = input.IgnoreDefaultInstalledAssemblySubsetTables;
            IgnoreDefaultInstalledAssemblyTables = input.IgnoreDefaultInstalledAssemblyTables;
            IgnoreTargetFrameworkAttributeVersionMismatch = input.IgnoreTargetFrameworkAttributeVersionMismatch;
            IgnoreVersionForFrameworkReferences = input.IgnoreVersionForFrameworkReferences;
            InstalledAssemblySubsetTables = ReadOnlyTaskItem.CreateArray(input.InstalledAssemblySubsetTables);
            InstalledAssemblyTables = ReadOnlyTaskItem.CreateArray(input.InstalledAssemblyTables);
            LatestTargetFrameworkDirectories = input.LatestTargetFrameworkDirectories;
            ProfileName = input.ProfileName;
            ResolvedSDKReferences = ReadOnlyTaskItem.CreateArray(input.ResolvedSDKReferences);
            SearchPaths = input.SearchPaths;
            Silent = input.Silent;
            StateFile = input.StateFile == null ? input.StateFile : Path.GetFullPath(input.StateFile);
            SupportsBindingRedirectGeneration = input.SupportsBindingRedirectGeneration;
            TargetedRuntimeVersion = input.TargetedRuntimeVersion;
            TargetFrameworkDirectories = input.TargetFrameworkDirectories;
            TargetFrameworkMoniker = input.TargetFrameworkMoniker;
            TargetFrameworkMonikerDisplayName = input.TargetFrameworkMonikerDisplayName;
            TargetFrameworkSubsets = input.TargetFrameworkSubsets;
            TargetFrameworkVersion = input.TargetFrameworkVersion;
            TargetProcessorArchitecture = input.TargetProcessorArchitecture;
            UnresolveFrameworkAssembliesFromHigherFrameworks = input.UnresolveFrameworkAssembliesFromHigherFrameworks;
            UseResolveAssemblyReferenceService = input.UseResolveAssemblyReferenceService;
            WarnOrErrorOnTargetArchitectureMismatch = input.WarnOrErrorOnTargetArchitectureMismatch;
        }

        [Key(0)]
        public string[] AllowedAssemblyExtensions { get; set; }

        [Key(1)]
        public string[] AllowedRelatedFileExtensions { get; set; }

        [Key(2)]
        public string AppConfigFile { get; set; }

        [Key(3)]
        public ReadOnlyTaskItem[] Assemblies { get; set; }

        [Key(4)]
        public ReadOnlyTaskItem[] AssemblyFiles { get; set; }

        [Key(5)]
        public bool AutoUnify { get; set; }

        [Key(6)]
        public string[] CandidateAssemblyFiles { get; set; }

        [Key(7)]
        public bool CopyLocalDependenciesWhenParentReferenceInGac { get; set; }

        [Key(8)]
        public bool DoNotCopyLocalIfInGac { get; set; }

        [Key(9)]
        public bool FindDependencies { get; set; }

        [Key(10)]
        public bool FindDependenciesOfExternallyResolvedReferences { get; set; }

        [Key(11)]
        public bool FindRelatedFiles { get; set; }

        [Key(12)]
        public bool FindSatellites { get; set; }

        [Key(13)]
        public bool FindSerializationAssemblies { get; set; }

        [Key(14)]
        public ReadOnlyTaskItem[] FullFrameworkAssemblyTables { get; set; }

        [Key(15)]
        public string[] FullFrameworkFolders { get; set; }

        [Key(16)]
        public string[] FullTargetFrameworkSubsetNames { get; set; }

        [Key(17)]
        public bool IgnoreDefaultInstalledAssemblySubsetTables { get; set; }

        [Key(18)]
        public bool IgnoreDefaultInstalledAssemblyTables { get; set; }

        [Key(19)]
        public bool IgnoreTargetFrameworkAttributeVersionMismatch { get; set; }

        [Key(20)]
        public bool IgnoreVersionForFrameworkReferences { get; set; }

        [Key(21)]
        public ReadOnlyTaskItem[] InstalledAssemblySubsetTables { get; set; }

        [Key(22)]
        public ReadOnlyTaskItem[] InstalledAssemblyTables { get; set; }

        [Key(23)]
        public string[] LatestTargetFrameworkDirectories { get; set; }

        [Key(24)]
        public string ProfileName { get; set; }

        [Key(25)]
        public ReadOnlyTaskItem[] ResolvedSDKReferences { get; set; }

        [Key(26)]
        public string[] SearchPaths { get; set; }

        [Key(27)]
        public bool Silent { get; set; }

        [Key(28)]
        public string StateFile { get; set; }

        [Key(29)]
        public bool SupportsBindingRedirectGeneration { get; set; }

        [Key(30)]
        public string TargetedRuntimeVersion { get; set; }

        [Key(31)]
        public string[] TargetFrameworkDirectories { get; set; }

        [Key(32)]
        public string TargetFrameworkMoniker { get; set; }

        [Key(33)]
        public string TargetFrameworkMonikerDisplayName { get; set; }

        [Key(34)]
        public string[] TargetFrameworkSubsets { get; set; }

        [Key(35)]
        public string TargetFrameworkVersion { get; set; }

        [Key(36)]
        public string TargetProcessorArchitecture { get; set; }

        [Key(37)]
        public bool UnresolveFrameworkAssembliesFromHigherFrameworks { get; set; }

        [Key(38)]
        public bool UseResolveAssemblyReferenceService { get; set; }

        [Key(39)]
        public string WarnOrErrorOnTargetArchitectureMismatch { get; set; }
    }
}
