// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiCompatibility.Rules;

namespace Microsoft.DotNet.ApiCompatibility.Abstractions
{
    /// <summary>
    /// Object that represents a mapping between two <see cref="INamespaceSymbol"/> objects.
    /// This also holds a list of <see cref="TypeMapper"/> to represent the mapping of types in between
    /// <see cref="ElementMapper{T}.Left"/> and <see cref="ElementMapper{T}.Right"/>.
    /// </summary>
    public class NamespaceMapper : ElementMapper<INamespaceSymbol>
    {
        private readonly Dictionary<ITypeSymbol, TypeMapper> _types;
        private bool _expandedTree = false;
        private readonly bool _typeforwardsOnly;

        /// <summary>
        /// The containing assembly of this namespace.
        /// </summary>
        public AssemblyMapper ContainingAssembly { get; }

        /// <summary>
        /// Instantiates an object with the provided <see cref="ComparingSettings"/>.
        /// </summary>
        /// <param name="settings">The settings used to diff the elements in the mapper.</param>
        /// <param name="rightSetSize">The number of elements in the right set to compare.</param>
        /// <param name="typeforwardsOnly">Indicates if <see cref="GetTypes"/> should only return typeforwards.</param>
        public NamespaceMapper(IRuleRunner ruleRunner,
            AssemblyMapper containingAssembly,
            MapperSettings settings = default,
            int rightSetSize = 1,
            bool typeforwardsOnly = false)
            : base(ruleRunner, settings, rightSetSize)
        {
            ContainingAssembly = containingAssembly;
            _types = new Dictionary<ITypeSymbol, TypeMapper>(Settings.EqualityComparer);
            _typeforwardsOnly = typeforwardsOnly;
        }

        /// <summary>
        /// Gets all the <see cref="TypeMapper"/> representing the types defined in the namespace including the typeforwards.
        /// </summary>
        /// <returns>The mapper representing the types in the namespace</returns>
        public IEnumerable<TypeMapper> GetTypes()
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

        /// <summary>
        /// Adds forwarded types to the mapper to the index specified in the mapper.
        /// </summary>
        /// <param name="forwardedTypes">List containing the <see cref="INamedTypeSymbol"/> that represents the forwarded types.</param>
        /// <param name="side">Side to add the forwarded types into, 0 (Left) or 1 (Right).</param>
        /// <param name="setIndex">Value representing the index on the set of elements corresponding to the compared side.</param>
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
                    if (!_types.TryGetValue(type, out TypeMapper? mapper))
                    {
                        mapper = new TypeMapper(RuleRunner, this, Settings, null, Right.Length);
                        _types.Add(type, mapper);
                    }

                    mapper.AddElement(type, side, setIndex);
                }
            }
        }
    }
}
