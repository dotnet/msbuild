// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

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
