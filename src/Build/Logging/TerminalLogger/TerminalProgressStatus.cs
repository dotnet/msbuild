// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Logging;

internal sealed class TerminalProgressStatus
{
    internal TerminalProgressStatus(TaskProgressStartedEventArgs progress, int nodeIndex, TerminalProgressStatus? parent = null)
    {
        OperationId = progress.OperationId;
        BuildEventContext = progress.BuildEventContext;
        NodeIndex = nodeIndex;
        Title = Sanitize(progress.Title);
        Unit = progress.Unit;
        Parent = parent;
        Depth = parent is null ? 0 : parent.Depth + 1;
        Retention = progress.Retention;
    }

    internal long OperationId { get; }
    internal BuildEventContext? BuildEventContext { get; }

    /// <summary>
    /// The node running the task that reported this operation, so the renderer can show the operation
    /// with the project it belongs to. A negative value means the node is unknown.
    /// </summary>
    internal int NodeIndex { get; }

    /// <summary>
    /// The operation this operation is nested in, or <see langword="null"/> for a top-level operation.
    /// </summary>
    internal TerminalProgressStatus? Parent { get; }

    /// <summary>
    /// The number of operations this operation is nested in. Each level indents the line once more.
    /// </summary>
    internal int Depth { get; }

    internal TaskProgressNestedRetention Retention { get; }

    /// <summary>
    /// The nested operations that are shown below this operation, in the order they started.
    /// </summary>
    internal List<TerminalProgressStatus> Children { get; } = [];

    /// <summary>
    /// Gets a value indicating whether the operation ended and the display keeps its final state.
    /// </summary>
    internal bool IsFinished { get; private set; }

    internal string Title { get; }
    internal TaskProgressUnit Unit { get; }
    internal long Completed { get; private set; }
    internal long? Total { get; private set; }
    internal string? Status { get; private set; }
    internal long Sequence { get; private set; }

    internal void Update(TaskProgressUpdatedEventArgs progress)
    {
        if (IsFinished || progress.Sequence < Sequence)
        {
            return;
        }

        Sequence = progress.Sequence;
        Completed = progress.Completed;
        Total = progress.Total;
        Status = Sanitize(progress.Status);
    }

    internal void Finish(TaskProgressFinishedEventArgs progress)
    {
        if (IsFinished || progress.Sequence < Sequence)
        {
            return;
        }

        IsFinished = true;
        Sequence = progress.Sequence;
        Completed = progress.Completed;
        Total = progress.Total;
        Status = Sanitize(progress.Summary);
    }

    /// <summary>
    /// Adds this operation and its nested operations to <paramref name="lines"/>, each nested operation directly below its parent.
    /// </summary>
    internal void AppendInDisplayOrder(List<TerminalProgressStatus> lines)
    {
        lines.Add(this);
        foreach (TerminalProgressStatus child in Children)
        {
            child.AppendInDisplayOrder(lines);
        }
    }

    internal string Render(int width)
    {
        string amount = TaskProgressFormatter.FormatAmount(Completed, Total, Unit);
        string progress = Total is long total && total > 0
            ? $"{RenderBar(Completed, total)} {amount}"
            : amount;

        // A second level of indentation shows that the operation belongs to the project above it. A nested
        // operation is one level further in, and a corner glyph connects it to the operation above it.
        string indent = Depth == 0
            ? TerminalLogger.DoubleIndentation
            : $"{TerminalLogger.DoubleIndentation}{new string(' ', NestedIndentWidth * (Depth - 1))}{NestedGlyph}";
        string text = Status is { Length: > 0 }
            ? $"{indent}[{progress}] {Title}: {Status}"
            : $"{indent}[{progress}] {Title}";

        return text.Length <= width ? text : text.Substring(0, Math.Max(width - 1, 0)) + (width > 0 ? "…" : string.Empty);
    }

    /// <summary>
    /// The glyph that connects a nested operation to its parent. Its width is the indentation of one nesting level,
    /// so the glyph of the next level starts below the text of this level.
    /// </summary>
    internal const string NestedGlyph = "└─ ";

    private const int NestedIndentWidth = 3;

    /// <summary>
    /// Renders the bar without brackets of its own, because <see cref="Render"/> already wraps the
    /// whole progress field in a single pair.
    /// </summary>
    private static string RenderBar(long completed, long total)
    {
        long boundedCompleted = Math.Min(Math.Max(completed, 0), total);
        int filled = (int)(boundedCompleted * 10m / total);
        return $"{new string('#', filled)}{new string('-', 10 - filled)}";
    }

    private static string Sanitize(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        string text = value!;
        char[] buffer = new char[text.Length];
        int count = 0;
        foreach (char character in text)
        {
            if (!char.IsControl(character))
            {
                buffer[count++] = character;
            }
        }

        return new string(buffer, 0, count);
    }
}
