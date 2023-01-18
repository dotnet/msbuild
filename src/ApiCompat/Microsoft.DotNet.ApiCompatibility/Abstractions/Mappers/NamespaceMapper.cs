// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiCompatibility.Rules;

namespace Microsoft.DotNet.ApiCompatibility.Abstractions
{
    /// <summary>
    /// Object that represents a mapping between two <see cref="INamespaceSymbol"/> objects.
    /// This also holds a list of <see cref="ITypeMapper"/> to represent the mapping of types in between
    /// <see cref="IElementMapper{T}.Left"/> and <see cref="IElementMapper{T}.Right"/>.
    /// </summary>
    public class NamespaceMapper : ElementMapper<INamespaceSymbol>, INamespaceMapper
    {
        private readonly Dictionary<ITypeSymbol, ITypeMapper> _types;
        private bool _expandedTree = false;
        private readonly bool _typeforwardsOnly;

        /// <inheritdoc />
        public IAssemblyMapper ContainingAssembly { get; }

        /// <summary>
        /// Instantiates an object with the provided <see cref="ComparingSettings"/>.
        /// </summary>
        /// <param name="settings">The settings used to diff the elements in the mapper.</param>
        /// <param name="rightSetSize">The number of elements in the right set to compare.</param>
        /// <param name="typeforwardsOnly">Indicates if <see cref="GetTypes"/> should only return typeforwards.</param>
        public NamespaceMapper(IRuleRunner ruleRunner,
            MapperSettings settings,
            int rightSetSize,
            IAssemblyMapper containingAssembly,
            bool typeforwardsOnly = false)
            : base(ruleRunner, settings, rightSetSize)
        {
            ContainingAssembly = containingAssembly;
            _types = new Dictionary<ITypeSymbol, ITypeMapper>(Settings.EqualityComparer);
            _typeforwardsOnly = typeforwardsOnly;
        }

        /// <inheritdoc />
        public IEnumerable<ITypeMapper> GetTypes()
        {
            if (!_expandedTree)
            {
                // if the typeforwardsOnly flag is specified it means this namespace is already
                // populated with the resolved typeforwards by the assembly mapper and that we 
                // didn't find this namespace in the initial assembly. So we avoid getting the types
                // as that would return the types defined in the assembly where the typeforwardes
                // were resolved from.
                if (!_typeforwardsOnly)
                {
                    AddOrCreateMappers(Left, ElementSide.Left);
                    for (int i = 0; i < Right.Length; i++)
                    {
                        AddOrCreateMappers(Right[i], ElementSide.Right, i);
                    }
                }

                _expandedTree = true;
            }

            return _types.Values;
        }

        /// <inheritdoc />
        public void AddForwardedTypes(IEnumerable<INamedTypeSymbol>? forwardedTypes, ElementSide side, int setIndex)
        {
            AddOrCreateMappers(forwardedTypes, side, setIndex);
        }

        private void AddOrCreateMappers(INamespaceSymbol? symbol, ElementSide side, int setIndex = 0)
        {
            if (symbol == null)
            {
                return;
            }

            AddOrCreateMappers(symbol.GetTypeMembers(), side, setIndex);
        }

        private void AddOrCreateMappers(IEnumerable<ITypeSymbol>? types, ElementSide side, int setIndex)
        {
            // Silently return if the element hasn't been added yet.
            if (types == null)
                return;

            foreach (ITypeSymbol type in types)
            {
                if (Settings.Filter.Include(type))
                {
                    if (!_types.TryGetValue(type, out ITypeMapper? mapper))
                    {
                        mapper = new TypeMapper(RuleRunner, Settings, Right.Length, this, null);
                        _types.Add(type, mapper);
                    }

                    mapper.AddElement(type, side, setIndex);
                }
            }
        }
    }
}
