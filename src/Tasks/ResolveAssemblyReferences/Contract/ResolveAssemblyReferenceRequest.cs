// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Build.Tasks.ResolveAssemblyReferences.Contract
{
    internal sealed class ResolveAssemblyReferenceRequest
    {
        public ResolveAssemblyReferenceRequest() { }
        
        public string[] AllowedAssemblyExtensions { get; set; }

        public string[] AllowedRelatedFileExtensions { get; set; }

        public string AppConfigFile { get; set; }

        public ReadOnlyTaskItem[] Assemblies { get; set; }

        public ReadOnlyTaskItem[] AssemblyFiles { get; set; }

        public bool AutoUnify { get; set; }

        public string[] CandidateAssemblyFiles { get; set; }

        public bool CopyLocalDependenciesWhenParentReferenceInGac { get; set; }

        public bool DoNotCopyLocalIfInGac { get; set; }

        public bool FindDependencies { get; set; }

        public bool FindDependenciesOfExternallyResolvedReferences { get; set; }

        public bool FindRelatedFiles { get; set; }

        public bool FindSatellites { get; set; }

        public bool FindSerializationAssemblies { get; set; }

        public ReadOnlyTaskItem[] FullFrameworkAssemblyTables { get; set; }

        public string[] FullFrameworkFolders { get; set; }

        public string[] FullTargetFrameworkSubsetNames { get; set; }

        public bool IgnoreDefaultInstalledAssemblySubsetTables { get; set; }

        public bool IgnoreDefaultInstalledAssemblyTables { get; set; }

        public bool IgnoreTargetFrameworkAttributeVersionMismatch { get; set; }

        public bool IgnoreVersionForFrameworkReferences { get; set; }

        public ReadOnlyTaskItem[] InstalledAssemblySubsetTables { get; set; }

        public ReadOnlyTaskItem[] InstalledAssemblyTables { get; set; }

        public string[] LatestTargetFrameworkDirectories { get; set; }

        public string ProfileName { get; set; }

        public ReadOnlyTaskItem[] ResolvedSDKReferences { get; set; }

        public string[] SearchPaths { get; set; }

        public bool Silent { get; set; }

        public string StateFile { get; set; }

        public bool SupportsBindingRedirectGeneration { get; set; }

        public string TargetedRuntimeVersion { get; set; }

        public string[] TargetFrameworkDirectories { get; set; }

        public string TargetFrameworkMoniker { get; set; }

        public string TargetFrameworkMonikerDisplayName { get; set; }

        public string[] TargetFrameworkSubsets { get; set; }

        public string TargetFrameworkVersion { get; set; }

        public string TargetProcessorArchitecture { get; set; }

        public bool UnresolveFrameworkAssembliesFromHigherFrameworks { get; set; }

        public bool UseResolveAssemblyReferenceService { get; set; }

        public string WarnOrErrorOnTargetArchitectureMismatch { get; set; }

        public string CurrentPath { get; set; }

        public string AssemblyInformationCacheOutputPath { get; set; }

        public ReadOnlyTaskItem[] AssemblyInformationCachePaths { get; set; }
    }
}
