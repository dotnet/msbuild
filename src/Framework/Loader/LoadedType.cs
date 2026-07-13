// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Reflection;
using System.Diagnostics.CodeAnalysis;
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

                // Check whether it's assignable to ITaskItem or ITaskItem[]. Simplify to just checking for ITaskItem.
                Type? pt = null;
                try
                {
                    pt = props[i].PropertyType;
                    if (pt.IsArray)
                    {
                        pt = pt.GetElementType();
                    }
                }
                catch (Exception e) when (!ExceptionHandling.IsCriticalException(e))
                {
                    // Skip properties that can't be loaded
                    continue;
                }

                bool isAssignableToITask = false;
                try
                {
                    isAssignableToITask = pt != null && iTaskItemType.IsAssignableFrom(pt);
                }
                catch (Exception e) when (!ExceptionHandling.IsCriticalException(e))
                {
                    // Can't determine assignability, default to false
                }

                Properties[i] = new ReflectableTaskPropertyInfo(props[i], outputAttribute, requiredAttribute, isAssignableToITask);
                if (loadedViaMetadataLoadContext && PropertyAssemblyQualifiedNames != null)
                {
                    try
                    {
                        PropertyAssemblyQualifiedNames[i] = Properties[i]?.PropertyType?.AssemblyQualifiedName ?? string.Empty;
                    }
                    catch (Exception e) when (!ExceptionHandling.IsCriticalException(e))
                    {
                        PropertyAssemblyQualifiedNames[i] = string.Empty;
                    }
                }
            }
        }

        #endregion

        /// <summary>
        /// Gets whether there's a LoadInSeparateAppDomain attribute on this type.
        /// </summary>
        public bool HasLoadInSeparateAppDomainAttribute { get; }

        /// <summary>
        /// Gets whether this type declares a public instance constructor that accepts a single
        /// <see cref="TaskEnvironment"/> parameter. When present, <see cref="CreateInstance"/> prefers this
        /// constructor over the parameterless one so the task can compute environment-dependent default values
        /// (for example, rooting a default output path) during construction.
        /// </summary>
        public bool HasTaskEnvironmentConstructor
        {
            get
            {
                EnsureConstructorsResolved();
                return _constructorNeedsEnvironment;
            }
        }

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
        /// until this first call — see <see cref="EnsureConstructorsResolved"/>.
        /// </remarks>
        internal ITask? CreateInstance(TaskEnvironment? taskEnvironment)
        {
            EnsureConstructorsResolved();

#if NET
            // Neither a parameterless nor a TaskEnvironment constructor exists; surface the same failure
            // Activator.CreateInstance would have produced rather than a NullReferenceException.
            if (_constructorInvoker is null)
            {
                throw new MissingMethodException(Type.FullName, ".ctor");
            }

            return _constructorNeedsEnvironment
                ? (ITask?)_constructorInvoker.Invoke(taskEnvironment ?? TaskEnvironment.Fallback)
                : (ITask?)_constructorInvoker.Invoke();
#else
            if (_constructor is null)
            {
                throw new MissingMethodException(Type.FullName, ".ctor");
            }

            return _constructorNeedsEnvironment
                ? (ITask?)_constructor.Invoke([taskEnvironment ?? TaskEnvironment.Fallback])
                : (ITask?)_constructor.Invoke(null);
#endif
        }

        // The single public instance constructor CreateInstance uses, chosen once in
        // EnsureConstructorsResolved: the TaskEnvironment constructor when the type declares one, otherwise
        // the parameterless constructor. _constructorNeedsEnvironment records which of the two it is, so no
        // second constructor reference has to be kept around. On .NET only the ConstructorInvoker is retained
        // (the ConstructorInfo it wraps is not, to keep this object small); older frameworks that lack
        // ConstructorInvoker fall back to invoking the ConstructorInfo directly.
#if NET
        private ConstructorInvoker? _constructorInvoker;
#else
        private ConstructorInfo? _constructor;
#endif

        private bool _constructorNeedsEnvironment;

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
        /// Resolves — on first access and at most once per observed result — the public instance constructors
        /// this type offers for task instantiation: the parameterless constructor and the one that takes a
        /// single <see cref="TaskEnvironment"/> parameter (if any), both found in a single reflection pass. On
        /// frameworks that support it, a cached <c>ConstructorInvoker</c> is also built for whichever
        /// constructor <see cref="CreateInstance"/> will use. The <see cref="TaskEnvironment"/> parameter is
        /// matched by full type name so that it also works for types loaded via MetadataLoadContext, whose
        /// <see cref="TaskEnvironment"/> is a distinct <see cref="Type"/> identity from the one loaded in the
        /// current context.
        /// </summary>
        /// <remarks>
        /// Resolution is deferred until first access so that the constructor reflection is only paid for types
        /// we actually instantiate — not for the many <see cref="LoadedType"/> instances built solely to
        /// marshal property metadata to a task host. A <see cref="LoadedType"/> is cached per task type and
        /// shared across threads in multi-threaded builds, so the memoization is intentionally lock-free: the
        /// worst a race can do is resolve the same (equivalent) constructor on more than one thread — every
        /// published field is written atomically (a reference, or a <see cref="bool"/>) — and the volatile
        /// <see cref="_constructorsResolved"/> flag guarantees a reader that observes <c>true</c> also observes
        /// those published references.
        /// </remarks>
        private void EnsureConstructorsResolved()
        {
            if (_constructorsResolved)
            {
                return;
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

            _constructorNeedsEnvironment = taskEnvironmentConstructor is not null;

            // Prefer the TaskEnvironment constructor when present so a task can compute environment-dependent
            // defaults during construction; otherwise use the parameterless constructor.
            ConstructorInfo? chosenConstructor = taskEnvironmentConstructor ?? parameterlessConstructor;

#if NET
            // Build the cached invoker for the chosen constructor. Types loaded only for metadata inspection
            // run in a task host and are never instantiated in-proc, so they never need an invoker (and a
            // MetadataLoadContext ConstructorInfo cannot be invoked). ConstructorInvoker caches an optimized,
            // Native AOT friendly invocation path so repeated instantiations approach Activator.CreateInstance
            // speed without generating dynamic code.
            if (chosenConstructor is not null && !LoadedViaMetadataLoadContext)
            {
                _constructorInvoker = ConstructorInvoker.Create(chosenConstructor);
            }
#else
            _constructor = chosenConstructor;
#endif

            _constructorsResolved = true;
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
