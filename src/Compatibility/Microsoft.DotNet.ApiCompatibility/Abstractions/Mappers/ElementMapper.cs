// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

namespace Microsoft.DotNet.ApiCompatibility.Abstractions
{
    /// <summary>
    /// Class that represents a mapping in between two objects of type <see cref="T"/>.
    /// </summary>
    public class ElementMapper<T>
    {
        private IEnumerable<CompatDifference> _differences;

        /// <summary>
        /// Property representing the Left hand side of the mapping.
        /// </summary>
        public T Left { get; private set; }

        /// <summary>
        /// Property representing the Right hand side of the mapping.
        /// </summary>
        public T Right { get; private set; }

        /// <summary>
        /// The <see cref="ComparingSettings"/> used to diff <see cref="Left"/> and <see cref="Right"/>.
        /// </summary>
        public ComparingSettings Settings { get; }

        /// <summary>
        /// Instantiates an object with the provided <see cref="ComparingSettings"/>.
        /// </summary>
        /// <param name="settings">The settings used to diff the elements in the mapper.</param>
        public ElementMapper(ComparingSettings settings)
        {
            Settings = settings;
        }

        /// <summary>
        /// Adds an element to the mapping given the index, 0 (Left) or 1 (Right).
        /// </summary>
        /// <param name="element">The element to add to the mapping.</param>
        /// <param name="index">Index representing the side of the mapping, 0 (Left) or 1 (Right).</param>
        public virtual void AddElement(T element, int index)
        {
            if ((uint)index > 1)
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

        /// <summary>
        /// Runs the rules found by the rule driver on the element mapper and returns a list of differences.
        /// </summary>
        /// <returns>The list of <see cref="CompatDifference"/>.</returns>
        public IEnumerable<CompatDifference> GetDifferences()
        {
            return _differences ??= Settings.RuleRunnerFactory.GetRuleRunner().Run(this);
        }
    }
}
