// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Xml;

namespace Microsoft.Build.Shared
{
    internal sealed class SolutionConfiguration
    {
        public const string ProjectAttribute = "Project";

        public const string AbsolutePathAttribute = "AbsolutePath";

        public const string BuildProjectInSolutionAttribute = "BuildProjectInSolution";

        public static readonly char[] ConfigPlatformSeparator = { '|' };

        // This field stores pre-cached project elements for project guids for quicker access by project guid
        private readonly Dictionary<string, XmlElement> _cachedProjectElements = new Dictionary<string, XmlElement>(StringComparer.OrdinalIgnoreCase);

        // This field stores pre-cached project elements for project guids for quicker access by project absolute path
        private readonly Dictionary<string, XmlElement> _cachedProjectElementsByAbsolutePath = new Dictionary<string, XmlElement>(StringComparer.OrdinalIgnoreCase);

        // This field stores the project absolute path for quicker access by project guid
        private readonly Dictionary<string, string> _cachedProjectAbsolutePathsByGuid = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // This field stores the project guid for quicker access by project absolute path
        private readonly Dictionary<string, string> _cachedProjectGuidsByAbsolutePath = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // This field stores the list of dependency project guids by depending project guid
        private readonly Dictionary<string, List<string>> _cachedDependencyProjectGuidsByDependingProjectGuid = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        public SolutionConfiguration(string xmlString)
        {
            // Example:
            //
            // <SolutionConfiguration>
            //  <ProjectConfiguration Project="{786E302A-96CE-43DC-B640-D6B6CC9BF6C0}" AbsolutePath="c:foo\Project1\A.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
            //  <ProjectConfiguration Project="{881C1674-4ECA-451D-85B6-D7C59B7F16FA}" AbsolutePath="c:foo\Project2\B.csproj" BuildProjectInSolution="True">Debug|AnyCPU<ProjectDependency Project="{4A727FF8-65F2-401E-95AD-7C8BBFBE3167}" /></ProjectConfiguration>
            //  <ProjectConfiguration Project="{4A727FF8-65F2-401E-95AD-7C8BBFBE3167}" AbsolutePath="c:foo\Project3\C.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
            // </SolutionConfiguration>
            //
            XmlNodeList? projectConfigurationElements = GetProjectConfigurations(xmlString);
            if (projectConfigurationElements != null)
            {
                foreach (XmlElement xmlElement in projectConfigurationElements)
                {
                    string projectGuid = xmlElement.GetAttribute(ProjectAttribute);
                    string projectAbsolutePath = xmlElement.GetAttribute(AbsolutePathAttribute);

                    // What we really want here is the normalized path, like we'd get with an item's "FullPath" metadata.  However,
                    // if there's some bogus full path in the solution configuration (e.g. a website with a "full path" of c:\solutiondirectory\http://localhost)
                    // we do NOT want to throw -- chances are extremely high that that's information that will never actually be used.  So resolve the full path
                    // but just swallow any IO-related exceptions that result.  If the path is bogus, the method will return null, so we'll just quietly fail
                    // to cache it below.
                    projectAbsolutePath = FileUtilities.GetFullPathNoThrow(projectAbsolutePath);

                    if (!string.IsNullOrEmpty(projectGuid))
                    {
                        _cachedProjectElements[projectGuid] = xmlElement;
                        if (!string.IsNullOrEmpty(projectAbsolutePath))
                        {
                            _cachedProjectElementsByAbsolutePath[projectAbsolutePath] = xmlElement;
                            _cachedProjectAbsolutePathsByGuid[projectGuid] = projectAbsolutePath;
                            _cachedProjectGuidsByAbsolutePath[projectAbsolutePath] = projectGuid;
                        }

                        foreach (XmlNode dependencyNode in xmlElement.ChildNodes)
                        {
                            if (dependencyNode.NodeType != XmlNodeType.Element)
                            {
                                continue;
                            }

                            XmlElement dependencyElement = ((XmlElement)dependencyNode);

                            if (!String.Equals(dependencyElement.Name, "ProjectDependency", StringComparison.Ordinal))
                            {
                                continue;
                            }

                            string dependencyGuid = dependencyElement.GetAttribute("Project");

                            if (dependencyGuid.Length == 0)
                            {
                                continue;
                            }

                            if (!_cachedDependencyProjectGuidsByDependingProjectGuid.TryGetValue(projectGuid, out List<string>? list))
                            {
                                list = new List<string>();
                                _cachedDependencyProjectGuidsByDependingProjectGuid.Add(projectGuid, list);
                            }

                            list.Add(dependencyGuid);
                        }
                    }
                }
            }
        }

        public static SolutionConfiguration Empty { get; } = new SolutionConfiguration(string.Empty);

        public ICollection<XmlElement> ProjectConfigurations => _cachedProjectElements.Values;

        public static XmlNodeList? GetProjectConfigurations(string xmlString)
        {
            XmlDocument? doc = null;

            if (!string.IsNullOrEmpty(xmlString))
            {
                doc = new XmlDocument();
                var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore };
                using (XmlReader reader = XmlReader.Create(new StringReader(xmlString), settings))
                {
                    doc.Load(reader);
                }
            }

            return doc?.DocumentElement?.ChildNodes;
        }

        public bool TryGetProjectByGuid(string projectGuid, [NotNullWhen(true)] out XmlElement? projectElement) => _cachedProjectElements.TryGetValue(projectGuid, out projectElement);

        public bool TryGetProjectByAbsolutePath(string projectFullPath, [NotNullWhen(true)] out XmlElement? projectElement) => _cachedProjectElementsByAbsolutePath.TryGetValue(projectFullPath, out projectElement);

        public bool TryGetProjectGuidByAbsolutePath(string projectFullPath, [NotNullWhen(true)] out string? projectGuid) => _cachedProjectGuidsByAbsolutePath.TryGetValue(projectFullPath, out projectGuid);

        public bool TryGetProjectDependencies(string projectGuid, [NotNullWhen(true)] out List<string>? dependencyProjectGuids) => _cachedDependencyProjectGuidsByDependingProjectGuid.TryGetValue(projectGuid, out dependencyProjectGuids);

        public bool TryGetProjectPathByGuid(string projectGuid, [NotNullWhen(true)] out string? projectPath) => _cachedProjectAbsolutePathsByGuid.TryGetValue(projectGuid, out projectPath);
    }
}
