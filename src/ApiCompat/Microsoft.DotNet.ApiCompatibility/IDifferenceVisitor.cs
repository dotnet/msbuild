// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.DotNet.ApiCompatibility.Abstractions;

namespace Microsoft.DotNet.ApiCompatibility
{
    /// <summary>
    /// The visitor that traverses the mappers' tree and gets it's differences in a <see cref="DiagnosticBag{CompatDifference}"/>.
    /// </summary>
    public interface IDifferenceVisitor
    {
        /// <summary>
        /// A list of <see cref="DiagnosticBag{CompatDifference}"/>.
        /// One per element compared in the right hand side.
        /// </summary>
        IEnumerable<CompatDifference> CompatDifferences { get; }

        /// <summary>
        /// Visits the tree for the given <see cref="ElementMapper{T}"/>.
        /// </summary>
        /// <typeparam name="T">Underlying type for the objects that the mapper holds.</typeparam>
        /// <param name="mapper"><see cref="ElementMapper{T}"/> to visit.</param>
        void Visit<T>(ElementMapper<T> mapper);

        /// <summary>
        /// Visits the <see cref="AssemblySetMapper"/> and visits each <see cref="AssemblyMapper"/> in the mapper.
        /// </summary>
        /// <param name="mapper">The <see cref="AssemblySetMapper"/> to visit.</param>
        void Visit(AssemblySetMapper mapper);

        /// <summary>
        /// Visits an <see cref="AssemblyMapper"/> and adds it's differences to the <see cref="DiagnosticBag{CompatDifference}"/>.
        /// </summary>
        /// <param name="assembly">The mapper to visit.</param>
        void Visit(AssemblyMapper assembly);

        /// <summary>
        /// Visits the <see cref="NamespaceMapper"/> and visits each <see cref="TypeMapper"/> in the mapper.
        /// </summary>
        /// <param name="mapper">The <see cref="NamespaceMapper"/> to visit.</param>
        void Visit(NamespaceMapper @namespace);

        /// <summary>
        /// Visits an <see cref="TypeMapper"/> and adds it's differences to the <see cref="DiagnosticBag{CompatDifference}"/>.
        /// </summary>
        /// <param name="type">The mapper to visit.</param>
        void Visit(TypeMapper type);

        /// <summary>
        /// Visits an <see cref="MemberMapper"/> and adds it's differences to the <see cref="DiagnosticBag{CompatDifference}"/>.
        /// </summary>
        /// <param name="member">The mapper to visit.</param>
        void Visit(MemberMapper member);
    }
}
