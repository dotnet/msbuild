// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

#nullable disable

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

        /// <summary>
        /// Sets the given metadata. The operation is equivalent to calling
        /// <see cref="ITaskItem.SetMetadata"/> on all metadata, but takes
        /// advantage of a faster bulk-set operation where applicable. The
        /// implementation may not perform the same parameter validation
        /// as SetMetadata.
        /// </summary>
        /// <param name="metadata">The metadata to set. The keys are assumed
        /// to be unique and values are assumed to be escaped.
        /// </param>
        void ImportMetadata(IEnumerable<KeyValuePair<string, string>> metadata);
    }
}
