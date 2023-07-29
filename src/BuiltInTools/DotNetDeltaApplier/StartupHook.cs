// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO.Pipes;
using Microsoft.Extensions.HotReload;

internal sealed class StartupHook
{
    private static readonly bool LogDeltaClientMessages = Environment.GetEnvironmentVariable("HOTRELOAD_DELTA_CLIENT_LOG_MESSAGES") == "1";

    /// <summary>
    /// Invoked by the runtime when the containing assembly is listed in DOTNET_STARTUP_HOOKS.
    /// </summary>
    public static void Initialize()
    {
        ClearHotReloadEnvironmentVariables(Environment.GetEnvironmentVariable, Environment.SetEnvironmentVariable);

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

    internal static void ClearHotReloadEnvironmentVariables(
        Func<string, string?> getEnvironmentVariable,
        Action<string, string?> setEnvironmentVariable)
    {
        // Workaround for https://github.com/dotnet/runtime/issues/58000
        // Clear any hot-reload specific environment variables. This should prevent child processes from being
        // affected by the current app's hot reload settings.
        const string StartupHooksEnvironment = "DOTNET_STARTUP_HOOKS";
        var environment = getEnvironmentVariable(StartupHooksEnvironment);
        setEnvironmentVariable(StartupHooksEnvironment, RemoveCurrentAssembly(environment));

        static string? RemoveCurrentAssembly(string? environment)
        {
            if (string.IsNullOrEmpty(environment))
            {
                return environment;
            }

            var assemblyLocation = typeof(StartupHook).Assembly.Location;
            var updatedValues = environment.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
                .Where(e => !string.Equals(e, assemblyLocation, StringComparison.OrdinalIgnoreCase));

            return string.Join(Path.PathSeparator, updatedValues);
        }
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

        var initPayload = new ClientInitializationPayload(hotReloadAgent.Capabilities);
        Log("Writing capabilities: " + initPayload.Capabilities);
        initPayload.Write(pipeClient);

        while (pipeClient.IsConnected)
        {
            var update = await UpdatePayload.ReadAsync(pipeClient, default);
            Log("Attempting to apply deltas.");

            hotReloadAgent.ApplyDeltas(update.Deltas);
            pipeClient.WriteByte(UpdatePayload.ApplySuccessValue);
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
