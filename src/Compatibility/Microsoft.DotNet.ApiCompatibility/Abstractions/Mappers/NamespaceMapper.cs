// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;

namespace Microsoft.DotNet.ApiCompatibility.Abstractions
{
    /// <summary>
    /// Object that represents a mapping between two <see cref="INamespaceSymbol"/> objects.
    /// This also holds a list of <see cref="TypeMapper"/> to represent the mapping of types in between
    /// <see cref="ElementMapper{T}.Left"/> and <see cref="ElementMapper{T}.Right"/>.
    /// </summary>
    public class NamespaceMapper : ElementMapper<INamespaceSymbol>
    {
        private Dictionary<ITypeSymbol, TypeMapper> _types;
        private IEnumerable<INamedTypeSymbol> _leftForwardedTypes;
        private IEnumerable<INamedTypeSymbol> _rightForwardedTypes;

        /// <summary>
        /// Instantiates an object with the provided <see cref="ComparingSettings"/>.
        /// </summary>
        /// <param name="settings">The settings used to diff the elements in the mapper.</param>
        public NamespaceMapper(ComparingSettings settings) : base(settings) { }

        /// <summary>
        /// Gets all the <see cref="TypeMapper"/> representing the types defined in the namespace including the typeforwards.
        /// </summary>
        /// <returns>The mapper representing the types in the namespace</returns>
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

        /// <summary>
        /// Adds forwarded types to the mapper to the index specified in the mapper.
        /// </summary>
        /// <param name="forwardedTypes">List containing the <see cref="INamedTypeSymbol"/> that represents the forwarded types.</param>
        /// <param name="index">Index to add the forwarded types into, 0 or 1.</param>
        public void AddForwardedTypes(IEnumerable<INamedTypeSymbol> forwardedTypes, int index)
        {
            if ((uint)index > 1)
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
