// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.ApiCompatibility.Mapping;
using Microsoft.DotNet.ApiCompatibility.Rules;

namespace Microsoft.DotNet.ApiCompatibility
{
    /// <summary>
    /// Factory to create an ApiComparer instance.
    /// </summary>
    public sealed class ApiComparerFactory : IApiComparerFactory
    {
        private readonly IRuleFactory _ruleFactory;
        private readonly ApiComparerSettings? _settings;
        private readonly IDifferenceVisitorFactory? _differenceVisitorFactory;
        private readonly IRuleContext? _ruleContext;
        private readonly Func<IRuleFactory, IRuleContext, IRuleRunner>? _ruleRunnerFactory;
        private readonly Func<IRuleRunner, IElementMapperFactory>? _elementMapperFactory;

        public ApiComparerFactory(IRuleFactory ruleFactory,
            ApiComparerSettings? settings = null,
            IDifferenceVisitorFactory? differenceVisitorFactory = null,
            IRuleContext? ruleContext = null,
            Func<IRuleFactory, IRuleContext, IRuleRunner>? ruleRunnerFactory = null,
            Func<IRuleRunner, IElementMapperFactory>? elementMapperFactory = null)
        {
            _settings = settings;
            _ruleFactory = ruleFactory;
            _differenceVisitorFactory = differenceVisitorFactory;
            _ruleContext = ruleContext;
            _ruleRunnerFactory = ruleRunnerFactory;
            _elementMapperFactory = elementMapperFactory;
        }

        /// <inheritdoc />
        public IApiComparer Create()
        {
            return new ApiComparer(_ruleFactory,
                _settings,
                _differenceVisitorFactory,
                _ruleContext,
                _ruleRunnerFactory,
                _elementMapperFactory);
        }
    }
}
