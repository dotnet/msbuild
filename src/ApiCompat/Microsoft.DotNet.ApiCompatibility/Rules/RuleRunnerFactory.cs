// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.s

using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiCompatibility.Abstractions;

namespace Microsoft.DotNet.ApiCompatibility.Rules
{
    public class RuleRunnerFactory
    {
        private readonly Lazy<RuleRunner> _runner;

        public RuleRunnerFactory(bool strictMode, IEqualityComparer<ISymbol> equalityComparer, bool includeInternalSymbols, bool withReferences)
        {
            _runner = new Lazy<RuleRunner>(() => new RuleRunner(strictMode, equalityComparer, includeInternalSymbols, withReferences));
        }

        public virtual IRuleRunner GetRuleRunner()
        {
            return _runner.Value;
        }
    }
}
