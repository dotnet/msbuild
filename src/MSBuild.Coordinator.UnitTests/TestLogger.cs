// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using Microsoft.Build.Framework.Coordinator;
using Xunit;

namespace Microsoft.Build.Coordinator.UnitTests;

internal sealed class TestLogger(ITestOutputHelper testOutput) : ICoordinatorLogger
{
    public bool IsEnabled => true;

    public void WriteLine(string message)
        => testOutput.WriteLine(message);

    public void WriteLine([InterpolatedStringHandlerArgument("")] ref ICoordinatorLogger.WriteLineInterpolatedStringHandler handler)
        => testOutput.WriteLine(handler.GetFormattedText());
}
