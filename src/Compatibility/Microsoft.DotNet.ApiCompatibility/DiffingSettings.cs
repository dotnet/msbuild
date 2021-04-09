// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiCompatibility.Abstractions;
using Microsoft.DotNet.ApiCompatibility.Rules;
using System.Collections.Generic;

namespace Microsoft.DotNet.ApiCompatibility
{
    public class DiffingSettings
    {
        public IRuleDriverFactory RuleDriverFactory { get; }
        public IDiffingFilter Filter { get; }
        public IEqualityComparer<ISymbol> EqualityComparer { get; }

        public DiffingSettings(IRuleDriverFactory ruleDriverFactory = null, IDiffingFilter filter = null, IEqualityComparer<ISymbol> equalityComparer = null)
        {
            RuleDriverFactory = ruleDriverFactory ?? new RuleDriverFactory();
            Filter = filter ?? new AccessibilityFilter(includeInternalSymbols: false);
            EqualityComparer = equalityComparer ?? new DefaultSymbolsEqualityComparer();
        }
    }
}
