// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;

namespace Microsoft.DotNet.ApiCompatibility.Mapping
{
    /// <summary>
    /// Interface that represents a mapping between two <see cref="ISymbol"/> objects.
    /// </summary>
    public interface IMemberMapper : IElementMapper<ISymbol>
    {
        /// <summary>
        /// The containing type of this member.
        /// </summary>
        ITypeMapper ContainingType { get; }
    }
}
