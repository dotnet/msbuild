// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiCompatibility.Rules;

namespace Microsoft.DotNet.ApiCompatibility.Mapping
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
        private readonly bool _typeForwardsOnly;

        /// <inheritdoc />
        public IAssemblyMapper ContainingAssembly { get; }

        /// <summary>
        /// Instantiates a namespace mapper.
        /// </summary>
        /// <param name="ruleRunner">The <see cref="IRuleRunner"/> that compares the namespace mapper elements.</param>
        /// <param name="settings">The <see cref="IMapperSettings"/> used to compare the namespace mapper elements.</param>
        /// <param name="rightSetSize">The number of elements in the right set to compare.</param>
        /// <param name="containingAssembly">The containing <see cref="IAssemblyMapper"/>.</param>
        /// <param name="typeForwardsOnly">Indicates if <see cref="GetTypes"/> should return forwarded types only.</param>
        public NamespaceMapper(IRuleRunner ruleRunner,
            IMapperSettings settings,
            int rightSetSize,
            IAssemblyMapper containingAssembly,
            bool typeForwardsOnly = false)
            : base(ruleRunner, settings, rightSetSize)
        {
            ContainingAssembly = containingAssembly;
            _types = new Dictionary<ITypeSymbol, ITypeMapper>(Settings.SymbolEqualityComparer);
            _typeForwardsOnly = typeForwardsOnly;
        }

        /// <inheritdoc />
        public IEnumerable<ITypeMapper> GetTypes()
        {
            if (!_expandedTree)
            {
                // if the _typeForwardsOnly flag is specified it means this namespace is already
                // populated with the resolved type forwards by the assembly mapper and that we 
                // didn't find this namespace in the initial assembly. So we avoid getting the types
                // as that would return the types defined in the assembly where the type forwards
                // were resolved from.
                if (!_typeForwardsOnly)
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
                if (Settings.SymbolFilter.Include(type))
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
