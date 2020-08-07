// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.BackEnd;
using System.Diagnostics;

namespace Microsoft.Build.Shared
{
    /// <summary>
    /// This class packages information about how to load a given assembly -- an assembly can be loaded by either its assembly
    /// name (strong or weak), or its filename/path.
    /// </summary>
    /// <remarks>
    /// Uses factory to instantiate correct private class to save space: only one field is ever used of the two.
    /// </remarks>
    internal abstract class AssemblyLoadInfo : ITranslatable, IEquatable<AssemblyLoadInfo>
    {
        /// <summary>
        /// This constructor initializes the assembly information.
        /// </summary>
        internal static AssemblyLoadInfo Create(string assemblyName, string assemblyFile)
        {
            ErrorUtilities.VerifyThrow((!string.IsNullOrEmpty(assemblyName)) || (!string.IsNullOrEmpty(assemblyFile)),
                "We must have either the assembly name or the assembly file/path.");
            ErrorUtilities.VerifyThrow((assemblyName == null) || (assemblyFile == null),
                "We must not have both the assembly name and the assembly file/path.");

            if (assemblyName != null)
            {
                return new AssemblyLoadInfoWithName(assemblyName);
            }
            else
            {
                return new AssemblyLoadInfoWithFile(assemblyFile);
            }
        }

        /// <summary>
        /// Gets the assembly's identity denoted by its strong/weak name.
        /// </summary>
        public abstract string AssemblyName
        {
            get;
        }

        /// <summary>
        /// Gets the path to the assembly file.
        /// </summary>
        public abstract string AssemblyFile
        {
            get;
        }

        /// <summary>
        /// Get the assembly location
        /// </summary>
        internal abstract string AssemblyLocation
        {
            get;
        }

        /// <summary>
        /// Computes a hashcode for this assembly info, so this object can be used as a key into
        /// a hash table.
        /// </summary>
        public override int GetHashCode()
        {
            return AssemblyLocation.GetHashCode();
        }

        public bool Equals(AssemblyLoadInfo other)
        {
            return Equals((object)other);
        }

        /// <summary>
        /// Determines if two AssemblyLoadInfos are effectively the same.
        /// </summary>
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

            return (this.AssemblyName == otherAssemblyInfo.AssemblyName) && (this.AssemblyFile == otherAssemblyInfo.AssemblyFile);
        }

        public void Translate(ITranslator translator)
        {
            ErrorUtilities.VerifyThrow(translator.Mode == TranslationDirection.WriteToStream, "write only");
            string assemblyName = AssemblyName;
            string assemblyFile = AssemblyFile;
            translator.Translate(ref assemblyName);
            translator.Translate(ref assemblyFile);
        }

        static public AssemblyLoadInfo FactoryForTranslation(ITranslator translator)
        {
            string assemblyName = null;
            string assemblyFile = null;
            translator.Translate(ref assemblyName);
            translator.Translate(ref assemblyFile);

            return Create(assemblyName, assemblyFile);
        }

        /// <summary>
        /// Assembly represented by name
        /// </summary>
        [DebuggerDisplay("{AssemblyName}")]
        private sealed class AssemblyLoadInfoWithName : AssemblyLoadInfo
        {
            /// <summary>
            /// Assembly name
            /// </summary>
            private string _assemblyName;

            /// <summary>
            /// Constructor
            /// </summary>
            internal AssemblyLoadInfoWithName(string assemblyName)
            {
                _assemblyName = assemblyName;
            }

            /// <summary>
            /// Gets the assembly's identity denoted by its strong/weak name.
            /// </summary>
            public override string AssemblyName
            {
                get { return _assemblyName; }
            }

            /// <summary>
            /// Gets the path to the assembly file.
            /// </summary>
            public override string AssemblyFile
            {
                get { return null; }
            }

            /// <summary>
            /// Get the assembly location
            /// </summary>
            internal override string AssemblyLocation
            {
                get { return _assemblyName; }
            }
        }

        /// <summary>
        /// Assembly info that uses a file path
        /// </summary>
        [DebuggerDisplay("{AssemblyFile}")]
        private sealed class AssemblyLoadInfoWithFile : AssemblyLoadInfo
        {
            /// <summary>
            /// Path to assembly
            /// </summary>
            private string _assemblyFile;

            /// <summary>
            /// Constructor
            /// </summary>
            internal AssemblyLoadInfoWithFile(string assemblyFile)
            {
                ErrorUtilities.VerifyThrow(Path.IsPathRooted(assemblyFile), "Assembly file path should be rooted");

                _assemblyFile = assemblyFile;
            }

            /// <summary>
            /// Gets the assembly's identity denoted by its strong/weak name.
            /// </summary>
            public override string AssemblyName
            {
                get { return null; }
            }

            /// <summary>
            /// Gets the path to the assembly file.
            /// </summary>
            public override string AssemblyFile
            {
                get { return _assemblyFile; }
            }

            /// <summary>
            /// Get the assembly location
            /// </summary>
            internal override string AssemblyLocation
            {
                get { return _assemblyFile; }
            }
        }
    }
}
