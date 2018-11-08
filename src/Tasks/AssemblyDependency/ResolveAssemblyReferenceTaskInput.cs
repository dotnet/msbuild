using Microsoft.Build.Framework;

namespace Microsoft.Build.Tasks
{
    internal class ResolveAssemblyReferenceTaskInput
    {
        internal string[] AllowedAssemblyExtensions { get; set; }

        internal string[] AllowedRelatedFileExtensions { get; set; }

        internal string AppConfigFile { get; set; }

        internal ITaskItem[] Assemblies { get; set; }

        internal ITaskItem[] AssemblyFiles { get; set; }

        internal bool AutoUnify { get; set; }

        internal IBuildEngine BuildEngine { get; set; }

        internal string[] CandidateAssemblyFiles { get; set; }

        internal bool CopyLocalDependenciesWhenParentReferenceInGac { get; set; }

        internal bool DoNotCopyLocalIfInGac { get; set; }

        internal bool FindDependencies { get; set; }

        internal bool FindDependenciesOfExternallyResolvedReferences { get; set; }

        internal bool FindRelatedFiles { get; set; }

        internal bool FindSatellites { get; set; }

        internal bool FindSerializationAssemblies { get; set; }

        internal ITaskItem[] FullFrameworkAssemblyTables { get; set; }

        internal string[] FullFrameworkFolders { get; set; }

        internal string[] FullTargetFrameworkSubsetNames { get; set; }

        internal bool IgnoreDefaultInstalledAssemblySubsetTables { get; set; }

        internal bool IgnoreDefaultInstalledAssemblyTables { get; set; }

        internal bool IgnoreTargetFrameworkAttributeVersionMismatch { get; set; }

        internal bool IgnoreVersionForFrameworkReferences { get; set; }

        internal ITaskItem[] InstalledAssemblySubsetTables { get; set; }

        internal ITaskItem[] InstalledAssemblyTables { get; set; }

        internal ResolveAssemblyReferenceIOTracker IoTracker { get; set; }

        internal string[] LatestTargetFrameworkDirectories { get; set; }

        internal string ProfileName { get; set; }

        internal ITaskItem[] ResolvedSDKReferences { get; set; }

        internal string[] SearchPaths { get; set; }

        internal bool ShouldExecuteInProcess { get; set; }

        internal bool Silent { get; set; }

        internal string StateFile { get; set; }

        internal bool SupportsBindingRedirectGeneration { get; set; }

        internal string TargetedRuntimeVersion { get; set; }

        internal string[] TargetFrameworkDirectories { get; set; }

        internal string TargetFrameworkMoniker { get; set; }

        internal string TargetFrameworkMonikerDisplayName { get; set; }

        internal string[] TargetFrameworkSubsets { get; set; }

        internal string TargetFrameworkVersion { get; set; }

        internal string TargetProcessorArchitecture { get; set; }

        internal bool UnresolveFrameworkAssembliesFromHigherFrameworks { get; set; }

        internal string WarnOrErrorOnTargetArchitectureMismatch { get; set; }
    }
}
