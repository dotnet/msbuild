// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using Microsoft.Build.Framework.Coordinator;
using Xunit;

namespace Microsoft.Build.Coordinator.UnitTests;

internal sealed class TestCoordinatorDebugOutput(ITestOutputHelper testOutput) : ICoordinatorDebugOutput
{
    private readonly object _lock = new();
    private readonly List<string> _lines = [];

    public bool IsEnabled => true;

    public IReadOnlyList<string> Lines
    {
        get
        {
            lock (_lock)
            {
                return [.. _lines];
            }
        }
    }

    public void WriteLine(string message)
    {
        lock (_lock)
        {
            _lines.Add(message);
        }

        testOutput.WriteLine(message);
    }

    public void WriteLine([InterpolatedStringHandlerArgument("")] ref ICoordinatorDebugOutput.WriteLineInterpolatedStringHandler handler)
        => WriteLine(handler.GetFormattedText());
}
