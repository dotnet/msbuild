// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Xml;
using Microsoft.Build.Framework;

#if NETFRAMEWORK
using NewPath = Microsoft.IO.Path;
#elif NET
using NewPath = System.IO.Path;
#endif

namespace Microsoft.Build.Shared
{
    /// <summary>
    /// The project entries of a solution configuration XML string.
    /// </summary>
    /// <remarks>
    /// Instances are immutable and don't retain the parsed XML, so they can be shared between concurrently executing tasks.
    /// </remarks>
    internal sealed class SolutionConfiguration
    {
        public const string ProjectAttribute = "Project";

        public const string AbsolutePathAttribute = "AbsolutePath";

        public const string BuildProjectInSolutionAttribute = "BuildProjectInSolution";

        public static readonly char[] ConfigPlatformSeparator = { '|' };

        // This field stores pre-cached project entries for quicker access by project guid
        private readonly Dictionary<string, ProjectEntry> _cachedProjectEntries;

        // This field stores pre-cached project entries for quicker access by project absolute path
        private readonly Dictionary<string, ProjectEntry> _cachedProjectEntriesByAbsolutePath;

        // This field stores the project absolute path for quicker access by project guid
        private readonly Dictionary<string, string> _cachedProjectAbsolutePathsByGuid;

        // This field stores the project guid for quicker access by project absolute path
        private readonly Dictionary<string, string> _cachedProjectGuidsByAbsolutePath;

        // This field stores the list of dependency project guids by depending project guid
        private readonly Dictionary<string, List<string>> _cachedDependencyProjectGuidsByDependingProjectGuid;

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
            int projectConfigurationCount = projectConfigurationElements?.Count ?? 0;

            _cachedProjectEntries = new Dictionary<string, ProjectEntry>(projectConfigurationCount, StringComparer.OrdinalIgnoreCase);
            _cachedProjectEntriesByAbsolutePath = new Dictionary<string, ProjectEntry>(projectConfigurationCount, StringComparer.OrdinalIgnoreCase);
            _cachedProjectAbsolutePathsByGuid = new Dictionary<string, string>(projectConfigurationCount, StringComparer.OrdinalIgnoreCase);
            _cachedProjectGuidsByAbsolutePath = new Dictionary<string, string>(projectConfigurationCount, StringComparer.OrdinalIgnoreCase);
            _cachedDependencyProjectGuidsByDependingProjectGuid = new Dictionary<string, List<string>>(projectConfigurationCount, StringComparer.OrdinalIgnoreCase);

            if (projectConfigurationElements != null)
            {
                bool dependsOnCurrentDirectory = false;
                int elementIndex = 0;

                foreach (XmlElement xmlElement in projectConfigurationElements)
                {
                    string projectGuid = xmlElement.GetAttribute(ProjectAttribute);

                    if (!string.IsNullOrEmpty(projectGuid))
                    {
                        string projectAbsolutePath = xmlElement.GetAttribute(AbsolutePathAttribute);
                        dependsOnCurrentDirectory |= IsResolvedAgainstCurrentDirectory(projectAbsolutePath);

                        // What we really want here is the normalized path, like we'd get with an item's "FullPath" metadata.  However,
                        // if there's some bogus full path in the solution configuration (e.g. a website with a "full path" of c:\solutiondirectory\http://localhost)
                        // we do NOT want to throw -- chances are extremely high that that's information that will never actually be used.  So resolve the full path
                        // but just swallow any IO-related exceptions that result.  If the path is bogus, the method will return null, so we'll just quietly fail
                        // to cache it below.
                        projectAbsolutePath = FileUtilities.GetFullPathNoThrow(projectAbsolutePath);

                        var projectEntry = new ProjectEntry(xmlElement.InnerText, xmlElement.GetAttribute(BuildProjectInSolutionAttribute), elementIndex);
                        _cachedProjectEntries[projectGuid] = projectEntry;
                        if (!string.IsNullOrEmpty(projectAbsolutePath))
                        {
                            _cachedProjectEntriesByAbsolutePath[projectAbsolutePath] = projectEntry;
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

                    elementIndex++;
                }

                DependsOnCurrentDirectory = dependsOnCurrentDirectory;
            }
        }

        public static SolutionConfiguration Empty { get; } = new SolutionConfiguration(string.Empty);

        /// <summary>
        /// Gets a value indicating whether a project path was resolved against the current directory, in which case
        /// parsing the same XML with a different current directory can produce different results.
        /// </summary>
        public bool DependsOnCurrentDirectory { get; }

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

        public bool TryGetProjectByGuid(string projectGuid, [NotNullWhen(true)] out ProjectEntry? projectEntry) => _cachedProjectEntries.TryGetValue(projectGuid, out projectEntry);

        public bool TryGetProjectByAbsolutePath(string projectFullPath, [NotNullWhen(true)] out ProjectEntry? projectEntry) => _cachedProjectEntriesByAbsolutePath.TryGetValue(projectFullPath, out projectEntry);

        public bool TryGetProjectGuidByAbsolutePath(string projectFullPath, [NotNullWhen(true)] out string? projectGuid) => _cachedProjectGuidsByAbsolutePath.TryGetValue(projectFullPath, out projectGuid);

        public bool TryGetProjectDependencies(string projectGuid, [NotNullWhen(true)] out IReadOnlyList<string>? dependencyProjectGuids)
        {
            if (_cachedDependencyProjectGuidsByDependingProjectGuid.TryGetValue(projectGuid, out List<string>? dependencies))
            {
                dependencyProjectGuids = dependencies;
                return true;
            }

            dependencyProjectGuids = null;
            return false;
        }

        public bool TryGetProjectPathByGuid(string projectGuid, [NotNullWhen(true)] out string? projectPath) => _cachedProjectAbsolutePathsByGuid.TryGetValue(projectGuid, out projectPath);

        /// <summary>
        /// Returns whether the full path of a project path depends on the current directory.
        /// </summary>
        private static bool IsResolvedAgainstCurrentDirectory(string path)
        {
            // Path.IsPathFullyQualified is not available in .NET Standard 2.0
            // in .NET Framework it's provided by package and in .NET it's built-in
#if NETFRAMEWORK || NET
            return path.Length > 0 && !NewPath.IsPathFullyQualified(path);
#else
            return path.Length > 0;
#endif
        }

        /// <summary>
        /// The contents of a project element of a solution configuration.
        /// </summary>
        internal sealed class ProjectEntry(string innerText, string buildProjectInSolution, int elementIndex)
        {
            /// <summary>
            /// Gets the text of the element, such as the project configuration <c>Debug|AnyCPU</c>.
            /// </summary>
            public string InnerText { get; } = innerText;

            /// <summary>
            /// Gets the value of the <see cref="BuildProjectInSolutionAttribute"/> attribute, or an empty string if the element doesn't have it.
            /// </summary>
            public string BuildProjectInSolution { get; } = buildProjectInSolution;

            /// <summary>
            /// Gets the position of the element among the children of the root element.
            /// </summary>
            public int ElementIndex { get; } = elementIndex;
        }
    }
}
