// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
        /// Creates an <see cref="IAssemblySetMapper"/> instance with optional given mapper settings and the count of the rights that are compared.
        /// </summary>
        /// <param name="settings">The mapper settings.</param>
        /// <param name="rightCount">The number of rights that are compared.</param>
        /// <returns>Returns an AssemblySetMapper based on the given inputs.</returns>
        IAssemblySetMapper CreateAssemblySetMapper(MapperSettings settings, int rightCount);

        /// <summary>
        /// Creates an <see cref="IAssemblyMapper"/> instance with optional given mapper assetings and the count of the rights that are compared.
        /// </summary>
        /// <param name="settings">The mapper settings.</param>
        /// <param name="rightCount">The number of rights that are compared.</param>
        /// <returns>Returns an AssemblyMapper based on the given inputs.</returns>
        IAssemblyMapper CreateAssemblyMapper(MapperSettings settings, int rightCount);
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
        public IAssemblySetMapper CreateAssemblySetMapper(MapperSettings settings, int rightCount) =>
            new AssemblySetMapper(_ruleRunner, settings, rightCount);

        /// <inheritdoc />
        public IAssemblyMapper CreateAssemblyMapper(MapperSettings settings, int rightCount) =>
            new AssemblyMapper(_ruleRunner, settings, rightCount);
    }
}
