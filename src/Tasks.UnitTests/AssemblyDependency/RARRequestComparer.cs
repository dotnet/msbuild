using Microsoft.Build.Tasks.ResolveAssemblyReferences.Contract;
using System;
using System.Collections.Generic;

namespace Microsoft.Build.Tasks.UnitTests.AssemblyDependency
{

    internal class RARRequestComparer
        : BaseComparer<ResolveAssemblyReferenceRequest>
    {
        internal static IEqualityComparer<ResolveAssemblyReferenceRequest> Instance { get; } = new RARRequestComparer();

        private static readonly IEqualityComparer<string> StringEqualityComparer = StringComparer.InvariantCulture;

        private RARRequestComparer() { }

        public override bool Equals(ResolveAssemblyReferenceRequest x, ResolveAssemblyReferenceRequest y)
        {
            // Same reference or null
            if (x == y)
                return true;

            if (x == null || y == null)
                return false;

            bool fieldsEqual = y != null &&
                   x.AppConfigFile == y.AppConfigFile &&
                   x.AutoUnify == y.AutoUnify &&
                   x.CopyLocalDependenciesWhenParentReferenceInGac == y.CopyLocalDependenciesWhenParentReferenceInGac &&
                   x.DoNotCopyLocalIfInGac == y.DoNotCopyLocalIfInGac &&
                   x.FindDependencies == y.FindDependencies &&
                   x.FindDependenciesOfExternallyResolvedReferences == y.FindDependenciesOfExternallyResolvedReferences &&
                   x.FindRelatedFiles == y.FindRelatedFiles &&
                   x.FindSatellites == y.FindSatellites &&
                   x.FindSerializationAssemblies == y.FindSerializationAssemblies &&
                   x.IgnoreDefaultInstalledAssemblySubsetTables == y.IgnoreDefaultInstalledAssemblySubsetTables &&
                   x.IgnoreDefaultInstalledAssemblyTables == y.IgnoreDefaultInstalledAssemblyTables &&
                   x.IgnoreTargetFrameworkAttributeVersionMismatch == y.IgnoreTargetFrameworkAttributeVersionMismatch &&
                   x.IgnoreVersionForFrameworkReferences == y.IgnoreVersionForFrameworkReferences &&
                   x.ProfileName == y.ProfileName &&
                   x.Silent == y.Silent &&
                   x.StateFile == y.StateFile &&
                   x.SupportsBindingRedirectGeneration == y.SupportsBindingRedirectGeneration &&
                   x.TargetedRuntimeVersion == y.TargetedRuntimeVersion &&
                   x.TargetFrameworkMoniker == y.TargetFrameworkMoniker &&
                   x.TargetFrameworkMonikerDisplayName == y.TargetFrameworkMonikerDisplayName &&
                   x.TargetFrameworkVersion == y.TargetFrameworkVersion &&
                   x.TargetProcessorArchitecture == y.TargetProcessorArchitecture &&
                   x.UnresolveFrameworkAssembliesFromHigherFrameworks == y.UnresolveFrameworkAssembliesFromHigherFrameworks &&
                   x.UseResolveAssemblyReferenceService == y.UseResolveAssemblyReferenceService &&
                   x.WarnOrErrorOnTargetArchitectureMismatch == y.WarnOrErrorOnTargetArchitectureMismatch;

            return fieldsEqual &&
                   CollectionEquals(x.AllowedAssemblyExtensions, y.AllowedAssemblyExtensions, StringEqualityComparer) &&
                   CollectionEquals(x.AllowedRelatedFileExtensions, y.AllowedRelatedFileExtensions, StringEqualityComparer) &&
                   CollectionEquals(x.Assemblies, y.Assemblies, ReadOnlyTaskItemComparer.Instance) &&
                   CollectionEquals(x.AssemblyFiles, y.AssemblyFiles, ReadOnlyTaskItemComparer.Instance) &&
                   CollectionEquals(x.CandidateAssemblyFiles, y.CandidateAssemblyFiles, StringEqualityComparer) &&
                   CollectionEquals(x.FullFrameworkAssemblyTables, y.FullFrameworkAssemblyTables, ReadOnlyTaskItemComparer.Instance) &&
                   CollectionEquals(x.FullFrameworkFolders, y.FullFrameworkFolders, StringEqualityComparer) &&
                   CollectionEquals(x.FullTargetFrameworkSubsetNames, y.FullTargetFrameworkSubsetNames, StringEqualityComparer) &&
                   CollectionEquals(x.InstalledAssemblySubsetTables, y.InstalledAssemblySubsetTables, ReadOnlyTaskItemComparer.Instance) &&
                   CollectionEquals(x.InstalledAssemblyTables, y.InstalledAssemblyTables, ReadOnlyTaskItemComparer.Instance) &&
                   CollectionEquals(x.LatestTargetFrameworkDirectories, y.LatestTargetFrameworkDirectories, StringEqualityComparer) &&
                   CollectionEquals(x.ResolvedSDKReferences, y.ResolvedSDKReferences, ReadOnlyTaskItemComparer.Instance) &&
                   CollectionEquals(x.SearchPaths, y.SearchPaths, StringEqualityComparer) &&
                   CollectionEquals(x.TargetFrameworkDirectories, y.TargetFrameworkDirectories, StringEqualityComparer) &&
                   CollectionEquals(x.TargetFrameworkSubsets, y.TargetFrameworkSubsets, StringEqualityComparer);
        }
    }
}
