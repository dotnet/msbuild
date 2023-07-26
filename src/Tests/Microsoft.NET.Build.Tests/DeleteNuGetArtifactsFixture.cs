// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;

namespace Microsoft.NET.Build.Tests
{
    public class ConstantStringValues
    {
        public static string TestDirectoriesNamePrefix = "Nuget_reference_compat";
        public static string ReferencerDirectoryName = "Reference";
        public static string NuGetSharedDirectoryNamePostfix = "_NuGetDependencies";
        public static string NetstandardTargetFrameworkIdentifier = ".NETStandard";
        public static string DependencyDirectoryNamePrefix = "D_";

        public static string ConstructNuGetPackageReferencePath(TestProject dependencyProject, string identifier, [CallerMemberName] string callingMethod = null)
        {
            return TestAssetsManager.GetTestDestinationDirectoryPath(dependencyProject.Name, callingMethod, identifier);
        }
    }

    public class DeleteNuGetArtifactsFixture : IDisposable
    {
        public DeleteNuGetArtifactsFixture()
        {
            DeleteNuGetArtifacts();
        }

        public void Dispose()
        {
            DeleteNuGetArtifacts();
        }

        private void DeleteNuGetArtifacts()
        {
            try
            {
                //  Delete the generated NuGet packages in the cache.
                foreach (string dir in Directory.EnumerateDirectories(TestContext.Current.NuGetCachePath, ConstantStringValues.DependencyDirectoryNamePrefix + "*"))
                {
                    Directory.Delete(dir, true);
                }
            }
            catch
            {
                // No-Op; as this is a precaution - do not throw an exception.
            }
        }
    }
}
