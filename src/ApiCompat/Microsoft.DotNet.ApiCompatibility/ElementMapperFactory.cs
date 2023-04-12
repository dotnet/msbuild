// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiCompatibility.Abstractions;
using Microsoft.DotNet.ApiCompatibility.Rules;

namespace Microsoft.DotNet.ApiCompatibility
{
    /// <summary>
    /// Factory to create an IElementMapper instance.
    /// </summary>
    public interface IElementMapperFactory
    {
        /// <summary>
        /// Creates an AssemblySetMapper instance with optional given mapper settings and the count of the rights that are compared.
        /// </summary>
        /// <param name="settings">The mapper settings.</param>
        /// <param name="rightCount">The number of rights that are compared.</param>
        /// <returns>Returns an AssemblySetMapper based on the given inputs.</returns>
        ElementMapper<IEnumerable<ElementContainer<IAssemblySymbol>>> CreateAssemblySetMapper(MapperSettings settings = default, int rightCount = 1);

        /// <summary>
        /// Creates an AssemblyMapper instance with optional given mapper assetings and the count of the rights that are compared.
        /// </summary>
        /// <param name="settings">The mapper settings.</param>
        /// <param name="rightCount">The number of rights that are compared.</param>
        /// <returns>Returns an AssemblyMapper based on the given inputs.</returns>
        ElementMapper<ElementContainer<IAssemblySymbol>> CreateAssemblyMapper(MapperSettings settings = default, int rightCount = 1);
    }

    /// <summary>
    /// Factory to create an ElementMapper instance.
    /// </summary>
    public sealed class ElementMapperFactory : IElementMapperFactory
    {
        private readonly IRuleRunner _ruleRunner;

        public ElementMapperFactory(IRuleRunner ruleRunner)
        {
            _ruleRunner = ruleRunner;
        }

        /// <inheritdoc />
        public ElementMapper<IEnumerable<ElementContainer<IAssemblySymbol>>> CreateAssemblySetMapper(MapperSettings settings = default, int rightCount = 1) =>
            new AssemblySetMapper(_ruleRunner, settings, rightCount);

        /// <inheritdoc />
        public ElementMapper<ElementContainer<IAssemblySymbol>> CreateAssemblyMapper(MapperSettings settings = default, int rightCount = 1) =>
            new AssemblyMapper(_ruleRunner, settings, rightCount);
    }
}
