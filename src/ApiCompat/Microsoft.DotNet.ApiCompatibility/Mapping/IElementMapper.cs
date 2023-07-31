// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.ApiCompatibility.Mapping
{
    /// <summary>
    /// Interface that represents a mapping in between two objects.
    /// </summary>
    public interface IElementMapper<T>
    {
        /// <summary>
        /// Property representing the Left hand side of the mapping.
        /// </summary>
        T? Left { get; }

        /// <summary>
        /// Property representing the Right hand side element(s) of the mapping.
        /// </summary>
        T?[] Right { get; }

        /// <summary>
        /// The <see cref="IMapperSettings"/> used to diff <see cref="Left"/> and <see cref="Right"/>.
        /// </summary>
        IMapperSettings Settings { get; }

        /// <summary>
        /// Adds an element to the mapping given the <paramref name="side"/> and the <paramref name="setIndex"/>.
        /// </summary>
        /// <param name="element">The element to add to the mapping.</param>
        /// <param name="side">Value representing the side of the mapping.</param>
        /// <param name="setIndex">Value representing the index the element is added. Only used when adding to <see cref="ElementSide.Right"/>.</param>
        void AddElement(T element, ElementSide side, int setIndex = 0);

        /// <summary>
        /// Returns the element from the specified <see cref="ElementSide"/> and index.
        /// </summary>
        /// <param name="side">Value representing the side of the mapping.</param>
        /// <param name="setIndex">Value representing the index the element is retrieved. Only used when adding to <see cref="ElementSide.Right"/>.</param>
        T? GetElement(ElementSide side, int setIndex);

        /// <summary>
        /// Runs the rules found by the rule driver on the element mapper and returns a list of differences.
        /// </summary>
        /// <returns>A list containing the list of differences for each possible combination of
        /// (<see cref="IElementMapper{T}.Left"/>, <see cref="IElementMapper{T}.Right"/>).
        /// One list of <see cref="CompatDifference"/> per the number of right elements that the <see cref="IElementMapper{T}"/> contains.</returns>
        IEnumerable<CompatDifference> GetDifferences();
    }
}
