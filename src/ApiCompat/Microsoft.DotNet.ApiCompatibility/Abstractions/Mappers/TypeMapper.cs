// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiCompatibility.Extensions;
using Microsoft.DotNet.ApiCompatibility.Rules;

namespace Microsoft.DotNet.ApiCompatibility.Abstractions
{
    /// <summary>
    /// Object that represents a mapping between two <see cref="ITypeSymbol"/> objects.
    /// This also holds the nested types as a list of <see cref="TypeMapper"/> and the members defined within the type
    /// as a list of <see cref="MemberMapper"/>
    /// </summary>
    public class TypeMapper : ElementMapper<ITypeSymbol>
    {
        private Dictionary<ITypeSymbol, TypeMapper>? _nestedTypes;
        private Dictionary<ISymbol, MemberMapper>? _members;

        /// <summary>
        /// The containg namespace of this type.
        /// </summary>
        public NamespaceMapper ContainingNamespace { get; }

        /// <summary>
        /// The containing type of this type. Null if the type isn't nested.
        /// </summary>
        internal TypeMapper? ContainingType { get; }

        /// <summary>
        /// Instantiates an object with the provided <see cref="ComparingSettings"/>.
        /// </summary>
        /// <param name="settings">The settings used to diff the elements in the mapper.</param>
        /// <param name="rightSetSize">The number of elements in the right set to compare.</param>
        public TypeMapper(IRuleRunner ruleRunner,
            NamespaceMapper containingNamespace,
            MapperSettings settings = default,
            TypeMapper? containingType = null,
            int rightSetSize = 1)
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

        /// <summary>
        /// Indicates whether we have a complete mapper and if the members should be diffed.
        /// </summary>
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

        /// <summary>
        /// Gets the nested types within the mapped types.
        /// </summary>
        /// <returns>The list of <see cref="TypeMapper"/> representing the nested types.</returns>
        public IEnumerable<TypeMapper> GetNestedTypes()
        {
            if (_nestedTypes == null)
            {
                _nestedTypes = new Dictionary<ITypeSymbol, TypeMapper>(Settings.EqualityComparer);

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
                        if (Settings.Filter.Include(nestedType))
                        {
                            if (!_nestedTypes.TryGetValue(nestedType, out TypeMapper? mapper))
                            {
                                mapper = new TypeMapper(RuleRunner, ContainingNamespace, Settings, this, Right.Length);
                                _nestedTypes.Add(nestedType, mapper);
                            }
                            mapper.AddElement(nestedType, side, setIndex);
                        }
                    }
                }
            }

            return _nestedTypes.Values;
        }

        /// <summary>
        /// Gets the members defined in this type.
        /// </summary>
        /// <returns>The list of <see cref="MemberMapper"/> representing the members.</returns>
        public IEnumerable<MemberMapper> GetMembers()
        {
            if (_members == null)
            {
                _members = new Dictionary<ISymbol, MemberMapper>(Settings.EqualityComparer);

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
                        if (Settings.Filter.Include(member) && member is not ITypeSymbol && !IsSpecialEnumField(member))
                        {
                            if (!_members.TryGetValue(member, out MemberMapper? mapper))
                            {
                                mapper = new MemberMapper(RuleRunner, this, Settings, Right.Length);
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
            !Settings.WarnOnMissingReferences &&
            member is IFieldSymbol &&
            member.Name == "value__" &&
            // When running without references, Roslyn doesn't set the type kind as enum. Compare by name instead.
            member.ContainingType.BaseType?.ToComparisonDisplayString() == "System.Enum";
    }
}
