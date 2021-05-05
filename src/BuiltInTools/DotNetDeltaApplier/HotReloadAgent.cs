// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using Microsoft.DotNet.Watcher.Tools;

namespace Microsoft.Extensions.HotReload
{
    internal class HotReloadAgent : IDisposable
    {
        private readonly Action<string> _log;
        private readonly AssemblyLoadEventHandler _assemblyLoad;
        private readonly ConcurrentDictionary<Guid, IReadOnlyList<UpdateDelta>> _deltas = new();
        private readonly ConcurrentDictionary<Assembly, Assembly> _appliedAssemblies = new();
        private volatile UpdateHandlerActions? _beforeAfterUpdates;

        public HotReloadAgent(Action<string> log)
        {
            _log = log;
            _assemblyLoad = OnAssemblyLoad;
            AppDomain.CurrentDomain.AssemblyLoad += _assemblyLoad;
        }

        private void OnAssemblyLoad(object? _, AssemblyLoadEventArgs eventArgs)
        {
            _beforeAfterUpdates = null;
            var loadedAssembly = eventArgs.LoadedAssembly;
            var moduleId = loadedAssembly.Modules.FirstOrDefault()?.ModuleVersionId;
            if (moduleId is null)
            {
                return;
            }

            if (_deltas.TryGetValue(moduleId.Value, out var updateDeltas) && _appliedAssemblies.TryAdd(loadedAssembly, loadedAssembly))
            {
                // A delta for this specific Module exists and we haven't called ApplyUpdate on this instance of Assembly as yet.
                ApplyDeltas(updateDeltas);
            }
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

        private UpdateHandlerActions GetMetadataUpdateHandlerActions()
        {
            var before = new List<Action<Type[]?>>();
            var after = new List<Action<Type[]?>>();

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (var attr in assembly.GetCustomAttributes<MetadataUpdateHandlerAttribute>())
                {
                    bool methodFound = false;
                    var handlerType = attr.HandlerType;

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
                        _log($"No BeforeUpdate or AfterUpdate method found on '{handlerType}'.");
                    }

                    Action<Type[]?> CreateAction(MethodInfo update)
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
                                _log($"Exception from '{action}': {ex}");
                            }
                        };
                    }

                    MethodInfo? GetUpdateMethod(Type handlerType, string name)
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
                                _log($"Type '{handlerType}' has method '{method}' that does not match the required signature.");
                                break;
                            }
                        }

                        return null;
                    }
                }
            }

            return new UpdateHandlerActions(before, after);
        }

        public void ApplyDeltas(IReadOnlyList<UpdateDelta> deltas)
        {
            try
            {
                UpdateHandlerActions beforeAfterUpdates = _beforeAfterUpdates ??= GetMetadataUpdateHandlerActions();

                beforeAfterUpdates.Before.ForEach(b => b(null)); // TODO: Get types to pass in

                foreach (var item in deltas)
                {
                    var assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.Modules.FirstOrDefault() is Module m && m.ModuleVersionId == item.ModuleId);
                    if (assembly is not null)
                    {
                        System.Reflection.Metadata.AssemblyExtensions.ApplyUpdate(assembly, item.MetadataDelta, item.ILDelta, ReadOnlySpan<byte>.Empty);
                    }
                }

                // Defer discovering the receiving deltas until the first hot reload delta.
                // This should give enough opportunity for AppDomain.GetAssemblies() to be sufficiently populated.

                beforeAfterUpdates.After.ForEach(a => a(null)); // TODO: Get types to pass in

                _log("Deltas applied.");
            }
            catch (Exception ex)
            {
                _log(ex.ToString());
            }
        }

        public void Dispose()
        {
            AppDomain.CurrentDomain.AssemblyLoad -= _assemblyLoad;
        }
    }
}
