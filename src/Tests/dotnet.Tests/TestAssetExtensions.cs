// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;

namespace Microsoft.DotNet.Tools.Test.Utilities
{
    static class TestAssetExtensions
    {
        public static TestAsset WithVersionVariables(this TestAsset testAssetInstance)
        {
            var assemblyMetadata = typeof(TestAssetExtensions).Assembly
                .GetCustomAttributes(typeof(AssemblyMetadataAttribute))
                .Cast<AssemblyMetadataAttribute>()
                .ToDictionary(a => a.Key, a => a.Value);

            return testAssetInstance.WithProjectChanges(project =>
            {
                var ns = project.Root.Name.Namespace;
                foreach (var valueToReplace in new[] { "MSTestVersion", "MicrosoftNETTestSdkPackageVersion" })
                {
                    var packageReferencesToUpdate =
                        project.Root.Descendants(ns + "PackageReference")
                            .Where(pr => pr.Attribute("Version").Value.Equals($"$({valueToReplace})",
                                                              StringComparison.OrdinalIgnoreCase));
                    foreach (var packageReference in packageReferencesToUpdate)
                    {
                        packageReference.Attribute("Version").Value = assemblyMetadata[valueToReplace];
                    }
                }
            });
        }

        //  For tests which want the global packages folder isolated in the repo, but
        //  can share it with other tests
        public static TestAsset WithRepoGlobalPackages(this TestAsset testAsset)
        {
            return testAsset.WithProjectChanges(project =>
            {
                var ns = project.Root.Name.Namespace;
                project.Root.Element(ns + "PropertyGroup")
                    .Add(new XElement(ns + "RestorePackagesPath", TestContext.Current.TestGlobalPackagesFolder));
            });
        }
    }
}
