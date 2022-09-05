// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;

namespace Microsoft.DotNet.ApiCompatibility.Abstractions
{
    /// <summary>
    /// Provider to retrieve the stream of a <see cref="MetadataInformation"/>.
    /// </summary>
    public interface IMetadataStreamProvider
    {
        /// <summary>
        /// Get the stream from a <see cref="MetadataInformation"/>.
        /// </summary>
        /// <param name="metadata">Pass in a <see cref="MetadataInformation"/> to be read.</param>
        /// <returns>Returns the stream from a provided <see cref="MetadataInformation"/> object.</returns>
        Stream GetStream(MetadataInformation metadata);
    }
}
