// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Reflection;
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
        /// <param name="loadedViaMetadataLoadContext">Whether this type was loaded via MetadataLoadContext</param>
        /// <param name="iTaskItemType">type of an ITaskItem</param>
        internal LoadedType(Type type, AssemblyLoadInfo assemblyLoadInfo, Assembly loadedAssembly, Type iTaskItemType, bool loadedViaMetadataLoadContext = false)
        {
            ErrorUtilities.VerifyThrow(type != null, "We must have the type.");
            ErrorUtilities.VerifyThrow(assemblyLoadInfo != null, "We must have the assembly the type was loaded from.");
            ErrorUtilities.VerifyThrow(loadedAssembly is not null, "The assembly should always be loaded even if only by MetadataLoadContext.");

            Type = type;
            Assembly = assemblyLoadInfo;

            HasSTAThreadAttribute = CheckForHardcodedSTARequirement();
            LoadedAssemblyName = loadedAssembly.GetName();
            LoadedViaMetadataLoadContext = loadedViaMetadataLoadContext;

            // For inline tasks loaded from bytes, Assembly.Location is empty, so use the original path
            Path = string.IsNullOrEmpty(loadedAssembly.Location)
                ? assemblyLoadInfo.AssemblyLocation
                : loadedAssembly.Location;

            LoadedAssembly = loadedAssembly;

#if !NET35
            // This block is reflection only loaded type implementation. Net35 does not support it, and fall backs to former implementation in #else
            // Property `Properties` set in this block aren't used by TaskHosts. Properties below are only used on the NodeProvider side to get information about the
            // properties and reflect over them without needing them to be fully loaded, so it also isn't need for TaskHosts.

            // MetadataLoadContext-loaded Type objects don't support testing for inherited attributes, so we manually walk the BaseType chain.
            Type? t = type;
            while (t is not null)
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
#else
            // For v3.5 fallback to old full type approach, as oppose to reflection only
            HasLoadInSeparateAppDomainAttribute = this.Type.GetTypeInfo().IsDefined(typeof(LoadInSeparateAppDomainAttribute), true /* inherited */);
            HasSTAThreadAttribute = this.Type.GetTypeInfo().IsDefined(typeof(RunInSTAAttribute), true /* inherited */);
            IsMarshalByRef = this.Type.IsMarshalByRef;
#endif
        }

        #endregion

        /// <summary>
        /// Gets whether there's a LoadInSeparateAppDomain attribute on this type.
        /// </summary>
        public bool HasLoadInSeparateAppDomainAttribute { get; }

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
                AssemblyName assemblyName = Type.GetTypeInfo().Assembly.GetName();
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

        #region Properties

        /// <summary>
        /// Gets the type that was loaded from an assembly.
        /// </summary>
        /// <value>The loaded type.</value>
        internal Type Type { get; private set; }

        internal AssemblyName LoadedAssemblyName { get; private set; }

        internal string Path { get; private set; }

        /// <summary>
        /// If we loaded an assembly for this type.
        /// We use this information to help created AppDomains to resolve types that it could not load successfully
        /// </summary>
        internal Assembly LoadedAssembly { get; private set; }

#if !NET35
        internal ReflectableTaskPropertyInfo[] Properties { get; private set; }
#endif

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
