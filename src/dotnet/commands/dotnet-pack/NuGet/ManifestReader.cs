// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using NuGet.Frameworks;
using NuGet.Packaging.Core;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
// TODO: Resources using NuGet.Resources;

namespace NuGet
{
    internal static class ManifestReader
    {
        private static readonly string[] RequiredElements = new string[] { "id", "version", "authors", "description" };

        public static Manifest ReadManifest(XDocument document)
        {
            var metadataElement = document.Root.ElementsNoNamespace("metadata").FirstOrDefault();
            if (metadataElement == null)
            {
                // TODO: Resources
                throw new InvalidDataException(
                    String.Format(CultureInfo.CurrentCulture, "NuGetResources.Manifest_RequiredElementMissing {0}", "metadata"));
            }

            return new Manifest(ReadMetadata(metadataElement),
                ReadFilesList(document.Root.ElementsNoNamespace("files").FirstOrDefault()));
        }

        private static ManifestMetadata ReadMetadata(XElement xElement)
        {
            var manifestMetadata = new ManifestMetadata();
            manifestMetadata.DependencySets = new List<PackageDependencySet>();
            manifestMetadata.PackageAssemblyReferences = new List<PackageReferenceSet>();
            manifestMetadata.MinClientVersionString = xElement.GetOptionalAttributeValue("minClientVersion");

            // we store all child elements under <metadata> so that we can easily check for required elements.
            var allElements = new HashSet<string>();

            XNode node = xElement.FirstNode;
            while (node != null)
            {
                var element = node as XElement;
                if (element != null)
                {
                    ReadMetadataValue(manifestMetadata, element, allElements);
                }
                node = node.NextNode;
            }

            // now check for required elements, which include <id>, <version>, <authors> and <description>
            foreach (var requiredElement in RequiredElements)
            {
                if (!allElements.Contains(requiredElement))
                {
                    // TODO: Resources
                    throw new InvalidDataException(
                        String.Format(CultureInfo.CurrentCulture, "NuGetResources.Manifest_RequiredElementMissing {0}", requiredElement));
                }
            }

            return manifestMetadata;
        }

        [SuppressMessage("Microsoft.Maintainability", "CA1502:AvoidExcessiveComplexity")]
        private static void ReadMetadataValue(ManifestMetadata manifestMetadata, XElement element, HashSet<string> allElements)
        {
            if (element.Value == null)
            {
                return;
            }

            allElements.Add(element.Name.LocalName);

            string value = element.Value?.Trim();
            switch (element.Name.LocalName)
            {
                case "id":
                    manifestMetadata.Id = value;
                    break;
                case "version":
                    manifestMetadata.Version = NuGetVersion.Parse(value);
                    break;
                case "authors":
                    manifestMetadata.Authors = value.Split(',').Select(a => a.Trim());
                    break;
                case "owners":
                    manifestMetadata.Owners = value.Split(',').Select(a => a.Trim());
                    break;
                case "licenseUrl":
                    manifestMetadata.LicenseUrl = new Uri(value);
                    break;
                case "projectUrl":
                    manifestMetadata.ProjectUrl = new Uri(value);
                    break;
                case "iconUrl":
                    manifestMetadata.IconUrl = new Uri(value);
                    break;
                case "requireLicenseAcceptance":
                    manifestMetadata.RequireLicenseAcceptance = XmlConvert.ToBoolean(value);
                    break;
                case "developmentDependency":
                    manifestMetadata.DevelopmentDependency = XmlConvert.ToBoolean(value);
                    break;
                case "description":
                    manifestMetadata.Description = value;
                    break;
                case "summary":
                    manifestMetadata.Summary = value;
                    break;
                case "releaseNotes":
                    manifestMetadata.ReleaseNotes = value;
                    break;
                case "copyright":
                    manifestMetadata.Copyright = value;
                    break;
                case "language":
                    manifestMetadata.Language = value;
                    break;
                case "title":
                    manifestMetadata.Title = value;
                    break;
                case "tags":
                    manifestMetadata.Tags = value;
                    break;
                case "serviceable":
                    manifestMetadata.Serviceable = XmlConvert.ToBoolean(value);
                    break;
                case "dependencies":
                    manifestMetadata.DependencySets = ReadDependencySets(element);
                    break;
                case "frameworkAssemblies":
                    manifestMetadata.FrameworkAssemblies = ReadFrameworkAssemblies(element);
                    break;
                case "references":
                    manifestMetadata.PackageAssemblyReferences = ReadReferenceSets(element);
                    break;
                case "contentFiles":
                    manifestMetadata.ContentFiles = ReadContentFiles(element);
                    break;
            }
        }

        private static List<ManifestContentFiles> ReadContentFiles(XElement contentFilesElement)
        {
            if (!contentFilesElement.HasElements)
            {
                return new List<ManifestContentFiles>(0);
            }

            var contentFileSets = (from element in contentFilesElement.ElementsNoNamespace("files")
                                   let includeAttribute = element.Attribute("include")
                                   where includeAttribute != null && !string.IsNullOrEmpty(includeAttribute.Value)
                                   let excludeAttribute = element.Attribute("exclude")
                                   let buildActionAttribute = element.Attribute("buildAction")
                                   let copyToOutputAttribute = element.Attribute("copyToOutput")
                                   let flattenAttribute = element.Attribute("flatten")
                                   select new ManifestContentFiles
                                   {
                                       Include = includeAttribute.Value?.Trim(),
                                       Exclude = excludeAttribute == null ? null : excludeAttribute.Value,
                                       BuildAction = buildActionAttribute == null ? null : buildActionAttribute.Value,
                                       CopyToOutput = copyToOutputAttribute == null ? null : copyToOutputAttribute.Value,
                                       Flatten = flattenAttribute == null ? null : flattenAttribute.Value
                                   }).ToList();

            return contentFileSets;
        }

        private static List<PackageReferenceSet> ReadReferenceSets(XElement referencesElement)
        {
            if (!referencesElement.HasElements)
            {
                return new List<PackageReferenceSet>(0);
            }

            if (referencesElement.ElementsNoNamespace("group").Any() &&
                referencesElement.ElementsNoNamespace("reference").Any())
            {
                // TODO: Resources
                throw new InvalidDataException("NuGetResources.Manifest_ReferencesHasMixedElements");
            }

            var references = ReadReference(referencesElement, throwIfEmpty: false);
            if (references.Count > 0)
            {
                // old format, <reference> is direct child of <references>
                var referenceSet = new PackageReferenceSet(references);
                return new List<PackageReferenceSet> { referenceSet };
            }
            else
            {
                var groups = referencesElement.ElementsNoNamespace("group");
                return (from element in groups
                        select new PackageReferenceSet(element.GetOptionalAttributeValue("targetFramework")?.Trim(),
                            ReadReference(element, throwIfEmpty: true))).ToList();
            }
        }

        public static List<string> ReadReference(XElement referenceElement, bool throwIfEmpty)
        {
            var references = (from element in referenceElement.ElementsNoNamespace("reference")
                              let fileAttribute = element.Attribute("file")
                              where fileAttribute != null && !String.IsNullOrEmpty(fileAttribute.Value)
                              select fileAttribute.Value?.Trim()
                             ).ToList();

            if (throwIfEmpty && references.Count == 0)
            {
                // TODO: Resources
                throw new InvalidDataException("NuGetResources.Manifest_ReferencesIsEmpty");
            }

            return references;
        }

        private static List<FrameworkAssemblyReference> ReadFrameworkAssemblies(XElement frameworkElement)
        {
            if (!frameworkElement.HasElements)
            {
                return new List<FrameworkAssemblyReference>(0);
            }

            return (from element in frameworkElement.ElementsNoNamespace("frameworkAssembly")
                    let assemblyNameAttribute = element.Attribute("assemblyName")
                    where assemblyNameAttribute != null && !String.IsNullOrEmpty(assemblyNameAttribute.Value)
                    select new FrameworkAssemblyReference(assemblyNameAttribute.Value?.Trim(),
                        new[] { NuGetFramework.Parse(element.GetOptionalAttributeValue("targetFramework")?.Trim()) })
                    ).ToList();
        }

        private static List<PackageDependencySet> ReadDependencySets(XElement dependenciesElement)
        {
            if (!dependenciesElement.HasElements)
            {
                return new List<PackageDependencySet>();
            }

            // Disallow the <dependencies> element to contain both <dependency> and 
            // <group> child elements. Unfortunately, this cannot be enforced by XSD.
            if (dependenciesElement.ElementsNoNamespace("dependency").Any() &&
                dependenciesElement.ElementsNoNamespace("group").Any())
            {
                // TODO: Resources
                throw new InvalidDataException("NuGetResources.Manifest_DependenciesHasMixedElements");
            }

            var dependencies = ReadDependencies(dependenciesElement);
            if (dependencies.Count > 0)
            {
                // old format, <dependency> is direct child of <dependencies>
                var dependencySet = new PackageDependencySet(dependencies);
                return new List<PackageDependencySet> { dependencySet };
            }
            else
            {
                var groups = dependenciesElement.ElementsNoNamespace("group");
                return (from element in groups
                        select new PackageDependencySet(element.GetOptionalAttributeValue("targetFramework")?.Trim(),
                            ReadDependencies(element))).ToList();
            }
        }

        private static List<PackageDependency> ReadDependencies(XElement containerElement)
        {


            // element is <dependency>
            return (from element in containerElement.ElementsNoNamespace("dependency")
                    let idElement = element.Attribute("id")
                    where idElement != null && !String.IsNullOrEmpty(idElement.Value)
                    select new PackageDependency(
                        idElement.Value?.Trim(),
                        VersionRange.Parse(element.GetOptionalAttributeValue("version")?.Trim()),
                        element.GetOptionalAttributeValue("include")?.Trim()?.Split(',').Select(a => a.Trim()).ToArray(),
                        element.GetOptionalAttributeValue("exclude")?.Trim()?.Split(',').Select(a => a.Trim()).ToArray()
                    )).ToList();
        }

        private static List<ManifestFile> ReadFilesList(XElement xElement)
        {
            if (xElement == null)
            {
                return null;
            }

            List<ManifestFile> files = new List<ManifestFile>();
            foreach (var file in xElement.ElementsNoNamespace("file"))
            {
                var srcElement = file.Attribute("src");
                if (srcElement == null || String.IsNullOrEmpty(srcElement.Value))
                {
                    continue;
                }

                string target = file.GetOptionalAttributeValue("target")?.Trim();
                string exclude = file.GetOptionalAttributeValue("exclude")?.Trim();

                // Multiple sources can be specified by using semi-colon separated values. 
                files.AddRange(from source in srcElement.Value.Trim(';').Split(';')
                               select new ManifestFile(source?.Trim(), target?.Trim(), exclude?.Trim() ));
            }
            return files;
        }
    }
}