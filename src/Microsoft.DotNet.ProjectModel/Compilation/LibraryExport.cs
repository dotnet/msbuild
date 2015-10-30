using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Extensions.ProjectModel.Compilation
{
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
    }
}
