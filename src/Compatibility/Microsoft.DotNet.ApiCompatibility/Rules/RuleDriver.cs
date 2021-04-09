using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiCompatibility.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.DotNet.ApiCompatibility.Rules
{
    public interface IRuleDriverFactory
    {
        IRuleDriver GetRuleDriver();
    }

    public class RuleDriverFactory : IRuleDriverFactory
    {
        private RuleDriver _driver;
        public IRuleDriver GetRuleDriver()
        {
            if (_driver == null)
                _driver = new RuleDriver();

            return _driver;
        }
    }

    public interface IRuleDriver
    {
        IEnumerable<CompatDifference> Run<T>(ElementMapper<T> mapper);
    }

    internal class RuleDriver : IRuleDriver
    {
        private readonly IEnumerable<Rule> _rules;

        internal RuleDriver()
        {
            _rules = GetRules();
        }

        public IEnumerable<CompatDifference> Run<T>(ElementMapper<T> mapper)
        {
            List<CompatDifference> differences = new();
            if (mapper is AssemblyMapper am)
            {
                Run(am, differences);
            }
            if (mapper is TypeMapper tm)
            {
                Run(tm, differences);
            }
            if (mapper is MemberMapper mm)
            {
                Run(mm, differences);
            }

            return differences;
        }

        private void Run(AssemblyMapper mapper, List<CompatDifference> differences)
        {
            foreach (Rule rule in _rules)
            {
                rule.Run(mapper, differences);
            }
        }

        private void Run(TypeMapper mapper, List<CompatDifference> differences)
        {
            foreach (Rule rule in _rules)
            {
                rule.Run(mapper, differences);
            }
        }

        private void Run(MemberMapper mapper, List<CompatDifference> differences)
        {
            foreach (Rule rule in _rules)
            {
                rule.Run(mapper, differences);
            }
        }

        private IEnumerable<Rule> GetRules()
        {
            return this.GetType().Assembly.GetTypes()
                .Where(t => !t.IsAbstract && typeof(Rule).IsAssignableFrom(t))
                .Select(t => (Rule)Activator.CreateInstance(t));
        }
    }
}
