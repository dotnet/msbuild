// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
#if !NETFRAMEWORK
using System.Runtime.Loader;
#endif
using System.Threading;
using Microsoft.Build.Framework;

#nullable disable

namespace Microsoft.Build.Shared
{
    internal struct TaskRuntimeInformation
    {
        public bool TaskHostNeeded;
        public string Architecture;
        public string Runtime;
    }

    /// <summary>
    /// This class is used to load types from their assemblies.
    /// </summary>
    internal class TypeLoader
    {
#if FEATURE_ASSEMBLYLOADCONTEXT
        /// <summary>
        /// AssemblyContextLoader used to load DLLs outside of msbuild.exe directory
        /// </summary>
        private static readonly CoreClrAssemblyLoader s_coreClrAssemblyLoader;
#endif

        /// <summary>
        /// Cache to keep track of the assemblyLoadInfos based on a given type filter.
        /// </summary>
        private static ConcurrentDictionary<Func<Type, object, bool>, ConcurrentDictionary<AssemblyLoadInfo, AssemblyInfoToLoadedTypes>> s_cacheOfLoadedTypesByFilter = new ConcurrentDictionary<Func<Type, object, bool>, ConcurrentDictionary<AssemblyLoadInfo, AssemblyInfoToLoadedTypes>>();

        /// <summary>
        /// Cache to keep track of the assemblyLoadInfos based on a given type filter for assemblies which are to be loaded for reflectionOnlyLoads.
        /// </summary>
        private static ConcurrentDictionary<Func<Type, object, bool>, ConcurrentDictionary<AssemblyLoadInfo, AssemblyInfoToLoadedTypes>> s_cacheOfReflectionOnlyLoadedTypesByFilter = new ConcurrentDictionary<Func<Type, object, bool>, ConcurrentDictionary<AssemblyLoadInfo, AssemblyInfoToLoadedTypes>>();

        /// <summary>
        /// Type filter for this typeloader
        /// </summary>
        private Func<Type, object, bool> _isDesiredType;

#if FEATURE_ASSEMBLYLOADCONTEXT
        static TypeLoader()
        {
            s_coreClrAssemblyLoader = new CoreClrAssemblyLoader();
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
            Assembly loadedAssembly = null;

            try
            {
                if (assemblyLoadInfo.AssemblyName != null)
                {
#if !FEATURE_ASSEMBLYLOADCONTEXT
                    loadedAssembly = Assembly.Load(assemblyLoadInfo.AssemblyName);
#else
                    loadedAssembly = Assembly.Load(new AssemblyName(assemblyLoadInfo.AssemblyName));
#endif
                }
                else
                {
#if !FEATURE_ASSEMBLYLOADCONTEXT
                    loadedAssembly = Assembly.UnsafeLoadFrom(assemblyLoadInfo.AssemblyFile);
#else
                    var baseDir = Path.GetDirectoryName(assemblyLoadInfo.AssemblyFile);
                    s_coreClrAssemblyLoader.AddDependencyLocation(baseDir);
                    loadedAssembly = s_coreClrAssemblyLoader.LoadFromPath(assemblyLoadInfo.AssemblyFile);
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

            return loadedAssembly;
        }

        /// <summary>
        /// Loads the specified type if it exists in the given assembly. If the type name is fully qualified, then a match (if
        /// any) is unambiguous; otherwise, if there are multiple types with the same name in different namespaces, the first type
        /// found will be returned.
        /// </summary>
        internal TypeInformation Load
        (
            string typeName,
            AssemblyLoadInfo assembly,
            bool taskHostFactoryExplicitlyRequested,
            out TaskRuntimeInformation runtimeInformation
        )
        {
            return GetLoadedType(s_cacheOfLoadedTypesByFilter, typeName, assembly, taskHostFactoryExplicitlyRequested, out runtimeInformation);
        }

        /// <summary>
        /// Loads the specified type if it exists in the given assembly. If the type name is fully qualified, then a match (if
        /// any) is unambiguous; otherwise, if there are multiple types with the same name in different namespaces, the first type
        /// found will be returned.
        /// </summary>
        /// <returns>The loaded type, or null if the type was not found.</returns>
        internal LoadedType ReflectionOnlyLoad
        (
            string typeName,
            AssemblyLoadInfo assembly
        )
        {
            return GetLoadedType(s_cacheOfReflectionOnlyLoadedTypesByFilter, typeName, assembly, false, out _)?.LoadedType;
        }

        /// <summary>
        /// Loads the specified type if it exists in the given assembly. If the type name is fully qualified, then a match (if
        /// any) is unambiguous; otherwise, if there are multiple types with the same name in different namespaces, the first type
        /// found will be returned.
        /// </summary>
        private TypeInformation GetLoadedType(
            ConcurrentDictionary<Func<Type, object, bool>, ConcurrentDictionary<AssemblyLoadInfo, AssemblyInfoToLoadedTypes>> cache,
            string typeName,
            AssemblyLoadInfo assembly,
            bool taskHostFactoryExplicitlyRequested,
            out TaskRuntimeInformation runtimeInformation)
        {
            // A given type filter have been used on a number of assemblies, Based on the type filter we will get another dictionary which 
            // will map a specific AssemblyLoadInfo to a AssemblyInfoToLoadedTypes class which knows how to find a typeName in a given assembly.
            ConcurrentDictionary<AssemblyLoadInfo, AssemblyInfoToLoadedTypes> loadInfoToType =
                cache.GetOrAdd(_isDesiredType, (_) => new ConcurrentDictionary<AssemblyLoadInfo, AssemblyInfoToLoadedTypes>());

            // Get an object which is able to take a typename and determine if it is in the assembly pointed to by the AssemblyInfo.
            AssemblyInfoToLoadedTypes typeNameToType =
                loadInfoToType.GetOrAdd(assembly, (_) => new AssemblyInfoToLoadedTypes(_isDesiredType, _));

            return typeNameToType.GetLoadedTypeByTypeName(typeName, taskHostFactoryExplicitlyRequested, out runtimeInformation);
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
            private readonly Object _lockObject = new Object();

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
            private ConcurrentDictionary<string, TypeInformation> _typeNameToTypeInformation;

            /// <summary>
            /// What is the type for the given type name, this may be null if the typeName does not map to a type.
            /// </summary>
            private ConcurrentDictionary<string, TypeInformation> _typeNameToTypeInformationTaskHost;

            /// <summary>
            /// List of public types in the assembly which match the type filter and their corresponding types
            /// </summary>
            private Dictionary<string, Type> _publicTypeNameToType;

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
            /// Given a type filter, and an assembly to load the type information from determine if a given type name is in the assembly or not.
            /// </summary>
            internal AssemblyInfoToLoadedTypes(Func<Type, object, bool> typeFilter, AssemblyLoadInfo loadInfo)
            {
                ErrorUtilities.VerifyThrowArgumentNull(typeFilter, "typefilter");
                ErrorUtilities.VerifyThrowArgumentNull(loadInfo, nameof(loadInfo));

                _isDesiredType = typeFilter;
                _assemblyLoadInfo = loadInfo;
                _typeNameToTypeInformation = new ConcurrentDictionary<string, TypeInformation>(StringComparer.OrdinalIgnoreCase);
                _typeNameToTypeInformationTaskHost = new ConcurrentDictionary<string, TypeInformation>(StringComparer.OrdinalIgnoreCase);
                _publicTypeNameToType = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
            }

            /// <summary>
            /// Determine if a given type name is in the assembly or not. Return null if the type is not in the assembly
            /// </summary>
            internal TypeInformation GetLoadedTypeByTypeName(string typeName, bool taskHostFactoryExplicitlyRequested, out TaskRuntimeInformation runtimeInformation)
            {
                ErrorUtilities.VerifyThrowArgumentNull(typeName, nameof(typeName));

                runtimeInformation = new() { TaskHostNeeded = taskHostFactoryExplicitlyRequested };
                if (_assemblyLoadInfo.AssemblyFile is not null)
                {
                    using (FileStream stream = File.OpenRead(_assemblyLoadInfo.AssemblyFile))
                    using (PEReader reader = new(stream))
                    {
                        MetadataReader metadataReader = reader.GetMetadataReader();
                        AssemblyDefinition assemblyDef = metadataReader.GetAssemblyDefinition();
                        string targetFramework = null;
                        foreach (CustomAttributeHandle attrHandle in assemblyDef.GetCustomAttributes())
                        {
                            CustomAttribute attr = metadataReader.GetCustomAttribute(attrHandle);
                            if (attr.Constructor.Kind == HandleKind.MemberReference && metadataReader.GetString(metadataReader.GetTypeReference((TypeReferenceHandle)metadataReader.GetMemberReference((MemberReferenceHandle)attr.Constructor).Parent).Name).Equals("TargetFrameworkAttribute"))
                            {
                                BlobReader blobReader = metadataReader.GetBlobReader(attr.Value);
                                blobReader.ReadInt16();
                                targetFramework = blobReader.ReadSerializedString();
                                break;
                            }
                        }

                        Attribute msbuildFramework = Assembly.GetExecutingAssembly().GetCustomAttribute(typeof(TargetFrameworkAttribute));
                        bool netcoremsbuild = msbuildFramework is not null && msbuildFramework is TargetFrameworkAttribute tfa && !tfa.FrameworkDisplayName.Contains("Framework");
                        if (targetFramework is not null)
                        {
                            if (netcoremsbuild)
                            {
                                if (targetFramework.Contains("Framework"))
                                {
                                    runtimeInformation.Runtime = targetFramework.Contains("4.") ?
                                        XMakeAttributes.MSBuildRuntimeValues.clr4 :
                                        XMakeAttributes.MSBuildRuntimeValues.clr2;
                                }
                            }
                            else
                            {
                                if (!targetFramework.Contains("Framework"))
                                {
                                    runtimeInformation.Runtime = XMakeAttributes.MSBuildRuntimeValues.net;
                                }
                            }
                        }

                        if (runtimeInformation.Runtime is not null)
                        {
                            ErrorUtilities.ThrowArgument("Shared.CurrentRuntimeDoesNotMatchTask", typeName, netcoremsbuild ? ".NET Framework" : ".NET Core", netcoremsbuild ? ".NET Core" : ".NET Framework");
                        }

                        bool msbuildIs64Bit = RuntimeInformation.ProcessArchitecture == Architecture.X64;
                        Machine machineArch = reader.PEHeaders.CoffHeader.Machine;
                        if (msbuildIs64Bit && machineArch == Machine.AM33)
                        {
                            runtimeInformation.Architecture = XMakeAttributes.MSBuildArchitectureValues.x86;
                        }
                        else if (!msbuildIs64Bit && machineArch == Machine.Amd64)
                        {
                            runtimeInformation.Architecture = XMakeAttributes.MSBuildArchitectureValues.x64;
                        }

                        if (netcoremsbuild && runtimeInformation.Architecture == XMakeAttributes.MSBuildArchitectureValues.x86)
                        {
                            ErrorUtilities.ThrowArgument("Shared.32BitTaskOnCore");
                        }

                        if (runtimeInformation.Architecture is not null || runtimeInformation.Runtime is not null)
                        {
                            runtimeInformation.TaskHostNeeded = true;
                        }
                    }
                }

                // Only one thread should be doing operations on this instance of the object at a time.
                TypeInformation typeInfo = runtimeInformation.TaskHostNeeded ?
                    _typeNameToTypeInformationTaskHost.GetOrAdd(typeName, key => FindTypeInformationUsingSystemReflectionMetadata(typeName)) :
                    _typeNameToTypeInformation.GetOrAdd(typeName, key => FindTypeInformationUsingLoadedType(typeName)
                    );

                return typeInfo;
            }

            private bool Required32Bit(Machine arch)
            {
                return arch == Machine.AM33 || arch == Machine.Arm || arch == Machine.ArmThumb2 || arch == Machine.I386 || arch == Machine.Alpha || arch == Machine.M32R;
            }

            private bool Required64Bit(Machine arch)
            {
                return arch == Machine.Amd64 || arch == Machine.Arm64 || arch == Machine.IA64 || arch == Machine.Alpha64;
            }

            /// <summary>
            /// The user has not explicitly requested a TaskHost; load the type and use it to find relevant information.
            /// </summary>
            /// <param name="typeName">The type to find.</param>
            /// <returns>A <c ref="TypeInformation"/> with a LoadedType indicating relevant information.</returns>
            private TypeInformation FindTypeInformationUsingLoadedType(string typeName)
            {
                if ((_assemblyLoadInfo.AssemblyName != null) && (typeName.Length > 0))
                {
                    try
                    {
                        // try to load the type using its assembly qualified name
                        Type t2 = Type.GetType(typeName + "," + _assemblyLoadInfo.AssemblyName, false /* don't throw on error */, true /* case-insensitive */);
                        if (t2 != null)
                        {
                            return _isDesiredType(t2, null) ? new TypeInformation(new LoadedType(t2, _assemblyLoadInfo, _loadedAssembly)) : null;
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
                    if (typeName.Length == 0 || TypeLoader.IsPartialTypeNameMatch(desiredTypeInAssembly.Key, typeName))
                    {
                        return new TypeInformation(new LoadedType(desiredTypeInAssembly.Value, _assemblyLoadInfo, _loadedAssembly));
                    }
                }

                return null;
            }

            /// <summary>
            /// Find type information using System.Reflection.Metadata to avoid loading (and locking) its containing assembly.
            /// </summary>
            /// <param name="typeName">The type to find.</param>
            /// <returns>A <c ref="TypeInformation"/> indicating relevant information about typeName.</returns>
            private TypeInformation FindTypeInformationUsingSystemReflectionMetadata(string typeName)
            {
                string path = _assemblyLoadInfo.AssemblyFile;

                // This should only be true for Microsoft.Build assemblies. We use this for testing.
                if (path is null)
                {
#if NETFRAMEWORK
                    AppDomainSetup setup = AppDomain.CurrentDomain.SetupInformation;
                    setup.LoaderOptimization = LoaderOptimization.SingleDomain;
                    AppDomain appDomain = AppDomain.CreateDomain("appDomainToFindPath", null, setup);
                    path = appDomain.Load(new AssemblyName(_assemblyLoadInfo.AssemblyName)).Location;
                    AppDomain.Unload(appDomain);
#else
                    AssemblyLoadContext alc = new("loadContextToFindPath", true);
                    path = alc.LoadFromAssemblyName(new AssemblyName(_assemblyLoadInfo.AssemblyName)).Location;
                    alc.Unload();
#endif
                }

                using (FileStream stream = File.OpenRead(path))
                using (PEReader peFile = new(stream))
                {
                    MetadataReader metadataReader = peFile.GetMetadataReader();
                    AssemblyDefinition assemblyDef = metadataReader.GetAssemblyDefinition();
                    foreach (TypeDefinitionHandle typeDefHandle in metadataReader.TypeDefinitions)
                    {
                        TypeDefinition typeDef = metadataReader.GetTypeDefinition(typeDefHandle);
                        if (TryGetTypeInformationFromDefinition(metadataReader, typeDef, typeName, out TypeInformation typeInformation))
                        {
                            typeInformation.Path = path;
                            return typeInformation;
                        }
                    }
                }

                return null;
            }

            /// <summary>
            /// Tries to find information about the type.
            /// </summary>
            /// <param name="metadataReader"><c ref="MetadataReader"/> for the assembly containing the type.</param>
            /// <param name="typeDef"><c ref="TypeDefinition"/> indicating the type currently under consideration.</param>
            /// <param name="typeName">The name of the task type to find.</param>
            /// <param name="typeInformation">The information, if we find it.</param>
            /// <returns>True if this type or one of its children matches typeName. False otherwise.</returns>
            private bool TryGetTypeInformationFromDefinition(MetadataReader metadataReader, TypeDefinition typeDef, string typeName, out TypeInformation typeInformation)
            {
                typeInformation = null;
                string currentTypeName = metadataReader.GetString(typeDef.Name);

                if (!(typeDef.Attributes.HasFlag(TypeAttributes.Public) || typeDef.Attributes.HasFlag(TypeAttributes.NestedPublic)) || !typeDef.Attributes.HasFlag(TypeAttributes.Class))
                {
                    return false;
                }

                if (currentTypeName.Length != 0 && !TypeLoader.IsPartialTypeNameMatch(currentTypeName, typeName))
                {
                    foreach (TypeDefinitionHandle typeDefHandle in typeDef.GetNestedTypes())
                    {
                        TypeDefinition childTypeDef = metadataReader.GetTypeDefinition(typeDefHandle);
                        if (TryGetTypeInformationFromDefinition(metadataReader, childTypeDef, typeName, out typeInformation))
                        {
                            return true;
                        }
                    }
                    return false;
                }

                // We found the right type! Now get its information.
                typeInformation = new();

                foreach (CustomAttributeHandle customAttrHandle in typeDef.GetCustomAttributes())
                {
                    CustomAttribute customAttribute = metadataReader.GetCustomAttribute(customAttrHandle);
                    MemberReference constructorReference = metadataReader.GetMemberReference((MemberReferenceHandle)customAttribute.Constructor);
                    if (constructorReference.Parent.Kind == HandleKind.TypeReference)
                    {
                        TypeReference typeReference = metadataReader.GetTypeReference((TypeReferenceHandle)constructorReference.Parent);
                        string customAttributeName = metadataReader.GetString(typeReference.Name);
                        switch (customAttributeName)
                        {
                            case "RunInSTAAttribute":
                                typeInformation.HasSTAThreadAttribute = true;
                                break;
                            case "LoadInSeparateAppDomainAttribute":
                                typeInformation.HasLoadInSeparateAppDomainAttribute = true;
                                break;
                        }
                    }
                }

                IEnumerable<PropertyDefinition> propertyDefinitions = typeDef.GetProperties().Select(prop => metadataReader.GetPropertyDefinition(prop));
                List<TypeInformation.PropertyInfo> typePropertyInfos = new();
                foreach (PropertyDefinition propertyDefinition in propertyDefinitions)
                {
                    TypeInformation.PropertyInfo toAdd = new();
                    toAdd.Name = metadataReader.GetString(propertyDefinition.Name);
                    SignatureDecoder<string, object> decoder = new(ConstantSignatureVisualizer.Instance, metadataReader, genericContext: null);
                    BlobReader blob = metadataReader.GetBlobReader(propertyDefinition.Signature);
                    MethodSignature<string> signature = decoder.DecodeMethodSignature(ref blob);
                    toAdd.PropertyType = StringToType(signature.ReturnType);
                    toAdd.OutputAttribute = false;
                    toAdd.RequiredAttribute = false;
                    foreach (CustomAttributeHandle attr in propertyDefinition.GetCustomAttributes())
                    {
                        EntityHandle referenceHandle = metadataReader.GetMemberReference((MemberReferenceHandle)metadataReader.GetCustomAttribute(attr).Constructor).Parent;
                        if (referenceHandle.Kind == HandleKind.TypeReference)
                        {
                            string name = metadataReader.GetString(metadataReader.GetTypeReference((TypeReferenceHandle)referenceHandle).Name);
                            if (name.Equals("OutputAttribute", StringComparison.OrdinalIgnoreCase))
                            {
                                toAdd.OutputAttribute = true;
                            }
                            else if (name.Equals("RequiredAttribute", StringComparison.OrdinalIgnoreCase))
                            {
                                toAdd.RequiredAttribute = true;
                            }
                        }
                    }
                    typePropertyInfos.Add(toAdd);
                }
                typeInformation.Properties = typePropertyInfos.ToArray();

                TypeDefinition parentTypeDefinition = typeDef;
                while (true)
                {
                    foreach (InterfaceImplementationHandle interfaceHandle in parentTypeDefinition.GetInterfaceImplementations())
                    {
                        if (metadataReader.GetString(metadataReader.GetTypeReference((TypeReferenceHandle)metadataReader.GetInterfaceImplementation(interfaceHandle).Interface).Name).Equals("IGeneratedTask"))
                        {
                            typeInformation.ImplementsIGeneratedTask = true;
                        }
                    }

                    if (parentTypeDefinition.BaseType.IsNil)
                    {
                        break;
                    }

                    // If the baseType is not a TypeDefinitionHandle, we won't be able to chase it without actually loading the assembly. We would need to find the assembly containing the base type
                    // and load it using System.Reflection.Metdata just as we're doing here, but we don't know its path without loading this assembly. Just assume it didn't implement IGeneratedTask.
                    bool shouldBreakLoop = false;
                    switch (parentTypeDefinition.BaseType.Kind)
                    {
                        case HandleKind.TypeDefinition:
                            parentTypeDefinition = metadataReader.GetTypeDefinition((TypeDefinitionHandle)parentTypeDefinition.BaseType);
                            break;
                        case HandleKind.TypeReference:
                            string parentName = metadataReader.GetString(metadataReader.GetTypeReference((TypeReferenceHandle)parentTypeDefinition.BaseType).Name);
                            if (parentName.Equals("IGeneratedTask"))
                            {
                                typeInformation.ImplementsIGeneratedTask = true;
                            }
                            else if (parentName.Equals("MarshalByRefObject"))
                            {
                                typeInformation.IsMarshalByRef = true;
                            }
                            shouldBreakLoop = true;
                            break;
                        case HandleKind.TypeSpecification:
                            shouldBreakLoop = true;
                            break;
                    }

                    string typeDefinitionName = metadataReader.GetString(parentTypeDefinition.Name);
                    if (typeDefinitionName.Equals("MarshalByRefObject"))
                    {
                        typeInformation.IsMarshalByRef = true;
                    }
                    if (shouldBreakLoop || typeDefinitionName.Equals("object"))
                    {
                        break;
                    }
                }

                foreach (InterfaceImplementationHandle interfaceHandle in typeDef.GetInterfaceImplementations())
                {
                    if (metadataReader.GetString(metadataReader.GetTypeReference((TypeReferenceHandle)metadataReader.GetInterfaceImplementation(interfaceHandle).Interface).Name).Equals("IGeneratedTask"))
                    {
                        typeInformation.ImplementsIGeneratedTask = true;
                    }
                }

                typeInformation.AssemblyName = _assemblyLoadInfo.AssemblyName is null ? new AssemblyName(Path.GetFileNameWithoutExtension(_assemblyLoadInfo.AssemblyFile)) : new AssemblyName(_assemblyLoadInfo.AssemblyName);

                typeInformation.Namespace = metadataReader.GetString(metadataReader.GetNamespaceDefinition(metadataReader.GetNamespaceDefinitionRoot().NamespaceDefinitions.First()).Name);

                return true;
            }

            private Type StringToType(string s)
            {
                // return Type.GetType(s, false, true) ?? typeof(object);
                // would be a much cleaner implementation of StringToType, but it unfortunately
                // expects not just the type name but also its namespace like "System,Int32"
                // rather than just "Int32" as we get from decoding the TypeDefinition's signature.
                return s switch
                {
                    "String" => typeof(String),
                    "Microsoft.Build.Framework.ITaskItem" => typeof(ITaskItem),
                    "Boolean" => typeof(Boolean),
                    "Int32" => typeof(Int32),
                    "Char" => typeof(Char),
                    "Single" => typeof(Single),
                    "Int64" => typeof(Int64),
                    "Double" => typeof(Double),
                    "Byte" => typeof(Byte),
                    "SByte" => typeof(SByte),
                    "Decimal" => typeof(Decimal),
                    "UInt32" => typeof(UInt32),
                    "IntPtr" => typeof(IntPtr),
                    "UIntPtr" => typeof(UIntPtr),
                    "UInt64" => typeof(UInt64),
                    "Int16" => typeof(Int16),
                    "UInt16" => typeof(UInt16),
                    "String[]" => typeof(String[]),
                    "Microsoft.Build.Framework.ITaskItem[]" => typeof(ITaskItem[]),
                    "Boolean[]" => typeof(Boolean[]),
                    "Int32[]" => typeof(Int32[]),
                    "Char[]" => typeof(Char[]),
                    "Single[]" => typeof(Single[]),
                    "Int64[]" => typeof(Int64[]),
                    "Double[]" => typeof(Double[]),
                    "Byte[]" => typeof(Byte[]),
                    "SByte[]" => typeof(SByte[]),
                    "Decimal[]" => typeof(Decimal[]),
                    "UInt32[]" => typeof(UInt32[]),
                    "IntPtr[]" => typeof(IntPtr[]),
                    "UIntPtr[]" => typeof(UIntPtr[]),
                    "UInt64[]" => typeof(UInt64[]),
                    "Int16[]" => typeof(Int16[]),
                    "UInt16[]" => typeof(UInt16[]),
                    "String?" => typeof(String),
                    "Microsoft.Build.Framework.ITaskItem?" => typeof(ITaskItem),
                    "Boolean?" => typeof(Boolean?),
                    "Int32?" => typeof(Int32?),
                    "Char?" => typeof(Char?),
                    "Single?" => typeof(Single?),
                    "Int64?" => typeof(Int64?),
                    "Double?" => typeof(Double?),
                    "Byte?" => typeof(Byte?),
                    "SByte?" => typeof(SByte?),
                    "Decimal?" => typeof(Decimal?),
                    "UInt32?" => typeof(UInt32?),
                    "IntPtr?" => typeof(IntPtr?),
                    "UIntPtr?" => typeof(UIntPtr?),
                    "UInt64?" => typeof(UInt64?),
                    "Int16?" => typeof(Int16?),
                    "UInt16?" => typeof(UInt16?),
                    "String?[]" => typeof(String[]),
                    "Microsoft.Build.Framework.ITaskItem?[]" => typeof(ITaskItem[]),
                    "Boolean?[]" => typeof(Boolean?[]),
                    "Int32?[]" => typeof(Int32?[]),
                    "Char?[]" => typeof(Char?[]),
                    "Single?[]" => typeof(Single?[]),
                    "Int64?[]" => typeof(Int64?[]),
                    "Double?[]" => typeof(Double?[]),
                    "Byte?[]" => typeof(Byte?[]),
                    "SByte?[]" => typeof(SByte?[]),
                    "Decimal?[]" => typeof(Decimal?[]),
                    "UInt32?[]" => typeof(UInt32?[]),
                    "IntPtr?[]" => typeof(IntPtr?[]),
                    "UIntPtr?[]" => typeof(UIntPtr?[]),
                    "UInt64?[]" => typeof(UInt64?[]),
                    "Int16?[]" => typeof(Int16?[]),
                    "UInt16?[]" => typeof(UInt16?[]),
                    _ => typeof(object),
                };
            }

            /// <summary>
            /// Scan the assembly pointed to by the assemblyLoadInfo for public types. We will use these public types to do partial name matching on 
            /// to find tasks, loggers, and task factories.
            /// </summary>
            [SuppressMessage("Microsoft.Reliability", "CA2001:AvoidCallingProblematicMethods", MessageId = "System.Reflection.Assembly.LoadFrom", Justification = "Necessary in this case.")]
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

        // Copied from https://github.com/dotnet/roslyn/blob/a9027f3d3bddcd77eb3c97bf0caba61335c08426/src/Compilers/Test/Core/Metadata/MetadataReaderUtils.cs#L405
        private sealed class ConstantSignatureVisualizer : ISignatureTypeProvider<string, object>
        {
            public static readonly ConstantSignatureVisualizer Instance = new();

            public string GetArrayType(string elementType, ArrayShape shape)
                => elementType + "[" + new string(',', shape.Rank) + "]";

            public string GetByReferenceType(string elementType)
                => elementType + "&";

            public string GetFunctionPointerType(MethodSignature<string> signature)
                => "method-ptr";

            public string GetGenericInstantiation(string genericType, ImmutableArray<string> typeArguments)
                => genericType + "{" + string.Join(", ", typeArguments) + "}";

            public string GetGenericMethodParameter(object genericContext, int index)
                => "!!" + index;

            public string GetGenericTypeParameter(object genericContext, int index)
                => "!" + index;

            public string GetModifiedType(string modifier, string unmodifiedType, bool isRequired)
                => (isRequired ? "modreq" : "modopt") + "(" + modifier + ") " + unmodifiedType;

            public string GetPinnedType(string elementType)
                => "pinned " + elementType;

            public string GetPointerType(string elementType)
                => elementType + "*";

            public string GetPrimitiveType(PrimitiveTypeCode typeCode)
                => typeCode.ToString();

            public string GetSZArrayType(string elementType)
                => elementType + "[]";

            public string GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind)
            {
                var typeDef = reader.GetTypeDefinition(handle);
                var name = reader.GetString(typeDef.Name);
                return typeDef.Namespace.IsNil ? name : reader.GetString(typeDef.Namespace) + "." + name;
            }

            public string GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind)
            {
                var typeRef = reader.GetTypeReference(handle);
                var name = reader.GetString(typeRef.Name);
                return typeRef.Namespace.IsNil ? name : reader.GetString(typeRef.Namespace) + "." + name;
            }

            public string GetTypeFromSpecification(MetadataReader reader, object genericContext, TypeSpecificationHandle handle, byte rawTypeKind)
            {
                var sigReader = reader.GetBlobReader(reader.GetTypeSpecification(handle).Signature);
                return new SignatureDecoder<string, object>(Instance, reader, genericContext).DecodeType(ref sigReader);
            }
        }
    }
}
