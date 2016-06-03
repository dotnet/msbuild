// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Versioning;

namespace NuGet
{
    public class PackageBuilder
    {
        private const string DefaultContentType = "application/octet";
        internal const string ManifestRelationType = "manifest";
        private bool _includeEmptyDirectories = false;

        public PackageBuilder()
        {
            Files = new List<IPackageFile>();
            DependencySets = new List<PackageDependencySet>();
            FrameworkAssemblies = new List<FrameworkAssemblyReference>();
            PackageAssemblyReferences = new List<PackageReferenceSet>();
            ContentFiles = new List<ManifestContentFiles>();
            Authors = new List<string>();
            Owners = new List<string>();
            Tags = new List<string>();
        }

        public string Id
        {
            get;
            set;
        }

        public NuGetVersion Version
        {
            get;
            set;
        }

        public string Title
        {
            get;
            set;
        }

        public List<string> Authors
        {
            get;
            private set;
        }

        public List<string> Owners
        {
            get;
            private set;
        }

        public Uri IconUrl
        {
            get;
            set;
        }

        public Uri LicenseUrl
        {
            get;
            set;
        }

        public Uri ProjectUrl
        {
            get;
            set;
        }

        public bool RequireLicenseAcceptance
        {
            get;
            set;
        }

        public bool Serviceable
        {
            get;
            set;
        }

        public bool DevelopmentDependency
        {
            get;
            set;
        }

        public string Description
        {
            get;
            set;
        }

        public string Summary
        {
            get;
            set;
        }

        public string ReleaseNotes
        {
            get;
            set;
        }

        public string Language
        {
            get;
            set;
        }

        public List<string> Tags
        {
            get;
            private set;
        }

        public string Copyright
        {
            get;
            set;
        }

        public List<PackageDependencySet> DependencySets
        {
            get;
            private set;
        }

        public List<IPackageFile> Files
        {
            get;
            private set;
        }

        public List<FrameworkAssemblyReference> FrameworkAssemblies
        {
            get;
            private set;
        }

        public List<PackageReferenceSet> PackageAssemblyReferences
        {
            get;
            private set;
        }

        public List<ManifestContentFiles> ContentFiles
        {
            get;
            private set;
        }

        public Version MinClientVersion
        {
            get;
            set;
        }

        public void Save(Stream stream)
        {
            // Make sure we're saving a valid package id
            PackageIdValidator.ValidatePackageId(Id);

            // Throw if the package doesn't contain any dependencies nor content
            if (!Files.Any() && !DependencySets.SelectMany(d => d.Dependencies).Any() && !FrameworkAssemblies.Any())
            {
                // TODO: Resources
                throw new InvalidOperationException("NuGetResources.CannotCreateEmptyPackage");
            }

            if (!ValidateSpecialVersionLength(Version))
            {
                // TODO: Resources
                throw new InvalidOperationException("NuGetResources.SemVerSpecialVersionTooLong");
            }

            ValidateDependencySets(Version, DependencySets);
            ValidateReferenceAssemblies(Files, PackageAssemblyReferences);

            using (var package = new ZipArchive(stream, ZipArchiveMode.Create))
            {
                // Validate and write the manifest
                WriteManifest(package, ManifestVersionUtility.DefaultVersion);

                // Write the files to the package
                var extensions = WriteFiles(package);

                extensions.Add("nuspec");

                WriteOpcContentTypes(package, extensions);
            }
        }

        private static string CreatorInfo()
        {
            var creatorInfo = new List<string>();
            var assembly = typeof(PackageBuilder).GetTypeInfo().Assembly;
            creatorInfo.Add(assembly.FullName);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                creatorInfo.Add("Linux");
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                creatorInfo.Add("OSX");
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                creatorInfo.Add("Windows");
            }

            var attribute = assembly.GetCustomAttributes<System.Runtime.Versioning.TargetFrameworkAttribute>().FirstOrDefault();
            if (attribute != null)
            {
                creatorInfo.Add(attribute.FrameworkDisplayName);
            }

            return String.Join(";", creatorInfo);
        }

        internal static void ValidateDependencySets(SemanticVersion version, IEnumerable<PackageDependencySet> dependencies)
        {
            if (version == null)
            {
                // We have independent validation for null-versions.
                return;
            }

            foreach (var dep in dependencies.SelectMany(s => s.Dependencies))
            {
                PackageIdValidator.ValidatePackageId(dep.Id);
            }

            // REVIEW: Do we want to keep enfocing this?
            /*if (version.IsPrerelease)
            {
                // If we are creating a production package, do not allow any of the dependencies to be a prerelease version.
                var prereleaseDependency = dependencies.SelectMany(set => set.Dependencies).FirstOrDefault(IsPrereleaseDependency);
                if (prereleaseDependency != null)
                {
                    throw new InvalidDataException(String.Format(CultureInfo.CurrentCulture, "NuGetResources.Manifest_InvalidPrereleaseDependency", prereleaseDependency.ToString()));
                }
            }*/
        }

        internal static void ValidateReferenceAssemblies(IEnumerable<IPackageFile> files, IEnumerable<PackageReferenceSet> packageAssemblyReferences)
        {
            var libFiles = new HashSet<string>(from file in files
                                               where !string.IsNullOrEmpty(file.Path) && file.Path.StartsWith("lib", StringComparison.OrdinalIgnoreCase)
                                               select Path.GetFileName(file.Path), StringComparer.OrdinalIgnoreCase);

            foreach (var reference in packageAssemblyReferences.SelectMany(p => p.References))
            {
                if (!libFiles.Contains(reference) &&
                    !libFiles.Contains(reference + ".dll") &&
                    !libFiles.Contains(reference + ".exe") &&
                    !libFiles.Contains(reference + ".winmd"))
                {
                    // TODO: Resources
                    throw new InvalidDataException(String.Format(CultureInfo.CurrentCulture, "NuGetResources.Manifest_InvalidReference", reference));
                }
            }
        }

        public void Populate(ManifestMetadata manifestMetadata)
        {
            Id = manifestMetadata.Id;
            Version = manifestMetadata.Version;
            Title = manifestMetadata.Title;
            AppendIfNotNull(Authors, manifestMetadata.Authors);
            AppendIfNotNull(Owners, manifestMetadata.Owners);
            IconUrl = manifestMetadata.IconUrl;
            LicenseUrl = manifestMetadata.LicenseUrl;
            ProjectUrl = manifestMetadata.ProjectUrl;
            RequireLicenseAcceptance = manifestMetadata.RequireLicenseAcceptance;
            DevelopmentDependency = manifestMetadata.DevelopmentDependency;
            Serviceable = manifestMetadata.Serviceable;
            Description = manifestMetadata.Description;
            Summary = manifestMetadata.Summary;
            ReleaseNotes = manifestMetadata.ReleaseNotes;
            Language = manifestMetadata.Language;
            Copyright = manifestMetadata.Copyright;
            MinClientVersion = manifestMetadata.MinClientVersion;

            if (manifestMetadata.Tags != null)
            {
                Tags.AddRange(ParseTags(manifestMetadata.Tags));
            }

            AppendIfNotNull(DependencySets, manifestMetadata.DependencySets);
            AppendIfNotNull(FrameworkAssemblies, manifestMetadata.FrameworkAssemblies);
            AppendIfNotNull(PackageAssemblyReferences, manifestMetadata.PackageAssemblyReferences);
            AppendIfNotNull(ContentFiles, manifestMetadata.ContentFiles);
        }

        public void PopulateFiles(string basePath, IEnumerable<ManifestFile> files)
        {
            foreach (var file in files)
            {
                AddFiles(basePath, file.Source, file.Target, file.Exclude);
            }
        }

        private void WriteManifest(ZipArchive package, int minimumManifestVersion)
        {
            string path = Id + Constants.ManifestExtension;

            WriteOpcManifestRelationship(package, path);

            ZipArchiveEntry entry = package.CreateEntry(path, CompressionLevel.Optimal);

            using (Stream stream = entry.Open())
            {
                Manifest manifest = Manifest.Create(this);
                manifest.Save(stream, minimumManifestVersion);
            }
        }

        private HashSet<string> WriteFiles(ZipArchive package)
        {
            var extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Add files that might not come from expanding files on disk
            foreach (var file in Files.Distinct())
            {
                using (Stream stream = file.GetStream())
                {
                    try
                    {
                        CreatePart(package, file.Path, stream);

                        var fileExtension = Path.GetExtension(file.Path);

                        // We have files without extension (e.g. the executables for Nix)
                        if (!string.IsNullOrEmpty(fileExtension))
                        {
                            extensions.Add(fileExtension.Substring(1));
                        }
                    }
                    catch
                    {
                        throw;
                    }
                }
            }

            return extensions;
        }

        private void AddFiles(string basePath, string source, string destination, string exclude = null)
        {
            List<PhysicalPackageFile> searchFiles = PathResolver.ResolveSearchPattern(basePath, source, destination, _includeEmptyDirectories).ToList();

            ExcludeFiles(searchFiles, basePath, exclude);

            if (!PathResolver.IsWildcardSearch(source) && !PathResolver.IsDirectoryPath(source) && !searchFiles.Any())
            {
                // TODO: Resources
                throw new FileNotFoundException(
                    String.Format(CultureInfo.CurrentCulture, "NuGetResources.PackageAuthoring_FileNotFound {0}", source));
            }


            Files.AddRange(searchFiles);
        }

        private static void ExcludeFiles(List<PhysicalPackageFile> searchFiles, string basePath, string exclude)
        {
            if (String.IsNullOrEmpty(exclude))
            {
                return;
            }

            // One or more exclusions may be specified in the file. Split it and prepend the base path to the wildcard provided.
            var exclusions = exclude.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var item in exclusions)
            {
                string wildCard = PathResolver.NormalizeWildcardForExcludedFiles(basePath, item);
                PathResolver.FilterPackageFiles(searchFiles, p => p.SourcePath, new[] { wildCard });
            }
        }

        private static void CreatePart(ZipArchive package, string path, Stream sourceStream)
        {
            if (PackageHelper.IsManifest(path))
            {
                return;
            }

            var entry = package.CreateEntry(PathUtility.GetPathWithForwardSlashes(path), CompressionLevel.Optimal);
            using (var stream = entry.Open())
            {
                sourceStream.CopyTo(stream);
            }
        }

        /// <summary>
        /// Tags come in this format. tag1 tag2 tag3 etc..
        /// </summary>
        private static IEnumerable<string> ParseTags(string tags)
        {
            return from tag in tags.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                   select tag.Trim();
        }

        private static bool IsPrereleaseDependency(PackageDependency dependency)
        {
            return dependency.VersionRange.MinVersion?.IsPrerelease == true ||
                   dependency.VersionRange.MaxVersion?.IsPrerelease == true;
        }

        private static bool ValidateSpecialVersionLength(SemanticVersion version)
        {
            if (!version.IsPrerelease)
            {
                return true;
            }

            return version == null || version.Release.Length <= 20;
        }

        private static void WriteOpcManifestRelationship(ZipArchive package, string path)
        {
            ZipArchiveEntry relsEntry = package.CreateEntry("_rels/.rels", CompressionLevel.Optimal);

            using (var writer = new StreamWriter(relsEntry.Open()))
            {
                writer.Write(String.Format(@"<?xml version=""1.0"" encoding=""utf-8""?>
<Relationships xmlns=""http://schemas.openxmlformats.org/package/2006/relationships"">
    <Relationship Type=""http://schemas.microsoft.com/packaging/2010/07/manifest"" Target=""/{0}"" Id=""{1}"" />
</Relationships>", path, GenerateRelationshipId()));
                writer.Flush();
            }
        }

        private static void WriteOpcContentTypes(ZipArchive package, HashSet<string> extensions)
        {
            // OPC backwards compatibility
            ZipArchiveEntry relsEntry = package.CreateEntry("[Content_Types].xml", CompressionLevel.Optimal);

            using (var writer = new StreamWriter(relsEntry.Open()))
            {
                writer.Write(@"<?xml version=""1.0"" encoding=""utf-8""?>
<Types xmlns=""http://schemas.openxmlformats.org/package/2006/content-types"">
    <Default Extension=""rels"" ContentType=""application/vnd.openxmlformats-package.relationships+xml"" />");
                foreach (var extension in extensions)
                {
                    writer.Write(@"<Default Extension=""" + extension + @""" ContentType=""application/octet"" />");
                }
                writer.Write("</Types>");
                writer.Flush();
            }
        }

        // Generate a relationship id for compatibility
        private static string GenerateRelationshipId()
        {
            return "R" + Guid.NewGuid().ToString("N").Substring(0, 16);
        }

        private static void AppendIfNotNull<T>(List<T> collection, IEnumerable<T> toAdd)
        {
            if (toAdd != null)
            {
                collection.AddRange(toAdd);
            }
        }
    }
}
