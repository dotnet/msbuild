// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.DotNet.ProjectModel.Compilation;
using Microsoft.DotNet.ProjectModel.Graph;
using Microsoft.DotNet.ProjectModel.Server.Helpers;

namespace Microsoft.DotNet.ProjectModel.Server.Models
{
    public class DependencyDescription
    {
        private DependencyDescription() { }

        public string Name { get; private set; }

        public string DisplayName { get; private set; }

        public string Version { get; private set; }

        public string Path { get; private set; }

        public string Type { get; private set; }

        public bool Resolved { get; private set; }

        public IEnumerable<DependencyItem> Dependencies { get; private set; }

        public IEnumerable<DiagnosticMessageView> Errors { get; private set; }

        public IEnumerable<DiagnosticMessageView> Warnings { get; private set; }

        public override bool Equals(object obj)
        {
            var other = obj as DependencyDescription;

            return other != null &&
                   Resolved == other.Resolved &&
                   string.Equals(Name, other.Name) &&
                   object.Equals(Version, other.Version) &&
                   string.Equals(Path, other.Path) &&
                   string.Equals(Type, other.Type) &&
                   Enumerable.SequenceEqual(Dependencies, other.Dependencies) &&
                   Enumerable.SequenceEqual(Errors, other.Errors) &&
                   Enumerable.SequenceEqual(Warnings, other.Warnings);
        }

        public override int GetHashCode()
        {
            // These objects are currently POCOs and we're overriding equals
            // so that things like Enumerable.SequenceEqual just work.
            return base.GetHashCode();
        }

        public static DependencyDescription Create(LibraryDescription library,
                                                   List<DiagnosticMessage> diagnostics,
                                                   IDictionary<string, LibraryExport> exportsLookup)
        {
            return new DependencyDescription
            {
                Name = library.Identity.Name,
                DisplayName = library.Identity.Name,
                Version = library.Identity.Version?.ToNormalizedString(),
                Type = library.Identity.Type.Value,
                Resolved = library.Resolved,
                Path = library.Path,
                Dependencies = library.Dependencies.Select(dependency => GetDependencyItem(dependency, exportsLookup)),
                Errors = diagnostics.Where(d => d.Severity == DiagnosticMessageSeverity.Error)
                                    .Select(d => new DiagnosticMessageView(d)),
                Warnings = diagnostics.Where(d => d.Severity == DiagnosticMessageSeverity.Warning)
                                      .Select(d => new DiagnosticMessageView(d))
            };
        }

        private static DependencyItem GetDependencyItem(LibraryRange dependency,
                                                        IDictionary<string, LibraryExport> exportsLookup)
        {
            return new DependencyItem
            {
                Name = dependency.Name,
                Version = exportsLookup[dependency.Name].Library.Identity.Version?.ToNormalizedString()
            };
        }
    }
}
