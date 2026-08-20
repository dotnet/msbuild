// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

#nullable disable

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// Implemented by item types whose metadata may include item definition values that are expanded on read
    /// rather than stored expanded.
    /// </summary>
    /// <remarks>
    /// Item definition metadata may reference built-in metadata, for example
    /// <c>&lt;OutputName&gt;%(Filename)&lt;/OutputName&gt;</c>. Such values are stored unexpanded so that they track
    /// the item spec they derive from. Anything that needs to reproduce the item faithfully - notably marshalling
    /// it to an out-of-proc task host - has to know which values those are, since a flattened copy freezes them.
    /// </remarks>
    internal interface IItemDefinitionMetadataProvider
    {
        /// <summary>
        /// Gets a value indicating whether any item definition metadata may contain an expression that is
        /// expanded on read. When false there is nothing to preserve and the flattened form is exact.
        /// </summary>
        bool HasExpandableItemDefinitionMetadata { get; }

        /// <summary>
        /// Enumerates only the item definition metadata that may contain an expression, escaped and unexpanded.
        /// Values masked by metadata set directly on the item are excluded, since those win and are never expanded.
        /// </summary>
        /// <remarks>
        /// Deliberately narrow: everything else is already correct in the flattened copy, so only these entries
        /// need to be carried separately. In practice this is a small handful of values.
        /// </remarks>
        IEnumerable<KeyValuePair<string, string>> EnumerateExpandableItemDefinitionMetadataEscaped();
    }
}
