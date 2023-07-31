// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.ApiCompatibility.Mapping;

namespace Microsoft.DotNet.ApiCompatibility
{
    /// <summary>
    /// A visitor that traverses a given mapping tree and stores differences in the <see cref="CompatDifferences"/> collection.
    /// </summary>
    public interface IDifferenceVisitor
    {
        /// <summary>
        /// A list of <see cref="CompatDifference"/> items which are associated with <see cref="MetadataInformation" />.
        /// </summary>
        IEnumerable<CompatDifference> CompatDifferences { get; }

        /// <summary>
        /// Visits the mapping tree for a given <see cref="IElementMapper{T}"/>.
        /// </summary>
        /// <typeparam name="T">Underlying type for the objects that the mapper holds.</typeparam>
        /// <param name="mapper">The <see cref="IElementMapper{T}"/> to visit.</param>
        void Visit<T>(IElementMapper<T> mapper);

        /// <summary>
        /// Visits the <see cref="IAssemblySetMapper"/> and visits each <see cref="IAssemblyMapper"/> in the mapper.
        /// </summary>
        /// <param name="mapper">The <see cref="IAssemblySetMapper"/> to visit.</param>
        void Visit(IAssemblySetMapper mapper);

        /// <summary>
        /// Visits an <see cref="IAssemblyMapper"/> and stores differences in the <see cref="CompatDifferences"/> collection.
        /// </summary>
        /// <param name="assembly">The <see cref="IAssemblyMapper"/> to visit.</param>
        void Visit(IAssemblyMapper assembly);

        /// <summary>
        /// Visits an <see cref="INamespaceMapper"/> and stores differences in the <see cref="CompatDifferences"/> collection.
        /// </summary>
        /// <param name="namespace">The <see cref="INamespaceMapper"/> to visit.</param>
        void Visit(INamespaceMapper @namespace);

        /// <summary>
        /// Visits an <see cref="ITypeMapper"/> and stores differences in the <see cref="CompatDifferences"/> collection.
        /// </summary>
        /// <param name="type">The <see cref="ITypeMapper"/> to visit.</param>
        void Visit(ITypeMapper type);

        /// <summary>
        /// Visits an <see cref="IMemberMapper"/> and stores differences in the <see cref="CompatDifferences"/> collection.
        /// </summary>
        /// <param name="member">The <see cref="IMemberMapper"/> to visit.</param>
        void Visit(IMemberMapper member);
    }
}
