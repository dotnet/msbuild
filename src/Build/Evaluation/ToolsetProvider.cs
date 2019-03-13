// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.Build.Collections;
using Microsoft.Build.Execution;
using Microsoft.Build.Shared;
using Microsoft.Build.BackEnd;

namespace Microsoft.Build.Evaluation
{
    /// <summary>
    /// Class which provides access to toolsets.
    /// </summary>
    internal class ToolsetProvider : IToolsetProvider, ITranslatable
    {
        /// <summary>
        /// A mapping of tools versions to Toolsets, which contain the public Toolsets.
        /// This is the collection we use internally.
        /// </summary>
        private Dictionary<string, Toolset> _toolsets;

        /// <summary>
        /// Constructor which will load toolsets from the specified locations.
        /// </summary>
        public ToolsetProvider(string defaultToolsVersion, PropertyDictionary<ProjectPropertyInstance> environmentProperties, PropertyDictionary<ProjectPropertyInstance> globalProperties, ToolsetDefinitionLocations toolsetDefinitionLocations)
        {
            InitializeToolsetCollection(environmentProperties, globalProperties, toolsetDefinitionLocations);
        }

        /// <summary>
        /// Constructor from an existing collection of toolsets.
        /// </summary>
        public ToolsetProvider(IEnumerable<Toolset> toolsets)
        {
            _toolsets = new Dictionary<string, Toolset>(StringComparer.OrdinalIgnoreCase);
            foreach (Toolset toolset in toolsets)
            {
                _toolsets[toolset.ToolsVersion] = toolset;
            }
        }

        /// <summary>
        /// Private constructor for deserialization
        /// </summary>
        private ToolsetProvider(ITranslator translator)
        {
            ((ITranslatable)this).Translate(translator);
        }

        #region IToolsetProvider Members

        /// <summary>
        /// Retrieves the toolsets.
        /// </summary>
        /// <comments>
        /// ValueCollection is already read-only. 
        /// </comments>
        public ICollection<Toolset> Toolsets => _toolsets.Values;

        /// <summary>
        /// Gets the specified toolset.
        /// </summary>
        public Toolset GetToolset(string toolsVersion)
        {
            ErrorUtilities.VerifyThrowArgumentLength(toolsVersion, "toolsVersion");
            _toolsets.TryGetValue(toolsVersion, out var toolset);

            return toolset;
        }

        #endregion

        #region INodePacketTranslatable Members

        /// <summary>
        /// Translates to and from binary form.
        /// </summary>
        void ITranslatable.Translate(ITranslator translator)
        {
            translator.TranslateDictionary(ref _toolsets, StringComparer.OrdinalIgnoreCase, Toolset.FactoryForDeserialization);
        }

        /// <summary>
        /// Factory for deserialization.
        /// </summary>
        internal static ToolsetProvider FactoryForDeserialization(ITranslator translator)
        {
            ToolsetProvider provider = new ToolsetProvider(translator);
            return provider;
        }

        #endregion

        /// <summary>
        /// Populate Toolsets with a dictionary of (toolset version, Toolset) 
        /// using information from the registry and config file, if any.  
        /// </summary>
        private void InitializeToolsetCollection(PropertyDictionary<ProjectPropertyInstance> environmentProperties, PropertyDictionary<ProjectPropertyInstance> globalProperties, ToolsetDefinitionLocations toolsetDefinitionLocations)
        {
            _toolsets = new Dictionary<string, Toolset>(StringComparer.OrdinalIgnoreCase);

            ToolsetReader.ReadAllToolsets(_toolsets, environmentProperties, globalProperties, toolsetDefinitionLocations);
        }
    }
}
