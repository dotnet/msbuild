// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Evaluation;

internal interface IItemTypeDefinition
{
    /// <summary>
    /// The item type to which this metadata applies.
    /// </summary>
    string ItemType { get; }
}
