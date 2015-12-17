using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.DotNet.ProjectModel;
using Microsoft.DotNet.ProjectModel.Compilation;
using Microsoft.DotNet.ProjectModel.Graph;

namespace Microsoft.Extensions.DependencyModel
{
    public static class DependencyContextBuilder
    {
        public static DependencyContext FromLibraryExporter(LibraryExporter libraryExporter, string target, string runtime)
        {
            var dependencies = libraryExporter.GetAllExports();

            return new DependencyContext(target, runtime,
                GetLibraries(dependencies, export => export.CompilationAssemblies),
                GetLibraries(dependencies, export => export.RuntimeAssemblies));
        }

        private static Library[] GetLibraries(IEnumerable<LibraryExport> dependencies, Func<LibraryExport, IEnumerable<LibraryAsset>> assemblySelector)
        {
            return dependencies.Select(export => GetLibrary(export, assemblySelector(export), dependencies)).ToArray();
        }

        private static Library GetLibrary(LibraryExport export, IEnumerable<LibraryAsset> libraryAssets, IEnumerable<LibraryExport> dependencies)
        {
            var serviceable = (export.Library as PackageDescription)?.Library.IsServiceable ?? false;
            var version = dependencies.Where(dependency => dependency.Library.Identity == export.Library.Identity);

            var libraryDependencies = export.Library.Dependencies.Select(libraryRange => GetDependency(libraryRange, dependencies)).ToArray();

            return new Library(
                export.Library.Identity.Type.ToString().ToLowerInvariant(),
                export.Library.Identity.Name,
                export.Library.Identity.Version.ToString(),
                export.Library.Hash,
                libraryAssets.Select(libraryAsset => libraryAsset.RelativePath).ToArray(),
                libraryDependencies,
                serviceable
                );
        }

        private static Dependency GetDependency(LibraryRange libraryRange, IEnumerable<LibraryExport> dependencies)
        {
            var version =
                dependencies.First(d => d.Library.Identity.Name == libraryRange.Name)
                    .Library.Identity.Version.ToString();
            return new Dependency(libraryRange.Name, version);
        }
    }
}
