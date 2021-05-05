// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using System.Collections.Generic;

namespace Microsoft.DotNet.ApiCompatibility.Abstractions
{
    /// <summary>
    /// Object that represents a mapping between two lists of <see cref="IAssemblySymbol"/>.
    /// </summary>
    public class AssemblySetMapper : ElementMapper<IEnumerable<IAssemblySymbol>>
    {
        private Dictionary<IAssemblySymbol, AssemblyMapper> _assemblies;

        /// <summary>
        /// Instantiates an object with the provided <see cref="ComparingSettings"/>.
        /// </summary>
        /// <param name="settings">The settings used to diff the elements in the mapper.</param>
        /// <param name="rightSetSize">The number of elements in the right set to compare.</param>
        public AssemblySetMapper(ComparingSettings settings, int rightSetSize = 1)
            : base(settings, rightSetSize) { }

        /// <summary>
        /// Gets the assembly mappers built from the provided lists of <see cref="IAssemblySymbol"/>.
        /// <returns>The list of <see cref="AssemblyMapper"/> representing the underlying assemblies.</returns>
        public IEnumerable<AssemblyMapper> GetAssemblies()
        {
            if (_assemblies == null)
            {
                _assemblies = new Dictionary<IAssemblySymbol, AssemblyMapper>(Settings.EqualityComparer);
                AddOrCreateMappers(Left, ElementSide.Left);

                if (Right.Length == 1)
                {
                    AddOrCreateMappers(Right[0], ElementSide.Right);
                }
                else
                {
                    for (int i = 0; i < Right.Length; i++)
                    {
                        AddOrCreateMappers(Right[i], ElementSide.Right, i);
                    }
                }

                void AddOrCreateMappers(IEnumerable<IAssemblySymbol> symbols, ElementSide side, int setIndex = 0)
                {
                    if (symbols == null)
                    {
                        return;
                    }

                    foreach (IAssemblySymbol assembly in symbols)
                    {
                        if (!_assemblies.TryGetValue(assembly, out AssemblyMapper mapper))
                        {
                            mapper = new AssemblyMapper(Settings, Right.Length);
                            _assemblies.Add(assembly, mapper);
                        }

                        mapper.AddElement(assembly, side, setIndex);
                    }
                }
            }

            return _assemblies.Values;
        }
    }
}
