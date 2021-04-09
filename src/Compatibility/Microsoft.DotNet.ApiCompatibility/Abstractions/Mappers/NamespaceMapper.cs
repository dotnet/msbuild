using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.DotNet.ApiCompatibility.Abstractions
{
    public class NamespaceMapper : ElementMapper<INamespaceSymbol>
    {
        private Dictionary<ITypeSymbol, TypeMapper> _types;
        private IEnumerable<INamedTypeSymbol> _leftForwardedTypes;
        private IEnumerable<INamedTypeSymbol> _rightForwardedTypes;

        public NamespaceMapper(DiffingSettings settings) : base(settings) { }

        public IEnumerable<TypeMapper> GetTypes()
        {
            if (_types == null)
            {
                _types = new Dictionary<ITypeSymbol, TypeMapper>(Settings.EqualityComparer);
                IEnumerable<ITypeSymbol> types;

                if (Left != null)
                {
                    types = Left.GetTypeMembers().AddRange(_leftForwardedTypes);
                    AddOrCreateMappers(0);
                }

                if (Right != null)
                {
                    types = Right.GetTypeMembers().AddRange(_rightForwardedTypes);
                    AddOrCreateMappers(1);
                }

                void AddOrCreateMappers(int index)
                {
                    if (types == null)
                        return;

                    foreach (var type in types)
                    {
                        if (Settings.Filter.Include(type))
                        {
                            if (!_types.TryGetValue(type, out TypeMapper mapper))
                            {
                                mapper = new TypeMapper(Settings);
                                _types.Add(type, mapper);
                            }

                            mapper.AddElement(type, index);
                        }
                    }
                }
            }

            return _types.Values;
        }

        public void AddForwardedTypes(IEnumerable<INamedTypeSymbol> forwardedTypes, int index)
        {
            if (index < 0 || index > 1)
                throw new ArgumentOutOfRangeException(nameof(index), $"Value must be 0 or 1");

            if (index == 0)
            {
                _leftForwardedTypes = forwardedTypes;
            }
            else
            {
                _rightForwardedTypes = forwardedTypes;
            }
        }
    }
}
