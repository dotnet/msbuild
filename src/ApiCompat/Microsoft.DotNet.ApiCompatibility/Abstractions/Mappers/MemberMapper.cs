// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiCompatibility.Rules;

namespace Microsoft.DotNet.ApiCompatibility.Abstractions
{
    /// <summary>
    /// Object that represents a mapping between two <see cref="ISymbol"/> objects.
    /// </summary>
    public class MemberMapper : ElementMapper<ISymbol>
    {
        /// <summary>
        /// The containg type of this member.
        /// </summary>
        public TypeMapper ContainingType { get; }

        /// <summary>
        /// Instantiates an object with the provided <see cref="ComparingSettings"/>.
        /// </summary>
        /// <param name="settings">The settings used to diff the elements in the mapper.</param>
        /// <param name="rightSetSize">The number of elements in the right set to compare.</param>
        public MemberMapper(IRuleRunner ruleRunner,
            TypeMapper containingType,
            MapperSettings settings = default,
            int rightSetSize = 1)
            : base(ruleRunner, settings, rightSetSize)
        {
            ContainingType = containingType;
        }

        // If we got to this point it means that ContainingType.Left is not null.
        // Because of that we can only check ContainingType.Right.
        internal bool ShouldDiffElement(int rightIndex) =>
            ContainingType.Right.Length == 1 || ContainingType.Right[rightIndex] != null;
    }
}
