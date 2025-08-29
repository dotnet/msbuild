﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Shared;

#nullable disable

namespace Microsoft.Build.Tasks
{
    /// <remarks>
    /// This class is a caching mechanism for the Register/UnregisterAssembly task to keep track of registered assemblies to clean up
    /// </remarks>
    internal sealed class AssemblyRegistrationCache : StateFileBase, ITranslatable
    {
        /// <summary>
        /// The list of registered assembly files.
        /// </summary>
        internal List<string> _assemblies = new List<string>();

        /// <summary>
        /// The list of registered type library files.
        /// </summary>
        internal List<string> _typeLibraries = new List<string>();

        /// <summary>
        /// The number of entries in the state file
        /// </summary>
        internal int Count
        {
            get
            {
                ErrorUtilities.VerifyThrow(_assemblies.Count == _typeLibraries.Count, "Internal assembly and type library lists should have the same number of entries in AssemblyRegistrationCache");
                return _assemblies.Count;
            }
        }

        /// <summary>
        /// Sets the entry with the specified index
        /// </summary>
        internal void AddEntry(string assemblyPath, string typeLibraryPath)
        {
            _assemblies.Add(assemblyPath);
            _typeLibraries.Add(typeLibraryPath);
        }

        /// <summary>
        /// Gets the entry with the specified index
        /// </summary>
        internal void GetEntry(int index, out string assemblyPath, out string typeLibraryPath)
        {
            ErrorUtilities.VerifyThrow((index >= 0) && (index < _assemblies.Count), "Invalid index in the call to AssemblyRegistrationCache.GetEntry");
            assemblyPath = _assemblies[index];
            typeLibraryPath = _typeLibraries[index];
        }

        public AssemblyRegistrationCache(ITranslator translator)
        {
            Translate(translator);
        }

        public AssemblyRegistrationCache() { }

        public override void Translate(ITranslator translator)
        {
            ErrorUtilities.VerifyThrowArgumentNull(translator, nameof(translator));
            translator.Translate(ref _assemblies);
            translator.Translate(ref _typeLibraries);
        }
    }
}
