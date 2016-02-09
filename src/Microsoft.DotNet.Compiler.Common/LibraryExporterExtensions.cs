using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.DotNet.ProjectModel.Compilation;

namespace Microsoft.DotNet.Cli.Compiler.Common
{
    public static class LibraryExporterExtensions
    {
        public static void WriteDepsTo(this IEnumerable<LibraryExport> exports, string path)
        {
            CreateDirectoryIfNotExists(path);

            File.WriteAllLines(path, exports.SelectMany(GenerateLines));
        }

        private static void CreateDirectoryIfNotExists(string path)
        {
            var depsFile = new FileInfo(path);
            depsFile.Directory.Create();
        }

        private static IEnumerable<string> GenerateLines(LibraryExport export)
        {
            return GenerateLines(export, export.RuntimeAssemblies, "runtime")
                .Union(GenerateLines(export, export.NativeLibraries, "native"));
        } 

        private static IEnumerable<string> GenerateLines(LibraryExport export, IEnumerable<LibraryAsset> items, string type)
        {
            return items.Select(i => DepsFormatter.EscapeRow(new[]
            {
                export.Library.Identity.Type.Value,
                export.Library.Identity.Name,
                export.Library.Identity.Version.ToNormalizedString(),
                export.Library.Hash,
                type,
                i.Name,
                i.RelativePath
            }));
        }

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

        public static void StructuredCopyTo(this IEnumerable<LibraryAsset> assets, string destinationPath)
        {
            if (!Directory.Exists(destinationPath))
            {
                Directory.CreateDirectory(destinationPath);
            }

            foreach (var asset in assets)
            {
                var targetName = ResolveTargetName(destinationPath, asset);

                File.Copy(asset.ResolvedPath, targetName, overwrite: true);
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
