// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Tasks.AssemblyDependency;

using CancellationTokenSource cts = new();
Console.CancelKeyPress += (object? sender, ConsoleCancelEventArgs args) =>
{
    args.Cancel = true;
    cts.Cancel();
};

using ResolveAssemblyReferenceService rarService = new();
await rarService.ExecuteAsync(cts.Token);
