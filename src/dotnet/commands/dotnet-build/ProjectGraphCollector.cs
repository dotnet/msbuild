// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using NuGet.Frameworks;
using Microsoft.DotNet.ProjectModel;
using System.Linq;
using System.Threading.Tasks;
using NuGet.LibraryModel;

namespace Microsoft.DotNet.Tools.Build
{
    internal class ProjectGraphCollector
    {
        private readonly bool _collectDependencies;
        private readonly Func<string, NuGetFramework, ProjectContext> _projectContextFactory;

        public ProjectGraphCollector(bool collectDependencies,
            Func<string, NuGetFramework, ProjectContext> projectContextFactory)
        {
            _collectDependencies = collectDependencies;
            _projectContextFactory = projectContextFactory;
        }

        public IEnumerable<ProjectGraphNode> Collect(IEnumerable<ProjectContext> contexts)
        {
            foreach (var context in contexts)
            {
                var libraries = context.LibraryManager.GetLibraries();
                var lookup = libraries.ToDictionary(l => l.Identity.Name, StringComparer.OrdinalIgnoreCase);
                var root = lookup[context.ProjectFile.Name];
                yield return TraverseProject((ProjectDescription) root, lookup, context);
            }
        }

        private ProjectGraphNode TraverseProject(ProjectDescription project, IDictionary<string, LibraryDescription> lookup, ProjectContext context = null)
        {
            var isRoot = context != null;
            var deps = new List<ProjectGraphNode>();
            if (isRoot || _collectDependencies)
            {
                foreach (var dependency in project.Dependencies)
                {
                    LibraryDescription libraryDescription;
                    if ((lookup.TryGetValue(dependency.Name, out libraryDescription)) && (!libraryDescription.Identity.Name.Equals(project.Identity.Name)))
                    {
                        if (libraryDescription.Resolved && libraryDescription.Identity.Type.Equals(LibraryType.Project))
                        {
                            deps.Add(TraverseProject((ProjectDescription)libraryDescription, lookup));
                        }
                        else
                        {
                            deps.AddRange(TraverseNonProject(libraryDescription, lookup));
                        }
                    }
                }
            }

            var task = context != null ? Task.FromResult(context) : Task.Run(() => _projectContextFactory(project.Path, project.Framework));
            return new ProjectGraphNode(task, deps, isRoot);
        }

        private IEnumerable<ProjectGraphNode> TraverseNonProject(LibraryDescription root, IDictionary<string, LibraryDescription> lookup)
        {
            Stack<LibraryDescription> libraries = new Stack<LibraryDescription>();
            libraries.Push(root);
            while (libraries.Count > 0)
            {
                var current = libraries.Pop();
                bool foundProject = false;
                foreach (var dependency in current.Dependencies)
                {
                    LibraryDescription libraryDescription;
                    if (lookup.TryGetValue(dependency.Name, out libraryDescription))
                    {
                        if (libraryDescription.Identity.Type.Equals(LibraryType.Project))
                        {
                            foundProject = true;
                            yield return TraverseProject((ProjectDescription) libraryDescription, lookup);
                        }
                        else
                        {
                            libraries.Push(libraryDescription);
                        }
                    }
                }
                // if package didn't have any project dependencies inside remove it from lookup
                // and do not traverse anymore
                if (!foundProject)
                {
                    lookup.Remove(current.Identity.Name);
                }
            }
        }
    }
}