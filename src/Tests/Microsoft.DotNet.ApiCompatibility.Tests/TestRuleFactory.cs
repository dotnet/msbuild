// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.ApiCompatibility.Rules;

namespace Microsoft.DotNet.ApiCompatibility.Tests
{
    internal class TestRuleFactory : IRuleFactory
    {
        private readonly HashSet<Func<IRuleSettings, IRuleRegistrationContext, IRule>> _ruleFactories;

        public TestRuleFactory(params Func<IRuleSettings, IRuleRegistrationContext, IRule>[] ruleFactories)
        {
            _ruleFactories = new HashSet<Func<IRuleSettings, IRuleRegistrationContext, IRule>>(ruleFactories);
        }

        public IRule[] CreateRules(IRuleSettings settings, IRuleRegistrationContext context)
        {
            List<IRule> rules = new();
            foreach (Func<IRuleSettings, IRuleRegistrationContext, IRule> ruleFactory in _ruleFactories)
            {
                rules.Add(ruleFactory(settings, context));
            }

            return rules.ToArray();
        }

        public TestRuleFactory WithRule(Func<IRuleSettings, IRuleRegistrationContext, IRule> ruleFactory)
        {
            var rules = new Func<IRuleSettings, IRuleRegistrationContext, IRule>[_ruleFactories.Count + 1];
            _ruleFactories.CopyTo(rules);
            rules[rules.Length - 1] = ruleFactory;

            return new TestRuleFactory(rules);
        }
    }
}
