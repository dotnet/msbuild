// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Reflection;
using System.Collections.Generic;
using Microsoft.Build.Framework;

namespace Microsoft.Build.BuildEngine.Shared
{
    /// <summary>
    /// This class packages information about a type loaded from an assembly: for example,
    /// the GenerateResource task class type or the ConsoleLogger logger class type.
    /// </summary>
    /// <owner>SumedhK</owner>
    internal sealed class LoadedType
    {
        #region Constructors

        /// <summary>
        /// Creates an instance of this class for the given type.
        /// </summary>
        /// <owner>SumedhK</owner>
        /// <param name="type"></param>
        /// <param name="assembly"></param>
        internal LoadedType(Type type, AssemblyLoadInfo assembly)
        {
            ErrorUtilities.VerifyThrow(type != null, "We must have the type.");
            ErrorUtilities.VerifyThrow(assembly != null, "We must have the assembly the type was loaded from.");

            this.type = type;
            this.assembly = assembly;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Gets the list of names of public instance properties that have the required attribute applied.
        /// Caches the result - since it can't change during the build.
        /// </summary>
        /// <returns></returns>
        public Dictionary<string, string> GetNamesOfPropertiesWithRequiredAttribute()
        {
            if (propertyInfoCache == null)
            {
                PopulatePropertyInfoCache();
            }
            return namesOfPropertiesWithRequiredAttribute;
        }

        /// <summary>
        /// Gets the list of names of public instance properties that have the output attribute applied.
        /// Caches the result - since it can't change during the build.
        /// </summary>
        /// <returns></returns>
        public Dictionary<string, string> GetNamesOfPropertiesWithOutputAttribute()
        {
            if (propertyInfoCache == null)
            {
                PopulatePropertyInfoCache();
            }
            return namesOfPropertiesWithOutputAttribute;
        }

        /// <summary>
        /// Get the cached propertyinfo of the given name
        /// </summary>
        /// <param name="propertyName">property name</param>
        /// <returns>PropertyInfo</returns>
        public PropertyInfo GetProperty(string propertyName)
        {   
            if (propertyInfoCache == null)
            {
                PopulatePropertyInfoCache();
            }

            PropertyInfo propertyInfo;
            if (!propertyInfoCache.TryGetValue(propertyName, out propertyInfo))
            {
                return null;
            }
            else
            {
                if (namesOfPropertiesWithAmbiguousMatches.ContainsKey(propertyName))
                {
                    // See comment in PopulatePropertyInfoCache
                    throw new AmbiguousMatchException();
                }

                return propertyInfo;
            }
        }

        /// <summary>
        /// Populate the cache of PropertyInfos for this type
        /// </summary>
        private void PopulatePropertyInfoCache()
        {
            if (propertyInfoCache == null)
            {
                propertyInfoCache = new Dictionary<string, PropertyInfo>(StringComparer.OrdinalIgnoreCase);
                namesOfPropertiesWithRequiredAttribute = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                namesOfPropertiesWithOutputAttribute = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                namesOfPropertiesWithAmbiguousMatches = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                PropertyInfo[] propertyInfos = this.Type.GetProperties(BindingFlags.Instance | BindingFlags.Public);

                for (int i = 0; i < propertyInfos.Length; i++)
                {
                    try
                    {
                        propertyInfoCache.Add(propertyInfos[i].Name, propertyInfos[i]);
                    }
                    catch (ArgumentException)
                    {
                        // We have encountered a duplicate entry in our hashtable; if we had used BindingFlags.IgnoreCase this
                        // would have produced an AmbiguousMatchException. In the old code, before this cache existed,
                        // that wouldn't have been thrown unless and until the project actually tried to set this ambiguous parameter.
                        // So rather than fail here, we store a list of ambiguous names and throw later, when one of them
                        // is requested.
                        namesOfPropertiesWithAmbiguousMatches[propertyInfos[i].Name] = String.Empty;
                    }

                    if (propertyInfos[i].IsDefined(typeof(RequiredAttribute), false /* uninherited */))
                    {
                        // we have a require attribute defined, keep a record of that
                        namesOfPropertiesWithRequiredAttribute[propertyInfos[i].Name] = String.Empty;
                    }

                    if (propertyInfos[i].IsDefined(typeof(OutputAttribute), false /* uninherited */))
                    {
                        // we have a output attribute defined, keep a record of that
                        namesOfPropertiesWithOutputAttribute[propertyInfos[i].Name] = String.Empty;
                    }
                }
            }
        }

        /// <summary>
        /// Gets whether there's a LoadInSeparateAppDomain attribute on this type.
        /// Caches the result - since it can't change during the build.
        /// </summary>
        /// <returns></returns>
        public bool HasLoadInSeparateAppDomainAttribute()
        {
            if (hasLoadInSeparateAppDomainAttribute == null)
            {
                hasLoadInSeparateAppDomainAttribute = this.Type.IsDefined(typeof(LoadInSeparateAppDomainAttribute), true /* inherited */);
            }

            return (bool)hasLoadInSeparateAppDomainAttribute;
        }

        #endregion


        #region Properties

        /// <summary>
        /// Gets the type that was loaded from an assembly.
        /// </summary>
        /// <owner>SumedhK</owner>
        /// <value>The loaded type.</value>
        internal Type Type
        {
            get
            {
                return type;
            }
        }

        /// <summary>
        /// Gets the assembly the type was loaded from.
        /// </summary>
        /// <owner>SumedhK</owner>
        /// <value>The assembly info for the loaded type.</value>
        internal AssemblyLoadInfo Assembly
        {
            get
            {
                return assembly;
            }
        }

        #endregion

        // the type that was loaded
        private Type type;
        // the assembly the type was loaded from
        private AssemblyLoadInfo assembly;

        // cache of names of required properties on this type
        private Dictionary<string, string> namesOfPropertiesWithRequiredAttribute;

        // cache of names of output properties on this type
        private Dictionary<string, string> namesOfPropertiesWithOutputAttribute;

        // cache of names of properties on this type whose names are ambiguous
        private Dictionary<string, string> namesOfPropertiesWithAmbiguousMatches;

        // cache of PropertyInfos for this type
        private Dictionary<string, PropertyInfo> propertyInfoCache;

        // whether the loadinseparateappdomain attribute is applied to this type
        private bool? hasLoadInSeparateAppDomainAttribute;
    }
}
