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
        /// <summary>
        /// Creates an instance of this class for the given type.
        /// </summary>
        /// <param name="type">The Type to be loaded</param>
        /// <param name="assemblyFilePath">The assembly file path used to load the assembly</param>
        /// <param name="loadedAssembly">The assembly which has been loaded, if any</param>
        internal LoadedType(Type type, string assemblyFilePath, Assembly loadedAssembly)
        {
            ErrorUtilities.VerifyThrow(type != null, "We must have the type.");
            ErrorUtilities.VerifyThrow(assemblyFilePath != null, "We must have the assembly file path the type was loaded from.");
            ErrorUtilities.VerifyThrow(loadedAssembly is not null, "The assembly should always be loaded even if only by MetadataLoadContext.");

            Type = type;
            AssemblyFilePath = assemblyFilePath;

            LoadedAssemblyName = loadedAssembly.GetName();

            // For inline tasks loaded from bytes, Assembly.Location is empty, so use the original path
            Path = string.IsNullOrEmpty(loadedAssembly.Location)
                ? assemblyFilePath
                : loadedAssembly.Location;

            LoadedAssembly = loadedAssembly;
            HasLoadInSeparateAppDomainAttribute = Type.IsDefined(typeof(LoadInSeparateAppDomainAttribute), inherit: true);
            IsMarshalByRef = Type.IsMarshalByRef;
        }

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
        /// Gets the type that was loaded from an assembly.
        /// </summary>
        /// <value>The loaded type.</value>
        internal Type Type { get; }

        internal AssemblyName LoadedAssemblyName { get; }

        internal string Path { get; }

        /// <summary>
        /// If we loaded an assembly for this type.
        /// We use this information to help created AppDomains to resolve types that it could not load successfully
        /// </summary>
        internal Assembly LoadedAssembly { get; }

        internal string AssemblyFilePath { get; }
    }
}
