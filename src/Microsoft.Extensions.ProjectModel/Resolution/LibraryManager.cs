// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using NuGet.Frameworks;
using NuGet.Versioning;

namespace Microsoft.Extensions.ProjectModel.Resolution
{
    public class LibraryManager
    {
        private IList<LibraryDescription> _libraries;
        private IList<DiagnosticMessage> _diagnostics;

        private readonly object _initializeLock = new object();
        private Dictionary<string, IEnumerable<LibraryDescription>> _inverse;
        private Dictionary<string, LibraryDescription> _graph;
        private readonly string _projectPath;
        private readonly NuGetFramework _targetFramework;

        public LibraryManager(string projectPath, NuGetFramework targetFramework, IList<LibraryDescription> libraries)
        {
            _projectPath = projectPath;
            _targetFramework = targetFramework;
            _libraries = libraries;
        }

        public void AddGlobalDiagnostics(DiagnosticMessage message)
        {
            if (_diagnostics == null)
            {
                _diagnostics = new List<DiagnosticMessage>();
            }

            _diagnostics.Add(message);
        }

        private Dictionary<string, LibraryDescription> Graph
        {
            get
            {
                EnsureGraph();
                return _graph;
            }
        }

        private Dictionary<string, IEnumerable<LibraryDescription>> InverseGraph
        {
            get
            {
                EnsureInverseGraph();
                return _inverse;
            }
        }

        public IEnumerable<LibraryDescription> GetReferencingLibraries(string name)
        {
            IEnumerable<LibraryDescription> libraries;
            if (InverseGraph.TryGetValue(name, out libraries))
            {
                return libraries;
            }

            return Enumerable.Empty<LibraryDescription>();
        }

        public LibraryDescription GetLibrary(string name)
        {
            LibraryDescription library;
            if (Graph.TryGetValue(name, out library))
            {
                return library;
            }

            return null;
        }

        public IEnumerable<LibraryDescription> GetLibraries()
        {
            EnsureGraph();
            return _graph.Values;
        }

        public IList<DiagnosticMessage> GetAllDiagnostics()
        {
            var messages = new List<DiagnosticMessage>();

            if (_diagnostics != null)
            {
                messages.AddRange(_diagnostics);
            }

            foreach (var library in GetLibraries())
            {
                string projectPath = library.RequestedRange.SourceFilePath ?? _projectPath;

                if (!library.Resolved)
                {
                    string message;
                    string errorCode;
                    if (library.Compatible)
                    {
                        errorCode = ErrorCodes.NU1001;
                        message = $"The dependency {library.RequestedRange.Name} {library.RequestedRange.VersionRange} could not be resolved.";
                    }
                    else
                    {
                        errorCode = ErrorCodes.NU1002;
                        var projectName = Directory.GetParent(_projectPath).Name;
                        message = $"The dependency {library.Identity} in project {projectName} does not support framework {library.Framework}.";
                    }

                    messages.Add(
                        new DiagnosticMessage(
                            errorCode,
                            message,
                            projectPath,
                            DiagnosticMessageSeverity.Error,
                            library.RequestedRange.SourceLine,
                            library.RequestedRange.SourceColumn,
                            library));
                }
                else
                {
                    // Skip libraries that aren't specified in a project.json
                    if (string.IsNullOrEmpty(library.RequestedRange.SourceFilePath))
                    {
                        continue;
                    }

                    if (library.RequestedRange.VersionRange == null)
                    {
                        // TODO: Show errors/warnings for things without versions
                        continue;
                    }

                    // If we ended up with a declared version that isn't what was asked for directly
                    // then report a warning
                    // Case 1: Non floating version and the minimum doesn't match what was specified
                    // Case 2: Floating version that fell outside of the range
                    if ((!library.RequestedRange.VersionRange.IsFloating &&
                         library.RequestedRange.VersionRange.MinVersion != library.Identity.Version) ||
                        (library.RequestedRange.VersionRange.IsFloating &&
                         !library.RequestedRange.VersionRange.EqualsFloating(library.Identity.Version)))
                    {
                        var message = string.Format("Dependency specified was {0} but ended up with {1}.", library.RequestedRange, library.Identity);
                        messages.Add(
                            new DiagnosticMessage(
                                ErrorCodes.NU1007,
                                message,
                                projectPath,
                                DiagnosticMessageSeverity.Warning,
                                library.RequestedRange.SourceLine,
                                library.RequestedRange.SourceColumn,
                                library));
                    }
                }
            }

            return messages;
        }

        private void EnsureGraph()
        {
            lock (_initializeLock)
            {
                if (_graph == null)
                {
                    _graph = _libraries.ToDictionary(l => l.Identity.Name, StringComparer.Ordinal);
                    _libraries = null;
                }
            }
        }

        private void EnsureInverseGraph()
        {
            EnsureGraph();

            lock (_initializeLock)
            {
                if (_inverse == null)
                {
                    BuildInverseGraph();
                }
            }
        }

        private void BuildInverseGraph()
        {
            var firstLevelLookups = new Dictionary<string, List<LibraryDescription>>(StringComparer.OrdinalIgnoreCase);
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in _graph.Values)
            {
                Visit(item, firstLevelLookups, visited);
            }

            _inverse = new Dictionary<string, IEnumerable<LibraryDescription>>(StringComparer.OrdinalIgnoreCase);

            // Flatten the graph
            foreach (var item in _graph.Values)
            {
                Flatten(item, firstLevelLookups: firstLevelLookups);
            }
        }

        private void Visit(LibraryDescription item,
                          Dictionary<string, List<LibraryDescription>> inverse,
                          HashSet<string> visited)
        {
            if (!visited.Add(item.Identity.Name))
            {
                return;
            }

            foreach (var dependency in item.Dependencies)
            {
                List<LibraryDescription> dependents;
                if (!inverse.TryGetValue(dependency.Name, out dependents))
                {
                    dependents = new List<LibraryDescription>();
                    inverse[dependency.Name] = dependents;
                }

                dependents.Add(item);
                Visit(_graph[dependency.Name], inverse, visited);
            }
        }

        private void Flatten(LibraryDescription info,
                             Dictionary<string, List<LibraryDescription>> firstLevelLookups,
                             HashSet<LibraryDescription> parentDependents = null)
        {
            IEnumerable<LibraryDescription> libraryDependents;
            if (!_inverse.TryGetValue(info.Identity.Name, out libraryDependents))
            {
                List<LibraryDescription> firstLevelDependents;
                if (firstLevelLookups.TryGetValue(info.Identity.Name, out firstLevelDependents))
                {
                    var allDependents = new HashSet<LibraryDescription>(LibraryDescriptionComparer.Instance);
                    foreach (var dependent in firstLevelDependents)
                    {
                        allDependents.Add(dependent);
                        Flatten(dependent, firstLevelLookups, allDependents);
                    }
                    libraryDependents = allDependents;
                }
                else
                {
                    libraryDependents = Enumerable.Empty<LibraryDescription>();
                }
                _inverse[info.Identity.Name] = libraryDependents;
            }
            AddRange(parentDependents, libraryDependents);
        }

        private static Func<IEnumerable<LibraryDescription>> GetLibraryInfoThunk(IEnumerable<LibraryDescription> libraries)
        {
            return () => libraries;
        }

        private static void AddRange(HashSet<LibraryDescription> source, IEnumerable<LibraryDescription> values)
        {
            if (source != null)
            {
                foreach (var value in values)
                {
                    source.Add(value);
                }
            }
        }

        private class LibraryDescriptionComparer : IEqualityComparer<LibraryDescription>
        {
            public static readonly LibraryDescriptionComparer Instance = new LibraryDescriptionComparer();

            private LibraryDescriptionComparer() { }
            public bool Equals(LibraryDescription x, LibraryDescription y)
            {
                return string.Equals(x.Identity.Name, y.Identity.Name, StringComparison.OrdinalIgnoreCase);
            }

            public int GetHashCode(LibraryDescription obj)
            {
                return StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Identity.Name);
            }
        }
    }
}