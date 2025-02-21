// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Text;
using Microsoft.Build.Framework.Logging;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Logging;

/// <summary>
/// Capture states on nodes to be rendered on display.
/// </summary>
internal sealed class TerminalNodesFrame
{
    private const int MaxColumn = 120;

    private readonly (TerminalNodeStatus nodeStatus, int durationLength)[] _nodes;

    private readonly StringBuilder _renderBuilder = new();

    public int Width { get; }
    public int Height { get; }
    public int NodesCount { get; private set; }

    public TerminalNodesFrame(TerminalNodeStatus?[] nodes, int width, int height)
    {
        Width = Math.Min(width, MaxColumn);
        Height = height;

        _nodes = new (TerminalNodeStatus, int)[nodes.Length];

        foreach (TerminalNodeStatus? status in nodes)
        {
            if (status is not null)
            {
                _nodes[NodesCount++].nodeStatus = status;
            }
        }
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
        string target = status.Target;
        string? targetPrefix = status.TargetPrefix;
        TerminalColor targetPrefixColor = status.TargetPrefixColor;

        var targetWithoutAnsiLength = !string.IsNullOrWhiteSpace(targetPrefix)
            // +1 because we will join them by space in the final output.
            ? targetPrefix!.Length + 1 + target.Length
            : target.Length;

        int renderedWidth = Length(durationString, project, targetFramework, targetWithoutAnsiLength);

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
                    return project.AsSpan();
                }
            }
        }

        var renderedTarget = !string.IsNullOrWhiteSpace(targetPrefix) ? $"{AnsiCodes.Colorize(targetPrefix, targetPrefixColor)} {target}" : target;
        return $"{TerminalLogger.Indentation}{project}{(targetFramework is null ? string.Empty : " ")}{AnsiCodes.Colorize(targetFramework, TerminalLogger.TargetFrameworkColor)} {AnsiCodes.SetCursorHorizontal(MaxColumn)}{AnsiCodes.MoveCursorBackward(targetWithoutAnsiLength + durationString.Length + 1)}{renderedTarget} {durationString}".AsSpan();

        static int Length(string durationString, string project, string? targetFramework, int targetWithoutAnsiLength) =>
                TerminalLogger.Indentation.Length +
                project.Length + 1 +
                (targetFramework?.Length ?? -1) + 1 +
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

        // Move cursor back to 1st line of nodes.
        sb.AppendLine($"{AnsiCodes.CSI}{previousFrame.NodesCount + 1}{AnsiCodes.MoveUpToLineStart}");

        int i = 0;
        for (; i < NodesCount; i++)
        {
            ReadOnlySpan<char> needed = RenderNodeStatus(i);

            // Do we have previous node string to compare with?
            if (previousFrame.NodesCount > i)
            {
                if (previousFrame._nodes[i] == _nodes[i])
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
        }

        // clear no longer used lines
        if (i < previousFrame.NodesCount)
        {
            sb.Append($"{AnsiCodes.CSI}{AnsiCodes.EraseInDisplay}");
        }

        return sb.ToString();
    }

    public void Clear()
    {
        NodesCount = 0;
    }
}
