// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Tools.Internal;

namespace Microsoft.DotNet.Watcher.Tools;

internal class MockReporter : IReporter
{
    public readonly List<string> Messages = new();

    public void Verbose(string message, string emoji = "⌚")
        => Messages.Add($"verbose {emoji} " + message);

    public void Output(string message, string emoji = "⌚")
        => Messages.Add($"output {emoji} " + message);

    public void Warn(string message, string emoji = "⌚")
        => Messages.Add($"warn {emoji} " + message);

    public void Error(string message, string emoji = "❌")
        => Messages.Add($"error {emoji} " + message);
}
