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
            AddElementIfNotNull(elem, ns, "authors", metadata.Authors, authors => string.Join(",", authors));
            AddElementIfNotNull(elem, ns, "owners", metadata.Owners, owners => string.Join(",", owners));
            AddElementIfNotNull(elem, ns, "licenseUrl", metadata.LicenseUrl);
            AddElementIfNotNull(elem, ns, "projectUrl", metadata.ProjectUrl);
            AddElementIfNotNull(elem, ns, "iconUrl", metadata.IconUrl);
            elem.Add(new XElement(ns + "requireLicenseAcceptance", metadata.RequireLicenseAcceptance));
            if (metadata.DevelopmentDependency == true)
            {
                elem.Add(new XElement(ns + "developmentDependency", metadata.DevelopmentDependency));
            }
            AddElementIfNotNull(elem, ns, "description", metadata.Description);
            AddElementIfNotNull(elem, ns, "summary", metadata.Summary);
            AddElementIfNotNull(elem, ns, "releaseNotes", metadata.ReleaseNotes);
            AddElementIfNotNull(elem, ns, "copyright", metadata.Copyright);
            AddElementIfNotNull(elem, ns, "language", metadata.Language);
            AddElementIfNotNull(elem, ns, "tags", metadata.Tags);
            if (metadata.Serviceable)
            {
                elem.Add(new XElement(ns + "serviceable", metadata.Serviceable));
            }

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


        public static XElement ToXElement(this IEnumerable<ManifestFile> fileList, XNamespace ns)
        {
            return GetXElementFromManifestFiles(ns, fileList);
        }

        public static string GetOptionalAttributeValue(this XElement element, string localName, string namespaceName = null)
        {
            XAttribute attr;
            if (string.IsNullOrEmpty(namespaceName))
            {
                attr = element.Attribute(localName);
            }
            else
            {
                attr = element.Attribute(XName.Get(localName, namespaceName));
            }
            return attr != null ? attr.Value : null;
        }

        public static string GetOptionalElementValue(this XContainer element, string localName, string namespaceName = null)
        {
            XElement child;
            if (string.IsNullOrEmpty(namespaceName))
            {
                child = element.ElementsNoNamespace(localName).FirstOrDefault();
            }
            else
            {
                child = element.Element(XName.Get(localName, namespaceName));
            }
            return child != null ? child.Value : null;
        }

        public static IEnumerable<XElement> ElementsNoNamespace(this XContainer container, string localName)
        {
            return container.Elements().Where(e => e.Name.LocalName == localName);
        }

        public static IEnumerable<XElement> ElementsNoNamespace(this IEnumerable<XContainer> source, string localName)
        {
            return source.Elements().Where(e => e.Name.LocalName == localName);
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

        private static XElement GetXElementFromPackageReference(XNamespace ns, string reference)
        {
            return new XElement(ns + Reference, new XAttribute(File, reference));
        }

        private static XElement GetXElementFromPackageDependency(XNamespace ns, PackageDependency dependency)
        {
            return new XElement(ns + "dependency",
                new XAttribute("id", dependency.Id),
                dependency.VersionRange != null ? new XAttribute("version", dependency.VersionRange.ToString()) : null,
                dependency.Include != null && dependency.Include.Any() ? new XAttribute("include", string.Join(",", dependency.Include)) : null,
                dependency.Exclude != null && dependency.Exclude.Any() ? new XAttribute("exclude", string.Join(",", dependency.Exclude)) : null);
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
                    new XAttribute("target", file.Target),
                    new XAttribute("exclude", file.Exclude)
                )));
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
    }
}
