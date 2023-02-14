// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.ApiCompatibility.Rules;

namespace Microsoft.DotNet.ApiCompatibility.Mapping
{
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
        public IAssemblySetMapper CreateAssemblySetMapper(IMapperSettings settings, int rightCount) =>
            new AssemblySetMapper(_ruleRunner, settings, rightCount);

        /// <inheritdoc />
        public IAssemblyMapper CreateAssemblyMapper(IMapperSettings settings, int rightCount) =>
            new AssemblyMapper(_ruleRunner, settings, rightCount);
    }
}
