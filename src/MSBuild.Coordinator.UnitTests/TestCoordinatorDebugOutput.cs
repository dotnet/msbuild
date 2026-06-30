// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using Microsoft.Build.Framework.Coordinator;
using Xunit;

namespace Microsoft.Build.Coordinator.UnitTests;

internal sealed class TestCoordinatorDebugOutput(ITestOutputHelper testOutput) : ICoordinatorDebugOutput
{
    public bool IsEnabled => true;

    public Action<string>? OnWriteLine { get; set; }

    public void WriteLine(string message)
    {
        OnWriteLine?.Invoke(message);
        testOutput.WriteLine(message);
    }

    public void WriteLine([InterpolatedStringHandlerArgument("")] ref ICoordinatorDebugOutput.WriteLineInterpolatedStringHandler handler)
        => WriteLine(handler.GetFormattedText());
}
