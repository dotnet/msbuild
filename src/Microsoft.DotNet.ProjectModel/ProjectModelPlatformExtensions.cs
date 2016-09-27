using System.Collections.Generic;
using System.Linq;
using Microsoft.DotNet.ProjectModel.Compilation;
using NuGet.LibraryModel;

namespace Microsoft.DotNet.ProjectModel
{
    public static class ProjectModelPlatformExtensions
    {
        public static HashSet<string> GetPlatformExclusionList(this ProjectContext context, IDictionary<string, LibraryExport> exports)
        {
            var exclusionList = new HashSet<string>();
            var redistPackage = context.PlatformLibrary;
            if (redistPackage == null)
            {
                return exclusionList;
            }

            var redistExport = exports[redistPackage.Identity.Name];

            exclusionList.Add(redistExport.Library.Identity.Name);
            CollectDependencies(exports, redistExport.Library.Dependencies, exclusionList);
            return exclusionList;
        }

        private static void CollectDependencies(
            IDictionary<string, LibraryExport> exports,
            IEnumerable<LibraryDependency> dependencies,
            HashSet<string> exclusionList)
        {
            foreach (var dependency in dependencies)
            {
                var export = exports[dependency.Name];
                if (export.Library.Identity.Version.Equals(dependency.LibraryRange.VersionRange.MinVersion))
                {
                    exclusionList.Add(export.Library.Identity.Name);
                    CollectDependencies(exports, export.Library.Dependencies, exclusionList);
                }
            }
        }

        public static HashSet<string> GetTypeBuildExclusionList(this ProjectContext context, IDictionary<string, LibraryExport> exports)
        {
            var acceptedExports = new HashSet<string>();

            // Accept the root project, obviously :)
            acceptedExports.Add(context.RootProject.Identity.Name);

            // Walk all dependencies, tagging exports. But don't walk through Build dependencies.
            CollectNonBuildDependencies(exports, context.RootProject.Dependencies, acceptedExports);

            // Whatever is left in exports was brought in ONLY by a build dependency
            var exclusionList = new HashSet<string>(exports.Keys);
            exclusionList.ExceptWith(acceptedExports);
            return exclusionList;
        }

        private static void CollectNonBuildDependencies(
            IDictionary<string, LibraryExport> exports,
            IEnumerable<LibraryDependency> dependencies,
            HashSet<string> acceptedExports)
        {
            foreach (var dependency in dependencies)
            {
                var export = exports[dependency.Name];
                if (!dependency.Type.Equals(LibraryDependencyType.Build))
                {
                    acceptedExports.Add(export.Library.Identity.Name);
                    CollectNonBuildDependencies(exports, export.Library.Dependencies, acceptedExports);
                }
            }
        }

        public static IEnumerable<LibraryExport> FilterExports(this IEnumerable<LibraryExport> exports, HashSet<string> exclusionList)
        {
            return exports.Where(e => !exclusionList.Contains(e.Library.Identity.Name));
        }
    }
}
