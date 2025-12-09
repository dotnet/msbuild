// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Build.Eventing;
using Microsoft.Build.Framework;
using static Microsoft.Build.Shared.XMakeAttributes;

#nullable disable

namespace Microsoft.Build.Shared
{
    /// <summary>
    /// This class is used to load types from their assemblies.
    /// </summary>
    internal class TypeLoader
    {
#if FEATURE_ASSEMBLYLOADCONTEXT
        /// <summary>
        /// AssemblyContextLoader used to load DLLs outside of msbuild.exe directory
        /// </summary>
        private static readonly CoreClrAssemblyLoader s_coreClrAssemblyLoader = new CoreClrAssemblyLoader();
#endif

        /// <summary>
        /// Assembly name that indicates .NET Core/5+ if present as a referenced assembly.
        /// </summary>
        private const string SystemRuntimeAssemblyName = "System.Runtime";

        /// <summary>
        /// NET target moniker name.
        /// </summary>
        private const string DotNetCoreIdentifier = ".NETCore";

        /// <summary>
        /// Assembly custom attribute name.
        /// </summary>
        private const string TargetFrameworkAttributeName = "TargetFrameworkAttribute";

        /// <summary>
        /// Versioning namespace name.
        /// </summary>
        private const string VersioningNamespaceName = "System.Runtime.Versioning";

        /// <summary>
        /// Cache to keep track of the assemblyLoadInfos based on a given type filter.
        /// </summary>
        private static readonly ConcurrentDictionary<Func<Type, object, bool>, ConcurrentDictionary<AssemblyLoadInfo, AssemblyInfoToLoadedTypes>> s_cacheOfLoadedTypesByFilter = new ConcurrentDictionary<Func<Type, object, bool>, ConcurrentDictionary<AssemblyLoadInfo, AssemblyInfoToLoadedTypes>>();

        /// <summary>
        /// Cache to keep track of the assemblyLoadInfos based on a given type filter for assemblies which are to be loaded for reflectionOnlyLoads.
        /// </summary>
        private static readonly ConcurrentDictionary<Func<Type, object, bool>, ConcurrentDictionary<AssemblyLoadInfo, AssemblyInfoToLoadedTypes>> s_cacheOfReflectionOnlyLoadedTypesByFilter = new ConcurrentDictionary<Func<Type, object, bool>, ConcurrentDictionary<AssemblyLoadInfo, AssemblyInfoToLoadedTypes>>();

        /// <summary>
        /// Type filter for this typeloader.
        /// </summary>
        private Func<Type, object, bool> _isDesiredType;

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

#if NETFRAMEWORK
        private static readonly string[] _runtimeAssembliesCLR35_20 = FindRuntimeAssembliesWithMicrosoftBuildFrameworkCLR2CLR35();

        /// <summary>
        /// Gathers a list of runtime assemblies for the <see cref="MetadataLoadContext"/>.
        /// This includes assemblies from the MSBuild installation directory, the current .NET runtime directory,
        /// and on .NET Framework, assemblies from older framework versions (2.0, 3.5).
        /// The path to the current `Microsoft.Build.Framework.dll` is also stored to ensure it's prioritized
        /// for resolving essential types like <see cref="ITaskItem"/>.
        /// These paths are used to create a <see cref="PathAssemblyResolver"/> for the <see cref="MetadataLoadContext"/>.
        /// </summary>
        private static string[] FindRuntimeAssembliesWithMicrosoftBuildFrameworkCLR2CLR35()
        {
            string v20Path = FrameworkLocationHelper.PathToDotNetFrameworkV20;
            string v35Path = FrameworkLocationHelper.PathToDotNetFrameworkV35;

            string[] clr2Assemblies = !string.IsNullOrEmpty(v20Path) && Directory.Exists(v20Path)
                ? Directory.GetFiles(v20Path, "*.dll")
                : [];
            string[] clr35Assemblies = !string.IsNullOrEmpty(v35Path) && Directory.Exists(v35Path)
                ? Directory.GetFiles(v35Path, "*.dll")
                : [];

            return [.. clr2Assemblies, .. clr35Assemblies];
        }
#endif

        /// <summary>
        /// Constructor.
        /// </summary>
        internal TypeLoader(Func<Type, object, bool> isDesiredType)
        {
            ErrorUtilities.VerifyThrow(isDesiredType != null, "need a type filter");

            _isDesiredType = isDesiredType;
        }

        /// <summary>
        /// Delegate used to log warning messages with formatted string support.
        /// </summary>
        /// <param name="format">A composite format string for the warning message.</param>
        /// <param name="args">An array of objects to format into the warning message.</param>
        internal delegate void LogWarningDelegate(string format, params object[] args);

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
                else if (assemblyLoadInfo.IsInlineTask)
                {
                    // Load inline task assemblies from bytes and register the path
                    return TaskFactoryUtilities.LoadTaskAssembly(assemblyLoadInfo.AssemblyFile);
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

        private static MetadataLoadContext CreateMetadataLoadContext(AssemblyLoadInfo assemblyLoadInfo)
        {
            string assemblyFilePath = assemblyLoadInfo.AssemblyFile;
            if (string.IsNullOrEmpty(assemblyFilePath) || !File.Exists(assemblyFilePath))
            {
                throw new FileNotFoundException(null, assemblyLoadInfo.AssemblyLocation);
            }

            string assemblyDirectory = Path.GetDirectoryName(assemblyFilePath);
            string[] dlls = Directory.GetFiles(assemblyDirectory, "*.dll");
            string[] exes = Directory.GetFiles(assemblyDirectory, "*.exe");
            string[] localAssemblies = [.. dlls, .. exes];

#if !NETFRAMEWORK

            // Deduplicate between MSBuild assemblies and task dependencies.
            Dictionary<string, string> assembliesDictionary = new(localAssemblies.Length + runtimeAssemblies.Length);
            foreach (string localPath in localAssemblies)
            {
                assembliesDictionary[Path.GetFileName(localPath)] = localPath;
            }

            foreach (string runtimeAssembly in runtimeAssemblies)
            {
                assembliesDictionary[Path.GetFileName(runtimeAssembly)] = runtimeAssembly;
            }

            return new MetadataLoadContext(new PathAssemblyResolver(assembliesDictionary.Values));

#else
           // Merge all assembly tiers into one dictionary with priority:
            // CLR2 < CLR3.5 < Local < Runtime (later entries overwrite earlier ones)
            Dictionary<string, string> assembliesDictionary = new(StringComparer.OrdinalIgnoreCase);

            // Add assemblies in priority order (later entries overwrite earlier ones)
            AddAssembliesToDictionary(
                assembliesDictionary,
                _runtimeAssembliesCLR35_20,
                localAssemblies,
                runtimeAssemblies);

            return new MetadataLoadContext(new PathAssemblyResolver(assembliesDictionary.Values));

            static void AddAssembliesToDictionary(Dictionary<string, string> assembliesDictionary, params string[][] assemblyPathArrays)
            {
                foreach (string[] assemblyPaths in assemblyPathArrays)
                {
                    foreach (string path in assemblyPaths)
                    {
                        assembliesDictionary[Path.GetFileName(path)] = path;
                    }
                }
            }
#endif
        }

        /// <summary>
        /// Loads the specified type if it exists in the given assembly. If the type name is fully qualified, then a match (if
        /// any) is unambiguous; otherwise, if there are multiple types with the same name in different namespaces, the first type
        /// found will be returned.
        /// </summary>
        internal LoadedType Load(
            string typeName,
            AssemblyLoadInfo assembly,
            LogWarningDelegate logWarning,
            bool useTaskHost = false,
            bool taskHostParamsMatchCurrentProc = true)
        {
            return GetLoadedType(s_cacheOfLoadedTypesByFilter, typeName, assembly, useTaskHost, taskHostParamsMatchCurrentProc, logWarning);
        }

        /// <summary>
        /// Loads the specified type if it exists in the given assembly. If the type name is fully qualified, then a match (if
        /// any) is unambiguous; otherwise, if there are multiple types with the same name in different namespaces, the first type
        /// found will be returned.
        /// </summary>
        /// <returns>The loaded type, or null if the type was not found.</returns>
        internal LoadedType ReflectionOnlyLoad(
            string typeName,
            AssemblyLoadInfo assembly) => GetLoadedType(s_cacheOfReflectionOnlyLoadedTypesByFilter, typeName, assembly, useTaskHost: false, taskHostParamsMatchCurrentProc: true, logWarning: (format, args) => { });

        /// <summary>
        /// Loads the specified type if it exists in the given assembly. If the type name is fully qualified, then a match (if
        /// any) is unambiguous; otherwise, if there are multiple types with the same name in different namespaces, the first type
        /// found will be returned.
        /// </summary>
        private LoadedType GetLoadedType(
            ConcurrentDictionary<Func<Type, object, bool>, ConcurrentDictionary<AssemblyLoadInfo, AssemblyInfoToLoadedTypes>> cache,
            string typeName,
            AssemblyLoadInfo assembly,
            bool useTaskHost,
            bool taskHostParamsMatchCurrentProc,
            LogWarningDelegate logWarning)
        {
            // A given type filter have been used on a number of assemblies, Based on the type filter we will get another dictionary which
            // will map a specific AssemblyLoadInfo to a AssemblyInfoToLoadedTypes class which knows how to find a typeName in a given assembly.
            ConcurrentDictionary<AssemblyLoadInfo, AssemblyInfoToLoadedTypes> loadInfoToType =
                cache.GetOrAdd(_isDesiredType, (_) => new ConcurrentDictionary<AssemblyLoadInfo, AssemblyInfoToLoadedTypes>());

            // Get an object which is able to take a typename and determine if it is in the assembly pointed to by the AssemblyInfo.
            AssemblyInfoToLoadedTypes typeNameToType =
                loadInfoToType.GetOrAdd(assembly, (_) => new AssemblyInfoToLoadedTypes(_isDesiredType, _));

            return typeNameToType.GetLoadedTypeByTypeName(typeName, useTaskHost, taskHostParamsMatchCurrentProc, logWarning);
        }

        /// <summary>
        /// Given a type filter and an asssemblyInfo object keep track of what types in a given assembly which match the type filter.
        /// Also, use this information to determine if a given TypeName is in the assembly which is pointed to by the AssemblyLoadInfo object.
        ///
        /// This type represents a combination of a type filter and an assemblyInfo object.
        /// </summary>
        [DebuggerDisplay("Types in {_assemblyLoadInfo} matching {_isDesiredType}")]
        private class AssemblyInfoToLoadedTypes
        {
            /// <summary>
            /// Lock to prevent two threads from using this object at the same time.
            /// Since we fill up internal structures with what is in the assembly
            /// </summary>
            private readonly LockType _lockObject = new();

            /// <summary>
            /// Type filter to pick the correct types out of an assembly
            /// </summary>
            private Func<Type, object, bool> _isDesiredType;

            /// <summary>
            /// Assembly load information so we can load an assembly
            /// </summary>
            private AssemblyLoadInfo _assemblyLoadInfo;

            /// <summary>
            /// What is the type for the given type name, this may be null if the typeName does not map to a type.
            /// </summary>
            private ConcurrentDictionary<string, Type> _typeNameToType;

            /// <summary>
            /// List of public types in the assembly which match the type filter and their corresponding types
            /// </summary>
            private Dictionary<string, Type> _publicTypeNameToType;

            private ConcurrentDictionary<string, LoadedType> _publicTypeNameToLoadedType;

            /// <summary>
            /// Have we scanned the public types for this assembly yet.
            /// </summary>
            private long _haveScannedPublicTypes;

            /// <summary>
            /// Assembly, if any, that we loaded for this type.
            /// We use this information to set the LoadedType.LoadedAssembly so that this object can be used
            /// to help created AppDomains to resolve those that it could not load successfully
            /// </summary>
            private Assembly _loadedAssembly;

            /// <summary>
            /// The architecture requirement of the assembly.
            /// </summary>
            private string _architecture;

            /// <summary>
            /// The runtime requirement of the assembly.
            /// Detected by examining referenced assemblies for System.Runtime (indicates .NET Core/5+).
            /// </summary>
            private string _runtime;

            /// <summary>
            /// Flag to track if we've already attempted to get assembly runtime/architecture.
            /// This prevents repeated expensive PE header reads.
            /// </summary>
            private volatile bool _hasReadRuntimeAndArchitecture;

            /// <summary>
            /// Given a type filter, and an assembly to load the type information from determine if a given type name is in the assembly or not.
            /// </summary>
            internal AssemblyInfoToLoadedTypes(Func<Type, object, bool> typeFilter, AssemblyLoadInfo loadInfo)
            {
                ErrorUtilities.VerifyThrowArgumentNull(typeFilter, "typefilter");
                ErrorUtilities.VerifyThrowArgumentNull(loadInfo);

                _isDesiredType = typeFilter;
                _assemblyLoadInfo = loadInfo;
                _typeNameToType = new(StringComparer.OrdinalIgnoreCase);
                _publicTypeNameToType = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
                _publicTypeNameToLoadedType = new(StringComparer.OrdinalIgnoreCase);
            }

            /// <summary>
            /// Determine if a given type name is in the assembly or not. Return null if the type is not in the assembly.
            /// </summary>
            internal LoadedType GetLoadedTypeByTypeName(
                string typeName,
                bool useTaskHost,
                bool taskHostParamsMatchCurrentProc,
                LogWarningDelegate logWarning)
            {
                ErrorUtilities.VerifyThrowArgumentNull(typeName);

                if (ShouldUseMetadataLoadContext(useTaskHost, taskHostParamsMatchCurrentProc))
                {
                    return GetTypeForOutOfProcExecution(typeName);
                }

                LoadedType loadedType;

                try
                {
                    loadedType = LoadInProc(typeName);
                }
                catch
                {
                    // The assembly can't be loaded in-proc due to architecture or runtime mismatch that was discovered during in-proc load.
                    // Fall back to metadata load context. It will prepare prerequisites for out of proc execution.
                    MSBuildEventSource.Log.FallbackAssemblyLoadStart(typeName);
                    loadedType = GetTypeForOutOfProcExecution(typeName);
                    logWarning("AssemblyLoad_Warning", loadedType?.LoadedAssemblyName?.Name);
                    MSBuildEventSource.Log.FallbackAssemblyLoadStop(typeName);
                }

                return loadedType;
            }

            /// <summary>
            /// Normal in-proc loading path.
            /// Only one thread should be doing operations on this instance of the object at a time
            /// This loads the assembly for actual execution (not metadata-only).
            /// </summary>
            /// <param name="typeName">The type to be loaded.</param>
            private LoadedType LoadInProc(string typeName)
            {
                Type type = _typeNameToType.GetOrAdd(typeName, (key) =>
                {
                    if ((_assemblyLoadInfo.AssemblyName != null) && (typeName.Length > 0))
                    {
                        try
                        {
                            // try to load the type using its assembly qualified name
                            Type t2 = Type.GetType(typeName + "," + _assemblyLoadInfo.AssemblyName, false /* don't throw on error */, true /* case-insensitive */);
                            if (t2 != null)
                            {
                                return !_isDesiredType(t2, null) ? null : t2;
                            }
                        }
                        catch (ArgumentException)
                        {
                            // Type.GetType() will throw this exception if the type name is invalid -- but we have no idea if it's the
                            // type or the assembly name that's the problem -- so just ignore the exception, because we're going to
                            // check the existence/validity of the assembly and type respectively, below anyway
                        }
                    }

                    if (Interlocked.Read(ref _haveScannedPublicTypes) == 0)
                    {
                        lock (_lockObject)
                        {
                            if (Interlocked.Read(ref _haveScannedPublicTypes) == 0)
                            {
                                ScanAssemblyForPublicTypes();
                                Interlocked.Exchange(ref _haveScannedPublicTypes, ~0);
                            }
                        }
                    }

                    foreach (KeyValuePair<string, Type> desiredTypeInAssembly in _publicTypeNameToType)
                    {
                        // if type matches partially on its name
                        if (typeName.Length == 0 || IsPartialTypeNameMatch(desiredTypeInAssembly.Key, typeName))
                        {
                            return desiredTypeInAssembly.Value;
                        }
                    }

                    return null;
                });

                return type != null
                    ? new LoadedType(type, _assemblyLoadInfo, _loadedAssembly ?? type.Assembly, typeof(ITaskItem), loadedViaMetadataLoadContext: false)
                    : null;
            }

            /// <summary>
            /// Determine whether an assembly is likely to be used out of process and thus loaded with a <see cref="MetadataLoadContext"/>.
            /// </summary>
            /// <param name="useTaskHost">Task Host Parameter was specified explicitly in XML or through environment variable.</param>
            /// <param name="taskHostParamsMatchCurrentProc">The parameter defines if Runtime/Architecture explicitly defined in XML match current process.</param>
            private bool ShouldUseMetadataLoadContext(bool useTaskHost, bool taskHostParamsMatchCurrentProc) =>
                (useTaskHost || !taskHostParamsMatchCurrentProc) && _assemblyLoadInfo.AssemblyFile is not null;

            private LoadedType GetTypeForOutOfProcExecution(string typeName) => _publicTypeNameToLoadedType
                .GetOrAdd(typeName, typeName =>
                {
                    MSBuildEventSource.Log.LoadAssemblyAndFindTypeStart();
                    using MetadataLoadContext context = CreateMetadataLoadContext(_assemblyLoadInfo);
                    Assembly loadedAssembly = context.LoadFromAssemblyPath(_assemblyLoadInfo.AssemblyFile);
                    SetArchitectureAndRuntime(loadedAssembly);

                    Type foundType = null;
                    int numberOfTypesSearched = 0;

                    // Try direct type lookup first (fastest)
                    if (!string.IsNullOrEmpty(typeName))
                    {
                        foundType = loadedAssembly.GetType(typeName, throwOnError: false);
                        if (foundType != null && foundType.IsPublic && _isDesiredType(foundType, null))
                        {
                            numberOfTypesSearched = 1;
                        }
                    }

                    // Fallback: enumerate all types for partial matching
                    if (foundType == null)
                    {
                        foreach (Type publicType in loadedAssembly.GetExportedTypes())
                        {
                            numberOfTypesSearched++;
                            try
                            {
                                if (_isDesiredType(publicType, null) && (typeName.Length == 0 || IsPartialTypeNameMatch(publicType.FullName, typeName)))
                                {
                                    foundType = publicType;
                                    break;
                                }
                            }
                            catch
                            {
                                // Ignore types that can't be loaded/reflected upon.
                                // These types might be needed out of proc and be resolved there.
                            }
                        }
                    }

                    if (foundType != null)
                    {
                        MSBuildEventSource.Log.CreateLoadedTypeStart(loadedAssembly.FullName);
                        var taskItemType = context.LoadFromAssemblyPath(microsoftBuildFrameworkPath).GetType(typeof(ITaskItem).FullName);
                        LoadedType loadedType = new(foundType, _assemblyLoadInfo, loadedAssembly, taskItemType, _runtime, _architecture, loadedViaMetadataLoadContext: true);

                        MSBuildEventSource.Log.CreateLoadedTypeStop(loadedAssembly.FullName);
                        return loadedType;
                    }

                    MSBuildEventSource.Log.LoadAssemblyAndFindTypeStop(_assemblyLoadInfo.AssemblyFile, numberOfTypesSearched);

                    return null;
                });

            /// <summary>
            /// Gets architecture and runtime from the assembly using MetadataLoadContext.
            /// </summary>
            private void SetArchitectureAndRuntime(Assembly assembly)
            {
                if (_hasReadRuntimeAndArchitecture)
                {
                    return;
                }

                try
                {
                    SetRuntime();
                    SetArchitecture();
                    _hasReadRuntimeAndArchitecture = true;
                }
                catch
                {
                    // If we fail to read the assembly for any reason don't throw, just reset the values.
                    _architecture = null;
                    _runtime = null;
                    _hasReadRuntimeAndArchitecture = false;
                }

                void SetRuntime()
                {
                    string targetFramework = null;
                    try
                    {
                        CustomAttributeData targetFrameworkAttr = assembly?
                            .GetCustomAttributesData()?
                            .FirstOrDefault(a => a.AttributeType.Name == TargetFrameworkAttributeName && a.AttributeType.Namespace == VersioningNamespaceName);

                        if (targetFrameworkAttr != null && targetFrameworkAttr.ConstructorArguments.Count > 0)
                        {
                            // the final value looks like: ".NETFramework,Version=v3.5"
                            targetFramework = targetFrameworkAttr.ConstructorArguments[0].Value as string ?? string.Empty;
                            _runtime = targetFramework.StartsWith(DotNetCoreIdentifier) ? MSBuildRuntimeValues.net : MSBuildRuntimeValues.clr4;
                        }
                    }
                    catch
                    {
                        // something went wrong with reading the custom attribute!
                    }

                    if (targetFramework == null && _runtime == null)
                    {
                        bool hasSystemRuntime = assembly.GetReferencedAssemblies().Any(a => string.Equals(a.Name, SystemRuntimeAssemblyName, StringComparison.OrdinalIgnoreCase));
                        if (hasSystemRuntime)
                        {
                            _runtime = MSBuildRuntimeValues.net;
                        }
                    }
                }

                void SetArchitecture()
                {
                    Module module = assembly?.Modules?.FirstOrDefault();
                    if (module == null)
                    {
                        return;
                    }

                    module.GetPEKind(out PortableExecutableKinds peKind, out ImageFileMachine machine);

                    bool isILOnly = (peKind & PortableExecutableKinds.ILOnly) != 0;
                    bool requires32Bit = (peKind & PortableExecutableKinds.Required32Bit) != 0;
                    bool prefers32Bit = (peKind & PortableExecutableKinds.Preferred32Bit) != 0;

                    if (requires32Bit || prefers32Bit)
                    {
                        _architecture = MSBuildArchitectureValues.x86;
                        return;
                    }

                    if (isILOnly && machine == ImageFileMachine.I386)
                    {
                        _architecture = MSBuildArchitectureValues.any;
                        return;
                    }

                    _architecture = machine switch
                    {
                        ImageFileMachine.I386 => MSBuildArchitectureValues.x86,
                        ImageFileMachine.AMD64 => MSBuildArchitectureValues.x64,
                        (ImageFileMachine)0xAA64 => MSBuildArchitectureValues.arm64,
                        _ => MSBuildArchitectureValues.any,
                    };
                }
            }

            /// <summary>
            /// Scan the assembly pointed to by the assemblyLoadInfo for public types. We will use these public types to do partial name matching on
            /// to find tasks, loggers, and task factories.
            /// </summary>
            private void ScanAssemblyForPublicTypes()
            {
                // we need to search the assembly for the type...
                _loadedAssembly = LoadAssembly(_assemblyLoadInfo);

                // only look at public types
                Type[] allPublicTypesInAssembly = _loadedAssembly.GetExportedTypes();
                foreach (Type publicType in allPublicTypesInAssembly)
                {
                    if (_isDesiredType(publicType, null))
                    {
                        _publicTypeNameToType.Add(publicType.FullName, publicType);
                    }
                }
            }
        }
    }
}
