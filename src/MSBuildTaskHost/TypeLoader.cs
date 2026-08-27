// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using Microsoft.Build.TaskHost.Collections;
using Microsoft.Build.TaskHost.Utilities;

namespace Microsoft.Build.TaskHost;

/// <summary>
/// This class is used to load types from their assemblies.
/// </summary>
internal sealed class TypeLoader
{
    /// <summary>
    /// Cache to keep track of the assemblyLoadInfos based on a given typeFilter.
    /// </summary>
    private static readonly ConcurrentDictionary<TypeFilter, ConcurrentDictionary<string, TypeCache>> s_cacheOfLoadedTypesByFilter = new();

    /// <summary>
    /// Typefilter for this typeloader
    /// </summary>
    private readonly TypeFilter _isDesiredType;

    /// <summary>
    /// Constructor.
    /// </summary>
    internal TypeLoader(TypeFilter isDesiredType)
    {
        ErrorUtilities.VerifyThrow(isDesiredType != null, "need a type filter");

        _isDesiredType = isDesiredType;
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
            isPartialMatch = string.Equals(typeName1, typeName2, StringComparison.OrdinalIgnoreCase);
        }

        return isPartialMatch;
    }

    /// <summary>
    /// Loads the specified type if it exists in the given assembly. If the type name is fully qualified, then a match (if
    /// any) is unambiguous; otherwise, if there are multiple types with the same name in different namespaces, the first type
    /// found will be returned.
    /// </summary>
    internal LoadedType? Load(string typeName, string assemblyFilePath)
        => GetLoadedType(s_cacheOfLoadedTypesByFilter, typeName, assemblyFilePath);

    /// <summary>
    /// Loads the specified type if it exists in the given assembly. If the type name is fully qualified, then a match (if
    /// any) is unambiguous; otherwise, if there are multiple types with the same name in different namespaces, the first type
    /// found will be returned.
    /// </summary>
    private LoadedType? GetLoadedType(
        ConcurrentDictionary<TypeFilter, ConcurrentDictionary<string, TypeCache>> cache,
        string typeName,
        string assemblyFilePath)
    {
        // A given type filter have been used on a number of assemblies, Based on the type filter
        // we will get another dictionary which will map a specific assembly file path to a
        // AssemblyInfoToLoadedTypes class which knows how to find a typeName in a given assembly.
        var loadInfoToType = cache.GetOrAdd(_isDesiredType, _ => new(StringComparer.OrdinalIgnoreCase));

        // Get an object which is able to take a type name and determine if it is in the assembly pointed to by the AssemblyInfo.
        var typeNameToType = loadInfoToType.GetOrAdd(assemblyFilePath, assemblyFilePath => new(_isDesiredType, assemblyFilePath));

        return typeNameToType.GetLoadedTypeByTypeName(typeName);
    }

    /// <summary>
    /// Given a type filter and an asssemblyInfo object keep track of what types in a given assembly which match the typefilter.
    /// Also, use this information to determine if a given TypeName is in the assembly which is pointed to by the AssemblyLoadInfo object.
    ///
    /// This type represents a combination of a type filter and an assemblyInfo object.
    /// </summary>
    private sealed class TypeCache
    {
        /// <summary>
        /// Lock to prevent two threads from using this object at the same time.
        /// Since we fill up internal structures with what is in the assembly.
        /// </summary>
        private readonly object _lockObject = new();

        /// <summary>
        /// Type filter to pick the correct types out of an assembly.
        /// </summary>
        private readonly TypeFilter _isDesiredType;

        /// <summary>
        /// Assembly file path so we can load an assembly.
        /// </summary>
        private readonly string _assemblyFilePath;

        /// <summary>
        /// What is the type for the given type name, this may be null if the typeName does not map to a type.
        /// </summary>
        private readonly ConcurrentDictionary<string, Type?> _typeNameToType;

        /// <summary>
        /// List of public types in the assembly which match the typefilter and their corresponding types.
        /// </summary>
        private readonly Dictionary<string, Type> _publicTypeNameToType;

        /// <summary>
        /// Have we scanned the public types for this assembly yet.
        /// </summary>
        private long _haveScannedPublicTypes;

        /// <summary>
        /// If we loaded an assembly for this type.
        /// We use this information to set the LoadedType.LoadedAssembly so that this object can be used
        /// to help created AppDomains to resolve those that it could not load successfully.
        /// </summary>
        private Assembly? _loadedAssembly;

        internal TypeCache(TypeFilter typeFilter, string assemblyFilePath)
        {
            ErrorUtilities.VerifyThrowArgumentNull(typeFilter);
            ErrorUtilities.VerifyThrowArgumentNull(assemblyFilePath);

            _isDesiredType = typeFilter;
            _assemblyFilePath = assemblyFilePath;
            _typeNameToType = new ConcurrentDictionary<string, Type?>(StringComparer.OrdinalIgnoreCase);
            _publicTypeNameToType = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Determine if a given type name is in the assembly or not. Return null if the type is not in the assembly
        /// </summary>
        internal LoadedType? GetLoadedTypeByTypeName(string typeName)
        {
            ErrorUtilities.VerifyThrowArgumentNull(typeName);

            // Only one thread should be doing operations on this instance of the object at a time.
            Type? type = _typeNameToType.GetOrAdd(typeName, (key) =>
            {
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
                ? new LoadedType(type, _assemblyFilePath, _loadedAssembly ?? type.Assembly)
                : null;
        }

        /// <summary>
        /// Scan the assembly pointed to by the assemblyLoadInfo for public types. We will use these public types to do partial name matching on
        /// to find tasks, loggers, and task factories.
        /// </summary>
        private void ScanAssemblyForPublicTypes()
        {
            // we need to search the assembly for the type...
            try
            {
                _loadedAssembly = Assembly.LoadFrom(_assemblyFilePath);
            }
            catch (ArgumentException e)
            {
                // Assembly.Load() and Assembly.LoadFrom() will throw an ArgumentException if the assembly name is invalid
                // convert to a FileNotFoundException because it's more meaningful
                // NOTE: don't use ErrorUtilities.VerifyThrowFileExists() here because that will hit the disk again
                throw new FileNotFoundException(message: null, _assemblyFilePath, e);
            }

            // only look at public types
            Type[] allPublicTypesInAssembly = _loadedAssembly.GetExportedTypes();
            foreach (Type publicType in allPublicTypesInAssembly)
            {
                if (_isDesiredType(publicType, filterCriteria: null))
                {
                    _publicTypeNameToType.Add(publicType.FullName, publicType);
                }
            }
        }
    }
}
