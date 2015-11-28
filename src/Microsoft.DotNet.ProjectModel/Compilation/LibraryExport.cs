// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Microsoft.DotNet.ProjectModel.Compilation
{
    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    public class LibraryExport
    {
        /// <summary>
        /// Gets the library that produced this export
        /// </summary>
        public LibraryDescription Library { get; }

        /// <summary>
        /// Gets a list of fully-qualified paths to MSIL binaries required to run
        /// </summary>
        public IEnumerable<LibraryAsset> RuntimeAssemblies { get; }

        /// <summary>
        /// Gets a list of fully-qualified paths to native binaries required to run
        /// </summary>
        public IEnumerable<LibraryAsset> NativeLibraries { get; }

        /// <summary>
        /// Gets a list of fully-qualified paths to MSIL metadata references
        /// </summary>
        public IEnumerable<LibraryAsset> CompilationAssemblies { get; }

        /// <summary>
        /// Gets a list of fully-qualified paths to source code file references
        /// </summary>
        public IEnumerable<string> SourceReferences { get; }

        public LibraryExport(LibraryDescription library, IEnumerable<LibraryAsset> compileAssemblies, IEnumerable<string> sourceReferences, IEnumerable<LibraryAsset> runtimeAssemblies, IEnumerable<LibraryAsset> nativeLibraries)
        {
            Library = library;
            CompilationAssemblies = compileAssemblies;
            SourceReferences = sourceReferences;
            RuntimeAssemblies = runtimeAssemblies;
            NativeLibraries = nativeLibraries;
        }

        private string DebuggerDisplay
        {
            get
            {
                return Library.Identity.ToString();
            }
        }
    }
}
