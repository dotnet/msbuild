// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.IO.Pipes;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.DotNet.Watcher.Tools;
using Microsoft.Extensions.HotReload;

internal sealed class StartupHook
{
    private static readonly bool LogDeltaClientMessages = Environment.GetEnvironmentVariable("HOTRELOAD_DELTA_CLIENT_LOG_MESSAGES") == "1";
    private static readonly DiagnosticListener _listener = new("_DOTNET_WATCH_EMULATED_CONTROL_C");

    public static void Initialize()
    {
        Task.Run(async () =>
        {
            using var hotReloadAgent = new HotReloadAgent(Log);
            try
            {
                await ReceiveDeltas(hotReloadAgent);
            }
            catch (Exception ex)
            {
                Log(ex.Message);
            }
        });
    }

    public static async Task ReceiveDeltas(HotReloadAgent hotReloadAgent)
    {
        Log("Attempting to receive deltas.");

        // This value is configured by dotnet-watch when the app is to be launched.
        var namedPipeName = Environment.GetEnvironmentVariable("DOTNET_HOTRELOAD_NAMEDPIPE_NAME") ??
            throw new InvalidOperationException("DOTNET_HOTRELOAD_NAMEDPIPE_NAME was not specified.");

        using var pipeClient = new NamedPipeClientStream(".", namedPipeName, PipeDirection.InOut, PipeOptions.CurrentUserOnly | PipeOptions.Asynchronous);
        try
        {
            await pipeClient.ConnectAsync(5000);
            Log("Connected.");
        }
        catch (TimeoutException)
        {
            Log("Unable to connect to hot-reload server.");
            return;
        }

        while (pipeClient.IsConnected)
        {
            var update = await UpdatePayload.ReadAsync(pipeClient, default);
            Log("Attempting to apply deltas.");

            hotReloadAgent.ApplyDeltas(update.Deltas);
            if (Environment.GetEnvironmentVariable("_DOTNET_WATCH_HOT_RESTART") == "1" &&
                update.ChangedFile is string changedFile &&
                (changedFile.EndsWith("Startup.cs", StringComparison.Ordinal) ||
                changedFile.EndsWith("Program.cs", StringComparison.Ordinal)))
            {
                // When hot restarting, HotRestart's Program.Main listens for indications that a Ctrl-C was emulated to keep looping.
                // This write signals that state.
                _listener.Write("signal", "1");

                // When hot-restarting is enabled, kill the current host by simulating a Ctrl-C event.
                // Since there isn't a programmatic way to doing this, use reflection to trigger the Console.CancelKeyPress event
                // https://github.com/dotnet/runtime/blob/49eef91d24e282d2548827a601a9caa65882e499/src/libraries/System.Console/src/System/Console.cs#L936
                var handleBreak = typeof(Console).GetMethod("HandleBreakEvent", BindingFlags.Static | BindingFlags.NonPublic, new[] { typeof(ConsoleSpecialKey) });
                handleBreak?.Invoke(obj: null, parameters: new object[] { ConsoleSpecialKey.ControlC });
            }

            pipeClient.WriteByte((byte)ApplyResult.Success);

        }
        Log("Stopped received delta updates. Server is no longer connected.");
    }

    private static void Log(string message)
    {
        if (LogDeltaClientMessages)
        {
            Console.WriteLine(message);
        }
    }
}
