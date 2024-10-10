// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Logging;


/// <summary>
/// Represents an item data (per single item type) that van be returned via <see cref="ProjectEvaluationFinishedEventArgs"/> or <see cref="ProjectStartedEventArgs"/>.
/// </summary>
public interface IItemData
{
    /// <summary>
    /// The item evaluated include value
    /// </summary>
    string EvaluatedInclude { get; }

    /// <summary>
    /// The item metadata
    /// </summary>
    IEnumerable<KeyValuePair<string, string>> EnumerateMetadata();
}
