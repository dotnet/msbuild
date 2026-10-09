// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Framework;

/// <summary>
/// Identifies the source location of an MSBuild item.
/// </summary>
[Serializable]
public readonly struct TaskItemLocation : IMSBuildElementLocation
{
    private readonly string? _file;

    /// <summary>
    /// Initializes a new item source location.
    /// </summary>
    public TaskItemLocation(string? file, int line, int column)
    {
        if (line < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(line));
        }

        if (column < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(column));
        }

        _file = file;
        Line = line;
        Column = column;
    }

    /// <inheritdoc/>
    public string File => _file ?? string.Empty;

    /// <inheritdoc/>
    public int Line { get; }

    /// <inheritdoc/>
    public int Column { get; }

    /// <inheritdoc/>
    public string LocationString => Line switch
    {
        > 0 when Column > 0 => $"{File} ({Line},{Column})",
        > 0 => $"{File} ({Line})",
        _ => File,
    };
}
