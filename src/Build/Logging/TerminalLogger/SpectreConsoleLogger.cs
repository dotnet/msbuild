// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NETFRAMEWORK
#nullable disable
#endif

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.Build.Framework.Logging;
using Microsoft.Build.Shared;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace Microsoft.Build.Logging;

/// <summary>
/// Manages Spectre.Console rendering for the TerminalLogger.
/// </summary>
/// <remarks>
/// This class uses Spectre.Console's Live Display with a Rows widget for the nodes section.
/// The Live Display runs on a background thread and updates at 60Hz.
/// </remarks>
internal sealed class SpectreConsoleLogger : IDisposable
{
    private const int RefreshRateHz = 60;
    private const int RefreshIntervalMs = 1000 / RefreshRateHz;

    private readonly IAnsiConsole _console;
    private readonly object _lock = new();

    private TerminalNodeStatus?[]? _nodes;
    private bool _disposed = false;
    internal LiveDisplay? _liveDisplay;

    /// <summary>
    /// Creates a new SpectreConsoleLogger.
    /// </summary>
    public SpectreConsoleLogger() : this(AnsiConsole.Console)
    {
    }

    /// <summary>
    /// Creates a new SpectreConsoleLogger with a specific console instance (for testing).
    /// </summary>
    public SpectreConsoleLogger(IAnsiConsole console)
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));
    }

    /// <summary>
    /// Gets the underlying IAnsiConsole for direct output.
    /// </summary>
    public IAnsiConsole Console => _console;

    /// <summary>
    /// Starts the live display for build nodes on a background thread.
    /// </summary>
    public void StartLiveDisplay(CancellationToken token)
    {
        lock (_lock)
        {
            if (_liveDisplay != null)
            {
                return; // Already started
            }

            _liveDisplay = CreateLiveDisplay(token);
        }
    }

    /// <summary>
    /// Updates the nodes array (will be picked up by next refresh).
    /// </summary>
    public void UpdateNodes(TerminalNodeStatus?[] nodes)
    {
        lock (_lock)
        {
            _nodes = nodes;
        }
    }

    /// <summary>
    /// Runs the live display loop using Spectre.Console's Live API.
    /// </summary>
    private LiveDisplay CreateLiveDisplay(CancellationToken cancellationToken)
    {
        Rows initialRows = CreateNodesRows();

        var liveDisplay = _console.Live(initialRows)
            .AutoClear(false);
        liveDisplay
            .Start(ctx =>
            {
                // Start the background refresher thread
                Thread refresherThread = new Thread(() => RefresherThreadProc(ctx, cancellationToken))
                {
                    Name = "Spectre Terminal Logger Refresher",
                    IsBackground = true,
                };
                refresherThread.Start();

                // Wait for cancellation
                while (!cancellationToken.IsCancellationRequested)
                {
                    _ = cancellationToken.WaitHandle.WaitOne(100);
                }

                // Wait for refresher thread to complete
                _ = refresherThread.Join(TimeSpan.FromSeconds(1));
            });
        return liveDisplay;
    }

    /// <summary>
    /// The refresher thread procedure that updates the display at 60Hz.
    /// </summary>
    private void RefresherThreadProc(LiveDisplayContext context, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            // Recreate rows with latest data
            Rows updatedRows = CreateNodesRows();

            // Update the live display
            context.UpdateTarget(updatedRows);
            context.Refresh();

            // Wait before next refresh (60Hz)
            if (cancellationToken.WaitHandle.WaitOne(RefreshIntervalMs))
            {
                break;
            }
        }
    }

    /// <summary>
    /// Creates a Rows widget for displaying build node status.
    /// </summary>
    private Rows CreateNodesRows()
    {
        TerminalNodeStatus?[]? currentNodes;
        lock (_lock)
        {
            currentNodes = _nodes;
        }

        return currentNodes == null
            ? new Rows([])
            : new Rows([.. currentNodes.Where(status => status != null).Select(status => CreateNodeRow(status!))]);
    }

    /// <summary>
    /// Creates a single row for a node status using a Columns widget.
    /// </summary>
    private Columns CreateNodeRow(TerminalNodeStatus status)
    {
        // Left-aligned elements: indentation + project + TFM + RID
        List<IRenderable> leftElements =
        [
            // Indentation + Project name
            new Markup(TerminalLogger.Indentation),
            new Markup(Markup.Escape(status.Project)),
        ];

        // Target Framework (if present)
        if (!string.IsNullOrWhiteSpace(status.TargetFramework))
        {
            leftElements.Add(new Markup($"[cyan]{Markup.Escape(status.TargetFramework)}[/]"));
        }

        // Runtime Identifier (if present)
        if (!string.IsNullOrWhiteSpace(status.RuntimeIdentifier))
        {
            leftElements.Add(new Markup($"[magenta]{Markup.Escape(status.RuntimeIdentifier)}[/]"));
        }

        // Right-aligned elements: target prefix + target + duration
        List<IRenderable> rightElements = [];

        // Target prefix (if present)
        if (!string.IsNullOrWhiteSpace(status.TargetPrefix))
        {
            string color = GetSpectreColor(status.TargetPrefixColor);
            rightElements.Add(new Markup($"[{color}]{Markup.Escape(status.TargetPrefix)}[/]"));
        }

        // Current target
        rightElements.Add(new Markup(Markup.Escape(status.Target)));

        // Duration
        string durationString = ResourceUtilities.FormatResourceStringIgnoreCodeAndKeyword(
            "DurationDisplay",
            status.Stopwatch.ElapsedSeconds);
        rightElements.Add(new Markup(Markup.Escape(durationString)));

        // Combine left elements into a single Columns widget (left-aligned)
        Columns leftColumns = new Columns(leftElements).Collapse();

        // Combine right elements into a single Columns widget (right-aligned)
        Columns rightColumns = new Columns(rightElements).Collapse();

        // Create the final row with left-aligned and right-aligned sections
        // We use a Columns widget with two columns: left (expand) and right (no expand)
        Columns rowColumns = new Columns([
            leftColumns,
            rightColumns,
        ]);

        // Set the left column to expand to fill space, right column to not expand
        return rowColumns.Expand();
    }

    /// <summary>
    /// Converts TerminalColor to Spectre.Console color name.
    /// </summary>
    private static string GetSpectreColor(TerminalColor color)
    {
        return color switch
        {
            TerminalColor.Black => "black",
            TerminalColor.Red => "red",
            TerminalColor.Green => "lime",
            TerminalColor.Yellow => "yellow",
            TerminalColor.Blue => "blue",
            TerminalColor.Magenta => "fuchsia",
            TerminalColor.Cyan => "aqua",
            TerminalColor.White => "white",
            _ => "default"
        };
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
    }
}
