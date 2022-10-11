// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;

namespace Microsoft.DotNet.ApiCompatibility.Abstractions
{
    /// <summary>
    /// Interface that represents a mapping between two <see cref="ISymbol"/> objects.
    /// </summary>
    public interface IMemberMapper : IElementMapper<ISymbol>
    {
        /// <summary>
        /// The containg type of this member.
        /// </summary>
        ITypeMapper ContainingType { get; }
    }
}
