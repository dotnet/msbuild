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
        public AssemblySetMapper(ComparingSettings settings) : base(settings) { }

        /// <summary>
        /// Gets the assembly mappers built from the provided lists of <see cref="IAssemblySymbol"/>.
        /// <returns>The list of <see cref="AssemblyMapper"/> representing the underlying assemblies.</returns>
        public IEnumerable<AssemblyMapper> GetAssemblies()
        {
            if (_assemblies == null)
            {
                _assemblies = new Dictionary<IAssemblySymbol, AssemblyMapper>(Settings.EqualityComparer);

                if (Left != null)
                {
                    AddOrCreateMappers(Left, 0);
                }

                if (Right != null)
                {
                    AddOrCreateMappers(Right, 1);
                }

                void AddOrCreateMappers(IEnumerable<IAssemblySymbol> elements, int index)
                {
                    foreach (IAssemblySymbol assembly in elements)
                    {
                        if (!_assemblies.TryGetValue(assembly, out AssemblyMapper mapper))
                        {
                            mapper = new AssemblyMapper(Settings);
                            _assemblies.Add(assembly, mapper);
                        }
                        mapper.AddElement(assembly, index);
                    }
                }
            }

            return _assemblies.Values;
        }
    }
}
