// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.ProjectConstruction;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.ApiCompatibility.Tests
{
    public class AssemblySymbolLoaderTests : SdkTest
    {
        public AssemblySymbolLoaderTests(ITestOutputHelper log) : base(log) { }

        private const string SimpleAssemblySourceContents = @"
namespace MyNamespace
{
    public class MyClass
    {
    }
}
";

        // Since we use typeof(string).Assembly.Location to resolve references
        // We need to target a framework compatible with what the test is being
        // built for so that we resolve the references correctly.
#if NETCOREAPP
        private const string TargetFrameworks = ToolsetInfo.CurrentTargetFramework;
#else
        private const string TargetFrameworks = "net471";
#endif

        private class TestAssetInfo
        {
            public TestAsset TestAsset { get; set; }
            public string OutputDirectory { get; set; }
        }

        // We use the same asset in multiple tests.
        // Creating a TestAsset and building it for each test
        // creates a lot of overhead, using the cache
        // speeds up test execution ~3x in this test assembly.
        // Tests within the same class run serially in xunit, so
        // it is fine to reuse the same asset.
        private class TestAssetCache
        {
            public static TestAssetCache Instance = new TestAssetCache();

            private ConcurrentDictionary<string, TestAssetInfo> Dictionary = new();

            private TestAssetInfo _asset = null;

            public TestAssetInfo GetSimpleAsset(TestAssetsManager manager)
                => _asset ?? InitAsset(manager);

            private TestAssetInfo InitAsset(TestAssetsManager manager)
            {
                Interlocked.CompareExchange(ref _asset, GetAsset(manager), null);
                return _asset;
            }

            private TestAssetInfo GetAsset(TestAssetsManager manager)
            {
                TestProject project = new("SimpleAsset")
                {
                    TargetFrameworks = TargetFrameworks,
                    IsExe = false
                };

                project.SourceFiles.Add("MyClass.cs", SimpleAssemblySourceContents);

                TestAsset testAsset = manager.CreateTestProject(project);
                BuildTestAsset(testAsset, out string outDir)
                    .Should()
                    .Pass();

                return new TestAssetInfo()
                {
                    TestAsset = testAsset,
                    OutputDirectory = outDir,
                };
            }
        }

        private TestAssetInfo GetSimpleTestAsset() => TestAssetCache.Instance.GetSimpleAsset(_testAssetsManager);

        [Fact]
        public void LoadAssembly_Throws()
        {
            AssemblySymbolLoader loader = new();
            Assert.Throws<FileNotFoundException>(() => loader.LoadAssembly(Guid.NewGuid().ToString("N").Substring(0, 8)));
            Assert.Throws<ArgumentNullException>("path", () => loader.LoadAssembly(null));
            Assert.Throws<ArgumentNullException>("stream", () => loader.LoadAssembly("Assembly", null));
            Assert.Throws<ArgumentNullException>("name", () => loader.LoadAssembly(null, new MemoryStream()));
        }

        [Fact]
        public void LoadAssemblies_Throws()
        {
            AssemblySymbolLoader loader = new();
            Assert.Throws<ArgumentNullException>("paths", () => loader.LoadAssemblies((string)null));
            Assert.Throws<ArgumentNullException>("paths", () => loader.LoadAssemblies((IEnumerable<string>)null));
        }

        [Fact]
        public void LoadAssemblyFromSourceFiles_Throws()
        {
            AssemblySymbolLoader loader = new();
            IEnumerable<string> paths = new[] { Guid.NewGuid().ToString("N") };
            Assert.Throws<FileNotFoundException>(() => loader.LoadAssemblyFromSourceFiles(paths, "assembly1", Array.Empty<string>()));
            Assert.Throws<ArgumentNullException>("filePaths", () => loader.LoadAssemblyFromSourceFiles(null, "assembly1", Array.Empty<string>()));
            Assert.Throws<ArgumentNullException>("filePaths", () => loader.LoadAssemblyFromSourceFiles(Array.Empty<string>(), "assembly1", Array.Empty<string>()));
            Assert.Throws<ArgumentNullException>("assemblyName", () => loader.LoadAssemblyFromSourceFiles(paths, null, Array.Empty<string>()));
        }

        [Fact]
        public void LoadMatchingAssemblies_Throws()
        {
            AssemblySymbolLoader loader = new();
            IEnumerable<string> paths = new[] { Guid.NewGuid().ToString("N") };
            IAssemblySymbol assembly = SymbolFactory.GetAssemblyFromSyntax("namespace MyNamespace { class Foo { } }");

            Assert.Throws<FileNotFoundException>(() => loader.LoadMatchingAssemblies(new[] { assembly }, paths));
            Assert.Throws<ArgumentNullException>("fromAssemblies", () => loader.LoadMatchingAssemblies(null, paths));
            Assert.Throws<ArgumentNullException>("searchPaths", () => loader.LoadMatchingAssemblies(Array.Empty<IAssemblySymbol>(), null));
        }

        [Fact]
        public void LoadMatchingAssembliesWarns()
        {
            IAssemblySymbol assembly = SymbolFactory.GetAssemblyFromSyntax("namespace MyNamespace { class Foo { } }");
            IEnumerable<string> paths = new[] { AppContext.BaseDirectory };

            AssemblySymbolLoader loader = new();
            IEnumerable<IAssemblySymbol> symbols = loader.LoadMatchingAssemblies(new[] { assembly }, paths);
            Assert.Empty(symbols);
            Assert.True(loader.HasLoadWarnings(out IEnumerable<AssemblyLoadWarning> warnings));

            IEnumerable<AssemblyLoadWarning> expected = new[]
            {
                new AssemblyLoadWarning(DiagnosticIds.AssemblyNotFound, assembly.Identity.GetDisplayName(), $"Could not find matching assembly: '{assembly.Identity.GetDisplayName()}' in any of the search directories.")
            };

            Assert.Equal(expected, warnings);
        }

        [Fact]
        public void LoadMatchingAssembliesSameIdentitySucceeds()
        {
            string assemblyName = nameof(LoadMatchingAssembliesSameIdentitySucceeds);
            IAssemblySymbol fromAssembly = SymbolFactory.GetAssemblyFromSyntax(SimpleAssemblySourceContents, assemblyName: assemblyName);

            TestProject testProject = new(assemblyName)
            {
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = false,
            };

            testProject.SourceFiles.Add("MyClass.cs", SimpleAssemblySourceContents);
            testProject.AdditionalProperties.Add("AssemblyVersion", "0.0.0.0");
            TestAsset testAsset = _testAssetsManager.CreateTestProject(testProject);

            BuildTestAsset(testAsset, out string outputDirectory)
                .Should()
                .Pass();

            AssemblySymbolLoader loader = new();
            IEnumerable<IAssemblySymbol> matchingAssemblies = loader.LoadMatchingAssemblies(new[] { fromAssembly }, new[] { outputDirectory });

            Assert.Single(matchingAssemblies);
            Assert.False(loader.HasLoadWarnings(out var _));
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void LoadMatchingAssemblies_DifferentIdentity(bool validateIdentities)
        {
            var assetInfo = GetSimpleTestAsset();
            IAssemblySymbol fromAssembly = SymbolFactory.GetAssemblyFromSyntax(SimpleAssemblySourceContents, assemblyName: assetInfo.TestAsset.TestProject.Name);

            AssemblySymbolLoader loader = new();
            IEnumerable<IAssemblySymbol> matchingAssemblies = loader.LoadMatchingAssemblies(new[] { fromAssembly }, new[] { assetInfo.OutputDirectory }, validateMatchingIdentity: validateIdentities);

            if (validateIdentities)
            {
                Assert.Empty(matchingAssemblies);
                Assert.True(loader.HasLoadWarnings(out IEnumerable<AssemblyLoadWarning> warnings));

                IEnumerable<AssemblyLoadWarning> expected = new[]
                {
                    new AssemblyLoadWarning(DiagnosticIds.AssemblyNotFound, fromAssembly.Identity.GetDisplayName(), $"Could not find matching assembly: '{fromAssembly.Identity.GetDisplayName()}' in any of the search directories.")
                };

                Assert.Equal(expected, warnings);
            }
            else
            {
                Assert.Single(matchingAssemblies);
                Assert.False(loader.HasLoadWarnings(out var _));
                Assert.NotEqual(fromAssembly.Identity, matchingAssemblies.FirstOrDefault().Identity);
            }
        }

        [Fact]
        public void LoadsSimpleAssemblyFromDirectory()
        {
            var assetInfo = GetSimpleTestAsset();
            AssemblySymbolLoader loader = new();
            IEnumerable<IAssemblySymbol> symbols = loader.LoadAssemblies(assetInfo.OutputDirectory);
            Assert.Single(symbols);

            IEnumerable<ITypeSymbol> types = symbols.FirstOrDefault()
                .GlobalNamespace
                .GetNamespaceMembers()
                .FirstOrDefault()
                .GetTypeMembers();

            Assert.Single(types);
            Assert.Equal("MyNamespace.MyClass", types.FirstOrDefault().ToDisplayString());
        }

        [Fact]
        public void LoadSimpleAssemblyFullPath()
        {
            var assetInfo = GetSimpleTestAsset();
            AssemblySymbolLoader loader = new();
            IAssemblySymbol symbol = loader.LoadAssembly(Path.Combine(assetInfo.OutputDirectory, assetInfo.TestAsset.TestProject.Name + ".dll"));

            IEnumerable<ITypeSymbol> types = symbol.GlobalNamespace
                .GetNamespaceMembers()
                .FirstOrDefault()
                .GetTypeMembers();

            Assert.Single(types);
            Assert.Equal("MyNamespace.MyClass", types.FirstOrDefault().ToDisplayString());
        }

        [Fact]
        public void LoadsMultipleAssembliesFromDirectory()
        {
            TestProject first = new("LoadsMultipleAssembliesFromDirectory_First")
            {
                TargetFrameworks = TargetFrameworks,
                IsExe = false
            };

            TestProject second = new("LoadsMultipleAssembliesFromDirectory_Second")
            {
                TargetFrameworks = TargetFrameworks,
                IsExe = false
            };

            first.ReferencedProjects.Add(second);
            TestAsset testAsset = _testAssetsManager.CreateTestProject(first);

            BuildTestAsset(testAsset, out string outputDirectory)
                .Should()
                .Pass();

            AssemblySymbolLoader loader = new();
            IEnumerable<IAssemblySymbol> symbols = loader.LoadAssemblies(outputDirectory);

            Assert.Equal(2, symbols.Count());

            IEnumerable<string> expected = new[] { "LoadsMultipleAssembliesFromDirectory_First", "LoadsMultipleAssembliesFromDirectory_Second" };
            IEnumerable<string> actual = symbols.Select(a => a.Name).OrderBy(a => a, StringComparer.Ordinal);

            Assert.Equal(expected, actual, StringComparer.Ordinal);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void LoadAssemblyResolveReferences_WarnsWhenEnabled(bool resolveReferences)
        {
            var assetInfo = GetSimpleTestAsset();
            AssemblySymbolLoader loader = new(resolveAssemblyReferences: resolveReferences);
            loader.LoadAssembly(Path.Combine(assetInfo.OutputDirectory, assetInfo.TestAsset.TestProject.Name + ".dll"));

            if (resolveReferences)
            {
                Assert.True(loader.HasLoadWarnings(out IEnumerable<AssemblyLoadWarning> warnings));

                string expectedReference = "System.Runtime.dll";

                if (TargetFrameworks.StartsWith("net4", StringComparison.OrdinalIgnoreCase))
                {
                    expectedReference = "mscorlib.dll";
                }

                IEnumerable<AssemblyLoadWarning> expected = new List<AssemblyLoadWarning>
                {
                    new AssemblyLoadWarning(DiagnosticIds.AssemblyReferenceNotFound, expectedReference, $"Could not resolve reference '{expectedReference}' in any of the provided search directories.")
                };

                Assert.Equal(expected, warnings);
            }
            else
            {
                Assert.False(loader.HasLoadWarnings(out IEnumerable<AssemblyLoadWarning> warnings));
                Assert.Empty(warnings);
            }
        }

        [Fact]
        public void LoadAssembliesShouldResolveReferencesNoWarnings()
        {
            var assetInfo = GetSimpleTestAsset();
            AssemblySymbolLoader loader = new(resolveAssemblyReferences: true);
            // AddReferenceSearchDirectories should be able to handle directories as well as full path to assemblies.
            loader.AddReferenceSearchDirectories(Path.GetDirectoryName(typeof(string).Assembly.Location));
            loader.AddReferenceSearchDirectories(Path.GetFullPath(typeof(string).Assembly.Location));
            loader.LoadAssembly(Path.Combine(assetInfo.OutputDirectory, assetInfo.TestAsset.TestProject.Name + ".dll"));

            Assert.False(loader.HasLoadWarnings(out IEnumerable<AssemblyLoadWarning> warnings));
            Assert.Empty(warnings);

            // Ensure we loaded more than one assembly since resolveReferences was set to true.
            Dictionary<string, MetadataReference> loadedAssemblies = (Dictionary<string, MetadataReference>)typeof(AssemblySymbolLoader)?.GetField("_loadedAssemblies", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(loader);
            Assert.True(loadedAssemblies != null && loadedAssemblies.Count > 1);
        }

        [Fact]
        public void LoadAssemblyFromStreamNoWarns()
        {
            var assetInfo = GetSimpleTestAsset();
            TestProject testProject = assetInfo.TestAsset.TestProject;
            AssemblySymbolLoader loader = new();
            using FileStream stream = File.OpenRead(Path.Combine(assetInfo.OutputDirectory, testProject.Name + ".dll"));
            IAssemblySymbol symbol = loader.LoadAssembly(testProject.Name, stream);

            Assert.False(loader.HasLoadWarnings(out var _));
            Assert.Equal(testProject.Name, symbol.Name, StringComparer.Ordinal);

            IEnumerable<ITypeSymbol> types = symbol.GlobalNamespace
                .GetNamespaceMembers()
                .FirstOrDefault()
                .GetTypeMembers();

            Assert.Single(types);
            Assert.Equal("MyNamespace.MyClass", types.FirstOrDefault().ToDisplayString());
        }

        private static CommandResult BuildTestAsset(TestAsset testAsset, out string outputDirectory)
        {
            BuildCommand buildCommand = new(testAsset);
            outputDirectory = buildCommand.GetOutputDirectory(testAsset.TestProject.TargetFrameworks).FullName;
            return buildCommand.Execute();
        }
    }
}
