// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Framework;

namespace Microsoft.Build.Tasks.ResolveAssemblyReferences.Contract
{
    internal class ResolveAssemblyReferenceTaskInput
    {
        public ResolveAssemblyReferenceTaskInput()
        {
        }

        public ResolveAssemblyReferenceTaskInput(ResolveAssemblyReferenceRequest request)
        {
            AllowedAssemblyExtensions = request.AllowedAssemblyExtensions;
            AllowedRelatedFileExtensions = request.AllowedRelatedFileExtensions;
            AppConfigFile = request.AppConfigFile;
            Assemblies = request.Assemblies;
            AssemblyFiles = request.AssemblyFiles;
            AutoUnify = request.AutoUnify;
            CandidateAssemblyFiles = request.CandidateAssemblyFiles;
            CopyLocalDependenciesWhenParentReferenceInGac = request.CopyLocalDependenciesWhenParentReferenceInGac;
            DoNotCopyLocalIfInGac = request.DoNotCopyLocalIfInGac;
            FindDependencies = request.FindDependencies;
            FindDependenciesOfExternallyResolvedReferences = request.FindDependenciesOfExternallyResolvedReferences;
            FindRelatedFiles = request.FindRelatedFiles;
            FindSatellites = request.FindSatellites;
            FindSerializationAssemblies = request.FindSerializationAssemblies;
            FullFrameworkAssemblyTables = request.FullFrameworkAssemblyTables;
            FullFrameworkFolders = request.FullFrameworkFolders;
            FullTargetFrameworkSubsetNames = request.FullTargetFrameworkSubsetNames;
            IgnoreDefaultInstalledAssemblySubsetTables = request.IgnoreDefaultInstalledAssemblySubsetTables;
            IgnoreDefaultInstalledAssemblyTables = request.IgnoreDefaultInstalledAssemblyTables;
            IgnoreTargetFrameworkAttributeVersionMismatch = request.IgnoreTargetFrameworkAttributeVersionMismatch;
            IgnoreVersionForFrameworkReferences = request.IgnoreVersionForFrameworkReferences;
            InstalledAssemblySubsetTables = request.InstalledAssemblySubsetTables;
            InstalledAssemblyTables = request.InstalledAssemblyTables;
            LatestTargetFrameworkDirectories = request.LatestTargetFrameworkDirectories;
            ProfileName = request.ProfileName;
            ResolvedSDKReferences = request.ResolvedSDKReferences;
            SearchPaths = request.SearchPaths;
            Silent = request.Silent;
            StateFile = request.StateFile;
            SupportsBindingRedirectGeneration = request.SupportsBindingRedirectGeneration;
            TargetedRuntimeVersion = request.TargetedRuntimeVersion;
            TargetFrameworkDirectories = request.TargetFrameworkDirectories;
            TargetFrameworkMoniker = request.TargetFrameworkMoniker;
            TargetFrameworkMonikerDisplayName = request.TargetFrameworkMonikerDisplayName;
            TargetFrameworkSubsets = request.TargetFrameworkSubsets;
            TargetFrameworkVersion = request.TargetFrameworkVersion;
            TargetProcessorArchitecture = request.TargetProcessorArchitecture;
            UnresolveFrameworkAssembliesFromHigherFrameworks = request.UnresolveFrameworkAssembliesFromHigherFrameworks;
            UseResolveAssemblyReferenceService = request.UseResolveAssemblyReferenceService;
            WarnOrErrorOnTargetArchitectureMismatch = request.WarnOrErrorOnTargetArchitectureMismatch;
        }

        public string[] AllowedAssemblyExtensions { get; set; }

        public string[] AllowedRelatedFileExtensions { get; set; }

        public string AppConfigFile { get; set; }

        public ITaskItem[] Assemblies { get; set; }

        public ITaskItem[] AssemblyFiles { get; set; }

        public bool AutoUnify { get; set; }

        public string[] CandidateAssemblyFiles { get; set; }

        public bool CopyLocalDependenciesWhenParentReferenceInGac { get; set; }

        public bool DoNotCopyLocalIfInGac { get; set; }

        public bool FindDependencies { get; set; }

        public bool FindDependenciesOfExternallyResolvedReferences { get; set; }

        public bool FindRelatedFiles { get; set; }

        public bool FindSatellites { get; set; }

        public bool FindSerializationAssemblies { get; set; }

        public ITaskItem[] FullFrameworkAssemblyTables { get; set; }

        public string[] FullFrameworkFolders { get; set; }

        public string[] FullTargetFrameworkSubsetNames { get; set; }

        public bool IgnoreDefaultInstalledAssemblySubsetTables { get; set; }

        public bool IgnoreDefaultInstalledAssemblyTables { get; set; }

        public bool IgnoreTargetFrameworkAttributeVersionMismatch { get; set; }

        public bool IgnoreVersionForFrameworkReferences { get; set; }

        public ITaskItem[] InstalledAssemblySubsetTables { get; set; }

        public ITaskItem[] InstalledAssemblyTables { get; set; }

        public string[] LatestTargetFrameworkDirectories { get; set; }

        public string ProfileName { get; set; }

        public ITaskItem[] ResolvedSDKReferences { get; set; }

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
    }
}
