// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Xml;

using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Utilities;

#nullable disable

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
                ArgumentNullException.ThrowIfNull(_projectReferences, nameof(ProjectReferences));
                return _projectReferences;
            }
            set => _projectReferences = value;
        }

        private ITaskItem[] _projectReferences;

        // This field stores all the distinct project references by project absolute path
        private readonly HashSet<string> _cachedProjectReferencesByAbsolutePath = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private SolutionConfiguration _solutionConfiguration = SolutionConfiguration.Empty;

        // The XML that _solutionConfiguration was parsed from, and this task's own copy of its project elements,
        // which is only parsed when a derived task asks for an element.
        private string _solutionConfigurationXml;
        private XmlElement[] _projectElements;

        private const string attributeProject = "Project";

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
            _solutionConfiguration = SolutionConfigurationCache.GetSolutionConfiguration(BuildEngine as IBuildEngine4, xmlString);
            _solutionConfigurationXml = xmlString;
            _projectElements = null;
        }

        /// <summary>
        /// Helper method for retrieving whatever was stored in the XML string for the given project
        /// </summary>
        protected string GetProjectItem(ITaskItem projectRef)
        {
            // A derived task may have changed the elements it got from GetProjectElement.
            if (_projectElements is not null)
            {
                return GetProjectElement(projectRef)?.InnerText;
            }

            return GetProjectEntry(projectRef)?.InnerText;
        }

        /// <summary>
        /// Helper method for retrieving the XML element for the given project
        /// </summary>
        [SuppressMessage("Microsoft.Design", "CA1059:MembersShouldNotExposeCertainConcreteTypes", MessageId = "System.Xml.XmlNode", Justification = "protected method on a public base class that has previously shipped, so changing this now would be a breaking change.")]
        protected XmlElement GetProjectElement(ITaskItem projectRef)
        {
            SolutionConfiguration.ProjectEntry projectEntry = GetProjectEntry(projectRef);

            if (projectEntry is null)
            {
                return null;
            }

            // The solution configuration can be shared with other tasks, so it doesn't keep the mutable XML elements.
            // Parse this task's own copy of them when they're first requested.
            if (_projectElements is null)
            {
                XmlNodeList projectElements = SolutionConfiguration.GetProjectConfigurations(_solutionConfigurationXml);
                _projectElements = new XmlElement[projectElements.Count];
                int elementIndex = 0;

                foreach (XmlElement projectElement in projectElements)
                {
                    _projectElements[elementIndex++] = projectElement;
                }
            }

            return _projectElements[projectEntry.ElementIndex];
        }

        /// <summary>
        /// Helper method for retrieving the solution configuration entry for the given project
        /// </summary>
        internal SolutionConfiguration.ProjectEntry GetProjectEntry(ITaskItem projectRef)
        {
            string projectGuid = projectRef.GetMetadata(attributeProject);

            if (_solutionConfiguration.TryGetProjectByGuid(projectGuid, out SolutionConfiguration.ProjectEntry projectEntry))
            {
                return projectEntry;
            }

            // We didn't find the project element by locating a project guid on the P2P reference
            // next we'll try a lookup by the absolute path of the project
            string projectFullPath = projectRef.GetMetadata("FullPath"); // reserved metadata "FullPath" is used at it will cache the value

            if (_solutionConfiguration.TryGetProjectByAbsolutePath(projectFullPath, out projectEntry))
            {
                return projectEntry;
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
            if (!_solutionConfiguration.TryGetProjectGuidByAbsolutePath(currentProjectAbsolutePath, out string projectGuid))
            {
                // We were passed a blob, but we weren't listed in it. Odd. Return.
                return;
            }

            // Use the guid to look up the dependencies for it
            if (!_solutionConfiguration.TryGetProjectDependencies(projectGuid, out IReadOnlyList<string> guids))
            {
                // We didn't have dependencies listed in the blob
                return;
            }

            // ProjectReferences is a fixed size array, so start aggregating in a list
            List<ITaskItem> updatedProjectReferenceList = new List<ITaskItem>(_projectReferences);

            foreach (string guid in guids)
            {
                // Get the absolute path of the dependency, using the blob
                if (!_solutionConfiguration.TryGetProjectPathByGuid(guid, out string path))
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

        /// <summary>
        /// Shares parsed solution configurations between the tasks of a build.
        /// </summary>
        /// <remarks>
        /// Every project of a solution build passes the same solution configuration XML, which lists every project in the
        /// solution, so parsing it in every project makes the total cost grow with the square of the solution size.
        /// </remarks>
        private sealed class SolutionConfigurationCache
        {
            private const string RegisteredTaskObjectKey = "Microsoft.Build.Tasks.ResolveProjectBase.SolutionConfigurationCache";

            // A build normally passes a single solution configuration, so a few entries are enough.
            private const int MaxEntries = 8;

            private readonly LockType _lock = new();

            // The most recently added entry is first. Comparing the XML is cheaper than hashing it for so few entries.
            private volatile (string Xml, SolutionConfiguration Configuration)[] _entries = [];

            internal static SolutionConfiguration GetSolutionConfiguration(IBuildEngine4 buildEngine, string xmlString)
            {
                if (string.IsNullOrEmpty(xmlString))
                {
                    return SolutionConfiguration.Empty;
                }

                if (buildEngine is null)
                {
                    return new SolutionConfiguration(xmlString);
                }

                if (buildEngine.GetRegisteredTaskObject(RegisteredTaskObjectKey, RegisteredTaskObjectLifetime.Build) is not SolutionConfigurationCache cache)
                {
                    buildEngine.RegisterTaskObject(RegisteredTaskObjectKey, new SolutionConfigurationCache(), RegisteredTaskObjectLifetime.Build, allowEarlyCollection: true);

                    // Another task may have registered its cache first.
                    cache = buildEngine.GetRegisteredTaskObject(RegisteredTaskObjectKey, RegisteredTaskObjectLifetime.Build) as SolutionConfigurationCache;

                    if (cache is null)
                    {
                        return new SolutionConfiguration(xmlString);
                    }
                }

                return cache.GetOrAdd(xmlString);
            }

            private SolutionConfiguration GetOrAdd(string xmlString)
            {
                SolutionConfiguration configuration = Find(_entries, xmlString);

                if (configuration is not null)
                {
                    return configuration;
                }

                configuration = new SolutionConfiguration(xmlString);

                // Relative paths are resolved against the current directory, which can differ between tasks.
                if (configuration.DependsOnCurrentDirectory)
                {
                    return configuration;
                }

                lock (_lock)
                {
                    (string Xml, SolutionConfiguration Configuration)[] entries = _entries;
                    SolutionConfiguration existingConfiguration = Find(entries, xmlString);

                    if (existingConfiguration is not null)
                    {
                        return existingConfiguration;
                    }

                    var updatedEntries = new (string Xml, SolutionConfiguration Configuration)[Math.Min(entries.Length + 1, MaxEntries)];
                    updatedEntries[0] = (xmlString, configuration);
                    Array.Copy(entries, 0, updatedEntries, 1, updatedEntries.Length - 1);
                    _entries = updatedEntries;
                }

                return configuration;
            }

            private static SolutionConfiguration Find((string Xml, SolutionConfiguration Configuration)[] entries, string xmlString)
            {
                foreach ((string xml, SolutionConfiguration configuration) in entries)
                {
                    if (string.Equals(xml, xmlString, StringComparison.Ordinal))
                    {
                        return configuration;
                    }
                }

                return null;
            }
        }
    }
}
