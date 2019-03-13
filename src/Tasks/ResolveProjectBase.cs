// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Xml;

using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Utilities;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Base class for ResolveNonMSBuildProjectOutput and AssignProjectConfiguration, since they have
    /// similar architecture
    /// </summary>
    public abstract class ResolveProjectBase : TaskExtension
    {
        #region Properties

        /// <summary>
        /// The list of project references
        /// </summary>
        [Required]
        public ITaskItem[] ProjectReferences
        {
            get
            {
                ErrorUtilities.VerifyThrowArgumentNull(_projectReferences, nameof(ProjectReferences));
                return _projectReferences;
            }
            set => _projectReferences = value;
        }

        private ITaskItem[] _projectReferences;

        // This field stores all the distinct project references by project absolute path
        private readonly HashSet<string> _cachedProjectReferencesByAbsolutePath = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

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

        private const string attributeProject = "Project";

        private const string attributeAbsolutePath = "AbsolutePath";

        #endregion

        #region Methods

        /// <summary>
        /// Checks if a project reference task item contains all the required attributes.
        /// Currently, the only required attribute is project GUID for inside the IDE mode.
        /// </summary>
        internal bool VerifyReferenceAttributes(ITaskItem reference, out string missingAttribute)
        {
            missingAttribute = attributeProject;
            string attrValue = reference.GetMetadata(missingAttribute);

            // missing project GUID? (no longer required, but if it's there then validate it)
            if (attrValue.Length > 0)
            {
                // invalid project GUID format?
                if (!Guid.TryParse(attrValue, out _))
                {
                    return false;
                }
            }

            missingAttribute = null;

            return true;
        }

        /// <summary>
        /// Checks all project reference task items for required attributes
        /// Internal for unit testing
        /// </summary>
        internal bool VerifyProjectReferenceItems(ITaskItem[] references, bool treatAsError)
        {
            bool referencesValid = true;

            foreach (ITaskItem reference in references)
            {
                _cachedProjectReferencesByAbsolutePath.Add(reference.GetMetadata("FullPath")); // metadata is cached and used again later

                if (!VerifyReferenceAttributes(reference, out string missingAttribute))
                {
                    if (treatAsError)
                    {
                        Log.LogErrorWithCodeFromResources("General.MissingOrUnknownProjectReferenceAttribute", reference.ItemSpec, missingAttribute);
                        referencesValid = false;
                    }
                    else
                    {
                        Log.LogWarningWithCodeFromResources("General.MissingOrUnknownProjectReferenceAttribute", reference.ItemSpec, missingAttribute);
                    }
                }
            }

            return referencesValid;
        }

        /// <summary>
        /// Pre-cache individual project elements from the XML string in a hashtable for quicker access.
        /// </summary>
        internal void CacheProjectElementsFromXml(string xmlString)
        {
            XmlDocument doc = null;

            if (!string.IsNullOrEmpty(xmlString))
            {
                doc = new XmlDocument();
                var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore };
                using (XmlReader reader = XmlReader.Create(new StringReader(xmlString), settings))
                {
                    doc.Load(reader);
                }
            }

            // Example:
            //
            //<SolutionConfiguration>
            //  <ProjectConfiguration Project="{786E302A-96CE-43DC-B640-D6B6CC9BF6C0}" AbsolutePath="c:foo\Project1\A.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
            //  <ProjectConfiguration Project="{881C1674-4ECA-451D-85B6-D7C59B7F16FA}" AbsolutePath="c:foo\Project2\B.csproj" BuildProjectInSolution="True">Debug|AnyCPU<ProjectDependency Project="{4A727FF8-65F2-401E-95AD-7C8BBFBE3167}" /></ProjectConfiguration>
            //  <ProjectConfiguration Project="{4A727FF8-65F2-401E-95AD-7C8BBFBE3167}" AbsolutePath="c:foo\Project3\C.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
            //</SolutionConfiguration>
            //
            if (doc?.DocumentElement != null)
            {
                foreach (XmlElement xmlElement in doc.DocumentElement.ChildNodes)
                {
                    string projectGuid = xmlElement.GetAttribute(attributeProject);
                    string projectAbsolutePath = xmlElement.GetAttribute(attributeAbsolutePath);

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

                            if (!_cachedDependencyProjectGuidsByDependingProjectGuid.TryGetValue(projectGuid, out List<string> list))
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

        /// <summary>
        /// Helper method for retrieving whatever was stored in the XML string for the given project
        /// </summary>
        protected string GetProjectItem(ITaskItem projectRef)
        {
            XmlElement projectElement = GetProjectElement(projectRef);
            return projectElement?.InnerText;
        }

        /// <summary>
        /// Helper method for retrieving the XML element for the given project
        /// </summary>
        [SuppressMessage("Microsoft.Design", "CA1059:MembersShouldNotExposeCertainConcreteTypes", MessageId = "System.Xml.XmlNode", Justification = "protected method on a public base class that has previously shipped, so changing this now would be a breaking change.")]
        protected XmlElement GetProjectElement(ITaskItem projectRef)
        {
            string projectGuid = projectRef.GetMetadata(attributeProject);

            if ((_cachedProjectElements.TryGetValue(projectGuid, out XmlElement projectElement)) && (projectElement != null))
            {
                return projectElement;
            }

            // We didn't find the project element by locating a project guid on the P2P reference
            // next we'll try a lookup by the absolute path of the project
            string projectFullPath = projectRef.GetMetadata("FullPath"); // reserved metadata "FullPath" is used at it will cache the value

            if ((_cachedProjectElementsByAbsolutePath.TryGetValue(projectFullPath, out projectElement)) && (projectElement != null))
            {
                return projectElement;
            }

            return null;
        }

        /// <summary>
        /// Helper method for retrieving the extra "project references" passed in the solution blob.
        /// These came from dependencies expressed in the solution file itself.
        /// </summary>
        protected void AddSyntheticProjectReferences(string currentProjectAbsolutePath)
        {
            // Get the guid for this project
            if (!_cachedProjectGuidsByAbsolutePath.TryGetValue(currentProjectAbsolutePath, out string projectGuid))
            {
                // We were passed a blob, but we weren't listed in it. Odd. Return.
                return;
            }

            // Use the guid to look up the dependencies for it
            if (!_cachedDependencyProjectGuidsByDependingProjectGuid.TryGetValue(projectGuid, out List<string> guids))
            {
                // We didn't have dependencies listed in the blob
                return;
            }

            // ProjectReferences is a fixed size array, so start aggregating in a list
            List<ITaskItem> updatedProjectReferenceList = new List<ITaskItem>(_projectReferences);

            foreach (string guid in guids)
            {
                // Get the absolute path of the dependency, using the blob
                if (!_cachedProjectAbsolutePathsByGuid.TryGetValue(guid, out string path))
                {
                    // We had a dependency listed in the blob that wasn't itself in the blob. Odd. Return.
                    continue;
                }

                // If the dependency's already specified as a project reference, ignore it; no sense referencing it twice
                if (!_cachedProjectReferencesByAbsolutePath.Contains(path))
                {
                    _cachedProjectReferencesByAbsolutePath.Add(path);
                    var item = new TaskItem(path);

                    // Unfortunately we've used several different metadata names to trigger
                    // project references to do stuff other than trigger a build
                    item.SetMetadata("ReferenceOutputAssembly", "false");
                    item.SetMetadata("LinkLibraryDependencies", "false");
                    item.SetMetadata("CopyLocal", "false");
                    item.SetMetadata("SkipGetTargetFrameworkProperties", "true");
                    item.SetMetadata("GlobalPropertiesToRemove", "TargetFramework");

                    updatedProjectReferenceList.Add(item);
                }
            }

            // Finally, set our new augmented project references list as the official list;
            // note that this means that the output parameter may include project references that weren't passed in
            _projectReferences = new ITaskItem[updatedProjectReferenceList.Count];
            updatedProjectReferenceList.CopyTo(_projectReferences);
        }

        #endregion
    }
}
