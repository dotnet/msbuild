// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.ProjectModel.Graph;

namespace Microsoft.Extensions.ProjectModel.Resolution
{
    public class LibraryManager
    {
        private readonly IList<LibraryDescription> _libraries;
        private readonly IList<DiagnosticMessage> _diagnostics;

        public LibraryManager(IList<LibraryDescription> libraries,
                              IList<DiagnosticMessage> diagnostics)
        {
            _libraries = libraries;
            _diagnostics = diagnostics;
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
                    foreach (var range in library.RequestedRanges)
                    {
                        // Skip libraries that aren't specified in a project.json
                        if (string.IsNullOrEmpty(range.SourceFilePath))
                        {
                            continue;
                        }

                        if (range.VersionRange == null)
                        {
                            // TODO: Show errors/warnings for things without versions
                            continue;
                        }

                        // If we ended up with a declared version that isn't what was asked for directly
                        // then report a warning
                        // Case 1: Non floating version and the minimum doesn't match what was specified
                        // Case 2: Floating version that fell outside of the range
                        if ((!range.VersionRange.IsFloating &&
                             range.VersionRange.MinVersion != library.Identity.Version) ||
                            (range.VersionRange.IsFloating &&
                             !range.VersionRange.Float.Satisfies(library.Identity.Version)))
                        {
                            var message = $"Dependency specified was {range} but ended up with {library.Identity}.";

                            AddDiagnostics(messages,
                                           library, 
                                           message,
                                           DiagnosticMessageSeverity.Warning,
                                           ErrorCodes.NU1007);
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

            return range.Name + " " + range.VersionRange;
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
    }
}