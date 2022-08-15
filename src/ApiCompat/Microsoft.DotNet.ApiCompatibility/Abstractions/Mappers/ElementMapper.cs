// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.DotNet.ApiCompatibility.Rules;

namespace Microsoft.DotNet.ApiCompatibility.Abstractions
{
    /// <summary>
    /// Class that represents a mapping in between two objects of type <see cref="T"/>.
    /// </summary>
    public abstract class ElementMapper<T>
    {
        private IReadOnlyList<IEnumerable<CompatDifference>>? _differences;

        /// <summary>
        /// Property representing the Left hand side of the mapping.
        /// </summary>
        public T? Left { get; private set; }

        /// <summary>
        /// Property representing the Right hand side element(s) of the mapping.
        /// </summary>
        public T?[] Right { get; private set; }

        /// <summary>
        /// The <see cref="MapperSettings"/> used to diff <see cref="Left"/> and <see cref="Right"/>.
        /// </summary>
        public MapperSettings Settings { get; }

        /// <summary>
        /// The rule runner to perform api comparison checks.
        /// </summary>
        protected IRuleRunner RuleRunner { get; }

        /// <summary>
        /// Instantiates an object with the provided <see cref="ComparingSettings"/>.
        /// </summary>
        /// <param name="settings">The settings used to diff the elements in the mapper.</param>
        /// <param name="rightSetSize">The number of elements in the right set to compare.</param>
        public ElementMapper(IRuleRunner ruleRunner,
            MapperSettings settings = default,
            int rightSetSize = 1)
        {
            if (rightSetSize < 1)
                throw new ArgumentOutOfRangeException(nameof(rightSetSize), Resources.ShouldBeGreaterThanZero);

            RuleRunner = ruleRunner;
            Settings = settings;
            Right = new T[rightSetSize];
        }

        /// <summary>
        /// Adds an element to the mapping given the <paramref name="side"/> and the <paramref name="setIndex"/>.
        /// </summary>
        /// <param name="element">The element to add to the mapping.</param>
        /// <param name="side">Value representing the side of the mapping.</param>
        /// <param name="setIndex">Value representing the index the element is added. Only used when adding to <see cref="ElementSide.Right"/>.</param>
        public virtual void AddElement(T element, ElementSide side, int setIndex = 0)
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
        /// Returns the element from the specified <see cref="ElementSide"/> and index.
        /// </summary>
        /// <param name="side">Value representing the side of the mapping.</param>
        /// <param name="setIndex">Value representing the index the element is retrieved. Only used when adding to <see cref="ElementSide.Right"/>.</param>
        /// <returns></returns>
        public T? GetElement(ElementSide side, int setIndex)
        {
            if (side == ElementSide.Left)
            {
                return Left;
            }

            return Right[setIndex];
        }

        /// <summary>
        /// Runs the rules found by the rule driver on the element mapper and returns a list of differences.
        /// </summary>
        /// <returns>A list containing the list of differences for each possible combination of
        /// (<see cref="ElementMapper{T}.Left"/>, <see cref="ElementMapper{T}.Right"/>).
        /// One list of <see cref="CompatDifference"/> per the number of right elements that the <see cref="ElementMapper{T}"/> contains.</returns>
        public IReadOnlyList<IEnumerable<CompatDifference>> GetDifferences()
        {
            return _differences ??= RuleRunner.Run(this);
        }
    }
}
