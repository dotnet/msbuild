// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

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
