// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using System.Collections.Generic;

namespace Microsoft.DotNet.ApiCompatibility.Abstractions
{
    /// <summary>
    /// Object that represents a mapping between two <see cref="ITypeSymbol"/> objects.
    /// This also holds the nested types as a list of <see cref="TypeMapper"/> and the members defined within the type
    /// as a list of <see cref="MemberMapper"/>
    /// </summary>
    public class TypeMapper : ElementMapper<ITypeSymbol>
    {
        private Dictionary<ITypeSymbol, TypeMapper> _nestedTypes;
        private Dictionary<ISymbol, MemberMapper> _members;

        /// <summary>
        /// Instantiates an object with the provided <see cref="ComparingSettings"/>.
        /// </summary>
        /// <param name="settings">The settings used to diff the elements in the mapper.</param>
        public TypeMapper(ComparingSettings settings) : base(settings) { }

        /// <summary>
        /// Indicates whether we have a complete mapper and if the members should be diffed.
        /// </summary>
        public bool ShouldDiffMembers => Left != null && Right != null;

        /// <summary>
        /// Gets the nested types within the mapped types.
        /// </summary>
        /// <returns>The list of <see cref="TypeMapper"/> representing the nested types.</returns>
        public IEnumerable<TypeMapper> GetNestedTypes()
        {
            if (_nestedTypes == null)
            {
                _nestedTypes = new Dictionary<ITypeSymbol, TypeMapper>(Settings.EqualityComparer);

                if (Left != null)
                {
                    AddOrCreateMappers(Left.GetTypeMembers(), 0);
                }

                if (Right != null)
                {
                    AddOrCreateMappers(Right.GetTypeMembers(), 1);
                }

                void AddOrCreateMappers(IEnumerable<ITypeSymbol> symbols, int index)
                {
                    foreach (var nestedType in symbols)
                    {
                        if (Settings.Filter.Include(nestedType))
                        {
                            if (!_nestedTypes.TryGetValue(nestedType, out TypeMapper mapper))
                            {
                                mapper = new TypeMapper(Settings);
                                _nestedTypes.Add(nestedType, mapper);
                            }
                            mapper.AddElement(nestedType, index);
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

                if (Left != null)
                {
                    AddOrCreateMappers(Left.GetMembers(), 0);
                }

                if (Right != null)
                {
                    AddOrCreateMappers(Right.GetMembers(), 1);
                }

                void AddOrCreateMappers(IEnumerable<ISymbol> symbols, int index)
                {
                    foreach (var member in symbols)
                    {
                        if (Settings.Filter.Include(member) && member is not ITypeSymbol)
                        {
                            if (!_members.TryGetValue(member, out MemberMapper mapper))
                            {
                                mapper = new MemberMapper(Settings);
                                _members.Add(member, mapper);
                            }
                            mapper.AddElement(member, index);
                        }
                    }
                }
            }

            return _members.Values;
        }
    }
}
