using System.Collections.Generic;
using System.IO;
using Microsoft.DotNet.ProjectModel.Compilation;
using System.Linq;
using Microsoft.DotNet.ProjectModel;

namespace Microsoft.DotNet.Cli.Utils
{
    internal static class LibraryExporterExtensions
    {
        internal static void CopyProjectDependenciesTo(this LibraryExporter exporter, string path, params ProjectDescription[] except)
        {
            exporter.GetAllExports()
                .Where(e => !except.Contains(e.Library))
                .Where(e => e.Library is ProjectDescription)
                .SelectMany(e => e.NativeLibraries.Union(e.RuntimeAssemblies))
                .CopyTo(path);
        }

        internal static void WriteDepsTo(this IEnumerable<LibraryExport> exports, string path)
        {
            File.WriteAllLines(path, exports.SelectMany(GenerateLines));
        }
        
        private static IEnumerable<string> GenerateLines(LibraryExport export)
        {
            return GenerateLines(export, export.RuntimeAssemblies, "runtime")
                .Union(GenerateLines(export, export.NativeLibraries, "native"));
        } 

        private static IEnumerable<string> GenerateLines(LibraryExport export, IEnumerable<LibraryAsset> items, string type)
        {
            return items.Select(i => CsvFormatter.EscapeRow(new[]
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

        internal static IEnumerable<LibraryAsset> RuntimeAssets(this LibraryExport export)
        {
            return export.RuntimeAssemblies.Union(export.NativeLibraries);
        }

        internal static void CopyTo(this IEnumerable<LibraryAsset> assets, string destinationPath)
        {
            foreach (var asset in assets)
            {
                File.Copy(asset.ResolvedPath, Path.Combine(destinationPath, Path.GetFileName(asset.ResolvedPath)),
                    overwrite: true);
            }
        }
    }
}
