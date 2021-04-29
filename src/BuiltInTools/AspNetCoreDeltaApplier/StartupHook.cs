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
    private static volatile UpdateHandlerActions? s_beforeAfterUpdates;

    public static void Initialize()
    {
        Task.Run(async () =>
        {
            AssemblyLoadEventHandler handler = (s, e) => s_beforeAfterUpdates = null;
            try
            {
                AppDomain.CurrentDomain.AssemblyLoad += handler;
                await ReceiveDeltas();
            }
            catch (Exception ex)
            {
                Log(ex.Message);
            }
            finally
            {
                AppDomain.CurrentDomain.AssemblyLoad -= handler;
            }
        });
    }

    private sealed class UpdateHandlerActions
    {
        public UpdateHandlerActions(List<Action<Type[]?>> before, List<Action<Type[]?>> after)
        {
            Before = before;
            After = after;
        }

        public List<Action<Type[]?>> Before { get; }
        public List<Action<Type[]?>> After { get; }
    }

    private static UpdateHandlerActions GetMetadataUpdateHandlerActions()
    {
        var before = new List<Action<Type[]?>>();
        var after = new List<Action<Type[]?>>();

        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            foreach (CustomAttributeData attr in assembly.GetCustomAttributesData())
            {
                if (attr.AttributeType.FullName != "System.Reflection.Metadata.MetadataUpdateHandlerAttribute")
                {
                    continue;
                }

                IList<CustomAttributeTypedArgument> ctorArgs = attr.ConstructorArguments;
                if (ctorArgs.Count != 1 ||
                    ctorArgs[0].Value is not Type handlerType)
                {
                    Log($"'{attr}' found with invalid arguments.");
                    continue;
                }

                bool methodFound = false;

                if (GetUpdateMethod(handlerType, "BeforeUpdate") is MethodInfo beforeUpdate)
                {
                    before.Add(CreateAction(beforeUpdate));
                    methodFound = true;
                }

                if (GetUpdateMethod(handlerType, "AfterUpdate") is MethodInfo afterUpdate)
                {
                    after.Add(CreateAction(afterUpdate));
                    methodFound = true;
                }

                if (!methodFound)
                {
                    Log($"No BeforeUpdate or AfterUpdate method found on '{handlerType}'.");
                }

                static Action<Type[]?> CreateAction(MethodInfo update)
                {
                    Action<Type[]?> action = update.CreateDelegate<Action<Type[]?>>();
                    return types =>
                    {
                        try
                        {
                            action(types);
                        }
                        catch (Exception ex)
                        {
                            Log($"Exception from '{action}': {ex}");
                        }
                    };
                }

                static MethodInfo? GetUpdateMethod(Type handlerType, string name)
                {
                    if (handlerType.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, new[] { typeof(Type[]) }) is MethodInfo updateMethod &&
                        updateMethod.ReturnType == typeof(void))
                    {
                        return updateMethod;
                    }

                    foreach (MethodInfo method in handlerType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance))
                    {
                        if (method.Name == name)
                        {
                            Log($"Type '{handlerType}' has method '{method}' that does not match the required signature.");
                            break;
                        }
                    }

                    return null;
                }
            }
        }

        return new UpdateHandlerActions(before, after);
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
                UpdateHandlerActions beforeAfterUpdates = s_beforeAfterUpdates ??= GetMetadataUpdateHandlerActions();

                beforeAfterUpdates.Before.ForEach(b => b(null)); // TODO: Get types to pass in

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

                // TODO: Remove once https://github.com/dotnet/aspnetcore/issues/31806 is addressed
                // Defer discovering the receiving deltas until the first hot reload delta.
                // This should give enough opportunity for AppDomain.GetAssemblies() to be sufficiently populated.
                receiveDeltaNotifications ??= GetAssembliesReceivingDeltas();
                receiveDeltaNotifications.ForEach(r => r.Invoke());

                beforeAfterUpdates.After.ForEach(a => a(null)); // TODO: Get types to pass in

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
