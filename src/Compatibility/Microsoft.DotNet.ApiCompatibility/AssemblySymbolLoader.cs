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
    public class AssemblySymbolLoader
    {
        private readonly HashSet<string> _referenceSearchPaths = new();
        private readonly List<string> _warnings = new();
        private readonly Dictionary<string, MetadataReference> _loadedAssemblies;
        private readonly bool _resolveReferences;
        private CSharpCompilation _cSharpCompilation;

        public AssemblySymbolLoader(string assemblyName = "", bool resolveAssemblyReferences = false)
        {
            if (string.IsNullOrEmpty(assemblyName))
            {
                assemblyName = $"AssemblyLoader_{DateTime.Now:MM_dd_yy_HH_mm_ss_FFF}";
            }

            _loadedAssemblies = new Dictionary<string, MetadataReference>();
            var compilationOptions = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable);
            _cSharpCompilation = CSharpCompilation.Create(assemblyName, options: compilationOptions);
            _resolveReferences = resolveAssemblyReferences;
        }

        public void AddReferenceSearchDirectories(string paths) => AddReferenceSearchDirectories(SplitPaths(paths));

        public void AddReferenceSearchDirectories(IEnumerable<string> paths)
        {
            if (paths == null)
            {
                throw new ArgumentNullException(nameof(paths));
            }

            foreach (string path in paths)
                _referenceSearchPaths.Add(path);
        }

        public bool HasRoslynDiagnostics(out IEnumerable<Diagnostic> diagnostics)
        {
            diagnostics = _cSharpCompilation.GetDiagnostics();
            return diagnostics.Any();
        }

        public bool HasLoadWarnings(out IEnumerable<string> warnings)
        {
            warnings = _warnings;
            return _warnings.Count > 0;
        }

        private static string[] SplitPaths(string paths) =>
            paths == null ? null : paths.Split(new char[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);

        public IEnumerable<IAssemblySymbol> LoadAssemblies(string paths) => LoadAssemblies(SplitPaths(paths));

        public IEnumerable<IAssemblySymbol> LoadAssemblies(IEnumerable<string> paths)
        {
            if (paths == null)
            {
                throw new ArgumentNullException(nameof(paths));
            }

            IEnumerable<MetadataReference> assembliesToReturn = LoadFromPaths(paths);

            List<IAssemblySymbol> result = new List<IAssemblySymbol>();
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

        public IAssemblySymbol LoadAssembly(string path)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            MetadataReference metadataReference = CreateOrGetMetadataReferenceFromPath(path);
            return (IAssemblySymbol)_cSharpCompilation.GetAssemblyOrModuleSymbol(metadataReference);
        }

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

            if (!_loadedAssemblies.TryGetValue(name, out MetadataReference metadataReference))
            {
                metadataReference = CreateAndAddReferenceToCompilation(name, stream);
            }

            return (IAssemblySymbol)_cSharpCompilation.GetAssemblyOrModuleSymbol(metadataReference);
        }
        
        public IAssemblySymbol LoadAssemblyFromSourceFiles(IEnumerable<string> filePaths, IEnumerable<string> referencePaths)
        {
            if (filePaths == null || filePaths.Count() == 0)
            {
                throw new ArgumentNullException(nameof(filePaths), $"Should not be null and contain at least one element");
            }

            List<SyntaxTree> syntaxTrees = new();
            foreach (string filePath in filePaths)
            {
                if (!File.Exists(filePath))
                {
                    throw new FileNotFoundException($"File '{filePath}' does not exist.");
                }

                syntaxTrees.Add(CSharpSyntaxTree.ParseText(File.ReadAllText(filePath)));
            }

            _cSharpCompilation = _cSharpCompilation.AddSyntaxTrees(syntaxTrees);

            LoadFromPaths(referencePaths);
            return _cSharpCompilation.Assembly;
        }

        public IEnumerable<IAssemblySymbol> LoadMatchingAssemblies(IEnumerable<IAssemblySymbol> fromAssemblies, IEnumerable<string> searchPaths, bool validateMatchingIdentity = true, bool errorOnMissingAssemblies = true)
        {
            if (fromAssemblies == null)
            {
                throw new ArgumentNullException(nameof(fromAssemblies));
            }

            if (searchPaths == null)
            {
                throw new ArgumentNullException(nameof(searchPaths));
            }

            List<IAssemblySymbol> matchingAssemblies = new List<IAssemblySymbol>();
            foreach (IAssemblySymbol assembly in fromAssemblies)
            {
                bool found = false;
                string name = $"{assembly.Name}.dll";
                foreach (string directory in searchPaths)
                {
                    if (!Directory.Exists(directory))
                    {
                        throw new FileNotFoundException($"Matching assembly search directory '{directory}' does not exist", nameof(searchPaths));
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

                if (errorOnMissingAssemblies && !found)
                {
                    string assemblyInfo = validateMatchingIdentity ? assembly.Identity.GetDisplayName() : assembly.Name;
                    _warnings.Add($"Could not find matching assembly: '{assemblyInfo}' in any of the search directories.");
                }
            }

            return matchingAssemblies;
        }

        private IEnumerable<MetadataReference> LoadFromPaths(IEnumerable<string> paths)
        {
            List<MetadataReference> result = new List<MetadataReference>();
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
                    _warnings.Add($"Could not find the provided path '{resolvedPath}' to load binaries from.");
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
                    _warnings.Add($"Could not resolve reference '{name}' in any of the provided search directories.");
                }
            }
        }
    }
}
