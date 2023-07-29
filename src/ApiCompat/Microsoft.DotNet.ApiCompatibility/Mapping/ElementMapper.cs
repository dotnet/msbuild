// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.ApiCompatibility.Rules;

namespace Microsoft.DotNet.ApiCompatibility.Mapping
{
    /// <summary>
    /// Class that represents a mapping in between two objects.
    /// </summary>
    public abstract class ElementMapper<T> : IElementMapper<T>
    {
        private IEnumerable<CompatDifference>? _differences;

        /// <inheritdoc />
        public T? Left { get; private set; }

        /// <inheritdoc />
        public T?[] Right { get; private set; }

        /// <inheritdoc />
        public IMapperSettings Settings { get; }

        /// <summary>
        /// The rule runner to perform api comparison checks.
        /// </summary>
        protected readonly IRuleRunner RuleRunner;

        /// <summary>
        /// Instantiates an element mapper.
        /// </summary>
        /// <param name="ruleRunner">The <see cref="IRuleRunner"/> that compares the mapper elements.</param>
        /// <param name="settings">The <see cref="IMapperSettings"/> used to compare the mapper elements.</param>
        /// <param name="rightSetSize">The number of elements in the right set to compare.</param>
        protected ElementMapper(IRuleRunner ruleRunner,
            IMapperSettings settings,
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
