// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Framework.Logging;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Logging;

/// <summary>
/// Capture states on nodes to be rendered on display.
/// </summary>
internal sealed class TerminalNodesFrame
{
    private const int MaxColumn = 120;

    private readonly (TerminalNodeStatus nodeStatus, int durationLength, int renderedWidth, bool canUpdateDuration)[] _nodes;

    /// <summary>
    /// The node index each entry in <see cref="_nodes"/> came from, so progress operations can be
    /// matched to the row of the project that reported them.
    /// </summary>
    private readonly int[] _nodeIndexes;

    private readonly TerminalProgressStatus[] _progress;

    private readonly StringBuilder _renderBuilder = new();

    public int Width { get; }
    public int Height { get; }
    public int NodesCount { get; private set; }
    public int ProgressCount => _progress.Length;

    /// <summary>
    /// The width of the terminal this frame was rendered for, before it is capped to <see cref="MaxColumn"/>.
    /// </summary>
    /// <remarks>
    /// <see cref="Width"/> drives layout, but line wrapping happens at the real terminal width, so that
    /// is what <see cref="GetPhysicalRows"/> needs.
    /// </remarks>
    public int TerminalWidth { get; }

    public TerminalNodesFrame(TerminalNodeStatus?[] nodes, IReadOnlyCollection<TerminalProgressStatus> progress, int width, int height)
    {
        Width = Math.Min(width, MaxColumn);
        TerminalWidth = width;
        Height = height;

        _nodes = new (TerminalNodeStatus, int, int, bool)[nodes.Length];
        _nodeIndexes = new int[nodes.Length];

        for (int nodeIndex = 0; nodeIndex < nodes.Length; nodeIndex++)
        {
            if (nodes[nodeIndex] is TerminalNodeStatus status)
            {
                _nodeIndexes[NodesCount] = nodeIndex;
                _nodes[NodesCount++].nodeStatus = status;
            }
        }

        _progress = [.. progress];
    }

    public TerminalNodesFrame(TerminalNodeStatus?[] nodes, int width, int height)
        : this(nodes, Array.Empty<TerminalProgressStatus>(), width, height)
    {
    }

    internal ReadOnlySpan<char> RenderNodeStatus(int i)
    {
        TerminalNodeStatus status = _nodes[i].nodeStatus;

        string durationString = ResourceUtilities.FormatResourceStringIgnoreCodeAndKeyword(
            "DurationDisplay",
            status.Stopwatch.ElapsedSeconds);

        _nodes[i].durationLength = durationString.Length;

        string project = status.Project;
        string? targetFramework = status.TargetFramework;
        string? runtimeIdentifier = status.RuntimeIdentifier;
        string target = status.Target;
        string? targetPrefix = status.TargetPrefix;
        TerminalColor targetPrefixColor = status.TargetPrefixColor;

        int targetWithoutAnsiLength = !string.IsNullOrWhiteSpace(targetPrefix)
            // +1 because we will join them by space in the final output.
            ? targetPrefix!.Length + 1 + target.Length
            : target.Length;

        int renderedWidth = Length(durationString, project, targetFramework, runtimeIdentifier, targetWithoutAnsiLength);

        if (renderedWidth > Width)
        {
            renderedWidth -= targetWithoutAnsiLength;
            targetPrefix = target = string.Empty;
            targetWithoutAnsiLength = 0;

            if (renderedWidth > Width)
            {
                int lastDotInProject = project.LastIndexOf('.');
                renderedWidth -= lastDotInProject;
                project = project.Substring(lastDotInProject + 1);

                if (renderedWidth > Width)
                {
                    _nodes[i].renderedWidth = project.Length;
                    return project.AsSpan();
                }
            }
        }

        // SetCursorHorizontal(MaxColumn) makes the logical line occupy the full layout width.
        _nodes[i].renderedWidth = Math.Max(Width, 1);
        _nodes[i].canUpdateDuration = true;
        var renderedTarget = !string.IsNullOrWhiteSpace(targetPrefix) ? $"{AnsiCodes.Colorize(targetPrefix, targetPrefixColor)} {target}" : target;
        var builder = StringBuilderCache.Acquire(renderedWidth);
        builder.Append(TerminalLogger.Indentation).Append(project);
        if (!string.IsNullOrWhiteSpace(targetFramework))
        {
            builder.Append(' ').Append(AnsiCodes.Colorize(targetFramework, TerminalLogger.TargetFrameworkColor));
        }
        if (!string.IsNullOrWhiteSpace(runtimeIdentifier))
        {
            builder.Append(' ').Append(AnsiCodes.Colorize(runtimeIdentifier, TerminalLogger.RuntimeIdentifierColor));
        }
        builder.Append(' ').Append(AnsiCodes.SetCursorHorizontal(MaxColumn))
               .Append(AnsiCodes.MoveCursorBackward(targetWithoutAnsiLength + durationString.Length + 1))
               .Append(renderedTarget)
               .Append(' ')
               .Append(durationString);
        var span = builder.ToString().AsSpan();
        StringBuilderCache.Release(builder);
        return span;

        static int Length(string durationString, string project, string? targetFramework, string? runtimeIdentifier, int targetWithoutAnsiLength) =>
                TerminalLogger.Indentation.Length +
                project.Length + 1 +
                (targetFramework?.Length ?? -1) + 1 +
                (runtimeIdentifier?.Length ?? -1) + 1 +
                targetWithoutAnsiLength + 1 +
                durationString.Length;
    }

    /// <summary>
    /// Render VT100 string to update from current to next frame.
    /// </summary>
    public string Render(TerminalNodesFrame previousFrame)
    {
        StringBuilder sb = _renderBuilder;
        sb.Clear();

        // Move cursor back to 1st line of nodes. The previous frame's lines are reflowed by the
        // terminal when it is resized, so measure them against the width we are rendering for now.
        int previousPhysicalRows = previousFrame.GetPhysicalRows(TerminalWidth);
        sb.AppendLine($"{AnsiCodes.CSI}{previousPhysicalRows + 1}{AnsiCodes.MoveUpToLineStart}");

        // More physical rows than logical lines means something wrapped, and the per-line diff below
        // cannot track where those extra rows landed.
        bool previousFrameWrapped = previousPhysicalRows > previousFrame.NodesCount + previousFrame.ProgressCount;
        bool redrawAll = previousFrameWrapped || ProgressLayoutShifts(previousFrame);
        if (redrawAll)
        {
            sb.Append($"{AnsiCodes.CSI}{AnsiCodes.EraseInDisplay}");
        }

        int i = 0;
        for (; i < NodesCount; i++)
        {
            ReadOnlySpan<char> needed = RenderNodeStatus(i);

            if (!redrawAll
                && previousFrame.NodesCount > 0
                && _nodes[i].renderedWidth > TerminalWidth)
            {
                sb.Append($"{AnsiCodes.CSI}{AnsiCodes.EraseInDisplay}");
                redrawAll = true;
            }

            // Do we have previous node string to compare with?
            if (!redrawAll && previousFrame.NodesCount > i)
            {
                if (previousFrame._nodes[i] == _nodes[i]
                    && _nodes[i].canUpdateDuration)
                {
                    // Same everything except time, AND same number of digits in time
                    string durationString = ResourceUtilities.FormatResourceStringIgnoreCodeAndKeyword("DurationDisplay", _nodes[i].nodeStatus.Stopwatch.ElapsedSeconds);
                    sb.Append($"{AnsiCodes.SetCursorHorizontal(MaxColumn)}{AnsiCodes.MoveCursorBackward(durationString.Length)}{durationString}");
                }
                else
                {
                    // TODO: check components to figure out skips and optimize this
                    sb.Append($"{AnsiCodes.CSI}{AnsiCodes.EraseInLine}");
                    sb.Append(needed);
                }
            }
            else
            {
                // From now on we have to simply WriteLine
                sb.Append(needed);
            }

            // Next line
            sb.AppendLine();

            // Show this project's operations underneath it. A trailing block would not say which
            // project each operation belongs to, and several projects often download similarly
            // named files at once.
            RenderProgressForNode(sb, _nodeIndexes[i], redrawAll);
        }

        // Operations whose project is no longer displayed still have to go somewhere.
        RenderOrphanedProgress(sb, redrawAll);

        // clear no longer used lines
        if (!redrawAll && i < previousFrame.NodesCount)
        {
            sb.Append($"{AnsiCodes.CSI}{AnsiCodes.EraseInDisplay}");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Returns whether operations move the node rows to different lines than they occupied in
    /// <paramref name="previousFrame"/>, which is the one case the per-line diff in <see cref="Render"/>
    /// cannot follow.
    /// </summary>
    /// <remarks>
    /// Operations render between the node rows, so a node row's line depends on how many operations were
    /// drawn above it. As long as that count is unchanged, every line keeps its place and the text on it
    /// can be rewritten in place, which avoids erasing and repainting the whole block on each update.
    /// </remarks>
    private bool ProgressLayoutShifts(TerminalNodesFrame previousFrame)
    {
        if (ProgressCount == 0 && previousFrame.ProgressCount == 0)
        {
            return false;
        }

        if (ProgressCount != previousFrame.ProgressCount)
        {
            return true;
        }

        int commonNodes = Math.Min(NodesCount, previousFrame.NodesCount);
        for (int i = 0; i < commonNodes; i++)
        {
            int nodeIndex = _nodeIndexes[i];
            if (nodeIndex != previousFrame._nodeIndexes[i]
                || ProgressCountForNode(nodeIndex) != previousFrame.ProgressCountForNode(nodeIndex))
            {
                return true;
            }
        }

        return false;
    }

    private int ProgressCountForNode(int nodeIndex)
    {
        int count = 0;
        for (int progressIndex = 0; progressIndex < _progress.Length; progressIndex++)
        {
            if (_progress[progressIndex].NodeIndex == nodeIndex)
            {
                count++;
            }
        }

        return count;
    }

    private void RenderProgressForNode(StringBuilder sb, int nodeIndex, bool redrawAll)
    {
        for (int progressIndex = 0; progressIndex < _progress.Length; progressIndex++)
        {
            if (_progress[progressIndex].NodeIndex == nodeIndex)
            {
                AppendProgressLine(sb, _progress[progressIndex], redrawAll);
            }
        }
    }

    /// <summary>
    /// Renders operations that no displayed node claims, which happens when a task reports progress
    /// without a node or when the project row disappears before the operation ends.
    /// </summary>
    private void RenderOrphanedProgress(StringBuilder sb, bool redrawAll)
    {
        for (int progressIndex = 0; progressIndex < _progress.Length; progressIndex++)
        {
            if (!IsNodeDisplayed(_progress[progressIndex].NodeIndex))
            {
                AppendProgressLine(sb, _progress[progressIndex], redrawAll);
            }
        }
    }

    private void AppendProgressLine(StringBuilder sb, TerminalProgressStatus progress, bool redrawAll)
    {
        // An operation's text shrinks as often as it grows, so clear the rest of the line first.
        // After a full erase there is nothing left to clear.
        if (!redrawAll)
        {
            sb.Append($"{AnsiCodes.CSI}{AnsiCodes.EraseInLine}");
        }

        sb.Append(progress.Render(Math.Max(Width, 1))).AppendLine();
    }

    private bool IsNodeDisplayed(int nodeIndex)
    {
        for (int i = 0; i < NodesCount; i++)
        {
            if (_nodeIndexes[i] == nodeIndex)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Returns how many physical terminal rows this frame's lines occupy when wrapped at <paramref name="terminalWidth"/>.
    /// </summary>
    internal int GetPhysicalRows(int terminalWidth)
    {
        terminalWidth = Math.Max(terminalWidth, 1);

        int physicalRows = 0;
        for (int i = 0; i < NodesCount; i++)
        {
            int renderedWidth = Math.Max(_nodes[i].renderedWidth, 1);
            physicalRows += ((renderedWidth - 1) / terminalWidth) + 1;
        }

        return physicalRows + ProgressCount;
    }

    public void Clear()
    {
        NodesCount = 0;
    }
}
