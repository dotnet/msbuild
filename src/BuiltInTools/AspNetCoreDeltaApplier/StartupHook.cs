// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.DotNet.Watcher.Tools;

internal sealed class StartupHook
{
    private static readonly bool LogDeltaClientMessages = Environment.GetEnvironmentVariable("HOTRELOAD_DELTA_CLIENT_LOG_MESSAGES") == "1";

    public static void Initialize()
    {
        Task.Run(async () =>
        {
            try
            {
                await ReceiveDeltas();
            }
            catch (Exception ex)
            {
                Log(ex.Message);
            }
        });
    }

    private static List<Action> GetAssembliesReceivingDeltas()
    {
        var receipients = new List<Action>();

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            var customAttributes = assembly.GetCustomAttributes<AssemblyMetadataAttribute>();
            var deltaReceiverAttributes = customAttributes.Where(a => a.Key == "ReceiveHotReloadDeltaNotification" && !string.IsNullOrEmpty(a.Value));
            foreach (var deltaReceiverAttribute in deltaReceiverAttributes)
            {
                Log($"Attempting to locate receiver {deltaReceiverAttribute.Value} in assembly {assembly}");
                var type = assembly.GetType(deltaReceiverAttribute.Value!, throwOnError: false);
                if (type is null)
                {
                    Log($"Could not find delta receiver type {deltaReceiverAttribute.Value} in assembly {assembly}.");
                    continue;
                }

                if (type.GetMethod("DeltaApplied", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static) is MethodInfo methodInfo)
                {
                    var action = methodInfo.CreateDelegate<Action>();
                    Action safeAction = () =>
                    {
                        try 
                        { 
                            action(); 
                        } 
                        catch (Exception ex) 
                        { 
                            Log(ex.ToString()); 
                        }
                    };

                    receipients.Add(safeAction);
                }
                else
                {
                    Log($"Could not find method 'DeltaApplied' on type {type}.");
                }
            }
        }

        return receipients;
    }

    public static async Task ReceiveDeltas()
    {
        Log("Attempting to receive deltas.");

        using var pipeClient = new NamedPipeClientStream(".", "netcore-hot-reload", PipeDirection.InOut, PipeOptions.CurrentUserOnly | PipeOptions.Asynchronous);
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

        List<Action>? receiveDeltaNotifications = null;

        while (pipeClient.IsConnected)
        {

            var update = await UpdatePayload.ReadAsync(pipeClient, default);
            Log("Attempting to apply deltas.");

            try
            {
                foreach (var item in update.Deltas)
                {
                    var assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.Modules.FirstOrDefault() is Module m && m.ModuleVersionId == item.ModuleId);
                    if (assembly is not null)
                    {
                        System.Reflection.Metadata.AssemblyExtensions.ApplyUpdate(assembly, item.MetadataDelta, item.ILDelta, ReadOnlySpan<byte>.Empty);
                    }
                }

                // We want to base this off of mvids, but we'll figure that out eventually.
                var applyResult = update.ChangedFile is string changedFile && changedFile.EndsWith(".razor", StringComparison.Ordinal) ?
                    ApplyResult.Success :
                    ApplyResult.Success_RefreshBrowser;
                pipeClient.WriteByte((byte)applyResult);

                // Defer discovering the receiving deltas until the first hot reload delta.
                // This should give enough opportunity for AppDomain.GetAssemblies() to be sufficiently populated.
                receiveDeltaNotifications ??= GetAssembliesReceivingDeltas();
                receiveDeltaNotifications.ForEach(r => r.Invoke());

                Log("Deltas applied.");
            }
            catch (Exception ex)
            {
                Log(ex.ToString());
            }
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

