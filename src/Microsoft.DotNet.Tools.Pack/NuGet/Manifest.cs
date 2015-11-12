// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace NuGet
{
    public class Manifest
    {
        private const string SchemaVersionAttributeName = "schemaVersion";

        public Manifest(ManifestMetadata metadata)
        {
            if (metadata == null)
            {
                throw new ArgumentNullException(nameof(metadata));
            }

            Metadata = metadata;
        }

        public ManifestMetadata Metadata { get; }

        /// <summary>
        /// Saves the current manifest to the specified stream.
        /// </summary>
        /// <param name="stream">The target stream.</param>
        public void Save(Stream stream)
        {
            Save(stream, validate: true, minimumManifestVersion: 1);
        }

        /// <summary>
        /// Saves the current manifest to the specified stream.
        /// </summary>
        /// <param name="stream">The target stream.</param>
        /// <param name="minimumManifestVersion">The minimum manifest version that this class must use when saving.</param>
        public void Save(Stream stream, int minimumManifestVersion)
        {
            Save(stream, validate: true, minimumManifestVersion: minimumManifestVersion);
        }

        public void Save(Stream stream, bool validate)
        {
            Save(stream, validate, minimumManifestVersion: 1);
        }

        public void Save(Stream stream, bool validate, int minimumManifestVersion)
        {
            int version = Math.Max(minimumManifestVersion, ManifestVersionUtility.GetManifestVersion(Metadata));
            var schemaNamespace = (XNamespace)ManifestSchemaUtility.GetSchemaNamespace(version);

            new XDocument(
                new XElement(schemaNamespace + "package",
                    Metadata.ToXElement(schemaNamespace))).Save(stream);
        }

        public static Manifest Create(PackageBuilder copy)
        {
            var metadata = new ManifestMetadata();
            metadata.Id = copy.Id?.Trim();
            metadata.Version = copy.Version;
            metadata.Title = copy.Title?.Trim();
            metadata.Authors = copy.Authors.Distinct();
            metadata.Owners = copy.Owners.Distinct();
            metadata.Tags = string.Join(",", copy.Tags).Trim();
            metadata.LicenseUrl = copy.LicenseUrl;
            metadata.ProjectUrl = copy.ProjectUrl;
            metadata.IconUrl = copy.IconUrl;
            metadata.RequireLicenseAcceptance = copy.RequireLicenseAcceptance;
            metadata.Description = copy.Description?.Trim();
            metadata.Copyright = copy.Copyright?.Trim();
            metadata.Summary = copy.Summary?.Trim();
            metadata.ReleaseNotes = copy.ReleaseNotes?.Trim();
            metadata.Language = copy.Language?.Trim();
            metadata.DependencySets = copy.DependencySets;
            metadata.FrameworkAssemblies = copy.FrameworkAssemblies;
            metadata.PackageAssemblyReferences = copy.PackageAssemblyReferences;
            metadata.MinClientVersionString = copy.MinClientVersion?.ToString();

            return new Manifest(metadata);
        }
    }
}
