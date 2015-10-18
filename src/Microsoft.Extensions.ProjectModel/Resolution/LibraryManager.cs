// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using NuGet.Frameworks;
using NuGet.Versioning;

namespace Microsoft.Extensions.ProjectModel.Resolution
{
    public class LibraryManager
    {
        private readonly IList<LibraryDescription> _libraries;
        private readonly IList<DiagnosticMessage> _diagnostics;
        private readonly string _projectPath;

        public LibraryManager(string projectPath, 
                              NuGetFramework targetFramework, 
                              IList<LibraryDescription> libraries,
                              IList<DiagnosticMessage> diagnostics)
        {
            _projectPath = projectPath;
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
    }
}