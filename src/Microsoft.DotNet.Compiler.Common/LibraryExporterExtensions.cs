using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.DotNet.ProjectModel;
using Microsoft.DotNet.ProjectModel.Compilation;
using Microsoft.DotNet.ProjectModel.Graph;
using Microsoft.Extensions.DependencyModel;

namespace Microsoft.DotNet.Cli.Compiler.Common
{
    public static class LibraryExporterExtensions
    {
        public static void CopyTo(this IEnumerable<LibraryAsset> assets, string destinationPath)
        {
            if (!Directory.Exists(destinationPath))
            {
                Directory.CreateDirectory(destinationPath);
            }

            foreach (var asset in assets)
            {
                File.Copy(asset.ResolvedPath, Path.Combine(destinationPath, Path.GetFileName(asset.ResolvedPath)), overwrite: true);
            }
        }

        public static void StructuredCopyTo(this IEnumerable<LibraryAsset> assets, string destinationPath, string tempLocation)
        {
            if (!Directory.Exists(destinationPath))
            {
                Directory.CreateDirectory(destinationPath);
            }

            foreach (var asset in assets)
            {
                var targetName = ResolveTargetName(destinationPath, asset);
                var transformedFile = asset.GetTransformedFile(tempLocation);

                File.Copy(transformedFile, targetName, overwrite: true);
            }
        }

        private static string ResolveTargetName(string destinationPath, LibraryAsset asset)
        {
            string targetName;
            if (!string.IsNullOrEmpty(asset.RelativePath))
            {
                targetName = Path.Combine(destinationPath, asset.RelativePath);
                var destinationAssetPath = Path.GetDirectoryName(targetName);

                if (!Directory.Exists(destinationAssetPath))
                {
                    Directory.CreateDirectory(destinationAssetPath);
                }
            }
            else
            {
                targetName = Path.Combine(destinationPath, Path.GetFileName(asset.ResolvedPath));
            }
            return targetName;
        }
    }
}
