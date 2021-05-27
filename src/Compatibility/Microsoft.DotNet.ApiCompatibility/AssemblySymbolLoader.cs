// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace Microsoft.DotNet.ApiCompatibility
{
    /// <summary>
    /// Class that loads <see cref="IAssemblySymbol"/> objects from source files, binaries or directories containing binaries.
    /// </summary>
    public class AssemblySymbolLoader
    {
        private readonly HashSet<string> _referenceSearchPaths = new();
        private readonly List<string> _warnings = new();
        private readonly Dictionary<string, MetadataReference> _loadedAssemblies;
        private readonly bool _resolveReferences;
        private CSharpCompilation _cSharpCompilation;

        /// <summary>
        /// Instanciate an object with the desired setting to resolve assembly references or not.
        /// </summary>
        /// <param name="resolveAssemblyReferences">Indicates whether it should try to resolve assembly references when loading or not.</param>
        public AssemblySymbolLoader(bool resolveAssemblyReferences = false)
        {
            _loadedAssemblies = new Dictionary<string, MetadataReference>();
            var compilationOptions = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable);
            _cSharpCompilation = CSharpCompilation.Create($"AssemblyLoader_{DateTime.Now:MM_dd_yy_HH_mm_ss_FFF}", options: compilationOptions);
            _resolveReferences = resolveAssemblyReferences;
        }

        /// <summary>
        /// Adds a set of paths to the search directories to resolve references from.
        /// This is only used when the setting to resolve assembly references is set to true.
        /// </summary>
        /// <param name="paths">Comma separated list of paths to register as search directories.</param>
        public void AddReferenceSearchDirectories(string paths) => AddReferenceSearchDirectories(SplitPaths(paths));

        /// <summary>
        /// Adds a set of paths to the search directories to resolve references from.
        /// This is only used when the setting to resolve assembly references is set to true.
        /// </summary>
        /// <param name="paths">The list of paths to register as search directories.</param>
        public void AddReferenceSearchDirectories(IEnumerable<string> paths)
        {
            if (paths == null)
            {
                throw new ArgumentNullException(nameof(paths));
            }

            foreach (string path in paths)
                _referenceSearchPaths.Add(path);
        }

        /// <summary>
        /// Indicates if the <see cref="CSharpCompilation"/> used to resolve binaries has any roslyn diagnostics.
        /// Might be useful when loading an assembly from source files.
        /// </summary>
        /// <param name="diagnostics">List of diagnostics.</param>
        /// <returns>True if there are any diagnostics, false otherwise.</returns>
        public bool HasRoslynDiagnostics(out IEnumerable<Diagnostic> diagnostics)
        {
            diagnostics = _cSharpCompilation.GetDiagnostics();
            return diagnostics.Any();
        }

        /// <summary>
        /// Indicates if the loader emitted any warnings that might affect the assembly resolution.
        /// </summary>
        /// <param name="warnings">List of warnings.</param>
        /// <returns>True if there are any warnings, false otherwise.</returns>
        public bool HasLoadWarnings(out IEnumerable<string> warnings)
        {
            warnings = _warnings;
            return _warnings.Count > 0;
        }

        private static string[] SplitPaths(string paths) =>
            paths == null ? null : paths.Split(new char[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);

        /// <summary>
        /// Loads a list of assemblies and gets its corresponding <see cref="IAssemblySymbol"/> from the specified paths.
        /// </summary>
        /// <param name="paths">Comma separated list of paths to load binaries from. Can be full paths to binaries or a directory.</param>
        /// <returns>The list of resolved <see cref="IAssemblySymbol"/>.</returns>
        public IEnumerable<IAssemblySymbol> LoadAssemblies(string paths) => LoadAssemblies(SplitPaths(paths));

        /// <summary>
        /// Loads a list of assemblies and gets its corresponding <see cref="IAssemblySymbol"/> from the specified paths.
        /// </summary>
        /// <param name="paths">List of paths to load binaries from. Can be full paths to binaries or a directory.</param>
        /// <returns>The list of resolved <see cref="IAssemblySymbol"/>.</returns>
        public IEnumerable<IAssemblySymbol> LoadAssemblies(IEnumerable<string> paths)
        {
            if (paths == null)
            {
                throw new ArgumentNullException(nameof(paths));
            }

            IEnumerable<MetadataReference> assembliesToReturn = LoadFromPaths(paths);

            List<IAssemblySymbol> result = new();
            foreach (MetadataReference metadataReference in assembliesToReturn)
            {
                ISymbol symbol = _cSharpCompilation.GetAssemblyOrModuleSymbol(metadataReference);
                if (symbol is IAssemblySymbol assemblySymbol)
                {
                    result.Add(assemblySymbol);
                }
            }

            return result;
        }

        /// <summary>
        /// Loads an assembly from the provided path.
        /// </summary>
        /// <param name="path">The full path to the assembly.</param>
        /// <returns><see cref="IAssemblySymbol"/> representing the loaded assembly.</returns>
        public IAssemblySymbol LoadAssembly(string path)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            MetadataReference metadataReference = CreateOrGetMetadataReferenceFromPath(path);
            return (IAssemblySymbol)_cSharpCompilation.GetAssemblyOrModuleSymbol(metadataReference);
        }

        /// <summary>
        /// Loads an assembly using the provided name from a given <see cref="Stream"/>.
        /// </summary>
        /// <param name="name">The name to use to resolve the assembly.</param>
        /// <param name="stream">The stream to read the metadata from.</param>
        /// <returns><see cref="IAssemblySymbol"/> respresenting the given <paramref name="stream"/>. If an 
        /// assembly with the same <paramref name="name"/> was already loaded, the previously loaded assembly is returned.</returns>
        public IAssemblySymbol LoadAssembly(string name, Stream stream)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            if (stream.Position >= stream.Length)
            {
                throw new ArgumentException(Resources.StreamPositionGreaterThanLength, nameof(stream));
            }

            if (!_loadedAssemblies.TryGetValue(name, out MetadataReference metadataReference))
            {
                metadataReference = CreateAndAddReferenceToCompilation(name, stream);
            }

            return (IAssemblySymbol)_cSharpCompilation.GetAssemblyOrModuleSymbol(metadataReference);
        }
        
        /// <summary>
        /// Loads an <see cref="IAssemblySymbol"/> containing the metadata from the provided source files and given assembly name.
        /// </summary>
        /// <param name="filePaths">The file paths to use as syntax trees to create the <see cref="IAssemblySymbol"/>.</param>
        /// <param name="assemblyName">The name of the <see cref="IAssemblySymbol"/>.</param>
        /// <param name="referencePaths">Paths to use as references if we want all the references to be included in the metadata.</param>
        /// <returns>The <see cref="IAssemblySymbol"/> containing the metadata from the provided source files.</returns>
        public IAssemblySymbol LoadAssemblyFromSourceFiles(IEnumerable<string> filePaths, string assemblyName, IEnumerable<string> referencePaths)
        {
            if (filePaths == null || filePaths.Count() == 0)
            {
                throw new ArgumentNullException(nameof(filePaths), Resources.ShouldNotBeNullAndContainAtLeastOneElement);
            }

            if (string.IsNullOrEmpty(assemblyName))
            {
                throw new ArgumentNullException(nameof(assemblyName), Resources.ShouldProvideValidAssemblyName);
            }

            _cSharpCompilation = _cSharpCompilation.WithAssemblyName(assemblyName);

            List<SyntaxTree> syntaxTrees = new();
            foreach (string filePath in filePaths)
            {
                if (!File.Exists(filePath))
                {
                    throw new FileNotFoundException(string.Format(Resources.FileDoesNotExist, filePath));
                }

                syntaxTrees.Add(CSharpSyntaxTree.ParseText(File.ReadAllText(filePath)));
            }

            _cSharpCompilation = _cSharpCompilation.AddSyntaxTrees(syntaxTrees);

            LoadFromPaths(referencePaths);
            return _cSharpCompilation.Assembly;
        }

        /// <summary>
        /// Loads a list of matching assemblies given the "from" list that we should try and load it's matching assembly from the given paths.
        /// </summary>
        /// <param name="fromAssemblies">List of <see cref="IAssemblySymbol"/> to search for.</param>
        /// <param name="searchPaths">List of search paths.</param>
        /// <param name="validateMatchingIdentity">Indicates if we should validate that the identity of the resolved assembly is the same.</param>
        /// <param name="warnOnMissingAssemblies">Indicates if a warning should be added to the warning list when a matching assembly is not found.</param>
        /// <returns>The list of matching assemblies represented as <see cref="IAssemblySymbol"/>.</returns>
        public IEnumerable<IAssemblySymbol> LoadMatchingAssemblies(IEnumerable<IAssemblySymbol> fromAssemblies, IEnumerable<string> searchPaths, bool validateMatchingIdentity = true, bool warnOnMissingAssemblies = true)
        {
            if (fromAssemblies == null)
            {
                throw new ArgumentNullException(nameof(fromAssemblies));
            }

            if (searchPaths == null)
            {
                throw new ArgumentNullException(nameof(searchPaths));
            }

            List<IAssemblySymbol> matchingAssemblies = new();
            foreach (IAssemblySymbol assembly in fromAssemblies)
            {
                bool found = false;
                string name = $"{assembly.Name}.dll";
                foreach (string directory in searchPaths)
                {
                    if (!Directory.Exists(directory))
                    {
                        throw new FileNotFoundException(string.Format(Resources.ShouldProvideValidAssemblyName, directory), nameof(searchPaths));
                    }

                    string possiblePath = Path.Combine(directory, name);
                    if (File.Exists(possiblePath))
                    {
                        MetadataReference reference = CreateOrGetMetadataReferenceFromPath(possiblePath);
                        ISymbol symbol = _cSharpCompilation.GetAssemblyOrModuleSymbol(reference);
                        if (symbol is IAssemblySymbol matchingAssembly)
                        {
                            if (validateMatchingIdentity && !matchingAssembly.Identity.Equals(assembly.Identity))
                            {
                                _cSharpCompilation = _cSharpCompilation.RemoveReferences(new[] { reference });
                                _loadedAssemblies.Remove(name);
                                continue;
                            }

                            matchingAssemblies.Add(matchingAssembly);
                            found = true;
                            break;
                        }
                    }
                }

                if (warnOnMissingAssemblies && !found)
                {
                    string assemblyInfo = validateMatchingIdentity ? assembly.Identity.GetDisplayName() : assembly.Name;
                    _warnings.Add(string.Format(Resources.MatchingAssemblyNotFound, assemblyInfo));
                }
            }

            return matchingAssemblies;
        }

        private IEnumerable<MetadataReference> LoadFromPaths(IEnumerable<string> paths)
        {
            List<MetadataReference> result = new();
            foreach (string path in paths)
            {
                string resolvedPath = Environment.ExpandEnvironmentVariables(path);
                string directory = null;
                if (Directory.Exists(resolvedPath))
                {
                    directory = resolvedPath;
                    result.AddRange(LoadAssembliesFromDirectory(resolvedPath));
                }
                else if (File.Exists(resolvedPath))
                {
                    directory = Path.GetDirectoryName(resolvedPath);
                    result.Add(CreateOrGetMetadataReferenceFromPath(resolvedPath));
                }
                else
                {
                    throw new FileNotFoundException(string.Format(Resources.ProvidedPathToLoadBinariesFromNotFound, resolvedPath));
                }

                if (_resolveReferences && !string.IsNullOrEmpty(directory))
                    _referenceSearchPaths.Add(directory);
            }

            return result;
        }

        private IEnumerable<MetadataReference> LoadAssembliesFromDirectory(string directory)
        {
            foreach (string assembly in Directory.EnumerateFiles(directory, "*.dll"))
            {
                yield return CreateOrGetMetadataReferenceFromPath(assembly);
            }
        }

        private MetadataReference CreateOrGetMetadataReferenceFromPath(string path)
        {
            // Roslyn doesn't support having two assemblies as references with the same identity and then getting the symbol for it.
            string name = Path.GetFileName(path);
            if (!_loadedAssemblies.TryGetValue(name, out MetadataReference metadataReference))
            {
                using FileStream stream = File.OpenRead(path);
                metadataReference = CreateAndAddReferenceToCompilation(name, stream);
            }

            return metadataReference;
        }

        private MetadataReference CreateAndAddReferenceToCompilation(string name, Stream fileStream)
        {
            // If we need to resolve references we can't reuse the same stream after creating the metadata
            // reference from it as Roslyn closes it. So instead we use PEReader and get the bytes
            // and create the metadata reference from that.
            using PEReader reader = new(fileStream);
            
            if (!reader.HasMetadata)
            {
                throw new ArgumentException(string.Format(Resources.ProvidedStreamDoesNotHaveMetadata, name));
            }

            PEMemoryBlock image = reader.GetEntireImage();
            MetadataReference metadataReference = MetadataReference.CreateFromImage(image.GetContent());
            _loadedAssemblies.Add(name, metadataReference);
            _cSharpCompilation = _cSharpCompilation.AddReferences(new MetadataReference[] { metadataReference });

            if (_resolveReferences)
            {
                ResolveReferences(reader);
            }


            return metadataReference;
        }

        private void ResolveReferences(PEReader peReader)
        {
            MetadataReader reader = peReader.GetMetadataReader();
            foreach (AssemblyReferenceHandle handle in reader.AssemblyReferences)
            {
                AssemblyReference reference = reader.GetAssemblyReference(handle);
                string name = $"{reader.GetString(reference.Name)}.dll";
                bool found = _loadedAssemblies.TryGetValue(name, out MetadataReference _);
                if (!found)
                {
                    foreach (string directory in _referenceSearchPaths)
                    {
                        string potentialPath = Path.Combine(directory, name);
                        if (File.Exists(potentialPath))
                        {
                            // TODO: add version check and add a warning if it doesn't match?
                            using FileStream resolvedStream = File.OpenRead(potentialPath);
                            CreateAndAddReferenceToCompilation(name, resolvedStream);
                            found = true;
                            break;
                        }
                    }
                }

                if (!found)
                {
                    _warnings.Add(string.Format(Resources.CouldNotResolveReference, name));
                }
            }
        }
    }
}
