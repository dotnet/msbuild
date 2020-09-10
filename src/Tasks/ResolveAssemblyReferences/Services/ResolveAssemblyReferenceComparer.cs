// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.Build.Tasks.ResolveAssemblyReferences.Contract;

namespace Microsoft.Build.Tasks.ResolveAssemblyReferences.Services
{
    internal static class ResolveAssemblyReferenceComparer
    {
        internal static bool CompareInput(ResolveAssemblyReferenceRequest x, ResolveAssemblyReferenceRequest y)
        {
            if (x == y)
            {
                return true;
            }

            if (x == null || y == null)
            {
                return false;
            }

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
                   x.WarnOrErrorOnTargetArchitectureMismatch == y.WarnOrErrorOnTargetArchitectureMismatch &&
                   x.CurrentPath == y.CurrentPath;

            return fieldsEqual &&
                   AreStringListsEqual(x.AllowedAssemblyExtensions, y.AllowedAssemblyExtensions) &&
                   AreStringListsEqual(x.AllowedRelatedFileExtensions, y.AllowedRelatedFileExtensions) &&
                   AreTaskItemListsEqual(x.Assemblies, y.Assemblies) &&
                   AreTaskItemListsEqual(x.AssemblyFiles, y.AssemblyFiles) &&
                   AreStringListsEqual(x.CandidateAssemblyFiles, y.CandidateAssemblyFiles) &&
                   AreTaskItemListsEqual(x.FullFrameworkAssemblyTables, y.FullFrameworkAssemblyTables) &&
                   AreStringListsEqual(x.FullFrameworkFolders, y.FullFrameworkFolders) &&
                   AreStringListsEqual(x.FullTargetFrameworkSubsetNames, y.FullTargetFrameworkSubsetNames) &&
                   AreTaskItemListsEqual(x.InstalledAssemblySubsetTables, y.InstalledAssemblySubsetTables) &&
                   AreTaskItemListsEqual(x.InstalledAssemblyTables, y.InstalledAssemblyTables) &&
                   AreStringListsEqual(x.LatestTargetFrameworkDirectories, y.LatestTargetFrameworkDirectories) &&
                   AreTaskItemListsEqual(x.ResolvedSDKReferences, y.ResolvedSDKReferences) &&
                   AreStringListsEqual(x.SearchPaths, y.SearchPaths) &&
                   AreStringListsEqual(x.TargetFrameworkDirectories, y.TargetFrameworkDirectories) &&
                   AreStringListsEqual(x.TargetFrameworkSubsets, y.TargetFrameworkSubsets);
        }

        internal static bool CompareOutput(ResolveAssemblyReferenceResponse x, ResolveAssemblyReferenceResponse y)
        {
            if (x == y)
            {
                return true;
            }

            if (x == null || y == null)
            {
                return false;
            }

            return x.DependsOnNETStandard == y.DependsOnNETStandard &&
                   x.DependsOnSystemRuntime == y.DependsOnSystemRuntime &&
                   AreTaskItemListsEqual(x.CopyLocalFiles, y.CopyLocalFiles) &&
                   AreTaskItemListsEqual(x.FilesWritten, y.FilesWritten) &&
                   AreTaskItemListsEqual(x.RelatedFiles, y.RelatedFiles) &&
                   AreTaskItemListsEqual(x.ResolvedDependencyFiles, y.ResolvedDependencyFiles) &&
                   AreTaskItemListsEqual(x.ResolvedFiles, y.ResolvedFiles) &&
                   AreTaskItemListsEqual(x.SatelliteFiles, y.SatelliteFiles) &&
                   AreTaskItemListsEqual(x.ScatterFiles, y.ScatterFiles) &&
                   AreTaskItemListsEqual(x.SerializationAssemblyFiles, y.SerializationAssemblyFiles) &&
                   AreTaskItemListsEqual(x.SuggestedRedirects, y.SuggestedRedirects); 
            //&&
            //       AreTaskItemListsEqual(x.Assemblies, y.Assemblies) &&
            //       AreTaskItemListsEqual(x.AssemblyFiles, y.AssemblyFiles);
        }

        private static bool AreStringListsEqual(string[] x, string[] y)
        {
            if (x == y)
            {
                return true;
            }

            if (x == null || y == null)
            {
                return false;
            }

            if (x.Length != y.Length)
            {
                return false;
            }

            for (int i = 0; i < x.Length; i++)
            {
                if (x[i] != y[i])
                {
                    return false;
                }
            }

            return true;
        }

        private static bool AreTaskItemListsEqual(ReadOnlyTaskItem[] x, ReadOnlyTaskItem[] y)
        {
            if (x == y)
            {
                return true;
            }

            if (x == null || y == null)
            {
                return false;
            }

            if (x.Length != y.Length)
            {
                return false;
            }

            for (int i = 0; i < x.Length; i++)
            {
                if (!AreTaskItemsEqual(x[i], y[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool AreTaskItemsEqual(ReadOnlyTaskItem x, ReadOnlyTaskItem y)
        {
            if (x == y)
            {
                return true;
            }

            if (x == null || y == null)
            {
                return false;
            }

            if (x.ItemSpec != y.ItemSpec || x.MetadataNameToValue.Count != y.MetadataNameToValue.Count)
            {
                return false;
            }

            foreach (KeyValuePair<string, string> metadataNameWithValue in x.MetadataNameToValue)
            {
                string metadataName = metadataNameWithValue.Key;
                string metadataValue = metadataNameWithValue.Value;

                bool hasMetadata = y.MetadataNameToValue.TryGetValue(metadataName, out string metadataValueToCompare);
                bool isMetadataEqual = hasMetadata && metadataValue == metadataValueToCompare;

                if (!isMetadataEqual)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
