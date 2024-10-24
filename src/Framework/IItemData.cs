// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace Microsoft.Build.Framework;

/// <summary>
/// Represents a metadata that can natively provide it's metadata.
/// </summary>
public interface IItemData
{
    /// <summary>
    /// Gets the item evaluated include data. It is in fact a 'specification' of the item (e.g. path on disk to a specific ProjectReference)
    /// </summary>
    string EvaluatedInclude
    {
        get;
    }

    /// <summary>
    /// The item metadata
    /// </summary>
    IEnumerable<KeyValuePair<string, string>> EnumerateMetadata();
}


/// <summary>
/// Structure defining single MSBuild property instance.
/// </summary>
/// <param name="Name">The name of property - e.g. 'TargetFramework'.</param>
/// <param name="Value">The actual value of property - e.g. 'net9'.</param>
public readonly record struct PropertyData(string Name, string Value);

/// <summary>
/// Structure defining single MSBuild item instance.
/// </summary>
/// <param name="Type">The type of property - e.g. 'PackageReference'.</param>
/// <param name="Value">The actual value of item - e.g. 'System.Text.Json'.</param>
public readonly record struct ItemData(string Type, IItemData Value);
