// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Build.Collections;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Shared;

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// A struct representing the uniquely-identifying portion of a BuildRequestConfiguration.  Used for lookups.
    /// </summary>
    [DebuggerDisplay(@"{DebugString()}")]
    internal class ConfigurationMetadata : IEquatable<ConfigurationMetadata>, ITranslatable
    {
        /// <summary>
        /// Constructor over a BuildRequestConfiguration.
        /// </summary>
        public ConfigurationMetadata(BuildRequestConfiguration configuration)
        {
            ErrorUtilities.VerifyThrowArgumentNull(configuration, nameof(configuration));
            _globalProperties = new PropertyDictionary<ProjectPropertyInstance>(configuration.GlobalProperties);
            _projectFullPath = FileUtilities.NormalizePath(configuration.ProjectFullPath);
            _toolsVersion = configuration.ToolsVersion;
        }

        /// <summary>
        /// Constructor over a Project.
        /// </summary>
        public ConfigurationMetadata(Project project)
        {
            ErrorUtilities.VerifyThrowArgumentNull(project, nameof(project));
            _globalProperties = new PropertyDictionary<ProjectPropertyInstance>(project.GlobalProperties.Count);
            foreach (KeyValuePair<string, string> entry in project.GlobalProperties)
            {
                _globalProperties[entry.Key] = ProjectPropertyInstance.Create(entry.Key, entry.Value);
            }

            _toolsVersion = project.ToolsVersion;
            _projectFullPath = FileUtilities.NormalizePath(project.FullPath);
        }

        /// <summary>
        /// Constructor taking individual arguments.
        /// </summary>
        public ConfigurationMetadata(string projectFullPath, PropertyDictionary<ProjectPropertyInstance> globalProperties)
        {
            ErrorUtilities.VerifyThrowArgumentLength(projectFullPath, nameof(projectFullPath));
            ErrorUtilities.VerifyThrowArgumentNull(globalProperties, nameof(globalProperties));

            _projectFullPath = projectFullPath;
            _toolsVersion = MSBuildConstants.CurrentToolsVersion;
            _globalProperties = globalProperties;
        }

        public ConfigurationMetadata(ITranslator translator)
        {
            Translate(translator);
        }

        private string _projectFullPath;

        /// <summary>
        /// The full path to the project to build.
        /// </summary>
        public string ProjectFullPath => _projectFullPath;

        private string _toolsVersion;

        /// <summary>
        /// The tools version specified for the configuration.
        /// Always specified.
        /// May have originated from a /tv switch, or an MSBuild task,
        /// or a Project tag, or the default.
        /// </summary>
        public string ToolsVersion => _toolsVersion;

        private PropertyDictionary<ProjectPropertyInstance> _globalProperties;

        /// <summary>
        /// The set of global properties which should be used when building this project.
        /// </summary>
        public PropertyDictionary<ProjectPropertyInstance> GlobalProperties => _globalProperties;

        /// <summary>
        /// This override is used to provide a hash code for storage in dictionaries and the like.
        /// </summary>
        /// <remarks>
        /// If two objects are Equal, they must have the same hash code, for dictionaries to work correctly.
        /// Two configurations are Equal if their global properties are equivalent, not necessary reference equals.
        /// So only include filename and tools version in the hashcode.
        /// </remarks>
        /// <returns>A hash code</returns>
        public override int GetHashCode()
        {
            return StringComparer.OrdinalIgnoreCase.GetHashCode(ProjectFullPath) ^ StringComparer.OrdinalIgnoreCase.GetHashCode(ToolsVersion);
        }

        public void Translate(ITranslator translator)
        {
            translator.Translate(ref _projectFullPath);
            translator.Translate(ref _toolsVersion);
            translator.TranslateDictionary(ref _globalProperties, ProjectPropertyInstance.FactoryForDeserialization);
        }

        public static ConfigurationMetadata FactoryForDeserialization(ITranslator translator)
        {
            return new ConfigurationMetadata(translator);
        }

        /// <summary>
        /// Determines object equality
        /// </summary>
        /// <param name="obj">The object to compare with</param>
        /// <returns>True if they contain the same data, false otherwise</returns>
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(obj, null))
            {
                return false;
            }

            if (GetType() != obj.GetType())
            {
                return false;
            }

            return InternalEquals((ConfigurationMetadata)obj);
        }

        #region IEquatable<ConfigurationMetadata> Members

        /// <summary>
        /// Equality of the configuration is the product of the equality of its members.
        /// </summary>
        /// <param name="other">The other configuration to which we will compare ourselves.</param>
        /// <returns>True if equal, false otherwise.</returns>
        public bool Equals(ConfigurationMetadata other)
        {
            if (ReferenceEquals(other, null))
            {
                return false;
            }

            return InternalEquals(other);
        }

        #endregion

        /// <summary>
        /// Compares this object with another for equality
        /// </summary>
        /// <param name="other">The object with which to compare this one.</param>
        /// <returns>True if the objects contain the same data, false otherwise.</returns>
        private bool InternalEquals(ConfigurationMetadata other)
        {
            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return ProjectFullPath.Equals(other.ProjectFullPath, StringComparison.OrdinalIgnoreCase) &&
                   ToolsVersion.Equals(other.ToolsVersion, StringComparison.OrdinalIgnoreCase) &&
                   GlobalProperties.Equals(other.GlobalProperties);
        }

        private string DebugString()
        {
            var truncatedProjectFile = FileUtilities.TruncatePathToTrailingSegments(ProjectFullPath, 2);

            return
                $"{truncatedProjectFile}, #GlobalProps={GlobalProperties.Count}";
        }
    }
}
