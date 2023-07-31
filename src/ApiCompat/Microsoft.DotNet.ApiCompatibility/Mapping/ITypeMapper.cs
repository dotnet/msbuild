// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;

namespace Microsoft.DotNet.ApiCompatibility.Mapping
{
    /// <summary>
    /// Interface that represents a mapping between two <see cref="ITypeSymbol"/> objects.
    /// This also holds the nested types as a list of <see cref="ITypeMapper"/> and the members defined within the type
    /// as a list of <see cref="IMemberMapper"/>
    /// </summary>
    public interface ITypeMapper : IElementMapper<ITypeSymbol>
    {
        /// <summary>
        /// The containing namespace of this type.
        /// </summary>
        INamespaceMapper ContainingNamespace { get; }

        /// <summary>
        /// The containing type of this type. Null if the type isn't nested.
        /// </summary>
        ITypeMapper? ContainingType { get; }

        /// <summary>
        /// Indicates whether we have a complete mapper and if the members should be diffed.
        /// </summary>
        bool ShouldDiffMembers { get; }

        /// <summary>
        /// Gets the nested types within the mapped types.
        /// </summary>
        /// <returns>The list of <see cref="ITypeMapper"/> representing the nested types.</returns>
        IEnumerable<ITypeMapper> GetNestedTypes();

        /// <summary>
        /// Gets the members defined in this type.
        /// </summary>
        /// <returns>The list of <see cref="IMemberMapper"/> representing the members.</returns>
        IEnumerable<IMemberMapper> GetMembers();
    }
}
