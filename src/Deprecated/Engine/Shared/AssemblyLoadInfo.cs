// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.Build.BuildEngine.Shared
{
    /// <summary>
    /// This class packages information about how to load a given assembly -- an assembly can be loaded by either its assembly
    /// name (strong or weak), or its filename/path.
    /// </summary>
    /// <owner>SumedhK</owner>
    internal sealed class AssemblyLoadInfo
    {
        #region Constructors

        /// <summary>
        /// This constructor initializes the assembly information.
        /// </summary>
        /// <owner>SumedhK</owner>
        /// <param name="assemblyName"></param>
        /// <param name="assemblyFile"></param>
        public AssemblyLoadInfo(string assemblyName, string assemblyFile)
        {
            ErrorUtilities.VerifyThrow(((assemblyName != null) && (assemblyName.Length > 0)) || ((assemblyFile != null) && (assemblyFile.Length > 0)),
                "We must have either the assembly name or the assembly file/path.");
            ErrorUtilities.VerifyThrow((assemblyName == null) || (assemblyFile == null),
                "We must not have both the assembly name and the assembly file/path.");

            this.assemblyName = assemblyName;
            this.assemblyFile = assemblyFile;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the assembly's identity denoted by its strong/weak name.
        /// </summary>
        /// <owner>SumedhK</owner>
        public string AssemblyName
        {
            get
            {
                return assemblyName;
            }
        }

        /// <summary>
        /// Gets the path to the assembly file.
        /// </summary>
        /// <owner>SumedhK</owner>
        public string AssemblyFile
        {
            get
            {
                return assemblyFile;
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Retrieves the load information for the assembly.
        /// </summary>
        /// <returns>The assembly name, or the assembly file/path.</returns>
        public override string ToString()
        {
            if (assemblyName != null)
            {
                ErrorUtilities.VerifyThrow(assemblyFile == null,
                    "We cannot have both the assembly name and the assembly file/path.");

                return assemblyName;
            }
            else
            {
                ErrorUtilities.VerifyThrow(assemblyFile != null,
                    "We must have either the assembly name or the assembly file/path.");

                return assemblyFile;
            }
        }

        /// <summary>
        /// Computes a hashcode for this assembly info, so this object can be used as a key into
        /// a hash table.
        /// </summary>
        /// <returns></returns>
        /// <owner>RGoel</owner>
        public override int GetHashCode()
        {
            return ToString().GetHashCode();
        }

        /// <summary>
        /// Determines if two AssemblyLoadInfos are effectively the same.
        /// </summary>
        /// <returns></returns>
        /// <owner>RGoel</owner>
        public override bool Equals(Object obj) 
        {
            if (obj == null)
            {
                return false;
            }

            AssemblyLoadInfo otherAssemblyInfo = obj as AssemblyLoadInfo;
            
            if (otherAssemblyInfo == null)
            {
                return false;
            }

            return ((this.AssemblyName == otherAssemblyInfo.AssemblyName) && (this.AssemblyFile == otherAssemblyInfo.AssemblyFile));
        }

        #endregion

        // the assembly's identity
        private string assemblyName;
        // the assembly filename/path
        private string assemblyFile;
    }
}
