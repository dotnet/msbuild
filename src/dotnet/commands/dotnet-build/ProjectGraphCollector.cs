using System;
using System.Collections.Generic;
using NuGet.Frameworks;
using Microsoft.DotNet.ProjectModel;
using Microsoft.DotNet.ProjectModel.Graph;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Tools.Build
{
    public class ProjectGraphCollector
    {
        private readonly Func<string, NuGetFramework, ProjectContext> _projectContextFactory;

        public ProjectGraphCollector(Func<string, NuGetFramework, ProjectContext> projectContextFactory)
        {
            _projectContextFactory = projectContextFactory;
        }

        public IEnumerable<ProjectGraphNode> Collect(IEnumerable<ProjectContext> contexts)
        {
            foreach (var context in contexts)
            {
                var libraries = context.LibraryManager.GetLibraries();
                var lookup = libraries.ToDictionary(l => l.Identity.Name);
                var root = lookup[context.ProjectFile.Name];
                yield return TraverseProject((ProjectDescription)root, lookup, context);
            }
        }

        private ProjectGraphNode TraverseProject(ProjectDescription project, IDictionary<string, LibraryDescription> lookup, ProjectContext context = null)
        {
            var deps = new List<ProjectGraphNode>();
            foreach (var dependency in project.Dependencies)
            {
                var libraryDescription = lookup[dependency.Name];

                if (libraryDescription.Identity.Type.Equals(LibraryType.Project))
                {
                    deps.Add(TraverseProject((ProjectDescription)libraryDescription, lookup));
                }
                else
                {
                    deps.AddRange(TraverseNonProject(libraryDescription, lookup));
                }
            }
            var task = context != null ? Task.FromResult(context) : Task.Run(() => _projectContextFactory(project.Path, project.Framework));
            return new ProjectGraphNode(task, deps, context != null);
        }

        private IEnumerable<ProjectGraphNode> TraverseNonProject(LibraryDescription root, IDictionary<string, LibraryDescription> lookup)
        {
            foreach (var dependency in root.Dependencies)
            {
                var libraryDescription = lookup[dependency.Name];

                if (libraryDescription.Identity.Type.Equals(LibraryType.Project))
                {
                    yield return TraverseProject((ProjectDescription)libraryDescription, lookup);
                }
                else
                {
                    foreach(var node in TraverseNonProject(libraryDescription, lookup))
                    {
                        yield return node;
                    }
                }
            }
        }
    }
}