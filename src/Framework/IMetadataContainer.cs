// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// Provides a way to efficiently enumerate item metadata
    /// </summary>
    internal interface IMetadataContainer
    {
        /// <summary>
        /// Returns a list of metadata names and unescaped values, including
        /// metadata from item definition groups, but not including built-in
        /// metadata. Implementations should be low-overhead as the method
        /// is used for serialization (in node packet translator) as well as
        /// in the binary logger.
        /// </summary>
        IEnumerable<KeyValuePair<string, string>> EnumerateMetadata();
    }
}
