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
        /// <param name="iTaskItemType">type of an ITaskItem</param>
        /// <param name="runtime">Assembly runtime based on assembly attributes.</param>
        /// <param name="architecture">Assembly architecture extracted from PE flags</param>
        /// <param name="loadedViaMetadataLoadContext">Whether this type was loaded via MetadataLoadContext</param>
        internal LoadedType(
            Type type,
            AssemblyLoadInfo assemblyLoadInfo,
            Assembly loadedAssembly,
            Type iTaskItemType,
            string? runtime = null,
            string? architecture = null,
            bool loadedViaMetadataLoadContext = false)
        {
            ErrorUtilities.VerifyThrow(type != null, "We must have the type.");
            ErrorUtilities.VerifyThrow(assemblyLoadInfo != null, "We must have the assembly the type was loaded from.");
            ErrorUtilities.VerifyThrow(loadedAssembly is not null, "The assembly should always be loaded even if only by MetadataLoadContext.");

            Type = type;
            Assembly = assemblyLoadInfo;

            HasSTAThreadAttribute = CheckForHardcodedSTARequirement();
            LoadedAssemblyName = loadedAssembly.GetName();
            LoadedViaMetadataLoadContext = loadedViaMetadataLoadContext;
            Architecture = architecture;
            Runtime = runtime;

            // For inline tasks loaded from bytes, Assembly.Location is empty, so use the original path
            Path = string.IsNullOrEmpty(loadedAssembly.Location)
                ? assemblyLoadInfo.AssemblyLocation
                : loadedAssembly.Location;

            LoadedAssembly = loadedAssembly;
            HasLoadInSeparateAppDomainAttribute = Type.GetTypeInfo().IsDefined(typeof(LoadInSeparateAppDomainAttribute), true /* inherited */);
            HasSTAThreadAttribute = Type.GetTypeInfo().IsDefined(typeof(RunInSTAAttribute), true /* inherited */);
            IsMarshalByRef = Type.IsMarshalByRef;
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

        internal string? Architecture { get; private set; }

        internal string? Runtime { get; private set; }

        internal string Path { get; private set; }

        /// <summary>
        /// If we loaded an assembly for this type.
        /// We use this information to help created AppDomains to resolve types that it could not load successfully
        /// </summary>
        internal Assembly LoadedAssembly { get; private set; }

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
