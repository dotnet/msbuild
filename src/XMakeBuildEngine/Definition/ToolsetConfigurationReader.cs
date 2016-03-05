// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>A class used to read the Toolset configuration.</summary>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Text;
using System.Globalization;
using System.Reflection;
using Microsoft.Build.Construction;
using Microsoft.Build.Collections;
using Microsoft.Build.Execution;
using Microsoft.Build.Shared;

using ErrorUtilities = Microsoft.Build.Shared.ErrorUtilities;
using InvalidToolsetDefinitionException = Microsoft.Build.Exceptions.InvalidToolsetDefinitionException;
using Microsoft.Build.Internal;

namespace Microsoft.Build.Evaluation
{
    /// <summary>
    /// Delegate for unit test purposes only
    /// </summary>
    internal delegate Configuration ReadApplicationConfiguration();

    /// <summary>
    /// Class used to read toolset configurations.
    /// </summary>
    internal class ToolsetConfigurationReader : ToolsetReader
    {
        /// <summary>
        /// A section of a toolset configuration
        /// </summary>
        private ToolsetConfigurationSection _configurationSection = null;

        /// <summary>
        /// Delegate used to read application configurations
        /// </summary>
        private ReadApplicationConfiguration _readApplicationConfiguration = null;

        /// <summary>
        /// Flag indicating that an attempt has been made to read the configuration
        /// </summary>
        private bool _configurationReadAttempted = false;

        /// <summary>
        /// Character used to separate search paths specified for MSBuildExtensionsPath* in
        /// the config file
        /// </summary>
        private char _separatorForExtensionsPathSearchPaths = ';';

        /// <summary>
        /// map of MSBuildExtensionsPath property kind to list of fallback search paths, per toolsVersion
        /// </summary>
        private Dictionary<string, Dictionary<MSBuildExtensionsPathReferenceKind, IList<string>>> _kindToPathsCachePerToolsVersion;

        /// <summary>
        /// Default constructor
        /// </summary>
        internal ToolsetConfigurationReader(PropertyDictionary<ProjectPropertyInstance> environmentProperties, PropertyDictionary<ProjectPropertyInstance> globalProperties)
            : this(environmentProperties, globalProperties, new ReadApplicationConfiguration(ToolsetConfigurationReader.ReadApplicationConfiguration))
        {
        }

        /// <summary>
        /// Constructor taking a delegate for unit test purposes only
        /// </summary>
        internal ToolsetConfigurationReader(PropertyDictionary<ProjectPropertyInstance> environmentProperties, PropertyDictionary<ProjectPropertyInstance> globalProperties, ReadApplicationConfiguration readApplicationConfiguration)
            : base(environmentProperties, globalProperties)
        {
            ErrorUtilities.VerifyThrowArgumentNull(readApplicationConfiguration, "readApplicationConfiguration");
            _readApplicationConfiguration = readApplicationConfiguration;
            _kindToPathsCachePerToolsVersion = new Dictionary<string, Dictionary<MSBuildExtensionsPathReferenceKind, IList<string>>>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Returns the list of tools versions
        /// </summary>
        protected override IEnumerable<ToolsetPropertyDefinition> ToolsVersions
        {
            get
            {
                if (ConfigurationSection != null)
                {
                    foreach (ToolsetElement toolset in ConfigurationSection.Toolsets)
                    {
                        ElementLocation location = ElementLocation.Create(toolset.ElementInformation.Source, toolset.ElementInformation.LineNumber, 0);

                        if (toolset.toolsVersion != null && toolset.toolsVersion.Length == 0)
                        {
                            InvalidToolsetDefinitionException.Throw("InvalidToolsetValueInConfigFileValue", location.LocationString);
                        }

                        yield return new ToolsetPropertyDefinition(toolset.toolsVersion, string.Empty, location);
                    }
                }
                else
                {
                    yield break;
                }
            }
        }

        /// <summary>
        /// Returns the default tools version, or null if none was specified
        /// </summary>
        protected override string DefaultToolsVersion
        {
            get
            {
                return (ConfigurationSection == null ? null : ConfigurationSection.Default);
            }
        }

        /// <summary>
        /// Returns the path to find overridetasks, or null if none was specified
        /// </summary>
        protected override string MSBuildOverrideTasksPath
        {
            get
            {
                return (ConfigurationSection == null ? null : ConfigurationSection.MSBuildOverrideTasksPath);
            }
        }

        /// <summary>
        /// DefaultOverrideToolsVersion attribute on msbuildToolsets element, specifying the toolsversion that should be used by 
        /// default to build projects with this version of MSBuild.
        /// </summary>
        protected override string DefaultOverrideToolsVersion
        {
            get
            {
                return (ConfigurationSection == null ? null : ConfigurationSection.DefaultOverrideToolsVersion);
            }
        }

        /// <summary>
        /// Lazy getter for the ToolsetConfigurationSection
        /// Returns null if the section is not present
        /// </summary>
        private ToolsetConfigurationSection ConfigurationSection
        {
            get
            {
                if (null == _configurationSection && !_configurationReadAttempted)
                {
                    try
                    {
                        Configuration configuration = _readApplicationConfiguration();
                        _configurationSection = ToolsetConfigurationReaderHelpers.ReadToolsetConfigurationSection(configuration);
                    }
                    catch (ConfigurationException ex)
                    {
                        // ConfigurationException is obsolete, but we catch it rather than 
                        // ConfigurationErrorsException (which is what we throw below) because it is more 
                        // general and we don't want to miss catching some other derived exception.
                        InvalidToolsetDefinitionException.Throw(ex, "ConfigFileReadError", ElementLocation.Create(ex.Source, ex.Line, 0).LocationString, ex.BareMessage);
                    }
                    finally
                    {
                        _configurationReadAttempted = true;
                    }
                }

                return _configurationSection;
            }
        }

        /// <summary>
        /// Provides an enumerator over property definitions for a specified tools version
        /// </summary>
        protected override IEnumerable<ToolsetPropertyDefinition> GetPropertyDefinitions(string toolsVersion)
        {
            ToolsetElement toolsetElement = ConfigurationSection.Toolsets.GetElement(toolsVersion);

            if (toolsetElement == null)
            {
                yield break;
            }

            foreach (ToolsetElement.PropertyElement propertyElement in toolsetElement.PropertyElements)
            {
                ElementLocation location = ElementLocation.Create(propertyElement.ElementInformation.Source, propertyElement.ElementInformation.LineNumber, 0);

                if (propertyElement.Name != null && propertyElement.Name.Length == 0)
                {
                    InvalidToolsetDefinitionException.Throw("InvalidToolsetValueInConfigFileValue", location.LocationString);
                }

                yield return new ToolsetPropertyDefinition(propertyElement.Name, propertyElement.Value, location);
            }
        }

        /// <summary>
        /// Provides an enumerator over the set of sub-toolset names available to a particular
        /// toolsversion.  MSBuild config files do not currently support sub-toolsets, so 
        /// we return nothing. 
        /// </summary>
        /// <param name="toolsVersion">The tools version.</param>
        /// <returns>An enumeration of the sub-toolsets that belong to that toolsversion.</returns>
        protected override IEnumerable<string> GetSubToolsetVersions(string toolsVersion)
        {
            yield break;
        }

        /// <summary>
        /// Provides an enumerator over property definitions for a specified sub-toolset version 
        /// under a specified toolset version. In the ToolsetConfigurationReader case, breaks 
        /// immediately because we do not currently support sub-toolsets in the configuration file. 
        /// </summary>
        /// <param name="toolsVersion">The tools version.</param>
        /// <param name="subToolsetVersion">The sub-toolset version.</param>
        /// <returns>An enumeration of property definitions.</returns>
        protected override IEnumerable<ToolsetPropertyDefinition> GetSubToolsetPropertyDefinitions(string toolsVersion, string subToolsetVersion)
        {
            yield break;
        }

        /// <summary>
        /// Returns a map of MSBuildExtensionsPath* property names/kind to list of search paths
        /// </summary>
        protected override Dictionary<MSBuildExtensionsPathReferenceKind, IList<string>> GetMSBuildExtensionPathsSearchPathsTable(string toolsVersion, string os)
        {
            Dictionary<MSBuildExtensionsPathReferenceKind, IList<string>> kindToPathsCache;
            var key = toolsVersion + ":" + os;
            if (_kindToPathsCachePerToolsVersion.TryGetValue(key, out kindToPathsCache))
            {
                return kindToPathsCache;
            }

            // Read and populate the map
            kindToPathsCache = new Dictionary<MSBuildExtensionsPathReferenceKind, IList<string>>();
            _kindToPathsCachePerToolsVersion[key] = kindToPathsCache;

            ToolsetElement toolsetElement = ConfigurationSection.Toolsets.GetElement(toolsVersion);
            var propertyCollection = toolsetElement?.AllMSBuildExtensionPathsSearchPaths?.GetElement(os)?.PropertyElements;
            if (propertyCollection == null || propertyCollection.Count == 0)
            {
                return kindToPathsCache;
            }

            var allPaths = new MSBuildExtensionsPathReferenceKind[] {
                                MSBuildExtensionsPathReferenceKind.Default,
                                MSBuildExtensionsPathReferenceKind.Path32,
                                MSBuildExtensionsPathReferenceKind.Path64,
                                MSBuildExtensionsPathReferenceKind.None
                            };

            foreach (MSBuildExtensionsPathReferenceKind kind in allPaths)
            {
                kindToPathsCache[kind] = ComputeDistinctListOfFallbackPathsFor(kind, propertyCollection);
            }

            return kindToPathsCache;
        }

        /// <summary>
        /// Returns a list of the search paths for a given MSBuildExtensionsPathReferenceKind
        /// </summary>
        private List<string> ComputeDistinctListOfFallbackPathsFor(MSBuildExtensionsPathReferenceKind refKind, ToolsetElement.PropertyElementCollection propertyCollection)
        {
            var extnPaths = new List<string>();
            var pathsFromConfig = propertyCollection.GetElement(refKind.StringRepresentation)?.Value;

            //FIXME: handle ; in path on Unix
            if (String.IsNullOrEmpty(pathsFromConfig))
            {
                return extnPaths;
            }

            var pathsTable = new HashSet<string>();
            foreach (var extnPath in pathsFromConfig.Split(new char[]{_separatorForExtensionsPathSearchPaths}, StringSplitOptions.RemoveEmptyEntries))
            {
                if (!pathsTable.Contains(extnPath))
                {
                    pathsTable.Add(extnPath);
                    extnPaths.Add(extnPath);
                }
            }

            return extnPaths;
        }

        /// <summary>
        /// Reads the application configuration file.
        /// NOTE: this is abstracted into a method to support unit testing GetToolsetDataFromConfiguration().
        /// Unit tests wish to avoid reading (nunit.exe) application configuration file.
        /// </summary>
        private static Configuration ReadApplicationConfiguration()
        {
            return ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
        }
    }
}
