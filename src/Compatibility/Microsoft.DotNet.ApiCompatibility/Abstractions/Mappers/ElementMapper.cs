using Microsoft.DotNet.ApiCompatibility.Rules;
using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.DotNet.ApiCompatibility.Abstractions
{
    public class ElementMapper<T>
    {
        private IEnumerable<CompatDifference> _differences;

        public T Left { get; private set; }
        public T Right { get; private set; }

        public DiffingSettings Settings { get; }

        public ElementMapper(DiffingSettings settings)
        {
            Settings = settings;
        }

        public virtual void AddElement(T element, int index)
        {
            if (index < 0 || index > 1)
                throw new ArgumentOutOfRangeException(nameof(index), "index should either be 0 or 1");

            if (index == 0)
            {
                Left = element;
            }
            else
            {
                Right = element;
            }
        }

        public IEnumerable<CompatDifference> GetDifferences()
        {
            return _differences ??= Settings.RuleDriverFactory.GetRuleDriver().Run(this);
        }
    }
}
