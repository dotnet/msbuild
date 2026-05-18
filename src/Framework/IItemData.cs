// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
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
/// <remarks>
/// The underlying data can be of various distinct types - so it needs to be accessed via provided accessor methods
/// </remarks>
public readonly struct ItemData
{
    private readonly Func<IEnumerable<KeyValuePair<string, string>>> _enumerateMetadata;

    public ItemData(string type, object value)
    {

        Type = type;
        Value = value;

        // The ProjectEvaluationFinishedEventArgs.Items are currently assigned only in Evaluator.Evaluate()
        //  where the only types that can be assigned are ProjectItem or ProjectItemInstance
        // However! NodePacketTranslator and BuildEventArgsReader might deserialize those as TaskItemData
        //  (see xml comments of TaskItemData for details)
        if (value is IItemData dt)
        {
            EvaluatedInclude = dt.EvaluatedInclude;
            _enumerateMetadata = dt.EnumerateMetadata;
        }
        else if (value is ITaskItem ti)
        {
            EvaluatedInclude = ti.ItemSpec;
            _enumerateMetadata = ti.EnumerateMetadata;
        }
        else
        {
            EvaluatedInclude = value.ToString() ?? string.Empty;
            _enumerateMetadata = () => [];
        }
    }

    /// <summary>
    /// The type of property - e.g. 'PackageReference'.
    /// </summary>
    public string Type { get; private init; }

    /// <summary>
    /// The actual value of item - e.g. 'System.Text.Json'.
    /// This can be of a distinct types, hence the helper methods <see cref="EvaluatedInclude"/> and <see cref="EnumerateMetadata"/>
    ///  are recommended for accessing the data
    /// </summary>
    internal object? Value { get; private init; }

    /// <summary>
    /// Gets the item evaluated include data. It is in fact a 'specification' of the item (e.g. path on disk to a specific ProjectReference)
    /// </summary>
    public string EvaluatedInclude { get; private init; }

    /// <summary>
    /// The item metadata
    /// </summary>
    public IEnumerable<KeyValuePair<string, string>> EnumerateMetadata()
        => _enumerateMetadata();
}
