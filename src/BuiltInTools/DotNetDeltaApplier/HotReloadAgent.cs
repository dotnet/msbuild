// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;

namespace Microsoft.Extensions.HotReload
{
    internal sealed class HotReloadAgent : IDisposable
    {
        private delegate void ApplyUpdateDelegate(Assembly assembly, ReadOnlySpan<byte> metadataDelta, ReadOnlySpan<byte> ilDelta, ReadOnlySpan<byte> pdbDelta);

        private readonly Action<string> _log;
        private readonly AssemblyLoadEventHandler _assemblyLoad;
        private readonly ConcurrentDictionary<Guid, List<UpdateDelta>> _deltas = new();
        private readonly ConcurrentDictionary<Assembly, Assembly> _appliedAssemblies = new();
        private readonly ApplyUpdateDelegate? _applyUpdate;
        private readonly string? _capabilities;
        private volatile UpdateHandlerActions? _handlerActions;

        public HotReloadAgent(Action<string> log)
        {
            var metadataUpdater = Type.GetType("System.Reflection.Metadata.MetadataUpdater, System.Runtime.Loader", throwOnError: false);

            if (metadataUpdater != null)
            {
                _applyUpdate = (ApplyUpdateDelegate?)metadataUpdater.GetMethod("ApplyUpdate", BindingFlags.Public | BindingFlags.Static, binder: null,
                    new[] { typeof(Assembly), typeof(ReadOnlySpan<byte>), typeof(ReadOnlySpan<byte>), typeof(ReadOnlySpan<byte>) }, modifiers: null)?.CreateDelegate(typeof(ApplyUpdateDelegate));

                if (_applyUpdate != null)
                {
                    try
                    {
                        _capabilities = metadataUpdater.GetMethod("GetCapabilities", BindingFlags.NonPublic | BindingFlags.Static, binder: null, Type.EmptyTypes, modifiers: null)?.
                            Invoke(obj: null, parameters: null) as string;
                    }
                    catch
                    {
                    }
                }
            }

            _log = log;
            _assemblyLoad = OnAssemblyLoad;
            AppDomain.CurrentDomain.AssemblyLoad += _assemblyLoad;
        }

        public string Capabilities => _capabilities ?? string.Empty;

        private void OnAssemblyLoad(object? _, AssemblyLoadEventArgs eventArgs)
        {
            _handlerActions = null;
            var loadedAssembly = eventArgs.LoadedAssembly;
            var moduleId = TryGetModuleId(loadedAssembly);
            if (moduleId is null)
            {
                return;
            }

            if (_deltas.TryGetValue(moduleId.Value, out var updateDeltas) && _appliedAssemblies.TryAdd(loadedAssembly, loadedAssembly))
            {
                // A delta for this specific Module exists and we haven't called ApplyUpdate on this instance of Assembly as yet.
                ApplyDeltas(loadedAssembly, updateDeltas);
            }
        }

        internal sealed class UpdateHandlerActions
        {
            public List<Action<Type[]?>> ClearCache { get; } = new();
            public List<Action<Type[]?>> UpdateApplication { get; } = new();
        }

        private UpdateHandlerActions GetMetadataUpdateHandlerActions()
        {
            // We need to execute MetadataUpdateHandlers in a well-defined order. For v1, the strategy that is used is to topologically
            // sort assemblies so that handlers in a dependency are executed before the dependent (e.g. the reflection cache action
            // in System.Private.CoreLib is executed before System.Text.Json clears its own cache.)
            // This would ensure that caches and updates more lower in the application stack are up to date
            // before ones higher in the stack are recomputed.
            var sortedAssemblies = TopologicalSort(AppDomain.CurrentDomain.GetAssemblies());
            var handlerActions = new UpdateHandlerActions();
            foreach (var assembly in sortedAssemblies)
            {
                foreach (var attr in assembly.GetCustomAttributesData())
                {
                    // Look up the attribute by name rather than by type. This would allow netstandard targeting libraries to
                    // define their own copy without having to cross-compile.
                    if (attr.AttributeType.FullName != "System.Reflection.Metadata.MetadataUpdateHandlerAttribute")
                    {
                        continue;
                    }

                    IList<CustomAttributeTypedArgument> ctorArgs = attr.ConstructorArguments;
                    if (ctorArgs.Count != 1 ||
                        ctorArgs[0].Value is not Type handlerType)
                    {
                        _log($"'{attr}' found with invalid arguments.");
                        continue;
                    }

                    GetHandlerActions(handlerActions, handlerType);
                }
            }

            return handlerActions;
        }

        internal void GetHandlerActions(UpdateHandlerActions handlerActions, Type handlerType)
        {
            bool methodFound = false;

            if (GetUpdateMethod(handlerType, "ClearCache") is MethodInfo clearCache)
            {
                handlerActions.ClearCache.Add(CreateAction(clearCache));
                methodFound = true;
            }

            if (GetUpdateMethod(handlerType, "UpdateApplication") is MethodInfo updateApplication)
            {
                handlerActions.UpdateApplication.Add(CreateAction(updateApplication));
                methodFound = true;
            }

            if (!methodFound)
            {
                _log($"No invokable methods found on metadata handler type '{handlerType}'. " +
                    $"Allowed methods are ClearCache, UpdateApplication");
            }

            Action<Type[]?> CreateAction(MethodInfo update)
            {
                var action = (Action<Type[]?>)update.CreateDelegate(typeof(Action<Type[]?>));
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
                if (handlerType.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, binder: null, new[] { typeof(Type[]) }, modifiers: null) is MethodInfo updateMethod &&
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

        internal static List<Assembly> TopologicalSort(Assembly[] assemblies)
        {
            var sortedAssemblies = new List<Assembly>(assemblies.Length);

            var visited = new HashSet<string>(StringComparer.Ordinal);

            foreach (var assembly in assemblies)
            {
                Visit(assemblies, assembly, sortedAssemblies, visited);
            }

            static void Visit(Assembly[] assemblies, Assembly assembly, List<Assembly> sortedAssemblies, HashSet<string> visited)
            {
                var assemblyIdentifier = assembly.GetName().Name!;
                if (!visited.Add(assemblyIdentifier))
                {
                    return;
                }

                foreach (var dependencyName in assembly.GetReferencedAssemblies())
                {
                    var dependency = Array.Find(assemblies, a => a.GetName().Name == dependencyName.Name);
                    if (dependency is not null)
                    {
                        Visit(assemblies, dependency, sortedAssemblies, visited);
                    }
                }

                sortedAssemblies.Add(assembly);
            }

            return sortedAssemblies;
        }

        public void ApplyDeltas(IReadOnlyList<UpdateDelta> deltas)
        {
            Debug.Assert(Capabilities.Length > 0);
            Debug.Assert(_applyUpdate != null);

            for (var i = 0; i < deltas.Count; i++)
            {
                var item = deltas[i];
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (TryGetModuleId(assembly) is Guid moduleId && moduleId == item.ModuleId)
                    {
                        _applyUpdate(assembly, item.MetadataDelta, item.ILDelta, ReadOnlySpan<byte>.Empty);
                    }
                }

                // Additionally stash the deltas away so it may be applied to assemblies loaded later.
                var cachedDeltas = _deltas.GetOrAdd(item.ModuleId, static _ => new());
                cachedDeltas.Add(item);
            }

            try
            {
                // Defer discovering metadata updata handlers until after hot reload deltas have been applied.
                // This should give enough opportunity for AppDomain.GetAssemblies() to be sufficiently populated.
                _handlerActions ??= GetMetadataUpdateHandlerActions();
                var handlerActions = _handlerActions;

                Type[]? updatedTypes = GetMetadataUpdateTypes(deltas);

                handlerActions.ClearCache.ForEach(a => a(updatedTypes));
                handlerActions.UpdateApplication.ForEach(a => a(updatedTypes));

                _log("Deltas applied.");
            }
            catch (Exception ex)
            {
                _log(ex.ToString());
            }
        }

        private Type[] GetMetadataUpdateTypes(IReadOnlyList<UpdateDelta> deltas)
        {
            List<Type>? types = null;

            foreach (var delta in deltas)
            {
                var assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(assembly => TryGetModuleId(assembly) is Guid moduleId && moduleId == delta.ModuleId);
                if (assembly is null)
                {
                    continue;
                }

                foreach (var updatedType in delta.UpdatedTypes)
                {
                    // Must be a TypeDef.
                    Debug.Assert(updatedType >> 24 == 0x02);

                    // The type has to be in the manifest module since Hot Reload does not support multi-module assemblies:
                    try
                    {
                        var type = assembly.ManifestModule.ResolveType(updatedType);
                        types ??= new();
                        types.Add(type);
                    }
                    catch (Exception e)
                    {
                        _log($"Failed to load type 0x{updatedType:X8}: {e.Message}");
                    }
                }
            }

            return types?.ToArray() ?? Type.EmptyTypes;
        }

        public void ApplyDeltas(Assembly assembly, IReadOnlyList<UpdateDelta> deltas)
        {
            Debug.Assert(_applyUpdate != null);

            try
            {
                foreach (var item in deltas)
                {
                    _applyUpdate(assembly, item.MetadataDelta, item.ILDelta, ReadOnlySpan<byte>.Empty);
                }

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

        private static Guid? TryGetModuleId(Assembly loadedAssembly)
        {
            try
            {
                return loadedAssembly.Modules.FirstOrDefault()?.ModuleVersionId;
            }
            catch
            {
                // Assembly.Modules might throw. See https://github.com/dotnet/aspnetcore/issues/33152
            }

            return default;
        }
    }
}
