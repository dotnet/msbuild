// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NuGet.Versioning;

namespace NuGet
{
    public class ManifestMetadata
    {
        private string _minClientVersionString;
        private IEnumerable<string> _authors = Enumerable.Empty<string>();
        private IEnumerable<string> _owners = Enumerable.Empty<string>();

        [ManifestVersion(5)]
        public string MinClientVersionString
        {
            get { return _minClientVersionString; }
            set
            {
                Version version = null;
                if (!string.IsNullOrEmpty(value) && !System.Version.TryParse(value, out version))
                {
                    // TODO: Resources
                    throw new InvalidDataException("NuGetResources.Manifest_InvalidMinClientVersion");
                }

                _minClientVersionString = value;
                MinClientVersion = version;
            }
        }

        public Version MinClientVersion { get; private set; }

        public string Id { get; set; }

        public NuGetVersion Version { get; set; }

        public string Title { get; set; }

        public IEnumerable<string> Authors
        {
            get { return _authors; }
            set { _authors = value ?? Enumerable.Empty<string>(); }
        }

        public IEnumerable<string> Owners
        {
            get { return (_owners == null || !_owners.Any()) ? _authors : _owners; }
            set { _owners = value ?? Enumerable.Empty<string>(); }
        }

        public Uri IconUrl { get; set; }

        public Uri LicenseUrl { get; set; }

        public Uri ProjectUrl { get; set; }

        public bool RequireLicenseAcceptance { get; set; }

        public bool DevelopmentDependency { get; set; }

        public string Description { get; set; }

        public string Summary { get; set; }

        [ManifestVersion(2)]
        public string ReleaseNotes { get; set; }

        [ManifestVersion(2)]
        public string Copyright { get; set; }

        public string Language { get; set; }

        public string Tags { get; set; }

        public bool Serviceable { get; set; }

        public IEnumerable<PackageDependencySet> DependencySets { get; set; } = new List<PackageDependencySet>();

        public ICollection<PackageReferenceSet> PackageAssemblyReferences { get; set; } = new List<PackageReferenceSet>();

        public IEnumerable<FrameworkAssemblyReference> FrameworkAssemblies { get; set; } = new List<FrameworkAssemblyReference>();
        
        public ICollection<ManifestContentFiles> ContentFiles { get; set; } = new List<ManifestContentFiles>();
    }
}
