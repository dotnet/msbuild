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
    private readonly NodeStatus[] _nodes;
    private readonly StringBuilder _renderBuilder = new();

    public int Width { get; }
    public int Height { get; }
    public int NodesCount { get; private set; }

    public NodesFrame(NodeStatus?[] nodes, int width, int height)
    {
        Width = width;
        Height = height;

        _nodes = new NodeStatus[nodes.Length];

            foreach (NodeStatus? status in nodes)
            {
                if (status is not null)
                {
                    _nodes[NodesCount++] = status;
                }
            }
    }

    private ReadOnlySpan<char> RenderNodeStatus(NodeStatus status)
    {
        string durationString = ResourceUtilities.FormatResourceStringIgnoreCodeAndKeyword(
            "DurationDisplay",
            status.Stopwatch.Elapsed.TotalSeconds);

        int totalWidth = TerminalLogger.Indentation.Length +
                         status.Project.Length + 1 +
                         (status.TargetFramework?.Length ?? -1) + 1 +
                         status.Target.Length + 1 +
                         durationString.Length;

        if (Width > totalWidth)
        {
            return $"{TerminalLogger.Indentation}{status.Project} {status.TargetFramework} {status.Target} {durationString}".AsSpan();
        }

        return string.Empty.AsSpan();
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
            var needed = RenderNodeStatus(_nodes[i]);

            // Do we have previous node string to compare with?
            if (previousFrame.NodesCount > i)
            {
                var previous = RenderNodeStatus(previousFrame._nodes[i]);

                if (!previous.SequenceEqual(needed))
                {
                    int commonPrefixLen = previous.CommonPrefixLength(needed);

                    if (commonPrefixLen != 0 && needed.Slice(0, commonPrefixLen).IndexOf('\x1b') == -1)
                    {
                        // no escape codes, so can trivially skip substrings
                        sb.Append($"{AnsiCodes.CSI}{commonPrefixLen}{AnsiCodes.MoveForward}");
                        sb.Append(needed.Slice(commonPrefixLen));
                    }
                    else
                    {
                        sb.Append(needed);
                    }

                    // Shall we clear rest of line
                    if (needed.Length < previous.Length)
                    {
                        sb.Append($"{AnsiCodes.CSI}{AnsiCodes.EraseInLine}");
                    }
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
