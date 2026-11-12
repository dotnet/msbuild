// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using Microsoft.Build.Framework.Coordinator;
using Xunit;

namespace Microsoft.Build.Coordinator.UnitTests;

internal sealed class TestCoordinatorDebugOutput(
    ITestOutputHelper testOutput,
    Action<string>? messageObserver = null) : ICoordinatorDebugOutput
{
    public bool IsEnabled => true;

    public void WriteLine(string message)
        => WriteLineCore(message);

    public void WriteLine([InterpolatedStringHandlerArgument("")] ref ICoordinatorDebugOutput.WriteLineInterpolatedStringHandler handler)
        => WriteLineCore(handler.GetFormattedText());

    private void WriteLineCore(string message)
    {
        testOutput.WriteLine(message);
        messageObserver?.Invoke(message);
    }
}
