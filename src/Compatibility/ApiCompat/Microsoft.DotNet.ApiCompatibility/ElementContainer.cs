// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;

namespace Microsoft.DotNet.ApiCompatibility
{
    /// <summary>
    /// Class to wrap an Element of T with it's <see cref="MetadataInformation"/>.
    /// </summary>
    /// <typeparam name="T">The type of the Element that is held</typeparam>
    public class ElementContainer<T> where T : ISymbol
    {
        /// <summary>
        /// The element that the container is holding.
        /// </summary>
        public readonly T Element;

        /// <summary>
        /// The metadata associated to the element.
        /// </summary>
        public readonly MetadataInformation MetadataInformation;

        /// <summary>
        /// Instantiates a new object with the <paramref name="element"/> and <paramref name="metadataInformation"/> used.
        /// </summary>
        /// <param name="element">Element to store in the container.</param>
        /// <param name="metadataInformation">Metadata related to the element</param>
        public ElementContainer(T element, MetadataInformation metadataInformation)
        {
            Element = element;
            MetadataInformation = metadataInformation;
        }
    }
}
