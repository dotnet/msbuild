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
    public class AssemblySetMapper : ElementMapper<IEnumerable<ElementContainer<IAssemblySymbol>>>, IAssemblySetMapper
    {
        private Dictionary<IAssemblySymbol, IAssemblyMapper>? _assemblies;

        /// <inheritdoc />
        public int AssemblyCount => _assemblies != null ? _assemblies.Count : 0;

        /// <summary>
        /// Instantiates an object with the provided <see cref="ComparingSettings"/>.
        /// </summary>
        /// <param name="settings">The settings used to diff the elements in the mapper.</param>
        /// <param name="rightSetSize">The number of elements in the right set to compare.</param>
        public AssemblySetMapper(IRuleRunner ruleRunner,
            MapperSettings settings,
            int rightSetSize)
            : base(ruleRunner, settings, rightSetSize) { }

        /// <inheritdoc />
        public IEnumerable<IAssemblyMapper> GetAssemblies()
        {
            if (_assemblies == null)
            {
                _assemblies = new Dictionary<IAssemblySymbol, IAssemblyMapper>(Settings.EqualityComparer);
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
                        if (!_assemblies.TryGetValue(assemblyContainer.Element, out IAssemblyMapper? mapper))
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
