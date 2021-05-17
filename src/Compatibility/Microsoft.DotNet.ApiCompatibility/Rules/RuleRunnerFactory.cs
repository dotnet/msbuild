// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.s

using Microsoft.DotNet.ApiCompatibility.Abstractions;

namespace Microsoft.DotNet.ApiCompatibility.Rules
{
    public class RuleRunnerFactory : IRuleRunnerFactory
    {
        private readonly bool _strictMode;
        private RuleRunner _driver;

        public RuleRunnerFactory(bool strictMode = false)
        {
            _strictMode = strictMode;
        }

        public IRuleRunner GetRuleRunner()
        {
            if (_driver == null)
                _driver = new RuleRunner(_strictMode);

            return _driver;
        }
    }
}
