// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

namespace Microsoft.DotNet.Internal.ProjectModel.Compilation
{
    internal class LibraryExportBuilder
    {
        private IList<LibraryAssetGroup> _runtimeAssemblyGroups;

        private IList<LibraryAsset> _runtimeAssets;

        private IList<LibraryAsset> _compilationAssemblies;

        private IList<LibraryAsset> _sourceReferences;

        private IList<LibraryAssetGroup> _nativeLibraryGroups;

        private IList<LibraryAsset> _embeddedResources;

        private IList<AnalyzerReference> _analyzerReferences;

        private IList<LibraryResourceAssembly> _resourceAssemblies;

        public LibraryDescription Library { get; set; }

        public IEnumerable<LibraryAssetGroup> RuntimeAssemblyGroups => _runtimeAssemblyGroups;

        public IEnumerable<LibraryAsset> RuntimeAssets => _runtimeAssets;

        public IEnumerable<LibraryAsset> CompilationAssemblies => _compilationAssemblies;

        public IEnumerable<LibraryAsset> SourceReferences => _sourceReferences;

        public IEnumerable<LibraryAssetGroup> NativeLibraryGroups => _nativeLibraryGroups;

        public IEnumerable<LibraryAsset> EmbeddedResources => _embeddedResources;

        public IEnumerable<AnalyzerReference> AnalyzerReferences => _analyzerReferences;

        public IEnumerable<LibraryResourceAssembly> ResourceAssemblies => _resourceAssemblies;

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
                RuntimeAssemblyGroups ?? EmptyArray<LibraryAssetGroup>.Value,
                RuntimeAssets ?? EmptyArray<LibraryAsset>.Value,
                NativeLibraryGroups ?? EmptyArray<LibraryAssetGroup>.Value,
                EmbeddedResources ?? EmptyArray<LibraryAsset>.Value,
                AnalyzerReferences ?? EmptyArray<AnalyzerReference>.Value,
                ResourceAssemblies ?? EmptyArray<LibraryResourceAssembly>.Value);
        }

        public LibraryExportBuilder WithLibrary(LibraryDescription libraryDescription)
        {
            Library = libraryDescription;
            return this;
        }

        public LibraryExportBuilder WithRuntimeAssemblyGroups(IEnumerable<LibraryAssetGroup> assets)
        {
            Replace(ref _runtimeAssemblyGroups, assets);
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

        public LibraryExportBuilder WithNativeLibraryGroups(IEnumerable<LibraryAssetGroup> assets)
        {
            Replace(ref _nativeLibraryGroups, assets);
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

        public LibraryExportBuilder WithResourceAssemblies(IEnumerable<LibraryResourceAssembly> assemblies)
        {
            Replace(ref _resourceAssemblies, assemblies);
            return this;
        }

        public LibraryExportBuilder AddRuntimeAssemblyGroup(LibraryAssetGroup asset)
        {
            Add(ref _runtimeAssemblyGroups, asset);
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

        public LibraryExportBuilder AddNativeLibraryGroup(LibraryAssetGroup asset)
        {
            Add(ref _nativeLibraryGroups, asset);
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

        public LibraryExportBuilder AddResourceAssembly(LibraryResourceAssembly assembly)
        {
            Add(ref _resourceAssemblies, assembly);
            return this;
        }

        private void Replace<T>(ref IList<T> list, IEnumerable<T> enumerable)
        {
            if (enumerable == null)
            {
                list = null;
            }
            else
            {
                list = new List<T>(enumerable);
            }
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