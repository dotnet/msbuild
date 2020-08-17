using Microsoft.Build.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Build.Tasks.ResolveAssemblyReferences.Contract
{
    public class ResolveAssemblyReferenceInput
    {
        public string[] AllowedAssemblyExtensions { get; internal set; }
        public string[] AllowedRelatedFileExtensions { get; internal set; }
        public string AppConfigFile { get; internal set; }
        public ITaskItem[] Assemblies { get; internal set; }
        public ITaskItem[] AssemblyFiles { get; internal set; }
        public bool AutoUnify { get; internal set; }
        public string[] CandidateAssemblyFiles { get; internal set; }
        public bool CopyLocalDependenciesWhenParentReferenceInGac { get; internal set; }
        public bool FindDependencies { get; internal set; }
        public bool FindDependenciesOfExternallyResolvedReferences { get; internal set; }
        public bool FindRelatedFiles { get; internal set; }
        public bool FindSatellites { get; internal set; }
        public bool FindSerializationAssemblies { get; internal set; }
        public ITaskItem[] FullFrameworkAssemblyTables { get; internal set; }
        public string[] FullFrameworkFolders { get; internal set; }
        public string[] FullTargetFrameworkSubsetNames { get; internal set; }
        public bool IgnoreDefaultInstalledAssemblySubsetTables { get; internal set; }
        public bool IgnoreDefaultInstalledAssemblyTables { get; internal set; }
        public bool IgnoreTargetFrameworkAttributeVersionMismatch { get; internal set; }
        public bool IgnoreVersionForFrameworkReferences { get; internal set; }
        public ITaskItem[] InstalledAssemblySubsetTables { get; internal set; }
        public ITaskItem[] InstalledAssemblyTables { get; internal set; }
        public string[] LatestTargetFrameworkDirectories { get; internal set; }
        public string ProfileName { get; internal set; }
        public string[] SearchPaths { get; internal set; }
        public bool Silent { get; internal set; }
        public string StateFile { get; internal set; }
        public bool SupportsBindingRedirectGeneration { get; internal set; }
        public string TargetedRuntimeVersion { get; internal set; }
        public string[] TargetFrameworkDirectories { get; internal set; }
        public string TargetFrameworkMoniker { get; internal set; }
        public string TargetFrameworkMonikerDisplayName { get; internal set; }
        public string[] TargetFrameworkSubsets { get; internal set; }
        public string TargetFrameworkVersion { get; internal set; }
        public string TargetProcessorArchitecture { get; internal set; }
        public bool UnresolveFrameworkAssembliesFromHigherFrameworks { get; internal set; }
        public bool UseResolveAssemblyReferenceService { get; internal set; }
        public string WarnOrErrorOnTargetArchitectureMismatch { get; internal set; }
    }
}
