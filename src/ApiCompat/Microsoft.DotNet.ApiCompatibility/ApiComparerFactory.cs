// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.DotNet.ApiCompatibility.Rules;

namespace Microsoft.DotNet.ApiCompatibility
{
    /// <summary>
    /// Factory to create an IApiComparer instance.
    /// </summary>
    public interface IApiComparerFactory
    {
        /// <summary>
        /// Creates an IApiComparer with optionally provided api copmarison settings.
        /// </summary>
        /// <returns>Returns an ApiComparer instance</returns>
        IApiComparer Create(ApiComparerSettings? settings = null);
    }

    /// <summary>
    /// Factory to create an ApiComparer instance.
    /// </summary>
    public sealed class ApiComparerFactory : IApiComparerFactory
    {
        private readonly IRuleFactory _ruleFactory;
        private readonly IDifferenceVisitorFactory? _differenceVisitorFactory;
        private readonly IRuleContext? _ruleContext;
        private readonly Func<IRuleFactory, IRuleContext, IRuleRunner>? _ruleRunnerFactory;
        private readonly Func<IRuleRunner, IElementMapperFactory>? _elementMapperFactory;

        public ApiComparerFactory(IRuleFactory ruleFactory,
            IDifferenceVisitorFactory? differenceVisitorFactory = null,
            IRuleContext? ruleContext = null,
            Func<IRuleFactory, IRuleContext, IRuleRunner>? ruleRunnerFactory = null,
            Func<IRuleRunner, IElementMapperFactory>? elementMapperFactory = null)
        {
            _ruleFactory = ruleFactory;
            _differenceVisitorFactory = differenceVisitorFactory;
            _ruleContext = ruleContext;
            _ruleRunnerFactory = ruleRunnerFactory;
            _elementMapperFactory = elementMapperFactory;
        }

        /// <inheritdoc />
        public IApiComparer Create(ApiComparerSettings? settings = null)
        {
            return new ApiComparer(_ruleFactory,
                settings,
                _differenceVisitorFactory,
                _ruleContext,
                _ruleRunnerFactory,
                _elementMapperFactory);
        }
    }
}
