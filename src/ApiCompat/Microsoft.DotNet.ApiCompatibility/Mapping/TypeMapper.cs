// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiCompatibility.Rules;
using Microsoft.DotNet.ApiSymbolExtensions;

namespace Microsoft.DotNet.ApiCompatibility.Mapping
{
    /// <summary>
    /// Object that represents a mapping between two <see cref="ITypeSymbol"/> objects.
    /// This also holds the nested types as a list of <see cref="ITypeMapper"/> and the members defined within the type
    /// as a list of <see cref="IMemberMapper"/>
    /// </summary>
    public class TypeMapper : ElementMapper<ITypeSymbol>, ITypeMapper
    {
        private Dictionary<ITypeSymbol, ITypeMapper>? _nestedTypes;
        private Dictionary<ISymbol, IMemberMapper>? _members;

        /// <inheritdoc />
        public INamespaceMapper ContainingNamespace { get; }

        /// <inheritdoc />
        public ITypeMapper? ContainingType { get; }

        /// <summary>
        /// Instantiates a type mapper.
        /// </summary>
        /// <param name="ruleRunner">The <see cref="IRuleRunner"/> that compares the type mapper elements.</param>
        /// <param name="settings">The <see cref="IMapperSettings"/> used to compare the type mapper elements.</param>
        /// <param name="rightSetSize">The number of elements in the right set to compare.</param>
        /// <param name="containingNamespace">The containing <see cref="INamespaceMapper"/>.</param>
        /// <param name="containingType">The containing <see cref="ITypeMapper"/>. Null, if the type doesn't have a containing type.</param>
        public TypeMapper(IRuleRunner ruleRunner,
            IMapperSettings settings,
            int rightSetSize,
            INamespaceMapper containingNamespace,
            ITypeMapper? containingType = null)
            : base(ruleRunner, settings, rightSetSize)
        {
            ContainingNamespace = containingNamespace;
            ContainingType = containingType;
        }

        internal bool ShouldDiffElement(int rightIndex)
        {
            if (ContainingType != null)
            {
                Debug.Assert(ContainingType.ShouldDiffMembers);

                // This should only be called at a point where containingType.ShouldDiffMembers is true
                // So that means that containingType.Left is not null and we don't need to check.
                // If containingType.Right only contains one element, we can assume it is not null.
                return ContainingType.Right.Length == 1 || ContainingType.Right[rightIndex] != null;
            }

            return true;
        }

        /// <inheritdoc />
        public bool ShouldDiffMembers
        {
            get
            {
                if (Left == null)
                    return false;

                if (Right.Length == 1 && Right[0] == null)
                    return false;

                for (int i = 0; i < Right.Length; i++)
                {
                    if (Right[i] != null)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        /// <inheritdoc />
        public IEnumerable<ITypeMapper> GetNestedTypes()
        {
            if (_nestedTypes == null)
            {
                _nestedTypes = new Dictionary<ITypeSymbol, ITypeMapper>(Settings.SymbolEqualityComparer);

                AddOrCreateMappers(Left, ElementSide.Left);
                for (int i = 0; i < Right.Length; i++)
                {
                    AddOrCreateMappers(Right[i], ElementSide.Right, i);
                }

                void AddOrCreateMappers(ITypeSymbol? symbol, ElementSide side, int setIndex = 0)
                {
                    // Silently return if the element hasn't been added yet.
                    if (symbol == null)
                    {
                        return;
                    }

                    foreach (INamedTypeSymbol nestedType in symbol.GetTypeMembers())
                    {
                        if (Settings.SymbolFilter.Include(nestedType))
                        {
                            if (!_nestedTypes.TryGetValue(nestedType, out ITypeMapper? mapper))
                            {
                                mapper = new TypeMapper(RuleRunner, Settings, Right.Length, ContainingNamespace, this);
                                _nestedTypes.Add(nestedType, mapper);
                            }
                            mapper.AddElement(nestedType, side, setIndex);
                        }
                    }
                }
            }

            return _nestedTypes.Values;
        }

        /// <inheritdoc />
        public IEnumerable<IMemberMapper> GetMembers()
        {
            if (_members == null)
            {
                _members = new Dictionary<ISymbol, IMemberMapper>(Settings.SymbolEqualityComparer);

                AddOrCreateMappers(Left, ElementSide.Left);
                for (int i = 0; i < Right.Length; i++)
                {
                    AddOrCreateMappers(Right[i], ElementSide.Right, i);
                }

                void AddOrCreateMappers(ITypeSymbol? symbol, ElementSide side, int setIndex = 0)
                {
                    // Silently return if the element hasn't been added yet.
                    if (symbol == null)
                    {
                        return;
                    }

                    foreach (ISymbol member in symbol.GetMembers())
                    {
                        // when running without references Roslyn doesn't filter out the special value__ field emitted
                        // for enums. The reason why we should filter it out, is because we could have a case
                        // where one side was loaded with references and one that was loaded without, if that is the case
                        // we would compare __value vs null and emit some warnings.
                        if (Settings.SymbolFilter.Include(member) && member is not ITypeSymbol && !IsSpecialEnumField(member))
                        {
                            if (!_members.TryGetValue(member, out IMemberMapper? mapper))
                            {
                                mapper = new MemberMapper(RuleRunner, Settings, Right.Length, this);
                                _members.Add(member, mapper);
                            }
                            mapper.AddElement(member, side, setIndex);
                        }
                    }
                }
            }

            return _members.Values;
        }

        private bool IsSpecialEnumField(ISymbol member) =>
            !Settings.WithReferences &&
            member is IFieldSymbol &&
            member.Name == "value__" &&
            // When running without references, Roslyn doesn't set the type kind as enum. Compare by name instead.
            member.ContainingType.BaseType?.ToComparisonDisplayString() == "System.Enum";
    }
}
