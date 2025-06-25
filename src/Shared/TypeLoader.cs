// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Build.Eventing;
using Microsoft.Build.Framework;

#nullable disable

namespace Microsoft.Build.Shared
{
    /// <summary>
    /// This class is used to load types from their assemblies.
    /// </summary>
    internal static class TypeLoader
    {
        /// <summary>
        /// Determines which types to load from the assembly.
        /// </summary>
        internal enum TypeFilter
        {
            /// <summary>
            /// Types which implement ITask.
            /// </summary>
            Task,

            /// <summary>
            /// Types which implement IForwardingLogger.
            /// </summary>
            ForwardingLogger,

            /// <summary>
            /// Types which implement ILogger.
            /// </summary>
            Logger,

            /// <summary>
            /// Types which impelment ITaskFactory.
            /// </summary>
            TaskFactory,
        }

#if FEATURE_ASSEMBLYLOADCONTEXT
        /// <summary>
        /// AssemblyContextLoader used to load DLLs outside of msbuild.exe directory
        /// </summary>
        private static readonly CoreClrAssemblyLoader s_coreClrAssemblyLoader = new CoreClrAssemblyLoader();
#endif

        /// <summary>
        /// Copy-on-write cache to keep track of the assemblyLoadInfos based on a given type filter.
        /// </summary>
        /// <remarks>
        /// ImmutableDictionary is chosen for both the assembly and type caches because:
        /// 1. These caches aren't expected to change often, and are mostly front-loaded with MSBuild types. Otherwise,
        /// we'd be paying the cost of locks for the entire build.
        /// 2 .Assembly loads are thread-safe and will no-op if loaded more than once..
        /// 3. On a race, we're okay to perform duplicate work and one thread will succeed in writing to the cache.
        /// </remarks>
        private static ImmutableDictionary<AssemblyLoadInfo, AssemblyInfoToLoadedTypes> s_cacheOfLoadedTypesByFilter =
            ImmutableDictionary<AssemblyLoadInfo, AssemblyInfoToLoadedTypes>.Empty;

        private static readonly string[] runtimeAssemblies = findRuntimeAssembliesWithMicrosoftBuildFramework();
        private static string microsoftBuildFrameworkPath;

        // We need to append Microsoft.Build.Framework from next to the executing assembly first to make sure it's loaded before the runtime variant.
        private static string[] findRuntimeAssembliesWithMicrosoftBuildFramework()
        {
            string msbuildDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            microsoftBuildFrameworkPath = Path.Combine(msbuildDirectory, "Microsoft.Build.Framework.dll");
            string[] msbuildAssemblies = Directory.GetFiles(msbuildDirectory, "*.dll");
            string[] runtimeAssemblies = Directory.GetFiles(RuntimeEnvironment.GetRuntimeDirectory(), "*.dll");

            return [.. runtimeAssemblies, .. msbuildAssemblies];
        }

        /// <summary>
        /// Determines if the given type implements a known interface based on the type filter.
        /// </summary>
        /// <returns>True if the type is a concrete class and implements the interface; otherwise, false.</returns>
        private static bool IsDesiredType(Type type, TypeFilter typeFilter)
            => IsDesiredType(type.GetTypeInfo(), typeFilter);

        /// <summary>
        /// Determines if the given type info implements a known interface based on the type filter.
        /// </summary>
        /// <returns>True if the type is a concrete class and implements the interface; otherwise, false.</returns>
        private static bool IsDesiredType(TypeInfo typeInfo, TypeFilter typeFilter)
        {
            if (typeInfo.IsClass && !typeInfo.IsAbstract)
            {
                string interfaceName = typeFilter switch
                {
                    TypeFilter.Task => "Microsoft.Build.Framework.ITask",
                    TypeFilter.ForwardingLogger => "IForwardingLogger",
                    TypeFilter.Logger => "ILogger",
                    TypeFilter.TaskFactory => "Microsoft.Build.Framework.ITaskFactory",
                    _ => throw new InternalErrorException("Unknown type filter"),
                };

                return typeInfo.GetInterface(interfaceName) != null;
            }

            return false;
        }

        /// <summary>
        /// Given two type names, looks for a partial match between them. A partial match is considered valid only if it occurs on
        /// the right side (tail end) of the name strings, and at the start of a class or namespace name.
        /// </summary>
        /// <remarks>
        /// 1) Matches are case-insensitive.
        /// 2) .NET conventions regarding namespaces and nested classes are respected, including escaping of reserved characters.
        /// </remarks>
        /// <example>
        /// "Csc" and "csc"                                                 ==> exact match
        /// "Microsoft.Build.Tasks.Csc" and "Microsoft.Build.Tasks.Csc"     ==> exact match
        /// "Microsoft.Build.Tasks.Csc" and "Csc"                           ==> partial match
        /// "Microsoft.Build.Tasks.Csc" and "Tasks.Csc"                     ==> partial match
        /// "MyTasks.ATask+NestedTask" and "NestedTask"                     ==> partial match
        /// "MyTasks.ATask\\+NestedTask" and "NestedTask"                   ==> partial match
        /// "MyTasks.CscTask" and "Csc"                                     ==> no match
        /// "MyTasks.MyCsc" and "Csc"                                       ==> no match
        /// "MyTasks.ATask\.Csc" and "Csc"                                  ==> no match
        /// "MyTasks.ATask\\\.Csc" and "Csc"                                ==> no match
        /// </example>
        /// <returns>true, if the type names match exactly or partially; false, if there is no match at all</returns>
        internal static bool IsPartialTypeNameMatch(string typeName1, string typeName2)
        {
            bool isPartialMatch = false;

            // if the type names are the same length, a partial match is impossible
            if (typeName1.Length != typeName2.Length)
            {
                string longerTypeName;
                string shorterTypeName;

                // figure out which type name is longer
                if (typeName1.Length > typeName2.Length)
                {
                    longerTypeName = typeName1;
                    shorterTypeName = typeName2;
                }
                else
                {
                    longerTypeName = typeName2;
                    shorterTypeName = typeName1;
                }

                // if the shorter type name matches the end of the longer one
                if (longerTypeName.EndsWith(shorterTypeName, StringComparison.OrdinalIgnoreCase))
                {
                    int matchIndex = longerTypeName.Length - shorterTypeName.Length;

                    // if the matched sub-string looks like the start of a namespace or class name
                    if ((longerTypeName[matchIndex - 1] == '.') || (longerTypeName[matchIndex - 1] == '+'))
                    {
                        int precedingBackslashes = 0;

                        // confirm there are zero, or an even number of \'s preceding it...
                        for (int i = matchIndex - 2; i >= 0; i--)
                        {
                            if (longerTypeName[i] == '\\')
                            {
                                precedingBackslashes++;
                            }
                            else
                            {
                                break;
                            }
                        }

                        if ((precedingBackslashes % 2) == 0)
                        {
                            isPartialMatch = true;
                        }
                    }
                }
            }
            else
            {
                isPartialMatch = (String.Equals(typeName1, typeName2, StringComparison.OrdinalIgnoreCase));
            }

            return isPartialMatch;
        }

        /// <summary>
        /// Load an assembly given its AssemblyLoadInfo
        /// </summary>
        /// <param name="assemblyLoadInfo"></param>
        /// <returns></returns>
        private static Assembly LoadAssembly(AssemblyLoadInfo assemblyLoadInfo)
        {
            try
            {
                if (assemblyLoadInfo.AssemblyName != null)
                {
                    return Assembly.Load(assemblyLoadInfo.AssemblyName);
                }
                else
                {
#if !FEATURE_ASSEMBLYLOADCONTEXT
                    return Assembly.UnsafeLoadFrom(assemblyLoadInfo.AssemblyFile);
#else
                    string baseDir = Path.GetDirectoryName(assemblyLoadInfo.AssemblyFile);
                    s_coreClrAssemblyLoader.AddDependencyLocation(baseDir);
                    return s_coreClrAssemblyLoader.LoadFromPath(assemblyLoadInfo.AssemblyFile);
#endif
                }
            }
            catch (ArgumentException e)
            {
                // Assembly.Load() and Assembly.LoadFrom() will throw an ArgumentException if the assembly name is invalid
                // convert to a FileNotFoundException because it's more meaningful
                // NOTE: don't use ErrorUtilities.VerifyThrowFileExists() here because that will hit the disk again
                throw new FileNotFoundException(null, assemblyLoadInfo.AssemblyLocation, e);
            }
        }

        private static MetadataLoadContext LoadMetadataLoadContext(string path)
        {
            string[] localAssemblies = Directory.GetFiles(Path.GetDirectoryName(path), "*.dll");

            // Deduplicate between MSBuild assemblies and task dependencies.
            Dictionary<string, string> assembliesDictionary = new(localAssemblies.Length + runtimeAssemblies.Length);
            foreach (string localPath in localAssemblies)
            {
                assembliesDictionary.Add(Path.GetFileName(localPath), localPath);
            }

            foreach (string runtimeAssembly in runtimeAssemblies)
            {
                assembliesDictionary[Path.GetFileName(runtimeAssembly)] = runtimeAssembly;
            }

            MetadataLoadContext context = new(new PathAssemblyResolver(assembliesDictionary.Values));
            return context;
        }

#nullable enable

        /// <summary>
        /// Loads the specified type if it exists in the given assembly. If the type name is fully qualified, then a match (if
        /// any) is unambiguous; otherwise, if there are multiple types with the same name in different namespaces, the first type
        /// found will be returned.
        /// </summary>
        internal static LoadedType? Load(
            string typeName,
            AssemblyLoadInfo assembly,
            TypeFilter typeFilter,
            bool useTaskHost = false)
        {
            return GetLoadedType(new LoadedTypeKey(typeFilter, typeName), assembly, useTaskHost);
        }

        /// <summary>
        /// Loads the specified type if it exists in the given assembly. If the type name is fully qualified, then a match (if
        /// any) is unambiguous; otherwise, if there are multiple types with the same name in different namespaces, the first type
        /// found will be returned.
        /// </summary>
        /// <returns>The loaded type, or null if the type was not found.</returns>
        internal static LoadedType? ReflectionOnlyLoad(
            string typeName,
            AssemblyLoadInfo assembly,
            TypeFilter typeFilter)
        {
            return GetLoadedType(new LoadedTypeKey(typeFilter, typeName), assembly, useTaskHost: false);
        }

        /// <summary>
        /// Loads the specified type if it exists in the given assembly. If the type name is fully qualified, then a match (if
        /// any) is unambiguous; otherwise, if there are multiple types with the same name in different namespaces, the first type
        /// found will be returned.
        /// </summary>
        private static LoadedType? GetLoadedType(LoadedTypeKey typeKey, AssemblyLoadInfo assembly, bool useTaskHost)
        {
            // A given type filter have been used on a number of assemblies, Based on the type filter we will get another dictionary which
            // will map a specific AssemblyLoadInfo to a AssemblyInfoToLoadedTypes class which knows how to find a typeName in a given assembly.
            // Get an object which is able to take a typename and determine if it is in the assembly pointed to by the AssemblyInfo.
            if (!s_cacheOfLoadedTypesByFilter.TryGetValue(assembly, out AssemblyInfoToLoadedTypes? typeNameToType))
            {
                typeNameToType = new AssemblyInfoToLoadedTypes(assembly);
                s_cacheOfLoadedTypesByFilter = s_cacheOfLoadedTypesByFilter.SetItem(assembly, typeNameToType);
            }

            return typeNameToType.GetLoadedTypeByTypeName(typeKey, useTaskHost);
        }

        private readonly record struct LoadedTypeKey(TypeFilter Filter, string Name);

        /// <summary>
        /// Given a type filter and an asssemblyInfo object keep track of what types in a given assembly which match the type filter.
        /// Also, use this information to determine if a given TypeName is in the assembly which is pointed to by the AssemblyLoadInfo object.
        ///
        /// This type represents a combination of a type filter and an assemblyInfo object.
        /// </summary>
        private class AssemblyInfoToLoadedTypes
        {
            /// <summary>
            /// Assembly load information so we can load an assembly
            /// </summary>
            private readonly AssemblyLoadInfo _assemblyLoadInfo;

            /// <summary>
            /// What is the type for the given type name and filter, this may be null if the typeName does not map to a type.
            /// </summary>
            private ImmutableDictionary<LoadedTypeKey, LoadedType?> _loadedTypes = ImmutableDictionary<LoadedTypeKey, LoadedType?>.Empty;

            /// <summary>
            /// A separate cache for types loaded via MetadataLoadContext, so that they don't pollute reflection-only loads.
            /// </summary>
            private ImmutableDictionary<LoadedTypeKey, LoadedType?> _loadedTypesFromMetadataLoadContext = ImmutableDictionary<LoadedTypeKey, LoadedType?>.Empty;

            /// <summary>
            /// List of public types in the assembly which match any type filter and their corresponding types
            /// </summary>
            private PublicTypes? _publicTypes;

            /// <summary>
            /// Given a type filter, and an assembly to load the type information from determine if a given type name is in the assembly or not.
            /// </summary>
            internal AssemblyInfoToLoadedTypes(AssemblyLoadInfo loadInfo)
            {
                ErrorUtilities.VerifyThrowArgumentNull(loadInfo);

                _assemblyLoadInfo = loadInfo;
            }

            /// <summary>
            /// Determine if a given type name is in the assembly or not. Return null if the type is not in the assembly
            /// </summary>
            internal LoadedType? GetLoadedTypeByTypeName(LoadedTypeKey typeKey, bool useTaskHost)
            {
                ErrorUtilities.VerifyThrowArgumentNull(typeKey.Name);

                if (useTaskHost && _assemblyLoadInfo.AssemblyFile is not null)
                {
                    return GetLoadedTypeFromTypeNameUsingMetadataLoadContext(typeKey);
                }

                // If multiple threads are doing operations on this instance, one thread will succeed in updating the
                // cache, and the rest will just perform duplicated work.
                if (!_loadedTypes.TryGetValue(typeKey, out LoadedType? loadedType))
                {
                    Type? type = null;
                    if ((_assemblyLoadInfo.AssemblyName != null) && (typeKey.Name.Length > 0))
                    {
                        try
                        {
                            // try to load the type using its assembly qualified name
                            Type? t2 = Type.GetType(typeKey.Name + "," + _assemblyLoadInfo.AssemblyName, false /* don't throw on error */, true /* case-insensitive */);
                            if (t2 != null)
                            {
                                type = !IsDesiredType(t2, typeKey.Filter) ? null : t2;
                            }
                        }
                        catch (ArgumentException)
                        {
                            // Type.GetType() will throw this exception if the type name is invalid -- but we have no idea if it's the
                            // type or the assembly name that's the problem -- so just ignore the exception, because we're going to
                            // check the existence/validity of the assembly and type respectively, below anyway
                        }
                    }

                    if (type == null)
                    {
                        _publicTypes ??= new PublicTypes(_assemblyLoadInfo);
                        foreach (Type desiredTypeInAssembly in _publicTypes.GetTypes(typeKey.Filter))
                        {
                            // if type matches partially on its name
                            if (typeKey.Name.Length == 0 || IsPartialTypeNameMatch(desiredTypeInAssembly.FullName, typeKey.Name))
                            {
                                type = desiredTypeInAssembly;
                                break;
                            }
                        }
                    }

                    loadedType = type != null ? new LoadedType(type, _assemblyLoadInfo, _publicTypes?.LoadedAssembly ?? type.Assembly, typeof(ITaskItem), loadedViaMetadataLoadContext: false) : null;
                    _loadedTypes = _loadedTypes.SetItem(typeKey, loadedType);
                }

                return loadedType;
            }

            /// <summary>
            /// Searches all public types from an assembly for a type that matches the given type name and filter.
            /// </summary>
            /// <remarks>
            /// Unlike reflection-loaded types, we can't cache the loaded Assembly or list of public Type objects.
            /// They will be invalid as soon as the MetadataLoadContext is disposed.
            /// </remarks>
            private LoadedType? GetLoadedTypeFromTypeNameUsingMetadataLoadContext(LoadedTypeKey typeKey)
            {
                if (!_loadedTypesFromMetadataLoadContext.TryGetValue(typeKey, out LoadedType? loadedType))
                {
                    MSBuildEventSource.Log.LoadAssemblyAndFindTypeStart();
                    string path = _assemblyLoadInfo.AssemblyFile;
                    using MetadataLoadContext context = LoadMetadataLoadContext(path);
                    Assembly loadedAssembly = context.LoadFromAssemblyPath(path);
                    int numberOfTypesSearched = 0;
                    foreach (Type publicType in loadedAssembly.GetExportedTypes())
                    {
                        numberOfTypesSearched++;
                        if (IsDesiredType(publicType, typeKey.Filter) && (typeKey.Name.Length == 0 || IsPartialTypeNameMatch(publicType.FullName, typeKey.Name)))
                        {
                            MSBuildEventSource.Log.CreateLoadedTypeStart(loadedAssembly.FullName!);
                            loadedType = new(publicType, _assemblyLoadInfo, loadedAssembly, context.LoadFromAssemblyPath(microsoftBuildFrameworkPath).GetType(typeof(ITaskItem).FullName!)!, loadedViaMetadataLoadContext: true);
                            MSBuildEventSource.Log.CreateLoadedTypeStop(loadedAssembly.FullName!);
                        }
                    }

                    MSBuildEventSource.Log.LoadAssemblyAndFindTypeStop(_assemblyLoadInfo.AssemblyFile, numberOfTypesSearched);

                    _loadedTypesFromMetadataLoadContext = _loadedTypesFromMetadataLoadContext.SetItem(typeKey, loadedType);
                }

                return loadedType;
            }
        }

        /// <summary>
        /// Container for all public types from an assembly that match on any type filter.
        /// </summary>
        private class PublicTypes
        {
            private readonly List<Type> _taskTypes = [];
            private readonly List<Type> _forwardingLoggerTypes = [];
            private readonly List<Type> _loggerTypes = [];
            private readonly List<Type> _taskFactoryTypes = [];

            /// <summary>
            /// Scan the assembly pointed to by the assemblyLoadInfo for public types. We will use these public types to do partial name matching on
            /// to find tasks, loggers, and task factories.
            /// </summary>
            internal PublicTypes(AssemblyLoadInfo assemblyLoadInfo)
            {
                // we need to search the assembly for the type...
                LoadedAssembly = LoadAssembly(assemblyLoadInfo);

                // only look at public types
                Type[] allPublicTypesInAssembly = LoadedAssembly.GetExportedTypes();
                foreach (Type publicType in allPublicTypesInAssembly)
                {
                    TypeInfo typeInfo = publicType.GetTypeInfo();

                    if (IsDesiredType(typeInfo, TypeFilter.Task))
                    {
                        _taskTypes.Add(publicType);
                    }

                    if (IsDesiredType(typeInfo, TypeFilter.ForwardingLogger))
                    {
                        _forwardingLoggerTypes.Add(publicType);
                    }

                    if (IsDesiredType(typeInfo, TypeFilter.Logger))
                    {
                        _loggerTypes.Add(publicType);
                    }

                    if (IsDesiredType(typeInfo, TypeFilter.TaskFactory))
                    {
                        _taskFactoryTypes.Add(publicType);
                    }
                }
            }

            /// <summary>
            /// Gets the assembly loaded from the original AsssemblyLoadInfo.
            /// </summary>
            internal Assembly LoadedAssembly { get; }

            /// <summary>
            /// Returns the cached list of public types which match the type filter.
            /// </summary>
            internal List<Type> GetTypes(TypeFilter typeFilter) => typeFilter switch
            {
                TypeFilter.Task => _taskTypes,
                TypeFilter.ForwardingLogger => _forwardingLoggerTypes,
                TypeFilter.Logger => _loggerTypes,
                TypeFilter.TaskFactory => _taskFactoryTypes,
                _ => throw new InternalErrorException("Unknown type filter"),
            };
        }
    }
}
