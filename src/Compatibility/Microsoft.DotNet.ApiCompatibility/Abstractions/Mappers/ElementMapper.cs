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
        private IReadOnlyList<IEnumerable<CompatDifference>> _differences;

        /// <summary>
        /// Property representing the Left hand side of the mapping.
        /// </summary>
        public T Left { get; private set; }

        /// <summary>
        /// Property representing the Right hand side element(s) of the mapping.
        /// </summary>
        public T[] Right { get; private set; }

        /// <summary>
        /// The <see cref="ComparingSettings"/> used to diff <see cref="Left"/> and <see cref="Right"/>.
        /// </summary>
        public ComparingSettings Settings { get; internal set; }

        /// <summary>
        /// Instantiates an object with the provided <see cref="ComparingSettings"/>.
        /// </summary>
        /// <param name="settings">The settings used to diff the elements in the mapper.</param>
        /// <param name="rightSetSize">The number of elements in the right set to compare.</param>
        public ElementMapper(ComparingSettings settings, int rightSetSize)
        {
            if (rightSetSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(rightSetSize), Resources.ShouldBeGreaterThanZero);

            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            Right = new T[rightSetSize];
        }

        /// <summary>
        /// Adds an element to the given <paramref name="side"/> using the index 0 for <see cref="ElementSide.Right"/>.
        /// </summary>
        /// <param name="element">The element to add.</param>
        /// <param name="side">Value representing the side of the mapping. </param>
        public virtual void AddElement(T element, ElementSide side) => AddElement(element, side, 0);

        /// <summary>
        /// Adds an element to the mapping given the <paramref name="side"/> and the <paramref name="setIndex"/>.
        /// </summary>
        /// <param name="element">The element to add to the mapping.</param>
        /// <param name="side">Value representing the side of the mapping.</param>
        /// <param name="setIndex">Value representing the index the element is added. Only used when adding to <see cref="ElementSide.Right"/>.</param>
        public virtual void AddElement(T element, ElementSide side, int setIndex)
        {
            if (side == ElementSide.Left)
            {
                Left = element;
            }
            else
            {
                if ((uint)setIndex >= Right.Length)
                    throw new ArgumentOutOfRangeException(nameof(setIndex), Resources.IndexShouldBeWithinSetSizeRange);

                Right[setIndex] = element;
            }
        }

        /// <summary>
        /// Runs the rules found by the rule driver on the element mapper and returns a list of differences.
        /// </summary>
        /// <returns>A list containing the list of differences for each possible combination of
        /// (<see cref="ElementMapper{T}.Left"/>, <see cref="ElementMapper{T}.Right"/>).
        /// One list of <see cref="CompatDifference"/> per the number of right elements that the <see cref="ElementMapper{T}"/> contains.</returns>
        public IReadOnlyList<IEnumerable<CompatDifference>> GetDifferences()
        {
            return _differences ??= Settings.RuleRunnerFactory.GetRuleRunner().Run(this);
        }

        internal T GetElement(ElementSide side, int setIndex)
        {
            if (side == ElementSide.Left)
            {
                return Left;
            }

            return Right[setIndex];
        }
    }
}
