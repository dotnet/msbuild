// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

#nullable disable

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// Implemented by item types that keep metadata originating in item definitions separate from
    /// metadata set directly on the item.
    /// </summary>
    /// <remarks>
    /// Item definition metadata may reference built-in metadata (for example
    /// <c>&lt;OutputName&gt;%(Filename)&lt;/OutputName&gt;</c>). Such values are stored unexpanded and
    /// expanded on read, so that they track the item spec they are derived from. Flattening the two
    /// collections together loses that distinction, so anything that needs to reproduce the item
    /// faithfully (for example marshalling it to an out-of-proc task host) has to keep them apart.
    /// </remarks>
    internal interface IItemDefinitionMetadataProvider
    {
        /// <summary>
        /// Gets a value indicating whether any item definition metadata may contain an expression
        /// that is expanded on read.
        /// </summary>
        bool HasExpandableItemDefinitionMetadata { get; }

        /// <summary>
        /// Enumerates metadata set directly on the item, escaped. These values are never expanded on read.
        /// </summary>
        IEnumerable<KeyValuePair<string, string>> EnumerateDirectMetadataEscaped();

        /// <summary>
        /// Enumerates metadata inherited from item definitions, escaped and <em>unexpanded</em>.
        /// Values masked by direct metadata are not included.
        /// </summary>
        IEnumerable<KeyValuePair<string, string>> EnumerateItemDefinitionMetadataEscaped();
    }
}
