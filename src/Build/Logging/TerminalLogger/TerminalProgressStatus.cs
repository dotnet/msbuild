// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Logging;

internal sealed class TerminalProgressStatus
{
    internal TerminalProgressStatus(TaskProgressStartedEventArgs progress, int nodeIndex)
    {
        OperationId = progress.OperationId;
        NodeIndex = nodeIndex;
        Title = Sanitize(progress.Title);
        Unit = progress.Unit;
    }

    internal long OperationId { get; }

    /// <summary>
    /// The node running the task that reported this operation, so the renderer can show the operation
    /// with the project it belongs to. A negative value means the node is unknown.
    /// </summary>
    internal int NodeIndex { get; }

    internal string Title { get; }
    internal TaskProgressUnit Unit { get; }
    internal long Completed { get; private set; }
    internal long? Total { get; private set; }
    internal string? Status { get; private set; }
    internal long Sequence { get; private set; }

    internal void Update(TaskProgressUpdatedEventArgs progress)
    {
        if (progress.Sequence < Sequence)
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
        if (progress.Sequence < Sequence)
        {
            return;
        }

        Sequence = progress.Sequence;
        Completed = progress.Completed;
        Total = progress.Total;
        Status = Sanitize(progress.Summary);
    }

    internal string Render(int width)
    {
        string amount = TaskProgressFormatter.FormatAmount(Completed, Total, Unit);
        string progress = Total is long total && total > 0
            ? $"{RenderBar(Completed, total)} {amount}"
            : amount;

        // A second level of indentation shows that the operation belongs to the project above it.
        string indent = TerminalLogger.Indentation + TerminalLogger.Indentation;
        string text = Status is { Length: > 0 }
            ? $"{indent}[{progress}] {Title}: {Status}"
            : $"{indent}[{progress}] {Title}";

        return text.Length <= width ? text : text.Substring(0, Math.Max(width - 1, 0)) + (width > 0 ? "…" : string.Empty);
    }

    /// <summary>
    /// Renders the bar without brackets of its own, because <see cref="Render"/> already wraps the
    /// whole progress field in a single pair.
    /// </summary>
    private static string RenderBar(long completed, long total)
    {
        long boundedCompleted = Math.Min(Math.Max(completed, 0), total);
        int filled = (int)(boundedCompleted * 10 / total);
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
