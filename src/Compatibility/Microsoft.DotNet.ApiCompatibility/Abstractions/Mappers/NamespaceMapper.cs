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
        private bool _expandedTree = false;

        /// <summary>
        /// Instantiates an object with the provided <see cref="ComparingSettings"/>.
        /// </summary>
        /// <param name="settings">The settings used to diff the elements in the mapper.</param>
        /// <param name="rightSetSize">The number of elements in the right set to compare.</param>
        public NamespaceMapper(ComparingSettings settings, int rightSetSize = 1)
            : base(settings, rightSetSize) { }

        /// <summary>
        /// Gets all the <see cref="TypeMapper"/> representing the types defined in the namespace including the typeforwards.
        /// </summary>
        /// <returns>The mapper representing the types in the namespace</returns>
        public IEnumerable<TypeMapper> GetTypes()
        {
            if (!_expandedTree)
            {
                EnsureTypesInitialized();
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

                _expandedTree = true;
            }

            return _types.Values;
        }

        /// <summary>
        /// Adds forwarded types to the mapper to the index specified in the mapper.
        /// </summary>
        /// <param name="forwardedTypes">List containing the <see cref="INamedTypeSymbol"/> that represents the forwarded types.</param>
        /// <param name="side">Side to add the forwarded types into, 0 (Left) or 1 (Right).</param>
        /// <param name="setIndex">Value representing the index on the set of elements corresponding to the compared side.</param>
        public void AddForwardedTypes(IEnumerable<INamedTypeSymbol> forwardedTypes, ElementSide side, int setIndex)
        {
            EnsureTypesInitialized();
            AddOrCreateMappers(forwardedTypes, side, setIndex);
        }

        private void EnsureTypesInitialized()
        {
            if (_types == null)
                _types = new Dictionary<ITypeSymbol, TypeMapper>(Settings.EqualityComparer);
        }

        private void AddOrCreateMappers(INamespaceSymbol symbol, ElementSide side, int setIndex = 0)
        {
            if (symbol == null)
            {
                return;
            }

            AddOrCreateMappers(symbol.GetTypeMembers(), side, setIndex);
        }

        private void AddOrCreateMappers(IEnumerable<ITypeSymbol> types, ElementSide side, int setIndex)
        {
            if (types == null)
                return;

            foreach (ITypeSymbol type in types)
            {
                if (Settings.Filter.Include(type))
                {
                    if (!_types.TryGetValue(type, out TypeMapper mapper))
                    {
                        mapper = new TypeMapper(Settings, null, Right.Length);
                        _types.Add(type, mapper);
                    }

                    mapper.AddElement(type, side, setIndex);
                }
            }
        }
    }
}
