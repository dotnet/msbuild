// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO.Pipes;
using System.Threading.Tasks;
using Microsoft.Extensions.HotReload;

internal sealed class StartupHook
{
    private static readonly bool LogDeltaClientMessages = Environment.GetEnvironmentVariable("HOTRELOAD_DELTA_CLIENT_LOG_MESSAGES") == "1";

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
