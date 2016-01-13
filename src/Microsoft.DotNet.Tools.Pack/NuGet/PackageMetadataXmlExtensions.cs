// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using NuGet.Frameworks;
using NuGet.Packaging.Core;
using NuGet.Versioning;

namespace NuGet
{
    internal static class PackageMetadataXmlExtensions
    {
        private const string References = "references";
        private const string Reference = "reference";
        private const string Group = "group";
        private const string File = "file";
        private const string TargetFramework = "targetFramework";
        private const string FrameworkAssemblies = "frameworkAssemblies";
        private const string FrameworkAssembly = "frameworkAssembly";
        private const string AssemblyName = "assemblyName";
        private const string Dependencies = "dependencies";
        private const string Files = "files";

        public static XElement ToXElement(this ManifestMetadata metadata, XNamespace ns)
        {
            var elem = new XElement(ns + "metadata");
            if (metadata.MinClientVersionString != null)
            {
                elem.SetAttributeValue("minClientVersion", metadata.MinClientVersionString);
            }

            elem.Add(new XElement(ns + "id", metadata.Id));
            elem.Add(new XElement(ns + "version", metadata.Version.ToString()));
            AddElementIfNotNull(elem, ns, "title", metadata.Title);
            elem.Add(new XElement(ns + "requireLicenseAcceptance", metadata.RequireLicenseAcceptance));
            elem.Add(new XElement(ns + "developmentDependency", metadata.DevelopmentDependency));
            AddElementIfNotNull(elem, ns, "authors", metadata.Authors, authors => string.Join(",", authors));
            AddElementIfNotNull(elem, ns, "owners", metadata.Owners, owners => string.Join(",", owners));
            AddElementIfNotNull(elem, ns, "licenseUrl", metadata.LicenseUrl);
            AddElementIfNotNull(elem, ns, "projectUrl", metadata.ProjectUrl);
            AddElementIfNotNull(elem, ns, "iconUrl", metadata.IconUrl);
            AddElementIfNotNull(elem, ns, "description", metadata.Description);
            AddElementIfNotNull(elem, ns, "summary", metadata.Summary);
            AddElementIfNotNull(elem, ns, "releaseNotes", metadata.ReleaseNotes);
            AddElementIfNotNull(elem, ns, "copyright", metadata.Copyright);
            AddElementIfNotNull(elem, ns, "language", metadata.Language);
            AddElementIfNotNull(elem, ns, "tags", metadata.Tags);

            elem.Add(GetXElementFromGroupableItemSets(
                ns,
                metadata.DependencySets,
                set => set.TargetFramework != null,
                set => set.TargetFramework.GetFrameworkString(),
                set => set.Dependencies,
                GetXElementFromPackageDependency,
                Dependencies,
                TargetFramework));

            elem.Add(GetXElementFromGroupableItemSets(
                ns,
                metadata.PackageAssemblyReferences,
                set => set.TargetFramework != null,
                set => set.TargetFramework.GetFrameworkString(),
                set => set.References,
                GetXElementFromPackageReference,
                References,
                TargetFramework));

            elem.Add(GetXElementFromFrameworkAssemblies(ns, metadata.FrameworkAssemblies));
            elem.Add(GetXElementFromManifestContentFiles(ns, metadata.ContentFiles));

            return elem;
        }

        public static Manifest ReadManifest(this XElement element, XNamespace ns)
        {
            if (element.Name != ns + "package")
            {
                return null;
            }

            var metadataElement = element.Element(ns + "metadata");
            if (metadataElement == null)
            {
                return null;
            }

            ManifestMetadata metadata = new ManifestMetadata();

            metadata.MinClientVersionString = metadataElement.Attribute("minClientVersion")?.Value;
            metadata.Id = metadataElement.Element(ns + "id")?.Value;
            metadata.Version = ConvertIfNotNull(metadataElement.Element(ns + "version")?.Value, s => new NuGetVersion(s));
            metadata.Title = metadataElement.Element(ns + "title")?.Value;
            metadata.RequireLicenseAcceptance = ConvertIfNotNull(metadataElement.Element(ns + "requireLicenseAcceptance")?.Value, s => bool.Parse(s));
            metadata.DevelopmentDependency = ConvertIfNotNull(metadataElement.Element(ns + "developmentDependency")?.Value, s => bool.Parse(s));
            metadata.Authors = ConvertIfNotNull(metadataElement.Element(ns + "authors")?.Value, s => s.Split(','));
            metadata.Owners = ConvertIfNotNull(metadataElement.Element(ns + "owners")?.Value, s => s.Split(','));
            metadata.LicenseUrl = ConvertIfNotNull(metadataElement.Element(ns + "licenseUrl")?.Value, s => new Uri(s));
            metadata.ProjectUrl = ConvertIfNotNull(metadataElement.Element(ns + "projectUrl")?.Value, s => new Uri(s));
            metadata.IconUrl = ConvertIfNotNull(metadataElement.Element(ns + "iconUrl")?.Value, s => new Uri(s));
            metadata.Description = metadataElement.Element(ns + "description")?.Value;
            metadata.Summary = metadataElement.Element(ns + "summary")?.Value;
            metadata.ReleaseNotes = metadataElement.Element(ns + "releaseNotes")?.Value;
            metadata.Copyright = metadataElement.Element(ns + "copyright")?.Value;
            metadata.Language = metadataElement.Element(ns + "language")?.Value;
            metadata.Tags = metadataElement.Element(ns + "tags")?.Value;
            
            metadata.DependencySets = GetItemSetsFromGroupableXElements(
                ns,
                metadataElement,
                Dependencies,
                "dependency",
                TargetFramework,
                GetPackageDependencyFromXElement,
                (tfm, deps) => new PackageDependencySet(tfm, deps));

            metadata.PackageAssemblyReferences = GetItemSetsFromGroupableXElements(
                ns,
                metadataElement,
                References,
                Reference,
                TargetFramework,
                GetPackageReferenceFromXElement,
                (tfm, refs) => new PackageReferenceSet(tfm, refs)).ToArray();

            metadata.FrameworkAssemblies = GetFrameworkAssembliesFromXElement(ns, metadataElement);

            metadata.ContentFiles = GetManifestContentFilesFromXElement(ns, metadataElement);

            Manifest manifest = new Manifest(metadata);

            var files = GetManifestFilesFromXElement(ns, element);
            if (files != null)
            {
                foreach(var file in files)
                {
                    manifest.Files.Add(file);
                }
            }

            return manifest;
        }

        public static XElement ToXElement(this IEnumerable<ManifestFile> fileList, XNamespace ns)
        {
            return GetXElementFromManifestFiles(ns, fileList);
        }

        private static XElement GetXElementFromGroupableItemSets<TSet, TItem>(
            XNamespace ns,
            IEnumerable<TSet> objectSets,
            Func<TSet, bool> isGroupable,
            Func<TSet, string> getGroupIdentifer,
            Func<TSet, IEnumerable<TItem>> getItems,
            Func<XNamespace, TItem, XElement> getXElementFromItem,
            string parentName,
            string identiferAttributeName)
        {
            if (objectSets == null || !objectSets.Any())
            {
                return null;
            }

            var groupableSets = new List<TSet>();
            var ungroupableSets = new List<TSet>();

            foreach (var set in objectSets)
            {
                if (isGroupable(set))
                {
                    groupableSets.Add(set);
                }
                else
                {
                    ungroupableSets.Add(set);
                }
            }

            var childElements = new List<XElement>();
            if (!groupableSets.Any())
            {
                // none of the item sets are groupable, then flatten the items
                childElements.AddRange(objectSets.SelectMany(getItems).Select(item => getXElementFromItem(ns, item)));
            }
            else
            {
                // move the group with null target framework (if any) to the front just for nicer display in UI
                foreach (var set in ungroupableSets.Concat(groupableSets))
                {
                    var groupElem = new XElement(
                        ns + Group,
                        getItems(set).Select(item => getXElementFromItem(ns, item)).ToArray());

                    if (isGroupable(set))
                    {
                        groupElem.SetAttributeValue(identiferAttributeName, getGroupIdentifer(set));
                    }

                    childElements.Add(groupElem);
                }
            }

            return new XElement(ns + parentName, childElements.ToArray());
        }

        private static IEnumerable<TSet> GetItemSetsFromGroupableXElements<TSet, TItem>(
            XNamespace ns,
            XElement parent,
            string rootName,
            string elementName,
            string identifierAttributeName,
            Func<XElement, TItem> getItemFromXElement,
            Func<string, IEnumerable<TItem>, TSet> getItemSet)
        {
            XElement rootElement = parent.Element(ns + rootName);
            
            if (rootElement == null)
            {
                return Enumerable.Empty<TSet>();
            }

            var groups = rootElement.Elements(ns + Group);

            if (groups == null || !groups.Any())
            {
                // no groupable sets, all are ungroupable
                return new[] { getItemSet(null, rootElement.Elements(ns + elementName).Select(e => getItemFromXElement(e))) };
            }

            return groups.Select(g => 
                getItemSet(g.Attribute(identifierAttributeName)?.Value,
                    g.Elements(ns + elementName).Select(e => getItemFromXElement(e))));
        }

        private static XElement GetXElementFromPackageReference(XNamespace ns, string reference)
        {
            return new XElement(ns + Reference, new XAttribute(File, reference));
        }

        private static string GetPackageReferenceFromXElement(XElement element)
        {
            return element.Attribute(File)?.Value;
        }

        private static XElement GetXElementFromPackageDependency(XNamespace ns, PackageDependency dependency)
        {
            return new XElement(ns + "dependency",
                new XAttribute("id", dependency.Id),
                dependency.VersionRange != null ? new XAttribute("version", dependency.VersionRange.ToString()) : null,
                dependency.Include != null && dependency.Include.Any() ? new XAttribute("include", string.Join(",", dependency.Include)) : null,
                dependency.Exclude != null && dependency.Exclude.Any() ? new XAttribute("exclude", string.Join(",", dependency.Exclude)) : null);
        }

        private static PackageDependency GetPackageDependencyFromXElement(XElement element)
        {
            return new PackageDependency(element.Attribute("id").Value,
                ConvertIfNotNull(element.Attribute("version")?.Value, s => VersionRange.Parse(s)),
                ConvertIfNotNull(element.Attribute("include")?.Value, s => s.Split(',')),
                ConvertIfNotNull(element.Attribute("exclude")?.Value, s => s.Split(',')));
        }

        private static XElement GetXElementFromFrameworkAssemblies(XNamespace ns, IEnumerable<FrameworkAssemblyReference> references)
        {
            if (references == null || !references.Any())
            {
                return null;
            }

            return new XElement(
                ns + FrameworkAssemblies,
                references.Select(reference =>
                    new XElement(ns + FrameworkAssembly,
                        new XAttribute(AssemblyName, reference.AssemblyName),
                        reference.SupportedFrameworks != null && reference.SupportedFrameworks.Any() ?
                            new XAttribute("targetFramework", string.Join(", ", reference.SupportedFrameworks.Select(f => f.GetFrameworkString()))) :
                            null)));
        }

        private static IEnumerable<FrameworkAssemblyReference> GetFrameworkAssembliesFromXElement(XNamespace ns, XElement parent)
        {
            var frameworkAssembliesElement = parent.Element(ns + FrameworkAssemblies);

            if (frameworkAssembliesElement == null)
            {
                return null;
            }

            return frameworkAssembliesElement.Elements(ns + FrameworkAssembly).Select(e =>
                new FrameworkAssemblyReference(e.Attribute(AssemblyName).Value,
                    e.Attribute("targetFramework")?.Value?.Split(',')?
                        .Select(tf => NuGetFramework.Parse(tf)) ?? Enumerable.Empty<NuGetFramework>()));
            
        }

        private static XElement GetXElementFromManifestFiles(XNamespace ns, IEnumerable<ManifestFile> files)
        {
            if (files == null || !files.Any())
            {
                return null;
            }

            return new XElement(ns + Files,
                files.Select(file =>
                new XElement(ns + File,
                    new XAttribute("src", file.Source),
                    new XAttribute("target", file.Source),
                    new XAttribute("exclude", file.Exclude)
                )));
        }

        private static IEnumerable<ManifestFile> GetManifestFilesFromXElement(XNamespace ns, XElement parent)
        {
            var filesElement = parent.Element(ns + Files);

            if (filesElement == null)
            {
                return null;
            }

            return filesElement.Elements(ns + File).Select(f => 
                new ManifestFile(f.Attribute("src").Value,
                    f.Attribute("target").Value,
                    f.Attribute("exclude")?.Value));
        }

        private static XElement GetXElementFromManifestContentFiles(XNamespace ns, IEnumerable<ManifestContentFiles> contentFiles)
        {
            if (contentFiles == null || !contentFiles.Any())
            {
                return null;
            }

            return new XElement(ns + "contentFiles",
                contentFiles.Select(file =>
                new XElement(ns + File,
                    new XAttribute("include", file.Include),
                    new XAttribute("exclude", file.Exclude),
                    new XAttribute("buildAction", file.BuildAction),
                    new XAttribute("copyToOutput", file.CopyToOutput),
                    new XAttribute("flatten", file.Flatten)
                )));
        }
        
        private static ICollection<ManifestContentFiles> GetManifestContentFilesFromXElement(XNamespace ns, XElement parent)
        {
            var contentFilesElement = parent.Element(ns + "contentFiles");

            if (contentFilesElement == null)
            {
                return null;
            }

            return contentFilesElement.Elements(ns + File).Select(cf =>
                new ManifestContentFiles()
                {
                    Include = cf.Attribute("include")?.Value,
                    Exclude = cf.Attribute("exclude")?.Value,
                    BuildAction = cf.Attribute("buildAction")?.Value,
                    CopyToOutput = cf.Attribute("copyToOutput")?.Value,
                    Flatten = cf.Attribute("flatten")?.Value,
                }).ToArray();
        }

        private static void AddElementIfNotNull<T>(XElement parent, XNamespace ns, string name, T value)
            where T : class
        {
            if (value != null)
            {
                parent.Add(new XElement(ns + name, value));
            }
        }

        private static void AddElementIfNotNull<T>(XElement parent, XNamespace ns, string name, T value, Func<T, object> process)
            where T : class
        {
            if (value != null)
            {
                var processed = process(value);
                if (processed != null)
                {
                    parent.Add(new XElement(ns + name, processed));
                }
            }
        }
        private static TDest ConvertIfNotNull<TDest, TSource>(TSource value, Func<TSource, TDest> convert)
        {
            if (value != null)
            {
                var converted = convert(value);

                return converted;
            }

            return default(TDest);
        }
    }
}
