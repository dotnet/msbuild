// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;

namespace Microsoft.DotNet.ApiCompatibility.Abstractions
{
    /// <summary>
    /// Class to wrap an Element of T with it's <see cref="MetadataInformation"/>.
    /// </summary>
    /// <typeparam name="T">The type of the Element that is holded</typeparam>
    public readonly struct ElementContainer<T> where T : ISymbol
    {
        /// <summary>
        /// The element that the container is holding.
        /// </summary>
        public T Element { get; }

        /// <summary>
        /// The metadata associated to the element.
        /// </summary>
        public MetadataInformation MetadataInformation { get; }

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
