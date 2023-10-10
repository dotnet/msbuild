// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;

namespace Microsoft.DotNet.ApiSymbolExtensions
{
    /// <summary>
    /// Loads <see cref="IAssemblySymbol"/> objects from source files, binaries or directories containing binaries.
    /// </summary>
    public interface IAssemblySymbolLoader
    {
        /// <summary>
        /// Adds a set of paths to the search directories to resolve references from. Paths may
        /// be directories or full paths to assembly files.
        /// This is only used when the setting to resolve assembly references is set to true.
        /// </summary>
        /// <param name="paths">The list of paths to register as search directories.</param>
        void AddReferenceSearchPaths(params string[] paths);

        /// <summary>
        /// Indicates if the compilation used to resolve binaries has any roslyn diagnostics.
        /// Might be useful when loading an assembly from source files.
        /// </summary>
        /// <param name="diagnostics">List of diagnostics.</param>
        /// <returns>True if there are any diagnostics, false otherwise.</returns>
        bool HasRoslynDiagnostics(out IReadOnlyList<Diagnostic> diagnostics);

        /// <summary>
        /// Indicates if the loader emitted any warnings that might affect the assembly resolution.
        /// </summary>
        /// <param name="warnings">List of warnings.</param>
        /// <returns>True if there are any warnings, false otherwise.</returns>
        bool HasLoadWarnings(out IReadOnlyList<AssemblyLoadWarning> warnings);

        /// <summary>
        /// Loads a list of assemblies and gets its corresponding <see cref="IAssemblySymbol"/> from the specified paths.
        /// </summary>
        /// <param name="paths">List of paths to load binaries from. Can be full paths to binaries or directories.</param>
        /// <returns>The list of resolved <see cref="IAssemblySymbol"/>.</returns>
        IReadOnlyList<IAssemblySymbol?> LoadAssemblies(params string[] paths);

        /// <summary>
        /// Loads assemblies from an archive based on the given relative paths.
        /// </summary>
        /// <param name="archivePath">The path to the archive that should be opened.</param>
        /// <param name="relativePaths">The relative paths that point to assemblies inside the archive.</param>
        /// <returns>The list of resolved and unresolved <see cref="IAssemblySymbol"/>.</returns>
        IReadOnlyList<IAssemblySymbol?> LoadAssembliesFromArchive(string archivePath, IReadOnlyList<string> relativePaths);

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
        /// <returns><see cref="IAssemblySymbol"/> representing the given <paramref name="stream"/>. If an 
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

        /// <summary>
        /// The list of metadata references represented as <see cref="MetadataReference" />.
        /// </summary>
        IEnumerable<MetadataReference> MetadataReferences { get; }
    }
}
