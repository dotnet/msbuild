using System.Collections.Generic;
using System.IO;
using System.Linq;
using NuGet.LibraryModel;

namespace Microsoft.DotNet.ProjectModel.Server.Helpers
{
    internal class DependencyTypeChangeFinder
    {
        public static IEnumerable<DiagnosticMessage> Diagnose(
            ProjectContext context,
            IEnumerable<string> previousSearchPaths)
        {
            var result = new List<DiagnosticMessage>();
            var project = context.ProjectFile;
            var libraries = context.LibraryManager.GetLibraries();

            var updatedSearchPath = GetUpdatedSearchPaths(previousSearchPaths, project.ResolveSearchPaths());
            var projectCandiates = GetProjectCandidates(updatedSearchPath);
            var rootDependencies = libraries.FirstOrDefault(library => string.Equals(library.Identity.Name, project.Name))
                                           ?.Dependencies
                                           ?.ToDictionary(libraryRange => libraryRange.Name);

            foreach (var library in libraries)
            {
                var diagnostic = Validate(library, projectCandiates, rootDependencies);
                if (diagnostic != null)
                {
                    result.Add(diagnostic);
                }
            }

            return result;
        }

        private static DiagnosticMessage Validate(LibraryDescription library,
                                                  HashSet<string> projectCandidates,
                                                  Dictionary<string, ProjectLibraryDependency> rootDependencies)
        {
            if (!library.Resolved || projectCandidates == null)
            {
                return null;
            }

            var foundCandidate = projectCandidates.Contains(library.Identity.Name);

            if ((library.Identity.Type == LibraryType.Project && !foundCandidate) ||
                (library.Identity.Type == LibraryType.Package && foundCandidate))
            {
                library.Resolved = false;

                var libraryRange = rootDependencies[library.Identity.Name];

                return new DiagnosticMessage(
                    ErrorCodes.NU1010,
                    $"The type of dependency {library.Identity.Name} was changed.",
                    libraryRange.SourceFilePath,
                    DiagnosticMessageSeverity.Error,
                    libraryRange.SourceLine,
                    libraryRange.SourceColumn,
                    library);
            }

            return null;
        }

        private static HashSet<string> GetProjectCandidates(IEnumerable<string> searchPaths)
        {
            if (searchPaths == null)
            {
                return null;
            }

            return new HashSet<string>(searchPaths.Where(path => Directory.Exists(path))
                                                  .SelectMany(path => Directory.GetDirectories(path))
                                                  .Where(path => File.Exists(Path.Combine(path, Project.FileName)))
                                                  .Select(path => Path.GetFileName(path)));
        }

        /// <summary>
        /// Returns the search paths if they're updated. Otherwise returns null.
        /// </summary>
        private static IEnumerable<string> GetUpdatedSearchPaths(IEnumerable<string> oldSearchPaths,
                                                                 IEnumerable<string> newSearchPaths)
        {
            // The oldSearchPaths is null when the current project is not initialized. It is not necessary to 
            // validate the dependency in this case.
            if (oldSearchPaths == null)
            {
                return null;
            }

            if (Enumerable.SequenceEqual(oldSearchPaths, newSearchPaths))
            {
                return null;
            }

            return newSearchPaths;
        }
    }
}
