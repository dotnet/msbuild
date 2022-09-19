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
    public abstract class ElementMapper<T> : IElementMapper<T>
    {
        private IEnumerable<CompatDifference>? _differences;

        /// <inheritdoc />
        public T? Left { get; private set; }

        /// <inheritdoc />
        public T?[] Right { get; private set; }

        /// <inheritdoc />
        public MapperSettings Settings { get; }

        /// <summary>
        /// The rule runner to perform api comparison checks.
        /// </summary>
        protected readonly IRuleRunner RuleRunner;

        /// <summary>
        /// Instantiates an object with the provided <see cref="ComparingSettings"/>.
        /// </summary>
        /// <param name="settings">The settings used to diff the elements in the mapper.</param>
        /// <param name="rightSetSize">The number of elements in the right set to compare.</param>
        public ElementMapper(IRuleRunner ruleRunner,
            MapperSettings settings,
            int rightSetSize)
        {
            if (rightSetSize < 1)
                throw new ArgumentOutOfRangeException(nameof(rightSetSize), Resources.ShouldBeGreaterThanZero);

            RuleRunner = ruleRunner;
            Settings = settings;
            Right = new T[rightSetSize];
        }

        /// <inheritdoc />
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

        /// <inheritdoc />
        public T? GetElement(ElementSide side, int setIndex)
        {
            if (side == ElementSide.Left)
            {
                return Left;
            }

            return Right[setIndex];
        }

        /// <inheritdoc />
        public IEnumerable<CompatDifference> GetDifferences()
        {
            return _differences ??= RuleRunner.Run(this);
        }
    }
}
