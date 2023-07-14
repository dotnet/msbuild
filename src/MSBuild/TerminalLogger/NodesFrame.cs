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
    private readonly List<string> _nodeStrings = new();
    private readonly StringBuilder _renderBuilder = new();

    public int Width { get; }
    public int Height { get; }
    public int NodesCount { get; private set; }

    public NodesFrame(NodeStatus?[] nodes, int width, int height)
    {
        Width = width;
        Height = height;
        Init(nodes);
    }

    public string NodeString(int index)
    {
        if (index >= NodesCount)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        return _nodeStrings[index];
    }

    private void Init(NodeStatus?[] nodes)
    {
        int i = 0;
        foreach (NodeStatus? n in nodes)
        {
            if (n is null)
            {
                continue;
            }
            string str = n.ToString();

            if (i < _nodeStrings.Count)
            {
                _nodeStrings[i] = str;
            }
            else
            {
                _nodeStrings.Add(str);
            }
            i++;

            // We cant output more than what fits on screen
            // -2 because cursor command F cant reach, in Windows Terminal, very 1st line, and last line is empty caused by very last WriteLine
            if (i >= Height - 2)
            {
                break;
            }
        }

        NodesCount = i;
    }

    private ReadOnlySpan<char> FitToWidth(ReadOnlySpan<char> input)
    {
        return input.Slice(0, Math.Min(input.Length, Width - 1));
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
            var needed = FitToWidth(NodeString(i).AsSpan());

            // Do we have previous node string to compare with?
            if (previousFrame.NodesCount > i)
            {
                var previous = FitToWidth(previousFrame.NodeString(i).AsSpan());

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
