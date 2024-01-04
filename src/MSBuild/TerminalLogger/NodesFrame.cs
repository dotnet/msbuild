// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

using System.Text;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Logging.TerminalLogger;

/// <summary>
/// Capture states on nodes to be rendered on display.
/// </summary>
internal sealed class NodesFrame
{
    private const int MaxColumn = 120;

    private readonly (NodeStatus nodeStatus, int durationLength)[] _nodes;

    private readonly StringBuilder _renderBuilder = new();

    public int Width { get; }
    public int Height { get; }
    public int NodesCount { get; private set; }

    public NodesFrame(NodeStatus?[] nodes, int width, int height)
    {
        Width = Math.Min(width, MaxColumn);
        Height = height;

        _nodes = new (NodeStatus, int)[nodes.Length];

        foreach (NodeStatus? status in nodes)
        {
            if (status is not null)
            {
                _nodes[NodesCount++].nodeStatus = status;
            }
        }
    }

    internal ReadOnlySpan<char> RenderNodeStatus(int i)
    {
        NodeStatus status = _nodes[i].nodeStatus;

        string durationString = ResourceUtilities.FormatResourceStringIgnoreCodeAndKeyword(
            "DurationDisplay",
            status.Stopwatch.ElapsedSeconds);

        _nodes[i].durationLength = durationString.Length;

        string project = status.Project;
        string? targetFramework = status.TargetFramework;
        string target = status.Target;

        int renderedWidth = Length(durationString, project, targetFramework, target);

        if (renderedWidth > Width)
        {
            renderedWidth -= target.Length;
            target = string.Empty;

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

        return $"{TerminalLogger.Indentation}{project}{(targetFramework is null ? string.Empty : " ")}{AnsiCodes.Colorize(targetFramework, TerminalLogger.TargetFrameworkColor)} {AnsiCodes.SetCursorHorizontal(MaxColumn)}{AnsiCodes.MoveCursorBackward(target.Length + durationString.Length + 1)}{target} {durationString}".AsSpan();

        static int Length(string durationString, string project, string? targetFramework, string target) =>
                TerminalLogger.Indentation.Length +
                project.Length + 1 +
                (targetFramework?.Length ?? -1) + 1 +
                target.Length + 1 +
                durationString.Length;
    }

    /// <summary>
    /// Render VT100 string to update from current to next frame.
    /// </summary>
    public string Render(NodesFrame previousFrame)
    {
        StringBuilder sb = _renderBuilder;
        sb.Clear();

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
