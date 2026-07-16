// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using Microsoft.Build.Framework.Coordinator;

namespace Microsoft.Build.Coordinator.UnitTests;

internal sealed class TestCoordinatorDebugOutput(TestContext testOutput) : ICoordinatorDebugOutput
{
    public bool IsEnabled => true;

    public void WriteLine(string message)
        => testOutput.WriteLine(message);

    public void WriteLine([InterpolatedStringHandlerArgument("")] ref ICoordinatorDebugOutput.WriteLineInterpolatedStringHandler handler)
        => testOutput.WriteLine(handler.GetFormattedText());
}
