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
