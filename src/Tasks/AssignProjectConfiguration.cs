// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Xml;

using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Tasks
{
    public class AssignProjectConfiguration : ResolveProjectBase
    {
        #region Properties

        /// <summary>
        /// A special XML string containing a project configuration for each project - we need to simply 
        /// match the projects and assign the appropriate configuration names to them
        /// </summary>
        public string SolutionConfigurationContents { get; set; }

        /// <summary>
        /// Whether to use the solution dependency information passed in the solution blob
        /// to add synthetic project references for the purposes of build ordering
        /// </summary>
        public bool AddSyntheticProjectReferencesForSolutionDependencies { get; set; }

        /// <summary>
        /// String containing a semicolon-delimited list of mappings from the platform names used
        /// by most VS types to those used by .vcxprojs.  
        /// </summary>
        /// <remarks>
        /// E.g.  "AnyCPU=Win32"
        /// </remarks>
        [SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "Vcx", Justification = "Public API that has already shipped; VCX is a recognizable short form for .vcxproj")]
        public string DefaultToVcxPlatformMapping
        {
            get => _defaultToVcxPlatformMapping ??
                   (_defaultToVcxPlatformMapping = "AnyCPU=Win32;X86=Win32;X64=X64;Itanium=Itanium");

            set
            {
                _defaultToVcxPlatformMapping = value;
                if (_defaultToVcxPlatformMapping != null && _defaultToVcxPlatformMapping.Length == 0)
                {
                    _defaultToVcxPlatformMapping = null;
                }
            }
        }

        private string _defaultToVcxPlatformMapping;

        /// <summary>
        /// String containing a semicolon-delimited list of mappings from .vcxproj platform names
        /// to the platform names use by most other VS project types.  
        /// </summary>
        /// <remarks>
        /// E.g.  "Win32=AnyCPU"
        /// </remarks>
        [SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "Vcx", Justification = "Public API that has already shipped; VCX is a recognizable short form for .vcxproj")]
        public string VcxToDefaultPlatformMapping
        {
            get
            {
                if (_vcxToDefaultPlatformMapping == null)
                {
                    if (String.Equals("Library", OutputType, StringComparison.OrdinalIgnoreCase))
                    {
                        _vcxToDefaultPlatformMapping = "Win32=AnyCPU;X64=X64;Itanium=Itanium";
                    }
                    else
                    {
                        _vcxToDefaultPlatformMapping = "Win32=X86;X64=X64;Itanium=Itanium";
                    }
                }

                return _vcxToDefaultPlatformMapping;
            }

            set
            {
                _vcxToDefaultPlatformMapping = value;

                if (_vcxToDefaultPlatformMapping != null && _vcxToDefaultPlatformMapping.Length == 0)
                {
                    _vcxToDefaultPlatformMapping = null;
                }
            }
        }

        private string _vcxToDefaultPlatformMapping;

        /// <summary>
        /// The current project's full path
        /// </summary>
        public string CurrentProject { get; set; }

        /// <summary>
        /// The current project's platform.
        /// </summary>
        public string CurrentProjectConfiguration { get; set; }

        /// <summary>
        /// The current project's platform.
        /// </summary>
        public string CurrentProjectPlatform { get; set; }

        /// <summary>
        /// Should we build references even if they were disabled in the project configuration
        /// </summary>
        public bool OnlyReferenceAndBuildProjectsEnabledInSolutionConfiguration { get; set; } = false;

        // Whether to set the project reference's GlobalPropertiesToRemove metadata to contain
        // Configuration and Platform. 

        /// <summary>
        /// Whether to set the GlobalPropertiesToRemove metadata on the project reference such that
        /// on an MSBuild call, the Configuration and Platform metadata will be unset, allowing the 
        /// child project to build in its default configuration / platform. 
        /// </summary>
        public bool ShouldUnsetParentConfigurationAndPlatform { get; set; } = false;

        /// <summary>
        /// The output type for the project
        /// </summary>
        public string OutputType { get; set; }

        /// <summary>
        /// True if we should use the default mappings to resolve the configuration/platform
        /// of the passed in project references, false otherwise.
        /// </summary>
        public bool ResolveConfigurationPlatformUsingMappings { get; set; }

        /// <summary>
        /// The list of resolved reference paths (preserving the original project reference attributes)
        /// </summary>
        [Output]
        public ITaskItem[] AssignedProjects { get; set; }

        /// <summary>
        /// The list of project reference items that could not be resolved using the pre-resolved list of outputs.
        /// Since VS only pre-resolves non-MSBuild projects, this means that project references in this list
        /// are in the MSBuild format.
        /// </summary>
        [Output]
        public ITaskItem[] UnassignedProjects { get; set; }

        private const string attrFullConfiguration = "FullConfiguration";
        private const string buildReferenceMetadataName = "BuildReference";
        private const string referenceOutputAssemblyMetadataName = "ReferenceOutputAssembly";
        private const string buildProjectInSolutionAttribute = "BuildProjectInSolution";
        private const string attrConfiguration = "Configuration";
        private const string attrPlatform = "Platform";
        private const string attrSetConfiguration = "SetConfiguration";
        private const string attrSetPlatform = "SetPlatform";

        private static readonly char[] s_configPlatformSeparator = { '|' };

        private IDictionary<string, string> _vcxToDefaultMap;
        private IDictionary<string, string> _defaultToVcxMap;
        private bool _mappingsPopulated;

        #endregion

        #region ITask Members

        /// <summary>
        /// Main task method
        /// </summary>
        /// <returns></returns>
        public override bool Execute()
        {
            try
            {
                if (!VerifyProjectReferenceItems(ProjectReferences, true /* treat problems as errors */))
                {
                    return false;
                }

                var resolvedReferences = new List<ITaskItem>(ProjectReferences.GetLength(0));
                var unresolvedReferences = new List<ITaskItem>(ProjectReferences.GetLength(0));

                if (!String.IsNullOrEmpty(SolutionConfigurationContents))
                {
                    CacheProjectElementsFromXml(SolutionConfigurationContents);
                }

                if (AddSyntheticProjectReferencesForSolutionDependencies)
                {
                    // The solution may have had project to project dependencies expressed in it, which were passed in with the blob.
                    // Add those to the list of project references as if they were regular project references.
                    AddSyntheticProjectReferences(CurrentProject);
                }

                foreach (ITaskItem projectRef in ProjectReferences)
                {
                    bool resolveSuccess = ResolveProject(projectRef, out ITaskItem resolvedReference);

                    if (resolveSuccess)
                    {
                        resolvedReferences.Add(resolvedReference);

                        Log.LogMessageFromResources(MessageImportance.Low, "AssignProjectConfiguration.ProjectConfigurationResolutionSuccess", projectRef.ItemSpec, resolvedReference.GetMetadata(attrFullConfiguration));
                    }
                    else
                    {
                        // If the reference was unresolved, we want to undefine the Configuration and Platform 
                        // global properties, so that the project will build using its default Configuration and
                        // Platform rather than that of its parent. 
                        if (ShouldUnsetParentConfigurationAndPlatform)
                        {
                            string globalPropertiesToRemove = projectRef.GetMetadata("GlobalPropertiesToRemove");

                            if (!String.IsNullOrEmpty(globalPropertiesToRemove))
                            {
                                globalPropertiesToRemove += ";";
                            }

                            if (projectRef is ITaskItem2 item2)
                            {
                                item2.SetMetadataValueLiteral("GlobalPropertiesToRemove", globalPropertiesToRemove + "Configuration;Platform");
                            }
                            else
                            {
                                projectRef.SetMetadata("GlobalPropertiesToRemove", EscapingUtilities.Escape(globalPropertiesToRemove + "Configuration;Platform"));
                            }
                        }

                        unresolvedReferences.Add(projectRef);

                        // This is not an error - we pass unresolved references to UnresolvedProjectReferences for further
                        // processing in the .targets file. This means this project was not checked for building in the
                        // active solution configuration.
                        Log.LogMessageFromResources(MessageImportance.Low, "AssignProjectConfiguration.ProjectConfigurationUnresolved", projectRef.ItemSpec);
                    }
                }

                AssignedProjects = resolvedReferences.ToArray();
                UnassignedProjects = unresolvedReferences.ToArray();
            }
            catch (XmlException e)
            {
                Log.LogErrorWithCodeFromResources("General.ErrorExecutingTask", GetType().Name, e.Message);
                return false;
            }

            return true;
        }

        #endregion

        #region Methods
        
        /// <summary>
        /// Given a project reference task item and an XML document containing project configurations, 
        /// find the configuration for that task item.
        /// </summary>
        /// <returns>true if resolved successfully</returns>
        internal bool ResolveProject(ITaskItem projectRef, out ITaskItem resolvedProjectWithConfiguration)
        {
            XmlElement projectConfigurationElement = null;
            string projectConfiguration = null;

            if (!String.IsNullOrEmpty(SolutionConfigurationContents))
            {
                projectConfigurationElement = GetProjectElement(projectRef);

                if (projectConfigurationElement != null)
                {
                    projectConfiguration = projectConfigurationElement.InnerText;
                }
            }

            if (projectConfiguration == null && ResolveConfigurationPlatformUsingMappings)
            {
                if (!_mappingsPopulated)
                {
                    SetupDefaultPlatformMappings();
                }

                string transformedPlatform;

                if (String.Equals(projectRef.GetMetadata("Extension"), ".vcxproj", StringComparison.OrdinalIgnoreCase))
                {
                    if (_defaultToVcxMap.TryGetValue(CurrentProjectPlatform, out transformedPlatform))
                    {
                        projectConfiguration = CurrentProjectConfiguration + s_configPlatformSeparator[0] + transformedPlatform;
                    }
                }
                else
                {
                    if (_vcxToDefaultMap.TryGetValue(CurrentProjectPlatform, out transformedPlatform))
                    {
                        projectConfiguration = CurrentProjectConfiguration + s_configPlatformSeparator[0] + transformedPlatform;
                    }
                }
            }

            SetBuildInProjectAndReferenceOutputAssemblyMetadata(OnlyReferenceAndBuildProjectsEnabledInSolutionConfiguration, projectRef, projectConfigurationElement);

            if (!string.IsNullOrEmpty(projectConfiguration))
            {
                resolvedProjectWithConfiguration = projectRef;
                resolvedProjectWithConfiguration.SetMetadata(attrFullConfiguration, projectConfiguration);

                string[] configurationPlatformParts = projectConfiguration.Split(s_configPlatformSeparator);
                resolvedProjectWithConfiguration.SetMetadata(attrSetConfiguration, "Configuration=" + configurationPlatformParts[0]);
                resolvedProjectWithConfiguration.SetMetadata(attrConfiguration, configurationPlatformParts[0]);

                if (configurationPlatformParts.Length > 1)
                {
                    resolvedProjectWithConfiguration.SetMetadata(attrSetPlatform, "Platform=" + configurationPlatformParts[1]);
                    resolvedProjectWithConfiguration.SetMetadata(attrPlatform, configurationPlatformParts[1]);
                }
                else
                {
                    resolvedProjectWithConfiguration.SetMetadata(attrSetPlatform, "Platform=");
                }

                return true;
            }

            resolvedProjectWithConfiguration = null;
            return false;
        }

        /// <summary>
        /// Given the project configuration blob and the project reference item, set the BuildInProject metadata and the ReferenceOutputAssembly metadata
        /// based on the contents of the blob.
        /// </summary>
        internal static void SetBuildInProjectAndReferenceOutputAssemblyMetadata(bool onlyReferenceAndBuildProjectsEnabledInSolutionConfiguration, ITaskItem resolvedProjectWithConfiguration, XmlElement projectConfigurationElement)
        {
            if (projectConfigurationElement != null && resolvedProjectWithConfiguration != null && onlyReferenceAndBuildProjectsEnabledInSolutionConfiguration)
            {
                // The value of the specified attribute. An empty string is returned if a matching attribute is not found or if the attribute does not have a specified or default value. 
                string buildProjectInSolution = projectConfigurationElement.GetAttribute(buildProjectInSolutionAttribute);

                // We could not parse out what was in the attribute, act as if it was not set in the first place. 
                if (bool.TryParse(buildProjectInSolution, out bool buildProject))
                {
                    // If we do not want to build references disabled in the solution configuration blob   
                    // and the solution configuration indicates the build for this project is disabled 
                    // We need to set the BuildReferenceMetadata to false and the ReferenceOutputAssembly to false (if they are not already set to anything)
                    if (!buildProject)
                    {
                        string buildReferenceMetadata = resolvedProjectWithConfiguration.GetMetadata(buildReferenceMetadataName);
                        string referenceOutputAssemblyMetadata = resolvedProjectWithConfiguration.GetMetadata(referenceOutputAssemblyMetadataName);

                        if (buildReferenceMetadata.Length == 0)
                        {
                            resolvedProjectWithConfiguration.SetMetadata(buildReferenceMetadataName, "false");
                        }

                        if (referenceOutputAssemblyMetadata.Length == 0)
                        {
                            resolvedProjectWithConfiguration.SetMetadata(referenceOutputAssemblyMetadataName, "false");
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Given the contents of VcxToDefaultPlatformMapping and DefaultToVcxPlatformMapping properties, 
        /// fill out the maps that will be used to translate between the two.  
        /// </summary>
        private void SetupDefaultPlatformMappings()
        {
            _vcxToDefaultMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _defaultToVcxMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (!String.IsNullOrEmpty(VcxToDefaultPlatformMapping))
            {
                PopulateMappingDictionary(_vcxToDefaultMap, VcxToDefaultPlatformMapping);
            }

            if (!String.IsNullOrEmpty(DefaultToVcxPlatformMapping))
            {
                PopulateMappingDictionary(_defaultToVcxMap, DefaultToVcxPlatformMapping);
            }

            _mappingsPopulated = true;
        }

        /// <summary>
        /// Given a dictionary to populate and a string of the format "a=b;c=d", populate the 
        /// dictionary with the given pairs.
        /// </summary>
        private void PopulateMappingDictionary(IDictionary<string, string> map, string mappingList)
        {
            string[] mappings = mappingList.Split(';');

            foreach (string mapping in mappings)
            {
                string[] platforms = mapping.Split('=');

                if (platforms.Length != 2)
                {
                    Log.LogErrorFromResources("AssignProjectConfiguration.IllegalMappingString", mapping.Trim(), mappingList);
                }
                else
                {
                    map.Add(platforms[0], platforms[1]);
                }
            }
        }

        #endregion
    }
}
