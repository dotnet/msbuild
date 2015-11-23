// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Tasks
{
    /// <remarks>
    /// This class is a caching mechanism for the Register/UnregisterAssembly task to keep track of registered assemblies to clean up
    /// </remarks>
    [Serializable()]
    internal sealed class AssemblyRegistrationCache : StateFileBase
    {
        /// <summary>
        /// The list of registered assembly files.
        /// </summary>
        private ArrayList assemblies = null;

        /// <summary>
        /// The list of registered type library files.
        /// </summary>
        private ArrayList typeLibraries = null;

        /// <summary>
        /// Construct.
        /// </summary>
        internal AssemblyRegistrationCache()
        {
            assemblies = new ArrayList();
            typeLibraries = new ArrayList();
        }

        /// <summary>
        /// The number of entries in the state file
        /// </summary>
        internal int Count
        {
            get
            {
                ErrorUtilities.VerifyThrow(assemblies.Count == typeLibraries.Count, "Internal assembly and type library lists should have the same number of entries in AssemblyRegistrationCache");
                return assemblies.Count;
            }
        }

        /// <summary>
        /// Sets the entry with the specified index
        /// </summary>
        /// <param name="index"></param>
        /// <param name="assemblyPath"></param>
        /// <param name="typeLibraryPath"></param>
        internal void AddEntry(string assemblyPath, string typeLibraryPath)
        {
            assemblies.Add(assemblyPath);
            typeLibraries.Add(typeLibraryPath);
        }

        /// <summary>
        /// Gets the entry with the specified index
        /// </summary>
        /// <param name="index"></param>
        /// <param name="assemblyPath"></param>
        /// <param name="typeLibraryPath"></param>
        internal void GetEntry(int index, out string assemblyPath, out string typeLibraryPath)
        {
            ErrorUtilities.VerifyThrow((index >= 0) && (index < assemblies.Count), "Invalid index in the call to AssemblyRegistrationCache.GetEntry");
            assemblyPath = (string)assemblies[index];
            typeLibraryPath = (string)typeLibraries[index];
        }
    }
}
