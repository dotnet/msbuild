// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
