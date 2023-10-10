// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiCompatibility.Rules;

namespace Microsoft.DotNet.ApiCompatibility.Mapping
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
        /// Instantiates an assembly set mapper.
        /// </summary>
        /// <param name="ruleRunner">The <see cref="IRuleRunner"/> that compares the assembly set mapper elements.</param>
        /// <param name="settings">The <see cref="IMapperSettings"/> used to compare the assembly set mapper elements.</param>
        /// <param name="rightSetSize">The number of elements in the right set to compare.</param>
        public AssemblySetMapper(IRuleRunner ruleRunner,
            IMapperSettings settings,
            int rightSetSize)
            : base(ruleRunner, settings, rightSetSize) { }

        /// <inheritdoc />
        public IEnumerable<IAssemblyMapper> GetAssemblies()
        {
            if (_assemblies == null)
            {
                _assemblies = new Dictionary<IAssemblySymbol, IAssemblyMapper>(Settings.SymbolEqualityComparer);
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
