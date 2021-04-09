using Microsoft.CodeAnalysis;
using System.Collections.Generic;
// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.DotNet.ApiCompatibility.Abstractions
{
    public class AssemblySetMapper : ElementMapper<IEnumerable<IAssemblySymbol>>
    {
        private Dictionary<IAssemblySymbol, AssemblyMapper> _assemblies;

        public AssemblySetMapper(DiffingSettings settings) : base(settings) { }

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
