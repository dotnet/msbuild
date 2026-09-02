// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
#if !NETSTANDARD2_0
using System.Collections.Immutable;
#endif
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Diagnostics.CodeAnalysis;
#if !NETSTANDARD2_0
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
#endif
#if NET
using System.Runtime.CompilerServices;
#endif
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Shared
{
    /// <summary>
    /// This class packages information about a type loaded from an assembly: for example,
    /// the GenerateResource task class type or the ConsoleLogger logger class type.
    /// </summary>
    internal sealed class LoadedType
    {
        #region Constructor

        /// <summary>
        /// Creates an instance of this class for the given type.
        /// </summary>
        /// <param name="type">The Type to be loaded</param>
        /// <param name="assemblyLoadInfo">Information used to load the assembly</param>
        /// <param name="loadedAssembly">The assembly which has been loaded, if any</param>
        /// <param name="iTaskItemType">type of an ITaskItem</param>
        /// <param name="runtime">Assembly runtime based on assembly attributes.</param>
        /// <param name="architecture">Assembly architecture extracted from PE flags</param>
        /// <param name="loadedViaMetadataLoadContext">Whether this type was loaded via MetadataLoadContext</param>
        internal LoadedType(
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)]
            Type type,
            AssemblyLoadInfo assemblyLoadInfo,
            Assembly loadedAssembly,
            Type iTaskItemType,
            string? runtime = null,
            string? architecture = null,
            bool loadedViaMetadataLoadContext = false)
        {
            Assumed.NotNull(type, "We must have the type.");
            Assumed.NotNull(assemblyLoadInfo, "We must have the assembly the type was loaded from.");
            Assumed.NotNull(loadedAssembly, "The assembly should always be loaded even if only by MetadataLoadContext.");

            Type = type;
            Assembly = assemblyLoadInfo;

            HasSTAThreadAttribute = CheckForHardcodedSTARequirement();
            LoadedAssemblyName = loadedAssembly.GetName();
            LoadedViaMetadataLoadContext = loadedViaMetadataLoadContext;
            Architecture = architecture;
            Runtime = runtime;

            // Assembly.Location is empty for inline tasks loaded from bytes, and for every assembly in a
            // single-file/Native AOT host; in those cases fall back to the original load path. On .NET the
            // read is guarded on dynamic-code support so ILC dead-strips it (and its IL3000) under AOT, while
            // the JIT still prefers the real loaded location.
#if NET
            string loadedAssemblyLocation = RuntimeFeature.IsDynamicCodeSupported ? loadedAssembly.Location : string.Empty;
#else
            string loadedAssemblyLocation = loadedAssembly.Location;
#endif
            Path = string.IsNullOrEmpty(loadedAssemblyLocation)
                ? assemblyLoadInfo.AssemblyLocation
                : loadedAssemblyLocation;

            LoadedAssembly = loadedAssembly;

            // This block is reflection only loaded type implementation. Net35 does not support it, and fall backs to former implementation in #else
            // Property `Properties` set in this block aren't used by TaskHosts. Properties below are only used on the NodeProvider side to get information about the
            // properties and reflect over them without needing them to be fully loaded, so it also isn't need for TaskHosts.

            // MetadataLoadContext-loaded Type objects don't support testing for inherited attributes, so we manually walk the BaseType chain.
            Type? t = type;
            while (t is not null)
            {
                try
                {
                    if (TypeUtilities.HasAttribute<LoadInSeparateAppDomainAttribute>(t))
                    {
                        HasLoadInSeparateAppDomainAttribute = true;
                    }

                    if (TypeUtilities.HasAttribute<RunInSTAAttribute>(t))
                    {
                        HasSTAThreadAttribute = true;
                    }

                    if (t.IsMarshalByRef)
                    {
                        IsMarshalByRef = true;
                    }
                }
                catch when (loadedViaMetadataLoadContext)
                {
                    // when assembly is loaded via metadata load context we can ignore exception because there is no expectation to have it in proc.
                    // BUT we should throw for in-proc case and handle it on higher level.
                }

                t = t.BaseType;
            }

            PropertyInfo[] props = type.GetProperties(BindingFlags.Instance | BindingFlags.Public);
            Properties = new ReflectableTaskPropertyInfo[props.Length];
            if (loadedViaMetadataLoadContext)
            {
                PropertyAssemblyQualifiedNames = new string[props.Length];
            }

            using var propertySignatureReader = loadedViaMetadataLoadContext ? new PropertySignatureReader() : null;
            for (int i = 0; i < props.Length; i++)
            {
                bool outputAttribute = false;
                bool requiredAttribute = false;
                foreach (CustomAttributeData attr in CustomAttributeData.GetCustomAttributes(props[i]))
                {
                    try
                    {
                        if (attr.AttributeType?.Name.Equals(nameof(OutputAttribute)) == true)
                        {
                            outputAttribute = true;
                        }
                        else if (attr.AttributeType?.Name.Equals(nameof(RequiredAttribute)) == true)
                        {
                            requiredAttribute = true;
                        }
                    }
                    catch (Exception e) when (!ExceptionHandling.IsCriticalException(e))
                    {
                        // Skip attributes that can't be loaded
                        continue;
                    }
                }

                Type propertyType;
                Type propertyElementType;
                try
                {
                    propertyType = props[i].PropertyType;
                    propertyElementType = propertyType.IsArray ? propertyType.GetElementType()! : propertyType;
                }
                catch (Exception e) when (!ExceptionHandling.IsCriticalException(e))
                {
                    Properties[i] = CreateUnresolvedProperty(props[i], outputAttribute, requiredAttribute, propertySignatureReader);
                    if (PropertyAssemblyQualifiedNames is not null)
                    {
                        PropertyAssemblyQualifiedNames[i] = string.Empty;
                    }

                    continue;
                }

                bool isAssignableToITask = false;
                try
                {
                    isAssignableToITask = iTaskItemType.IsAssignableFrom(propertyElementType);
                }
                catch (Exception e) when (!ExceptionHandling.IsCriticalException(e))
                {
                    // Can't determine assignability, default to false
                }

                try
                {
                    Properties[i] = new ReflectableTaskPropertyInfo(
                        props[i],
                        propertyType,
                        outputAttribute,
                        requiredAttribute,
                        isAssignableToITask,
                        loadedViaMetadataLoadContext
                            ? GetParameterTypeForExpansion(propertyType, propertyElementType)
                            : null);
                }
                catch (Exception e) when (!ExceptionHandling.IsCriticalException(e))
                {
                    Properties[i] = CreateUnresolvedProperty(props[i], outputAttribute, requiredAttribute, propertySignatureReader);
                    if (PropertyAssemblyQualifiedNames is not null)
                    {
                        PropertyAssemblyQualifiedNames[i] = string.Empty;
                    }

                    continue;
                }

                if (loadedViaMetadataLoadContext && PropertyAssemblyQualifiedNames is not null)
                {
                    try
                    {
                        PropertyAssemblyQualifiedNames[i] = propertyType.AssemblyQualifiedName ?? string.Empty;
                    }
                    catch (Exception e) when (!ExceptionHandling.IsCriticalException(e))
                    {
                        PropertyAssemblyQualifiedNames[i] = string.Empty;
                    }
                }
            }
        }

        private static ReflectableTaskPropertyInfo CreateUnresolvedProperty(
            PropertyInfo propertyInfo,
            bool output,
            bool required,
            PropertySignatureReader? propertySignatureReader) =>
            new(
                propertyInfo,
                output,
                required,
                propertySignatureReader?.TryRead(propertyInfo));

        private sealed class PropertySignatureReader : IDisposable
        {
#if NETSTANDARD2_0
            internal Type? TryRead(PropertyInfo propertyInfo) => null;

            public void Dispose()
            {
            }
#else
            private readonly Dictionary<string, PEReader> _readers = new(FileUtilities.PathComparer);

            internal Type? TryRead(PropertyInfo propertyInfo)
            {
                try
                {
                    string? assemblyPath = propertyInfo.DeclaringType?.Assembly.Location;
                    if (assemblyPath is null || assemblyPath.Length == 0 || !File.Exists(assemblyPath))
                    {
                        return null;
                    }

                    if (!_readers.TryGetValue(assemblyPath, out PEReader? peReader))
                    {
                        FileStream stream = File.OpenRead(assemblyPath);
                        try
                        {
                            peReader = new PEReader(stream);
                        }
                        catch
                        {
                            stream.Dispose();
                            throw;
                        }

                        if (!peReader.HasMetadata)
                        {
                            peReader.Dispose();
                            return null;
                        }

                        _readers.Add(assemblyPath, peReader);
                    }

                    return ReadParameterTypeForExpansion(propertyInfo, peReader.GetMetadataReader());
                }
                catch (Exception e) when (!ExceptionHandling.IsCriticalException(e))
                {
                    return null;
                }
            }

            public void Dispose()
            {
                foreach (PEReader reader in _readers.Values)
                {
                    reader.Dispose();
                }
            }
#endif
        }

#if !NETSTANDARD2_0
        private static Type? ReadParameterTypeForExpansion(PropertyInfo propertyInfo, MetadataReader metadataReader)
        {
            EntityHandle handle = MetadataTokens.EntityHandle(propertyInfo.MetadataToken);
            if (handle.Kind != HandleKind.PropertyDefinition)
            {
                return null;
            }

            PropertyDefinition propertyDefinition = metadataReader.GetPropertyDefinition((PropertyDefinitionHandle)handle);
            if (metadataReader.GetBlobReader(propertyDefinition.Signature).Length > MaximumPropertySignatureLength)
            {
                return null;
            }

            Type parameterType = propertyDefinition.DecodeSignature(s_parameterTypeSignatureProvider, genericContext: null).ReturnType;
            return parameterType == UnsupportedParameterType ? null : parameterType;
        }
#endif

        private static Type? GetParameterTypeForExpansion(Type propertyType, Type propertyElementType)
        {
            bool isArray = propertyType.IsArray;
            if (isArray && (propertyType.GetArrayRank() != 1 || propertyElementType.IsArray))
            {
                return null;
            }

            Type? scalarType;
            string? elementTypeName = propertyElementType.FullName;
            if (elementTypeName == typeof(AbsolutePath).FullName)
            {
                scalarType = typeof(AbsolutePath);
            }
            else if (elementTypeName == typeof(FileInfo).FullName)
            {
                scalarType = typeof(FileInfo);
            }
            else if (elementTypeName == typeof(DirectoryInfo).FullName)
            {
                scalarType = typeof(DirectoryInfo);
            }
            else if (propertyElementType.IsValueType || elementTypeName == typeof(string).FullName)
            {
                scalarType = typeof(string);
            }
            else
            {
                return null;
            }

            if (!isArray)
            {
                return scalarType;
            }

            return GetArrayExpansionType(scalarType);
        }

        private static Type? GetArrayExpansionType(Type scalarType)
        {
            if (scalarType == typeof(string))
            {
                return typeof(string[]);
            }

            if (scalarType == typeof(AbsolutePath))
            {
                return typeof(AbsolutePath[]);
            }

            if (scalarType == typeof(FileInfo))
            {
                return typeof(FileInfo[]);
            }

            return scalarType == typeof(DirectoryInfo) ? typeof(DirectoryInfo[]) : null;
        }

#if !NETSTANDARD2_0
        private const int MaximumPropertySignatureLength = 256;

        private static readonly Type UnsupportedParameterType = typeof(void);

        private static readonly ParameterTypeSignatureProvider s_parameterTypeSignatureProvider = new();

        private sealed class ParameterTypeSignatureProvider : ISignatureTypeProvider<Type, object?>
        {
            public Type GetArrayType(Type elementType, ArrayShape shape) => UnsupportedParameterType;

            public Type GetByReferenceType(Type elementType) => UnsupportedParameterType;

            public Type GetFunctionPointerType(MethodSignature<Type> signature) => UnsupportedParameterType;

            public Type GetGenericInstantiation(Type genericType, ImmutableArray<Type> typeArguments) => UnsupportedParameterType;

            public Type GetGenericMethodParameter(object? genericContext, int index) => UnsupportedParameterType;

            public Type GetGenericTypeParameter(object? genericContext, int index) => UnsupportedParameterType;

            public Type GetModifiedType(Type modifier, Type unmodifiedType, bool isRequired) => unmodifiedType;

            public Type GetPinnedType(Type elementType) => elementType;

            public Type GetPointerType(Type elementType) => UnsupportedParameterType;

            public Type GetPrimitiveType(PrimitiveTypeCode typeCode) =>
                typeCode is PrimitiveTypeCode.Object or PrimitiveTypeCode.TypedReference or PrimitiveTypeCode.Void
                    ? UnsupportedParameterType
                    : typeof(string);

            public Type GetSZArrayType(Type elementType) =>
                GetArrayExpansionType(elementType) ?? UnsupportedParameterType;

            public Type GetTypeFromDefinition(
                MetadataReader reader,
                TypeDefinitionHandle handle,
                byte rawTypeKind)
            {
                TypeDefinition definition = reader.GetTypeDefinition(handle);
                return GetNamedType(reader, definition.Namespace, definition.Name, rawTypeKind);
            }

            public Type GetTypeFromReference(
                MetadataReader reader,
                TypeReferenceHandle handle,
                byte rawTypeKind)
            {
                TypeReference reference = reader.GetTypeReference(handle);
                return GetNamedType(reader, reference.Namespace, reference.Name, rawTypeKind);
            }

            public Type GetTypeFromSpecification(
                MetadataReader reader,
                object? genericContext,
                TypeSpecificationHandle handle,
                byte rawTypeKind) => UnsupportedParameterType;

            private static Type GetNamedType(
                MetadataReader reader,
                StringHandle namespaceHandle,
                StringHandle nameHandle,
                byte rawTypeKind)
            {
                if (reader.StringComparer.Equals(namespaceHandle, typeof(AbsolutePath).Namespace!)
                    && reader.StringComparer.Equals(nameHandle, nameof(AbsolutePath)))
                {
                    return typeof(AbsolutePath);
                }

                if (reader.StringComparer.Equals(namespaceHandle, typeof(FileInfo).Namespace!)
                    && reader.StringComparer.Equals(nameHandle, nameof(FileInfo)))
                {
                    return typeof(FileInfo);
                }

                if (reader.StringComparer.Equals(namespaceHandle, typeof(DirectoryInfo).Namespace!)
                    && reader.StringComparer.Equals(nameHandle, nameof(DirectoryInfo)))
                {
                    return typeof(DirectoryInfo);
                }

                return rawTypeKind == (byte)SignatureTypeKind.ValueType
                    ? typeof(string)
                    : UnsupportedParameterType;
            }
        }
#endif

        #endregion

        /// <summary>
        /// Gets whether there's a LoadInSeparateAppDomain attribute on this type.
        /// </summary>
        public bool HasLoadInSeparateAppDomainAttribute { get; }

        /// <summary>
        /// Creates an <see cref="ITask"/> instance of this loaded type. When the type declares a constructor
        /// that takes a single <see cref="TaskEnvironment"/>, that constructor is invoked with
        /// <paramref name="taskEnvironment"/> — falling back to <see cref="TaskEnvironment.Fallback"/> when the
        /// caller does not supply one — so the task can compute environment-dependent defaults during
        /// construction; otherwise the public parameterless constructor is used. The engine still assigns the
        /// TaskEnvironment property separately after construction.
        /// </summary>
        /// <remarks>
        /// Instantiation goes through a cached <c>ConstructorInvoker</c> (or, on frameworks that predate
        /// it, the cached <see cref="ConstructorInfo"/>) rather than <see cref="Activator.CreateInstance(Type)"/>
        /// / <see cref="Activator.CreateInstance(Type, object[])"/>. This keeps every task-creation path on a
        /// single, Native AOT friendly mechanism that generates no dynamic code, while letting repeated
        /// instantiations approach the speed of the CLR's cached activator. Constructor discovery is deferred
        /// until this first call — see <see cref="GetResolvedConstructor"/>.
        /// </remarks>
        internal ITask? CreateInstance(TaskEnvironment? taskEnvironment)
        {
            ResolvedConstructor resolvedConstructor = GetResolvedConstructor();

#if NET
            // Neither a parameterless nor a TaskEnvironment constructor exists; surface the same failure
            // Activator.CreateInstance would have produced rather than a NullReferenceException.
            if (resolvedConstructor.Invoker is null)
            {
                throw new MissingMethodException(Type.FullName, ".ctor");
            }

            return resolvedConstructor.NeedsEnvironment
                ? (ITask?)resolvedConstructor.Invoker.Invoke(taskEnvironment ?? TaskEnvironment.Fallback)
                : (ITask?)resolvedConstructor.Invoker.Invoke();
#else
            if (resolvedConstructor.Constructor is null)
            {
                throw new MissingMethodException(Type.FullName, ".ctor");
            }

            return resolvedConstructor.NeedsEnvironment
                ? (ITask?)resolvedConstructor.Constructor.Invoke([taskEnvironment ?? TaskEnvironment.Fallback])
                : (ITask?)resolvedConstructor.Constructor.Invoke(null);
#endif
        }

        /// <summary>
        /// The public instance constructor <see cref="CreateInstance"/> uses, resolved once by
        /// <see cref="GetResolvedConstructor"/>: the TaskEnvironment constructor when the type declares one,
        /// otherwise the parameterless constructor. <see cref="NeedsEnvironment"/> records which of the two it
        /// is, so no second constructor reference has to be kept around. On .NET only a
        /// <c>ConstructorInvoker</c> is retained (the ConstructorInfo it wraps is not, to keep this small);
        /// older frameworks that lack ConstructorInvoker invoke the <see cref="ConstructorInfo"/> directly.
        /// This is a value type so caching it costs no extra allocation.
        /// </summary>
        private readonly struct ResolvedConstructor
        {
#if NET
            internal ResolvedConstructor(ConstructorInvoker? invoker, bool needsEnvironment)
            {
                Invoker = invoker;
                NeedsEnvironment = needsEnvironment;
            }

            internal ConstructorInvoker? Invoker { get; }
#else
            internal ResolvedConstructor(ConstructorInfo? constructor, bool needsEnvironment)
            {
                Constructor = constructor;
                NeedsEnvironment = needsEnvironment;
            }

            internal ConstructorInfo? Constructor { get; }
#endif

            internal bool NeedsEnvironment { get; }
        }

        private ResolvedConstructor _resolvedConstructor;

        private volatile bool _constructorsResolved;

        /// <summary>
        /// Gets whether there's a STAThread attribute on the Execute method of this type.
        /// </summary>
        public bool HasSTAThreadAttribute { get; }

        /// <summary>
        /// Gets whether this type implements MarshalByRefObject.
        /// </summary>
        public bool IsMarshalByRef { get; }

        /// <summary>
        /// Gets whether this type was loaded by using MetadataLoadContext.
        /// </summary>
        public bool LoadedViaMetadataLoadContext { get; }

        /// <summary>
        /// Determines if the task has a hardcoded requirement for STA thread usage.
        /// </summary>
        private bool CheckForHardcodedSTARequirement()
        {
            // Special hard-coded attributes for certain legacy tasks which need to run as STA because they were written before
            // we changed to running all tasks in MTA.
            if (String.Equals("Microsoft.Build.Tasks.Xaml.PartialClassGenerationTask", Type.FullName, StringComparison.OrdinalIgnoreCase))
            {
                AssemblyName assemblyName = Type.Assembly.GetName();
                Version lastVersionToForce = new Version(3, 5);
                if (assemblyName.Version?.CompareTo(lastVersionToForce) > 0)
                {
                    if (String.Equals(assemblyName.Name, "PresentationBuildTasks", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Returns — resolving on first access and memoizing thereafter — the constructor
        /// <see cref="CreateInstance"/> should invoke for this type: the one that takes a single
        /// <see cref="TaskEnvironment"/> parameter when the type declares it, otherwise the parameterless
        /// constructor (both discovered in a single reflection pass). On frameworks that support it, the result
        /// carries a cached <c>ConstructorInvoker</c> for that constructor. The <see cref="TaskEnvironment"/>
        /// parameter is matched by full type name so it also works for types loaded via MetadataLoadContext,
        /// whose <see cref="TaskEnvironment"/> is a distinct <see cref="Type"/> identity from the one in the
        /// current context.
        /// </summary>
        /// <remarks>
        /// Resolution is deferred until first access so that the constructor reflection is only paid for types
        /// we actually instantiate — not for the many <see cref="LoadedType"/> instances built solely to
        /// marshal property metadata to a task host. A <see cref="LoadedType"/> is cached per task type and
        /// shared across threads in multi-threaded builds, so the memoization is intentionally lock-free (rather
        /// than, say, a <see cref="System.Lazy{T}"/>, which would add a per-instance allocation this size-tuned
        /// type avoids): the worst a race can do is resolve the same (equivalent) constructor on more than one
        /// thread, and the volatile <see cref="_constructorsResolved"/> flag guarantees a reader that observes
        /// <c>true</c> also observes the fully-written <see cref="_resolvedConstructor"/> that was published
        /// before it.
        /// </remarks>
        private ResolvedConstructor GetResolvedConstructor()
        {
            if (_constructorsResolved)
            {
                return _resolvedConstructor;
            }

            ConstructorInfo? parameterlessConstructor = null;
            ConstructorInfo? taskEnvironmentConstructor = null;

            try
            {
                foreach (ConstructorInfo constructor in Type.GetConstructors(BindingFlags.Instance | BindingFlags.Public))
                {
                    ParameterInfo[] parameters = constructor.GetParameters();
                    if (parameters.Length == 0)
                    {
                        parameterlessConstructor = constructor;
                    }
                    else if (parameters.Length == 1 &&
                        string.Equals(parameters[0].ParameterType.FullName, TaskEnvironmentTypeFullName, StringComparison.Ordinal))
                    {
                        taskEnvironmentConstructor = constructor;
                    }
                }
            }
            catch when (LoadedViaMetadataLoadContext)
            {
                // Reflecting over constructors of a MetadataLoadContext-loaded type can fail; such types are
                // executed in a task host rather than instantiated in-proc, so it is safe to report none here.
            }

            bool needsEnvironment = taskEnvironmentConstructor is not null;

            // Prefer the TaskEnvironment constructor when present so a task can compute environment-dependent
            // defaults during construction; otherwise use the parameterless constructor.
            ConstructorInfo? chosenConstructor = taskEnvironmentConstructor ?? parameterlessConstructor;

#if NET
            // Build the cached invoker for the chosen constructor. Types loaded only for metadata inspection
            // run in a task host and are never instantiated in-proc, so they never need an invoker (and a
            // MetadataLoadContext ConstructorInfo cannot be invoked). ConstructorInvoker caches an optimized,
            // Native AOT friendly invocation path so repeated instantiations approach Activator.CreateInstance
            // speed without generating dynamic code.
            ConstructorInvoker? invoker = chosenConstructor is not null && !LoadedViaMetadataLoadContext
                ? ConstructorInvoker.Create(chosenConstructor)
                : null;
            ResolvedConstructor resolvedConstructor = new(invoker, needsEnvironment);
#else
            ResolvedConstructor resolvedConstructor = new(chosenConstructor, needsEnvironment);
#endif

            _resolvedConstructor = resolvedConstructor;
            _constructorsResolved = true;
            return resolvedConstructor;
        }

        private static readonly string TaskEnvironmentTypeFullName = typeof(TaskEnvironment).FullName!;

        #region Properties

        /// <summary>
        /// Gets the type that was loaded from an assembly.
        /// </summary>
        /// <value>The loaded type.</value>
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)]
        internal Type Type { get; private set; }

        internal AssemblyName LoadedAssemblyName { get; private set; }

        internal string? Architecture { get; private set; }

        internal string? Runtime { get; private set; }

        internal string Path { get; private set; }

        /// <summary>
        /// If we loaded an assembly for this type.
        /// We use this information to help created AppDomains to resolve types that it could not load successfully
        /// </summary>
        internal Assembly LoadedAssembly { get; private set; }

        internal ReflectableTaskPropertyInfo[] Properties { get; private set; }

        /// <summary>
        /// Assembly-qualified names for properties. Only has a value if this type was loaded using MetadataLoadContext.
        /// </summary>
        internal string[]? PropertyAssemblyQualifiedNames { get; private set; }

        /// <summary>
        /// Gets the assembly the type was loaded from.
        /// </summary>
        /// <value>The assembly info for the loaded type.</value>
        internal AssemblyLoadInfo Assembly { get; private set; }

        #endregion
    }
}
