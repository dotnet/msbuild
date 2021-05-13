// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Diagnostics;

namespace Microsoft.DotNet.ApiCompatibility.Abstractions
{
    /// <summary>
    /// Object that represents a mapping between two <see cref="ITypeSymbol"/> objects.
    /// This also holds the nested types as a list of <see cref="TypeMapper"/> and the members defined within the type
    /// as a list of <see cref="MemberMapper"/>
    /// </summary>
    public class TypeMapper : ElementMapper<ITypeSymbol>
    {
        private readonly TypeMapper _containingType;
        private Dictionary<ITypeSymbol, TypeMapper> _nestedTypes;
        private Dictionary<ISymbol, MemberMapper> _members;

        /// <summary>
        /// Instantiates an object with the provided <see cref="ComparingSettings"/>.
        /// </summary>
        /// <param name="settings">The settings used to diff the elements in the mapper.</param>
        /// <param name="rightSetSize">The number of elements in the right set to compare.</param>
        public TypeMapper(ComparingSettings settings, TypeMapper containingType = null, int rightSetSize = 1)
            : base(settings, rightSetSize)
        {
            _containingType = containingType;
        }

        internal bool ShouldDiffElement(int rightIndex)
        {
            if (IsNested)
            {
                Debug.Assert(_containingType.ShouldDiffMembers);

                // This should only be called at a point where containingType.ShouldDiffMembers is true
                // So that means that containingType.Left is not null and we don't need to check.
                // If containingType.Right only contains one element, we can assume it is not null.
                return _containingType.Right.Length == 1 || _containingType.Right[rightIndex] != null;
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
        /// Indicates whether a type is nested or not.
        /// </summary>
        public bool IsNested => _containingType != null;

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

                void AddOrCreateMappers(ITypeSymbol symbol, ElementSide side, int setIndex = 0)
                {
                    if (symbol == null)
                    {
                        return;
                    }

                    foreach (INamedTypeSymbol nestedType in symbol.GetTypeMembers())
                    {
                        if (Settings.Filter.Include(nestedType))
                        {
                            if (!_nestedTypes.TryGetValue(nestedType, out TypeMapper mapper))
                            {
                                mapper = new TypeMapper(Settings, this, Right.Length);
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

                void AddOrCreateMappers(ITypeSymbol symbol, ElementSide side, int setIndex = 0)
                {
                    if (symbol == null)
                    {
                        return;
                    }

                    foreach (ISymbol member in symbol.GetMembers())
                    {
                        if (Settings.Filter.Include(member) && member is not ITypeSymbol)
                        {
                            if (!_members.TryGetValue(member, out MemberMapper mapper))
                            {
                                mapper = new MemberMapper(Settings, this, Right.Length);
                                _members.Add(member, mapper);
                            }
                            mapper.AddElement(member, side, setIndex);
                        }
                    }
                }
            }

            return _members.Values;
        }
    }
}
