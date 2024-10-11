// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Framework;


/// <summary>
/// Represents an item data (per single item type) that might be returned via <see cref="ProjectEvaluationFinishedEventArgs"/> or <see cref="ProjectStartedEventArgs"/>.
/// </summary>
public interface IItemData
{
    ///// <summary>
    ///// The item evaluated include value
    ///// </summary>
    //string EvaluatedInclude { get; }

    /// <summary>
    /// Gets or sets the item "specification" e.g. for disk-based items this would be the file path.
    /// </summary>
    /// <remarks>
    /// This should be named "EvaluatedInclude" but that would be a breaking change to this interface.
    /// </remarks>
    /// <value>The item-spec string.</value>
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
