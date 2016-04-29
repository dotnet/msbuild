using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.DotNet.ProjectModel.Compilation;
using Microsoft.DotNet.ProjectModel.Graph;

namespace Microsoft.DotNet.ProjectModel
{
    public static class ProjectModelPlatformExtensions
    {
        public static IEnumerable<LibraryExport> ExcludePlatformExports(this ProjectContext context, IEnumerable<LibraryExport> allExports)
        {
            var exclusionList = context.GetPlatformExclusionList(allExports);
            return allExports.Where(e => !exclusionList.Contains(e.Library.Identity.Name));
        }

        public static HashSet<string> GetPlatformExclusionList(this ProjectContext context, IEnumerable<LibraryExport> allExports)
        {
            var exclusionList = new HashSet<string>();
            var redistPackage = context.PlatformLibrary;
            if (redistPackage == null)
            {
                return exclusionList;
            }
            var exports = allExports
                .Where(e => e.Library.Identity.Type.Equals(LibraryType.Package))
                .ToDictionary(e => e.Library.Identity.Name);

            var redistExport = exports[redistPackage.Identity.Name];

            exclusionList.Add(redistExport.Library.Identity.Name);
            CollectDependencies(exports, redistExport.Library.Dependencies, exclusionList);
            return exclusionList;
        }

        private static void CollectDependencies(Dictionary<string, LibraryExport> exports, IEnumerable<LibraryRange> dependencies, HashSet<string> exclusionList)
        {
            foreach (var dependency in dependencies)
            {
                var export = exports[dependency.Name];
                if (export.Library.Identity.Version.Equals(dependency.VersionRange.MinVersion))
                {
                    exclusionList.Add(export.Library.Identity.Name);
                    CollectDependencies(exports, export.Library.Dependencies, exclusionList);
                }
            }
        }
    }
}
