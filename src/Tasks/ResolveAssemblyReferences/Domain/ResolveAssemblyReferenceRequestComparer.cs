using System.Collections.Generic;

namespace Microsoft.Build.Tasks.ResolveAssemblyReferences.Domain
{
    internal class ResolveAssemblyReferenceRequestComparer
    {
        internal static bool Equals(ResolveAssemblyReferenceRequest x, ResolveAssemblyReferenceRequest y)
        {
            return x.StateFile == y.StateFile
                   && AreStringListsEqual(x.AllowedAssemblyExtensions, y.AllowedAssemblyExtensions)
                   && AreStringListsEqual(x.AllowedRelatedFileExtensions, y.AllowedRelatedFileExtensions)
                   && x.AppConfigFile == y.AppConfigFile
                   && AreTaskItemListsEqual(x.Assemblies, y.Assemblies)
                   && AreTaskItemListsEqual(x.AssemblyFiles, y.AssemblyFiles)
                   && x.AutoUnify == y.AutoUnify
                   && AreStringListsEqual(x.CandidateAssemblyFiles, y.CandidateAssemblyFiles)
                   && x.CopyLocalDependenciesWhenParentReferenceInGac == y.CopyLocalDependenciesWhenParentReferenceInGac
                   && x.DoNotCopyLocalIfInGac == y.DoNotCopyLocalIfInGac
                   && x.FindDependencies == y.FindDependencies
                   && x.FindDependenciesOfExternallyResolvedReferences ==
                   y.FindDependenciesOfExternallyResolvedReferences
                   && x.FindRelatedFiles == y.FindRelatedFiles
                   && x.FindSatellites == y.FindSatellites
                   && x.FindSerializationAssemblies == y.FindSerializationAssemblies
                   && AreTaskItemListsEqual(x.FullFrameworkAssemblyTables, y.FullFrameworkAssemblyTables)
                   && AreStringListsEqual(x.FullFrameworkFolders, y.FullFrameworkFolders)
                   && AreStringListsEqual(x.FullTargetFrameworkSubsetNames, y.FullTargetFrameworkSubsetNames)
                   && x.IgnoreDefaultInstalledAssemblySubsetTables == y.IgnoreDefaultInstalledAssemblySubsetTables
                   && x.IgnoreDefaultInstalledAssemblyTables == y.IgnoreDefaultInstalledAssemblyTables
                   && x.IgnoreTargetFrameworkAttributeVersionMismatch == y.IgnoreTargetFrameworkAttributeVersionMismatch
                   && x.IgnoreVersionForFrameworkReferences == y.IgnoreTargetFrameworkAttributeVersionMismatch
                   && x.IgnoreVersionForFrameworkReferences == y.IgnoreVersionForFrameworkReferences
                   && AreTaskItemListsEqual(x.InstalledAssemblySubsetTables, y.InstalledAssemblySubsetTables)
                   && AreTaskItemListsEqual(x.InstalledAssemblyTables, y.InstalledAssemblyTables)
                   && AreStringListsEqual(x.LatestTargetFrameworkDirectories, y.LatestTargetFrameworkDirectories)
                   && x.ProfileName == y.ProfileName
                   && AreTaskItemListsEqual(x.ResolvedSDKReferences, y.ResolvedSDKReferences)
                   && AreStringListsEqual(x.SearchPaths, y.SearchPaths)
                   && x.Silent == y.Silent
                   && x.StateFile == y.StateFile
                   && x.SupportsBindingRedirectGeneration == y.SupportsBindingRedirectGeneration
                   && x.TargetedRuntimeVersion == y.TargetedRuntimeVersion
                   && AreStringListsEqual(x.TargetFrameworkDirectories, y.TargetFrameworkDirectories)
                   && x.TargetFrameworkMoniker == y.TargetFrameworkMoniker
                   && x.TargetFrameworkMonikerDisplayName == y.TargetFrameworkMonikerDisplayName
                   && AreStringListsEqual(x.TargetFrameworkSubsets, y.TargetFrameworkSubsets)
                   && x.TargetFrameworkVersion == y.TargetFrameworkVersion
                   && x.TargetProcessorArchitecture == y.TargetProcessorArchitecture
                   && x.UnresolveFrameworkAssembliesFromHigherFrameworks ==
                   y.UnresolveFrameworkAssembliesFromHigherFrameworks
                   && x.WarnOrErrorOnTargetArchitectureMismatch == y.WarnOrErrorOnTargetArchitectureMismatch;
        }

        private static bool AreStringListsEqual(string[] x, string[] y)
        {
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
