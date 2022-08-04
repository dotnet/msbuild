// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Microsoft.DotNet.ApiCompatibility
{
    /// <summary>
    /// Loads <see cref="IAssemblySymbol"/> objects from source files, binaries or directories containing binaries.
    /// </summary>
    public class AssemblySymbolLoader : IAssemblySymbolLoader
    {
        /// <summary>
        /// Dictionary that holds the paths to help loading dependencies. Keys will be assembly name and 
        /// value are the containing folder.
        /// </summary>
        private readonly Dictionary<string, string> _referencePaths = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<AssemblyLoadWarning> _warnings = new();
        private readonly Dictionary<string, MetadataReference> _loadedAssemblies;
        private readonly bool _resolveReferences;
        private CSharpCompilation _cSharpCompilation;

        /// <inheritdoc />
        public AssemblySymbolLoader(bool resolveAssemblyReferences = false)
        {
            _loadedAssemblies = new Dictionary<string, MetadataReference>();
            var compilationOptions = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable);
            _cSharpCompilation = CSharpCompilation.Create($"AssemblyLoader_{DateTime.Now:MM_dd_yy_HH_mm_ss_FFF}", options: compilationOptions);
            _resolveReferences = resolveAssemblyReferences;
        }

        /// <inheritdoc />
        public void AddReferenceSearchDirectories(string paths) => AddReferenceSearchDirectories(SplitPaths(paths));

        /// <inheritdoc />
        public void AddReferenceSearchDirectories(IEnumerable<string> paths)
        {
            if (paths == null)
            {
                throw new ArgumentNullException(nameof(paths));
            }

            foreach (string path in paths)
            {
                FileAttributes attr = File.GetAttributes(path);

                if (attr.HasFlag(FileAttributes.Directory))
                {
                    if (!_referencePaths.ContainsKey(path))
                        _referencePaths.Add(path, path);
                }
                else
                {
                    string assemblyName = Path.GetFileName(path);
                    if (!_referencePaths.ContainsKey(assemblyName))
                    {
                        string? directoryName = Path.GetDirectoryName(path);
                        if (directoryName != null)
                        {
                            _referencePaths.Add(assemblyName, directoryName);
                        }
                    }
                }
            }
        }

        /// <inheritdoc />
        public bool HasRoslynDiagnostics(out IEnumerable<Diagnostic> diagnostics)
        {
            diagnostics = _cSharpCompilation.GetDiagnostics();
            return diagnostics.Any();
        }

        private static string[] SplitPaths(string paths) =>
            paths.Split(new char[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);

        /// <inheritdoc />
        public bool HasLoadWarnings(out IEnumerable<AssemblyLoadWarning> warnings)
        {
            warnings = _warnings;
            return _warnings.Count > 0;
        }

        /// <inheritdoc />
        public IEnumerable<IAssemblySymbol> LoadAssemblies(string paths) => LoadAssemblies(SplitPaths(paths));

        /// <inheritdoc />
        public IEnumerable<IAssemblySymbol> LoadAssemblies(IEnumerable<string> paths)
        {
            IEnumerable<MetadataReference> assembliesToReturn = LoadFromPaths(paths);

            List<IAssemblySymbol> result = new();
            foreach (MetadataReference metadataReference in assembliesToReturn)
            {
                ISymbol? symbol = _cSharpCompilation.GetAssemblyOrModuleSymbol(metadataReference);
                if (symbol is IAssemblySymbol assemblySymbol)
                {
                    result.Add(assemblySymbol);
                }
            }

            return result;
        }

        /// <inheritdoc />
        public IAssemblySymbol? LoadAssembly(string path)
        {
            MetadataReference metadataReference = CreateOrGetMetadataReferenceFromPath(path);
            return _cSharpCompilation.GetAssemblyOrModuleSymbol(metadataReference) as IAssemblySymbol;
        }

        /// <inheritdoc />
        public IAssemblySymbol? LoadAssembly(string name, Stream stream)
        {
            if (stream.Position >= stream.Length)
            {
                throw new ArgumentException(Resources.StreamPositionGreaterThanLength, nameof(stream));
            }

            if (!_loadedAssemblies.TryGetValue(name, out MetadataReference? metadataReference))
            {
                metadataReference = CreateAndAddReferenceToCompilation(name, stream);
            }

            return _cSharpCompilation.GetAssemblyOrModuleSymbol(metadataReference) as IAssemblySymbol;
        }

        /// <inheritdoc />
        public IAssemblySymbol LoadAssemblyFromSourceFiles(IEnumerable<string> filePaths, string? assemblyName, IEnumerable<string> referencePaths)
        {
            if (!filePaths.Any())
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

        /// <inheritdoc />
        public IEnumerable<IAssemblySymbol> LoadMatchingAssemblies(IEnumerable<IAssemblySymbol> fromAssemblies, IEnumerable<string> searchPaths, bool validateMatchingIdentity = true, bool warnOnMissingAssemblies = true)
        {
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
                        ISymbol? symbol = _cSharpCompilation.GetAssemblyOrModuleSymbol(reference);
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
                    _warnings.Add(new AssemblyLoadWarning(DiagnosticIds.AssemblyNotFound, assemblyInfo, string.Format(Resources.MatchingAssemblyNotFound, assemblyInfo)));
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
                string? directory = null;
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
                    _referencePaths.Add(Path.GetFileName(directory), directory);  // Not Sure about this one, we should do something else here.
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
            if (!_loadedAssemblies.TryGetValue(name, out MetadataReference? metadataReference))
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
                    // First we try to see if a reference path for this specific assembly was passed in directly, and if so
                    // we use that.
                    if (_referencePaths.TryGetValue(name, out string? fullReferencePath))
                    {
                        using FileStream resolvedStream = File.OpenRead(Path.Combine(fullReferencePath, name));
                        CreateAndAddReferenceToCompilation(name, resolvedStream);
                        found = true;
                    }
                    // If we can't find a specific reference path for the dependency, then we look in the folders where the
                    // rest of the reference paths are located to see if we can find the dependency there.
                    else
                    {
                        foreach (KeyValuePair<string, string> referencePath in _referencePaths)
                        {
                            string potentialPath = Path.Combine(referencePath.Value, name);
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
                        _warnings.Add(new AssemblyLoadWarning(DiagnosticIds.AssemblyReferenceNotFound, name, string.Format(Resources.CouldNotResolveReference, name)));
                    }
                }
            }
        }
    }
}
