// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiCompatibility.Rules;

namespace Microsoft.DotNet.ApiCompatibility.Abstractions
{
    /// <summary>
    /// Object that represents a mapping between two lists of <see cref="IAssemblySymbol"/>.
    /// </summary>
    public class AssemblySetMapper : ElementMapper<IEnumerable<ElementContainer<IAssemblySymbol>>>
    {
        private Dictionary<IAssemblySymbol, AssemblyMapper>? _assemblies;

        /// <summary>
        /// The number of assemblies mapped.
        /// </summary>
        public int AssemblyCount => _assemblies != null ? _assemblies.Count : 0;

        /// <summary>
        /// Instantiates an object with the provided <see cref="ComparingSettings"/>.
        /// </summary>
        /// <param name="settings">The settings used to diff the elements in the mapper.</param>
        /// <param name="rightSetSize">The number of elements in the right set to compare.</param>
        public AssemblySetMapper(IRuleRunner ruleRunner,
            MapperSettings settings = default,
            int rightSetSize = 1)
            : base(ruleRunner, settings, rightSetSize) { }

        /// <summary>
        /// Gets the assembly mappers built from the provided lists of <see cref="IAssemblySymbol"/>.
        /// <returns>The list of <see cref="AssemblyMapper"/> representing the underlying assemblies.</returns>
        public IEnumerable<AssemblyMapper> GetAssemblies()
        {
            if (_assemblies == null)
            {
                _assemblies = new Dictionary<IAssemblySymbol, AssemblyMapper>(Settings.EqualityComparer);
                AddOrCreateMappers(Left, ElementSide.Left);

                for (int i = 0; i < Right.Length; i++)
                {
                    AddOrCreateMappers(Right[i], ElementSide.Right, i);
                }

                void AddOrCreateMappers(IEnumerable<ElementContainer<IAssemblySymbol>>? assemblyContainers, ElementSide side, int setIndex = 0)
                {
                    // Silently return if the element hasn't been added yet.
                    if (assemblyContainers == null)
                    {
                        return;
                    }

                    foreach (ElementContainer<IAssemblySymbol> assemblyContainer in assemblyContainers)
                    {
                        if (!_assemblies.TryGetValue(assemblyContainer.Element, out AssemblyMapper? mapper))
                        {
                            mapper = new AssemblyMapper(RuleRunner, Settings, Right.Length, this);
                            _assemblies.Add(assemblyContainer.Element, mapper);
                        }

                        mapper.AddElement(assemblyContainer, side, setIndex);
                    }
                }
            }

            return _assemblies.Values;
        }
    }
}
