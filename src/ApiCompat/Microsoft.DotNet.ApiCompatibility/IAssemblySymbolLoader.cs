// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Collections.Generic;
using System.IO;

namespace Microsoft.DotNet.ApiCompatibility
{
    /// <summary>
    /// Loads <see cref="IAssemblySymbol"/> objects from source files, binaries or directories containing binaries.
    /// </summary>
    public interface IAssemblySymbolLoader
    {
        /// <summary>
        /// Adds a set of paths to the search directories to resolve references from.
        /// This is only used when the setting to resolve assembly references is set to true.
        /// </summary>
        /// <param name="paths">Comma separated list of paths to register as search directories.</param>
        void AddReferenceSearchDirectories(string paths);

        /// <summary>
        /// Adds a set of paths to the search directories to resolve references from. Paths may
        /// be directories or full paths to assembly files.
        /// This is only used when the setting to resolve assembly references is set to true.
        /// </summary>
        /// <param name="paths">The list of paths to register as search directories.</param>
        void AddReferenceSearchDirectories(IEnumerable<string> paths);

        /// <summary>
        /// Indicates if the <see cref="CSharpCompilation"/> used to resolve binaries has any roslyn diagnostics.
        /// Might be useful when loading an assembly from source files.
        /// </summary>
        /// <param name="diagnostics">List of diagnostics.</param>
        /// <returns>True if there are any diagnostics, false otherwise.</returns>
        bool HasRoslynDiagnostics(out IEnumerable<Diagnostic> diagnostics);

        /// <summary>
        /// Indicates if the loader emitted any warnings that might affect the assembly resolution.
        /// </summary>
        /// <param name="warnings">List of warnings.</param>
        /// <returns>True if there are any warnings, false otherwise.</returns>
        bool HasLoadWarnings(out IEnumerable<AssemblyLoadWarning> warnings);

        /// <summary>
        /// Loads a list of assemblies and gets its corresponding <see cref="IAssemblySymbol"/> from the specified paths.
        /// </summary>
        /// <param name="paths">Comma separated list of paths to load binaries from. Can be full paths to binaries or a directory.</param>
        /// <returns>The list of resolved <see cref="IAssemblySymbol"/>.</returns>
        IEnumerable<IAssemblySymbol> LoadAssemblies(string paths);

        /// <summary>
        /// Loads a list of assemblies and gets its corresponding <see cref="IAssemblySymbol"/> from the specified paths.
        /// </summary>
        /// <param name="paths">List of paths to load binaries from. Can be full paths to binaries or a directory.</param>
        /// <returns>The list of resolved <see cref="IAssemblySymbol"/>.</returns>
        IEnumerable<IAssemblySymbol> LoadAssemblies(IEnumerable<string> paths);

        /// <summary>
        /// Loads an assembly from the provided path.
        /// </summary>
        /// <param name="path">The full path to the assembly.</param>
        /// <returns><see cref="IAssemblySymbol"/> representing the loaded assembly.</returns>
        IAssemblySymbol? LoadAssembly(string path);

        /// <summary>
        /// Loads an assembly using the provided name from a given <see cref="Stream"/>.
        /// </summary>
        /// <param name="name">The name to use to resolve the assembly.</param>
        /// <param name="stream">The stream to read the metadata from.</param>
        /// <returns><see cref="IAssemblySymbol"/> respresenting the given <paramref name="stream"/>. If an 
        /// assembly with the same <paramref name="name"/> was already loaded, the previously loaded assembly is returned.</returns>
        IAssemblySymbol? LoadAssembly(string name, Stream stream);

        /// <summary>
        /// Loads an <see cref="IAssemblySymbol"/> containing the metadata from the provided source files and given assembly name.
        /// </summary>
        /// <param name="filePaths">The file paths to use as syntax trees to create the <see cref="IAssemblySymbol"/>.</param>
        /// <param name="assemblyName">The name of the <see cref="IAssemblySymbol"/>.</param>
        /// <param name="referencePaths">Paths to use as references if we want all the references to be included in the metadata.</param>
        /// <returns>The <see cref="IAssemblySymbol"/> containing the metadata from the provided source files.</returns>
        IAssemblySymbol LoadAssemblyFromSourceFiles(IEnumerable<string> filePaths, string assemblyName, IEnumerable<string> referencePaths);

        /// <summary>
        /// Loads a list of matching assemblies given the "from" list that we should try and load it's matching assembly from the given paths.
        /// </summary>
        /// <param name="fromAssemblies">List of <see cref="IAssemblySymbol"/> to search for.</param>
        /// <param name="searchPaths">List of search paths.</param>
        /// <param name="validateMatchingIdentity">Indicates if we should validate that the identity of the resolved assembly is the same.</param>
        /// <param name="warnOnMissingAssemblies">Indicates if a warning should be added to the warning list when a matching assembly is not found.</param>
        /// <returns>The list of matching assemblies represented as <see cref="IAssemblySymbol"/>.</returns>
        IEnumerable<IAssemblySymbol> LoadMatchingAssemblies(IEnumerable<IAssemblySymbol> fromAssemblies, IEnumerable<string> searchPaths, bool validateMatchingIdentity = true, bool warnOnMissingAssemblies = true);
    }
}
