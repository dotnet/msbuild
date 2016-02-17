// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

namespace Microsoft.DotNet.ProjectModel.Compilation
{
    public class LibraryExportBuilder
    {
        private IList<LibraryAsset> _runtimeAssemblies;

        private IList<LibraryAsset> _runtimeAssets;

        private IList<LibraryAsset> _compilationAssemblies;

        private IList<LibraryAsset> _compilationAssets;

        private IList<LibraryAsset> _debugAssets;

        private IList<LibraryAsset> _sourceReferences;

        private IList<LibraryAsset> _nativeLibraries;

        private IList<LibraryAsset> _embeddedResources;

        private IList<AnalyzerReference> _analyzerReferences;

        public LibraryDescription Library { get; set; }

        public IEnumerable<LibraryAsset> RuntimeAssemblies => _runtimeAssemblies;

        public IEnumerable<LibraryAsset> RuntimeAssets => _runtimeAssets;

        public IEnumerable<LibraryAsset> CompilationAssemblies => _compilationAssemblies;

        public IEnumerable<LibraryAsset> CompilationAssets => _compilationAssets;

        public IEnumerable<LibraryAsset> SourceReferences => _sourceReferences;

        public IEnumerable<LibraryAsset> NativeLibraries => _nativeLibraries;

        public IEnumerable<LibraryAsset> EmbeddedResources => _embeddedResources;

        public IEnumerable<AnalyzerReference> AnalyzerReferences => _analyzerReferences;

        public static LibraryExportBuilder Create(LibraryDescription library = null)
        {
            return new LibraryExportBuilder().WithLibrary(library);
        }

        public LibraryExport Build()
        {
            if (Library == null)
            {
                throw new InvalidOperationException("Cannot build LibraryExport withoud Library set");
            }
            return new LibraryExport(
                Library,
                CompilationAssemblies ?? EmptyArray<LibraryAsset>.Value,
                SourceReferences ?? EmptyArray<LibraryAsset>.Value,
                RuntimeAssemblies ?? EmptyArray<LibraryAsset>.Value,
                RuntimeAssets ?? EmptyArray<LibraryAsset>.Value,
                NativeLibraries ?? EmptyArray<LibraryAsset>.Value,
                EmbeddedResources ?? EmptyArray<LibraryAsset>.Value,
                AnalyzerReferences ?? EmptyArray<AnalyzerReference>.Value);
        }

        public LibraryExportBuilder WithLibrary(LibraryDescription libraryDescription)
        {
            Library = libraryDescription;
            return this;
        }

        public LibraryExportBuilder WithRuntimeAssemblies(IEnumerable<LibraryAsset> assets)
        {
            Replace(ref _runtimeAssemblies, assets);
            return this;
        }

        public LibraryExportBuilder WithRuntimeAssets(IEnumerable<LibraryAsset> assets)
        {
            Replace(ref _runtimeAssets, assets);
            return this;
        }

        public LibraryExportBuilder WithCompilationAssemblies(IEnumerable<LibraryAsset> assets)
        {
            Replace(ref _compilationAssemblies, assets);
            return this;
        }
        
        public LibraryExportBuilder WithSourceReferences(IEnumerable<LibraryAsset> assets)
        {
            Replace(ref _sourceReferences, assets);
            return this;
        }

        public LibraryExportBuilder WithNativeLibraries(IEnumerable<LibraryAsset> assets)
        {
            Replace(ref _nativeLibraries, assets);
            return this;
        }

        public LibraryExportBuilder WithEmbedddedResources(IEnumerable<LibraryAsset> assets)
        {
            Replace(ref _embeddedResources, assets);
            return this;
        }

        public LibraryExportBuilder WithAnalyzerReference(IEnumerable<AnalyzerReference> assets)
        {
            Replace(ref _analyzerReferences, assets);
            return this;
        }

        public LibraryExportBuilder AddRuntimeAssembly(LibraryAsset asset)
        {
            Add(ref _runtimeAssemblies, asset);
            return this;
        }

        public LibraryExportBuilder AddRuntimeAsset(LibraryAsset asset)
        {
            Add(ref _runtimeAssets, asset);
            return this;
        }

        public LibraryExportBuilder AddCompilationAssembly(LibraryAsset asset)
        {
            Add(ref _compilationAssemblies, asset);
            return this;
        }

        public LibraryExportBuilder AddSourceReference(LibraryAsset asset)
        {
            Add(ref _sourceReferences, asset);
            return this;
        }

        public LibraryExportBuilder AddNativeLibrary(LibraryAsset asset)
        {
            Add(ref _compilationAssets, asset);
            return this;
        }

        public LibraryExportBuilder AddEmbedddedResource(LibraryAsset asset)
        {
            Add(ref _embeddedResources, asset);
            return this;
        }

        public LibraryExportBuilder AddAnalyzerReference(AnalyzerReference asset)
        {
            Add(ref _analyzerReferences, asset);
            return this;
        }

        private void Replace<T>(ref IList<T> list, IEnumerable<T> enumerable)
        {
            list = new List<T>(enumerable);
        }

        private void Add<T>(ref IList<T> list, T item)
        {
            if (list == null)
            {
                list = new List<T>();
            }
            list.Add(item);
        }
    }
}