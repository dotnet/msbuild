// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;

namespace Microsoft.DotNet.ApiCompatibility.Abstractions
{
    /// <summary>
    /// Object that represents a mapping between two <see cref="ISymbol"/> objects.
    /// </summary>
    public class MemberMapper : ElementMapper<ISymbol>
    {
        private readonly TypeMapper _containingType;

        /// <summary>
        /// Instantiates an object with the provided <see cref="ComparingSettings"/>.
        /// </summary>
        /// <param name="settings">The settings used to diff the elements in the mapper.</param>
        /// <param name="rightSetSize">The number of elements in the right set to compare.</param>
        public MemberMapper(ComparingSettings settings, TypeMapper containingType, int rightSetSize = 1)
            : base(settings, rightSetSize)
        {
            _containingType = containingType;
        }

        // If we got to this point it means that _containingType.Left is not null.
        // Because of that we can only check _containingType.Right.
        internal bool ShouldDiffElement(int rightIndex) =>
            _containingType.Right.Length == 1 || _containingType.Right[rightIndex] != null;
    }
}
