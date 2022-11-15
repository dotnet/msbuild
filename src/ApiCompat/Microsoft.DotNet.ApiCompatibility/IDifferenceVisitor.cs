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
        /// Visits the tree for the given <see cref="IElementMapper{T}"/>.
        /// </summary>
        /// <typeparam name="T">Underlying type for the objects that the mapper holds.</typeparam>
        /// <param name="mapper"><see cref="ElementMapper{T}"/> to visit.</param>
        void Visit<T>(IElementMapper<T> mapper);

        /// <summary>
        /// Visits the <see cref="IAssemblySetMapper"/> and visits each <see cref="IAssemblyMapper"/> in the mapper.
        /// </summary>
        /// <param name="mapper">The <see cref="AssemblySetMapper"/> to visit.</param>
        void Visit(IAssemblySetMapper mapper);

        /// <summary>
        /// Visits an <see cref="IAssemblyMapper"/> and adds it's differences to the <see cref="DiagnosticBag{CompatDifference}"/>.
        /// </summary>
        /// <param name="assembly">The mapper to visit.</param>
        void Visit(IAssemblyMapper assembly);

        /// <summary>
        /// Visits the <see cref="INamespaceMapper"/> and visits each <see cref="ITypeMapper"/> in the mapper.
        /// </summary>
        /// <param name="mapper">The <see cref="NamespaceMapper"/> to visit.</param>
        void Visit(INamespaceMapper @namespace);

        /// <summary>
        /// Visits an <see cref="ITypeMapper"/> and adds it's differences to the <see cref="DiagnosticBag{CompatDifference}"/>.
        /// </summary>
        /// <param name="type">The mapper to visit.</param>
        void Visit(ITypeMapper type);

        /// <summary>
        /// Visits an <see cref="IMemberMapper"/> and adds it's differences to the <see cref="DiagnosticBag{CompatDifference}"/>.
        /// </summary>
        /// <param name="member">The mapper to visit.</param>
        void Visit(IMemberMapper member);
    }
}
