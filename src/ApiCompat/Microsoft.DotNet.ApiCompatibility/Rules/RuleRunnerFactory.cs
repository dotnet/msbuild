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

        public RuleRunnerFactory(string? leftName, string[]? rightNames, IEqualityComparer<ISymbol> equalityComparer, bool includeInternalSymbols, bool strictMode, bool withReferences)
        {
            if (string.IsNullOrEmpty(leftName))
                leftName = RuleRunner.DEFAULT_LEFT_NAME;

            rightNames ??= new string[] { RuleRunner.DEFAULT_RIGHT_NAME };
            if (rightNames.Length == 0)
            {
                throw new ArgumentException(Resources.RightNamesAtLeastOne, nameof(rightNames));
            }

            for (int i = 0; i < rightNames.Length; i++)
            {
                if (string.IsNullOrEmpty(rightNames[i]))
                {
                    rightNames[i] = RuleRunner.DEFAULT_RIGHT_NAME;
                }
            }

            _runner = new Lazy<RuleRunner>(() => new RuleRunner(leftName!, rightNames, strictMode, equalityComparer, includeInternalSymbols, withReferences));
        }

        public virtual IRuleRunner GetRuleRunner()
        {
            return _runner.Value;
        }
    }
}
