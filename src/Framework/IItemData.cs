// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace Microsoft.Build.Framework;

/// <summary>
/// Represents an item data (per single item type) that might be returned via <see cref="ProjectEvaluationFinishedEventArgs"/> or <see cref="ProjectStartedEventArgs"/>.
/// </summary>
public interface IItemData
{
    /// <summary>
    /// Gets the item evaluated include data. It is in fact a 'specification' of the item (e.g. path on disk to a specific ProjectReference)
    /// </summary>
    /// <remarks>
    /// This should be named "EvaluatedInclude" but that would be a breaking change to the upstream interface.
    /// </remarks>
    string ItemSpec
    {
        get;
    }
}

/// <summary>
/// Represents a metadata that can natively provide it's metadata.
/// </summary>
public interface IItemDataWithMetadata : IItemData
{
    /// <summary>
    /// The item metadata
    /// </summary>
    IEnumerable<KeyValuePair<string, string>> EnumerateMetadata();
}
