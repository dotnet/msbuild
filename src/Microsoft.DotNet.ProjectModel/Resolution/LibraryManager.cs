// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.DotNet.ProjectModel.Graph;
using Microsoft.DotNet.ProjectModel.Utilities;
using NuGet.Versioning;

namespace Microsoft.DotNet.ProjectModel.Resolution
{
    public class LibraryManager
    {
        private readonly IList<LibraryDescription> _libraries;
        private readonly IList<DiagnosticMessage> _diagnostics;
        private readonly string _projectPath;

        public LibraryManager(IList<LibraryDescription> libraries,
                              IList<DiagnosticMessage> diagnostics,
                              string projectPath)
        {
            _libraries = libraries;
            _diagnostics = diagnostics;
            _projectPath = projectPath;
        }

        public IList<LibraryDescription> GetLibraries()
        {
            return _libraries;
        }

        public IList<DiagnosticMessage> GetAllDiagnostics()
        {
            var messages = new List<DiagnosticMessage>();

            if (_diagnostics != null)
            {
                messages.AddRange(_diagnostics);
            }

            var dependencies = new Dictionary<string, List<DependencyItem>>();
            var topLevel = new List<LibraryItem>();

            foreach (var library in GetLibraries())
            {
                if (!library.Resolved)
                {
                    string message;
                    string errorCode;
                    if (library.Compatible)
                    {
                        foreach (var range in library.RequestedRanges)
                        {
                            errorCode = ErrorCodes.NU1001;
                            message = $"The dependency {FormatLibraryRange(range)} could not be resolved.";

                            AddDiagnostics(messages, library, message, DiagnosticMessageSeverity.Error, errorCode);
                        }
                    }
                    else
                    {
                        errorCode = ErrorCodes.NU1002;
                        message = $"The dependency {library.Identity} does not support framework {library.Framework}.";

                        AddDiagnostics(messages, library, message, DiagnosticMessageSeverity.Error, errorCode);
                    }
                }
                else
                {
                    // Store dependency -> library for later
                    // J.N -> [(R1, P1), (R2, P2)]
                    foreach (var dependency in library.Dependencies)
                    {
                        List<DependencyItem> items;
                        if (!dependencies.TryGetValue(dependency.Name, out items))
                        {
                            items = new List<DependencyItem>();
                            dependencies[dependency.Name] = items;
                        }
                        items.Add(new DependencyItem(dependency, library));
                    }

                    foreach (var range in library.RequestedRanges)
                    {
                        // Skip libraries that aren't specified in a project.json
                        // Only report problems for this project
                        if (string.IsNullOrEmpty(range.SourceFilePath))
                        {
                            continue;
                        }

                        // We only care about things requested in this project
                        if (!string.Equals(_projectPath, range.SourceFilePath))
                        {
                            continue;
                        }

                        if (range.VersionRange == null)
                        {
                            // TODO: Show errors/warnings for things without versions
                            continue;
                        }

                        topLevel.Add(new LibraryItem(range, library));

                        // If we ended up with a declared version that isn't what was asked for directly
                        // then report a warning
                        // Case 1: Non floating version and the minimum doesn't match what was specified
                        // Case 2: Floating version that fell outside of the range
                        if ((!range.VersionRange.IsFloating &&
                             range.VersionRange.MinVersion != library.Identity.Version) ||
                            (range.VersionRange.IsFloating &&
                             !range.VersionRange.Float.Satisfies(library.Identity.Version)))
                        {
                            var message = $"Dependency specified was {FormatLibraryRange(range)} but ended up with {library.Identity}.";

                            messages.Add(
                            new DiagnosticMessage(
                                ErrorCodes.NU1007,
                                message,
                                range.SourceFilePath,
                                DiagnosticMessageSeverity.Warning,
                                range.SourceLine,
                                range.SourceColumn,
                                library));
                        }
                    }
                }
            }

            // Version conflicts
            foreach (var libraryItem in topLevel)
            {
                var library = libraryItem.Library;

                if (library.Identity.Type != LibraryType.Package)
                {
                    continue;
                }

                List<DependencyItem> items;
                if (dependencies.TryGetValue(library.Identity.Name, out items))
                {
                    foreach (var item in items)
                    {
                        var versionRange = item.Dependency.VersionRange;

                        if (versionRange == null)
                        {
                            continue;
                        }

                        if (library.Identity.Version.IsPrerelease && !versionRange.IncludePrerelease)
                        {
                            versionRange = VersionRange.SetIncludePrerelease(versionRange, includePrerelease: true);
                        }

                        if (item.Library != library && !versionRange.Satisfies(library.Identity.Version))
                        {
                            var message = $"Dependency conflict. {item.Library.Identity} expected {FormatLibraryRange(item.Dependency)} but got {library.Identity.Version}";

                            messages.Add(
                            new DiagnosticMessage(
                                ErrorCodes.NU1012,
                                message,
                                libraryItem.RequestedRange.SourceFilePath,
                                DiagnosticMessageSeverity.Warning,
                                libraryItem.RequestedRange.SourceLine,
                                libraryItem.RequestedRange.SourceColumn,
                                library));
                        }
                    }
                }
            }

            return messages;
        }

        private static string FormatLibraryRange(LibraryRange range)
        {
            if (range.VersionRange == null)
            {
                return range.Name;
            }

            return range.Name + " " + VersionUtility.RenderVersion(range.VersionRange);
        }

        private void AddDiagnostics(List<DiagnosticMessage> messages, 
                                    LibraryDescription library, 
                                    string message, 
                                    DiagnosticMessageSeverity severity, 
                                    string errorCode)
        {
            // A (in project.json) -> B (unresolved) (not in project.json)
            foreach (var source in GetRangesWithSourceLocations(library).Distinct())
            {
                // We only care about things requested in this project
                if (!string.Equals(_projectPath, source.SourceFilePath))
                {
                    continue;
                }

                messages.Add(
                    new DiagnosticMessage(
                        errorCode,
                        message,
                        source.SourceFilePath,
                        severity,
                        source.SourceLine,
                        source.SourceColumn,
                        library));

            }
        }

        private IEnumerable<LibraryRange> GetRangesWithSourceLocations(LibraryDescription library)
        {
            foreach (var range in library.RequestedRanges)
            {
                if (!string.IsNullOrEmpty(range.SourceFilePath))
                {
                    yield return range;
                }
            }

            foreach (var parent in library.Parents)
            {
                foreach (var relevantPath in GetRangesWithSourceLocations(parent))
                {
                    yield return relevantPath;
                }
            }
        }

        private struct DependencyItem
        {
            public LibraryRange Dependency { get; private set; }
            public LibraryDescription Library { get; private set; }

            public DependencyItem(LibraryRange dependency, LibraryDescription library)
            {
                Dependency = dependency;
                Library = library;
            }
        }

        private struct LibraryItem
        {
            public LibraryRange RequestedRange { get; private set; }
            public LibraryDescription Library { get; private set; }

            public LibraryItem(LibraryRange requestedRange, LibraryDescription library)
            {
                RequestedRange = requestedRange;
                Library = library;
            }
        }
    }
}