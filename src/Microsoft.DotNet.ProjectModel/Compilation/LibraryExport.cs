// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Diagnostics;

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
        /// Non assembly runtime assets.
        /// </summary>
        public IEnumerable<LibraryAsset> RuntimeAssets { get; }

        /// <summary>
        /// Gets a list of fully-qualified paths to native binaries required to run
        /// </summary>
        public IEnumerable<LibraryAsset> NativeLibraries { get; }

        /// <summary>
        /// Gets a list of fully-qualified paths to MSIL metadata references
        /// </summary>
        public IEnumerable<LibraryAsset> CompilationAssemblies { get; }

        /// <summary>
        /// Get a list of embedded resource files provided by this export.
        /// </summary>
        public IEnumerable<LibraryAsset> EmbeddedResources { get; }

        /// <summary>
        /// Gets a list of fully-qualified paths to source code file references
        /// </summary>
        public IEnumerable<LibraryAsset> SourceReferences { get; }

        /// <summary>
        /// Get a list of analyzers provided by this export.
        /// </summary>
        public IEnumerable<AnalyzerReference> AnalyzerReferences { get; }

        public LibraryExport(LibraryDescription library,
                             IEnumerable<LibraryAsset> compileAssemblies,
                             IEnumerable<LibraryAsset> sourceReferences,
                             IEnumerable<LibraryAsset> runtimeAssemblies,
                             IEnumerable<LibraryAsset> runtimeAssets,
                             IEnumerable<LibraryAsset> nativeLibraries,
                             IEnumerable<LibraryAsset> embeddedResources,
                             IEnumerable<AnalyzerReference> analyzers)
        {
            Library = library;
            CompilationAssemblies = compileAssemblies;
            SourceReferences = sourceReferences;
            RuntimeAssemblies = runtimeAssemblies;
            RuntimeAssets = runtimeAssets;
            NativeLibraries = nativeLibraries;
            EmbeddedResources = embeddedResources;
            AnalyzerReferences = analyzers;
        }

        private string DebuggerDisplay => Library.Identity.ToString();
    }
}
