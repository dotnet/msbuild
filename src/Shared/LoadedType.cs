// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Shared
{
    /// <summary>
    /// This class packages information about a type loaded from an assembly: for example,
    /// the GenerateResource task class type or the ConsoleLogger logger class type.
    /// </summary>
    internal sealed class LoadedType
    {
        #region Constructors

        /// <summary>
        /// Creates an instance of this class for the given type.
        /// </summary>
        internal LoadedType(Type type, AssemblyLoadInfo assemblyLoadInfo)
            : this(type, assemblyLoadInfo, null)
        {
        }

        /// <summary>
        /// Creates an instance of this class for the given type.
        /// </summary>
        /// <param name="type">The Type to be loaded</param>
        /// <param name="assemblyLoadInfo">Information used to load the assembly</param>
        /// <param name="loadedAssembly">The assembly which has been loaded, if any</param>
        internal LoadedType(Type type, AssemblyLoadInfo assemblyLoadInfo, Assembly loadedAssembly)
        {
            ErrorUtilities.VerifyThrow(type != null, "We must have the type.");
            ErrorUtilities.VerifyThrow(assemblyLoadInfo != null, "We must have the assembly the type was loaded from.");

            _type = type;
            _assembly = assemblyLoadInfo;
            _loadedAssembly = loadedAssembly;

            CheckForHardcodedSTARequirement();
            HasLoadInSeparateAppDomainAttribute();
            HasSTAThreadAttribute();
        }


        #endregion

        #region Methods
        /// <summary>
        /// Gets whether there's a LoadInSeparateAppDomain attribute on this type.
        /// Caches the result - since it can't change during the build.
        /// </summary>
        /// <returns></returns>
        public bool HasLoadInSeparateAppDomainAttribute()
        {
            if (_hasLoadInSeparateAppDomainAttribute == null)
            {
                _hasLoadInSeparateAppDomainAttribute = this.Type.GetTypeInfo().IsDefined(typeof(LoadInSeparateAppDomainAttribute), true /* inherited */);
            }

            return (bool)_hasLoadInSeparateAppDomainAttribute;
        }

        /// <summary>
        /// Gets whether there's a STAThread attribute on the Execute method of this type.
        /// Caches the result - since it can't change during the build.
        /// </summary>
        /// <returns></returns>
        public bool HasSTAThreadAttribute()
        {
            if (_hasSTAThreadAttribute == null)
            {
                _hasSTAThreadAttribute = this.Type.GetTypeInfo().IsDefined(typeof(RunInSTAAttribute), true /* inherited */);
            }

            return (bool)_hasSTAThreadAttribute;
        }

        #endregion

        /// <summary>
        /// Determines if the task has a hardcoded requirement for STA thread usage.
        /// </summary>
        private void CheckForHardcodedSTARequirement()
        {
            // Special hard-coded attributes for certain legacy tasks which need to run as STA because they were written before
            // we changed to running all tasks in MTA.
            if (String.Equals("Microsoft.Build.Tasks.Xaml.PartialClassGenerationTask", _type.FullName, StringComparison.OrdinalIgnoreCase))
            {
                AssemblyName assemblyName = _type.GetTypeInfo().Assembly.GetName();
                Version lastVersionToForce = new Version(3, 5);
                if (assemblyName.Version.CompareTo(lastVersionToForce) > 0)
                {
                    if (String.Equals(assemblyName.Name, "PresentationBuildTasks", StringComparison.OrdinalIgnoreCase))
                    {
                        _hasSTAThreadAttribute = true;
                    }
                }
            }
        }

        #region Properties

        /// <summary>
        /// Gets the type that was loaded from an assembly.
        /// </summary>
        /// <value>The loaded type.</value>
        internal Type Type
        {
            get
            {
                return _type;
            }
        }

        /// <summary>
        /// If we loaded an assembly for this type.
        /// We use this information to help created AppDomains to resolve types that it could not load successfully
        /// </summary>
        internal Assembly LoadedAssembly
        {
            get
            {
                return _loadedAssembly;
            }
        }

        /// <summary>
        /// Gets the assembly the type was loaded from.
        /// </summary>
        /// <value>The assembly info for the loaded type.</value>
        internal AssemblyLoadInfo Assembly
        {
            get
            {
                return _assembly;
            }
        }

        #endregion

        // the type that was loaded
        private Type _type;
        // the assembly the type was loaded from
        private AssemblyLoadInfo _assembly;

        // whether the loadinseparateappdomain attribute is applied to this type
        private bool? _hasLoadInSeparateAppDomainAttribute;

        // whether the STAThread attribute is applied to this type
        private bool? _hasSTAThreadAttribute;

        /// <summary>
        /// Assembly, if any, that we loaded for this type.
        /// We use this information to help created AppDomains to resolve types that it could not load successfully
        /// </summary>
        private Assembly _loadedAssembly;
    }
}
