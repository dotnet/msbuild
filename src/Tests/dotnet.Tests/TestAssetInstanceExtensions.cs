using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Xml.Linq;
using Microsoft.DotNet.TestFramework;

namespace Microsoft.DotNet.Tools.Test.Utilities
{
    static class TestAssetInstanceExtensions
    {
        public static TestAssetInstance WithVersionVariables(this TestAssetInstance testAssetInstance)
        {
            var assemblyMetadata = typeof(TestAssetInstanceExtensions).Assembly
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

        public static TestAssetInstance WithRepoGlobalPackages(this TestAssetInstance testAssetInstance)
        {
            return testAssetInstance.WithProjectChanges(project =>
            {
                var ns = project.Root.Name.Namespace;
                project.Root.Element(ns + "PropertyGroup")
                    .Add(new XElement(ns + "RestorePackagesPath", RepoDirectoriesProvider.TestGlobalPackagesFolder));

            });
        }
    }
}
