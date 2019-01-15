using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Tasks;
using Microsoft.Build.Utilities;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Build.UnitTests.ResolveAssemblyReference_Tests
{
    /// <summary>
    /// Unit tests for the ResolveAssemblyReference task that involve, among other things, checking suggested redirects 
    /// </summary>
    public sealed class SuggestedRedirects : ResolveAssemblyReferenceTestFixture
    {
        public SuggestedRedirects(ITestOutputHelper output) : base(output)
        {
        }


        /// <summary>
        /// Consider this dependency chain:
        ///
        /// App
        ///   References - A
        ///        Depends on D version 1
        ///   References - B
        ///        Depends on D version 2
        ///
        /// And neither D1 nor D2 are CopyLocal = true. In this case, both dependencies
        /// are kept because this will work in a SxS manner.
        /// </summary>
        [Fact]
        public void ConflictBetweenNonCopyLocalDependencies()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            MockEngine e = new MockEngine(_output);
            t.BuildEngine = e;

            t.Assemblies = new ITaskItem[]
            {
                new TaskItem("A"),
                new TaskItem("B")
            };

            t.SearchPaths = new string[]
            {
                s_myLibrariesRootPath,
                s_myLibraries_V1Path,
                s_myLibraries_V2Path
            };

            Execute(t);

            Assert.Equal(3, t.ResolvedDependencyFiles.Length);
            Assert.True(ContainsItem(t.ResolvedDependencyFiles, s_myLibraries_V2_DDllPath));
            Assert.True(ContainsItem(t.ResolvedDependencyFiles, s_myLibraries_V1_DDllPath));
            Assert.True(ContainsItem(t.ResolvedDependencyFiles, s_myLibraries_V2_GDllPath));

            Assert.Single(t.SuggestedRedirects);
            Assert.True(ContainsItem(t.SuggestedRedirects, @"D, Culture=neutral, PublicKeyToken=aaaaaaaaaaaaaaaa")); // "Expected to find suggested redirect, but didn't"
            Assert.Equal(1, e.Warnings); // "Should only be one warning for suggested redirects."
        }

        /// <summary>
        /// Consider this dependency chain:
        ///
        /// App
        ///   References - A
        ///        Depends on D version 1
        ///   References - B
        ///        Depends on D version 2
        ///
        /// And both D1 and D2 are CopyLocal = true. This case is a warning because both
        /// assemblies can't be copied to the output directory.
        /// </summary>
        [Fact]
        public void ConflictBetweenCopyLocalDependencies()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            MockEngine engine = new MockEngine(_output);
            t.BuildEngine = engine;

            t.Assemblies = new ITaskItem[] {
                new TaskItem("A"), new TaskItem("B")
            };

            t.SearchPaths = new string[] {
                s_myLibrariesRootPath, s_myLibraries_V1Path, s_myLibraries_V2Path
            };

            t.TargetFrameworkDirectories = new string[] { s_myVersion20Path };

            bool result = Execute(t);

            Assert.Equal(1, engine.Warnings); // @"Expected a warning because this is an unresolvable conflict."
            Assert.Single(t.SuggestedRedirects);
            Assert.True(ContainsItem(t.SuggestedRedirects, @"D, Culture=neutral, PublicKeyToken=aaaaaaaaaaaaaaaa")); // "Expected to find suggested redirect, but didn't"
            Assert.Equal(1, engine.Warnings); // "Should only be one warning for suggested redirects."
            Assert.Contains(
                String.Format
                    (
                        AssemblyResources.GetString
                        (
                            "ResolveAssemblyReference.ConflictRedirectSuggestion"
                        ),
                        "D, Culture=neutral, PublicKeyToken=aaaaaaaaaaaaaaaa",
                        "1.0.0.0",
                        s_myLibraries_V1_DDllPath,
                        "2.0.0.0",
                        s_myLibraries_V2_DDllPath
                    ),
                engine.Log
            );
        }


        /// <summary>
        /// Consider this dependency chain:
        ///
        /// App
        ///   References - A
        ///        Depends on D version 1
        ///   References - B
        ///        Depends on D version 2
        ///
        /// And both D1 and D2 are CopyLocal = true. In this case, there is no warning because
        /// AutoUnify is set to true.
        /// </summary>
        [Fact]
        public void ConflictBetweenCopyLocalDependenciesWithAutoUnify()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            MockEngine engine = new MockEngine(_output);
            t.BuildEngine = engine;
            t.AutoUnify = true;

            t.Assemblies = new ITaskItem[] {
                new TaskItem("A"), new TaskItem("B")
            };

            t.SearchPaths = new string[] {
                s_myLibrariesRootPath, s_myLibraries_V1Path, s_myLibraries_V2Path
            };

            t.TargetFrameworkDirectories = new string[] { s_myVersion20Path };

            bool result = Execute(t);

            // RAR will now produce suggested redirects even if AutoUnify is on.
            Assert.Single(t.SuggestedRedirects);
            Assert.Equal(0, engine.Warnings); // "Should be no warning for suggested redirects."
        }

        /// <summary>
        /// Consider this dependency chain:
        ///
        /// App
        ///   References - A
        ///        Depends on D version 1
        ///   References - B
        ///        Depends on D version 2
        ///   References - D, version 1
        ///
        /// Both D1 and D2 are CopyLocal. This is a warning because D1 is a lower version
        /// than D2 so that can't unify. These means that eventually when they're copied
        /// to the output directory they'll conflict.
        /// </summary>
        [Fact]
        public void ConflictWithBackVersionPrimary()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            MockEngine e = new MockEngine(_output);
            t.BuildEngine = e;

            t.Assemblies = new ITaskItem[]
            {
                new TaskItem("B"),
                new TaskItem("A"),
                new TaskItem("D, Version=1.0.0.0, Culture=neutral, PublicKeyToken=aaaaaaaaaaaaaaaa")
            };

            t.SearchPaths = new string[]
            {
                s_myLibrariesRootPath, s_myLibraries_V2Path, s_myLibraries_V1Path
            };

            t.TargetFrameworkDirectories = new string[] { s_myVersion20Path };

            bool result = Execute(t);

            Assert.Equal(1, e.Warnings); // @"Expected one warning."
            e.AssertLogContainsMessageFromResource(AssemblyResources.GetString, "ResolveAssemblyReference.FoundConflicts", "D");

            Assert.Empty(t.SuggestedRedirects);
            Assert.Equal(3, t.ResolvedFiles.Length);
            Assert.True(ContainsItem(t.ResolvedFiles, s_myLibraries_V1_DDllPath)); // "Expected to find assembly, but didn't."
        }


        /// <summary>
        /// Same as ConflictWithBackVersionPrimary, except AutoUnify is true.
        /// Even when AutoUnify is set we should see a warning since the binder will not allow
        /// an older version to satisfy a reference to a newer version.
        /// </summary>
        [Fact]
        public void ConflictWithBackVersionPrimaryWithAutoUnify()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            MockEngine e = new MockEngine(_output);
            t.BuildEngine = e;

            t.Assemblies = new ITaskItem[]
            {
                new TaskItem("B"),
                new TaskItem("A"),
                new TaskItem("D, Version=1.0.0.0, Culture=neutral, PublicKeyToken=aaaaaaaaaaaaaaaa")
            };

            t.AutoUnify = true;

            t.SearchPaths = new string[]
            {
                s_myLibrariesRootPath, s_myLibraries_V2Path, s_myLibraries_V1Path
            };

            t.TargetFrameworkDirectories = new string[] { s_myVersion20Path };

            bool result = Execute(t);

            Assert.Equal(1, e.Warnings); // @"Expected one warning."
            e.AssertLogContainsMessageFromResource(AssemblyResources.GetString, "ResolveAssemblyReference.FoundConflicts", "D");

            Assert.Empty(t.SuggestedRedirects);
            Assert.Equal(3, t.ResolvedFiles.Length);
            Assert.True(ContainsItem(t.ResolvedFiles, s_myLibraries_V1_DDllPath)); // "Expected to find assembly, but didn't."
        }


        /// <summary>
        /// Consider this dependency chain:
        ///
        /// App
        ///   References - Microsoft.Office.Interop.Excel
        ///        Depends on Office, Version=12.0.0.0, Culture=neutral, PublicKeyToken=71e9bce111e9429c
        ///
        ///   References - MS.Internal.Test.Automation.Office.Excel
        ///        Depends on Office, Version=12.0.0.0, Culture=neutral, PublicKeyToken=94de0004b6e3fcc5
        ///
        /// Notice that the two primaries have dependencies that only differ by PKT. Suggested redirects should
        /// only happen if the two assemblies differ by nothing but version.
        /// </summary>
        [Fact]
        public void Regress313747_FalseSuggestedRedirectsWhenAssembliesDifferOnlyByPkt()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            MockEngine e = new MockEngine(_output);
            t.BuildEngine = e;

            t.Assemblies = new ITaskItem[]
            {
                new TaskItem("Microsoft.Office.Interop.Excel"),
                new TaskItem("MS.Internal.Test.Automation.Office.Excel")
            };

            t.SearchPaths = new string[]
            {
                @"c:\Regress313747",
            };

            Execute(t);

            Assert.Empty(t.SuggestedRedirects);
        }

        /// <summary>
        /// Consider this dependency chain:
        ///
        /// (1) Primary reference A v 2.0.0.0 is found.
        /// (2) Primary reference B is found.
        /// (3) Primary reference B depends on A v 1.0.0.0
        /// (4) Dependency A v 1.0.0.0 is not found.
        /// (5) App.Config does not contain a binding redirect from A v 1.0.0.0 -> 2.0.0.0
        ///
        /// We need to warn and suggest an app.config entry because the runtime environment will require a binding
        /// redirect to function. Without a binding redirect, loading B will cause A.V1 to try to load. It won't be
        /// there and there won't be a binding redirect to point it at 2.0.0.0.
        /// </summary>
        [Fact]
        [Trait("Category", "mono-osx-failing")]
        [Trait("Category", "netcore-osx-failing")]
        [Trait("Category", "netcore-linux-failing")]
        public void Regress442570_MissingBackVersionShouldWarn()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            MockEngine e = new MockEngine(_output);
            t.BuildEngine = e;

            t.Assemblies = new ITaskItem[]
            {
                new TaskItem("A"),
                new TaskItem("B")
            };

            t.SearchPaths = new string[]
            {
                @"c:\Regress442570",
            };

            Execute(t);

            // Expect a suggested redirect plus a warning
            Assert.Single(t.SuggestedRedirects);
            Assert.Equal(1, e.Warnings);
        }

        /// <summary>
        /// Consider this dependency chain:
        ///
        /// (1) Primary reference A v 2.0.0.0 is found and marked externally resolved.
        /// (2) Primary reference B is externally resolved and depends on A v 1.0.0.0
        ///
        /// When AutoGenerateBindingRedirects is used, we need to find the redirect
        /// in the externally resolved graph.
        /// </summary>
        [Fact]
        public void RedirectsAreSuggestedInExternallyResolvedGraph()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            MockEngine e = new MockEngine(_output);
            t.BuildEngine = e;

            // NB: These are what common targets would set when AutoGenerateBindingRedirects is enabled.
            t.AutoUnify = true;
            t.FindDependenciesOfExternallyResolvedReferences = true;

            t.Assemblies = new ITaskItem[]
            {
                new TaskItem("A", new Dictionary<string, string> { ["ExternallyResolved"] = "true" } ),
                new TaskItem("B", new Dictionary<string, string> { ["ExternallyResolved"] = "true" } ),
            };

            t.SearchPaths = new string[]
            {
                s_regress442570_RootPath
            };

            Execute(t);

            // Expect a suggested redirect with no warning.
            Assert.Single(t.SuggestedRedirects);
            Assert.Equal(0, e.Warnings);
        }

        /// <summary>
        /// Consider this dependency chain:
        ///
        /// App
        ///   References - A
        ///        Depends on D version 1 (but PKT=null)
        ///   References - B
        ///        Depends on D version 2 (but PKT=null)
        ///
        /// There should be no suggested redirect because only strongly named assemblies can have
        /// binding redirects.
        /// </summary>
        [Fact]
        [Trait("Category", "mono-osx-failing")]
        [Trait("Category", "netcore-osx-failing")]
        [Trait("Category", "netcore-linux-failing")]
        public void Regress387218_UnificationRequiresStrongName()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            MockEngine e = new MockEngine(_output);
            t.BuildEngine = e;

            t.Assemblies = new ITaskItem[]
            {
                new TaskItem("A"),
                new TaskItem("B")
            };

            t.SearchPaths = new string[]
            {
                @"c:\Regress387218",
                @"c:\Regress387218\v1",
                @"c:\Regress387218\v2"
            };

            Execute(t);

            Assert.Equal(2, t.ResolvedDependencyFiles.Length);
            Assert.True(ContainsItem(t.ResolvedDependencyFiles, @"c:\Regress387218\v2\D.dll")); // "Expected to find assembly, but didn't."
            Assert.True(ContainsItem(t.ResolvedDependencyFiles, @"c:\Regress387218\v1\D.dll")); // "Expected to find assembly, but didn't."
            Assert.Empty(t.SuggestedRedirects);
            Assert.Equal(0, e.Warnings); // "Should only be no warning about suggested redirects."
        }

        /// <summary>
        /// Consider this dependency chain:
        ///
        /// App
        ///   References - A
        ///        Depends on D version 1 (but Culture=fr)
        ///   References - B
        ///        Depends on D version 2 (but Culture=en)
        ///
        /// There should be no suggested redirect because assemblies with different cultures cannot unify.
        /// </summary>
        [Fact]
        [Trait("Category", "mono-osx-failing")]
        [Trait("Category", "netcore-osx-failing")]
        [Trait("Category", "netcore-linux-failing")]
        public void Regress390219_UnificationRequiresSameCulture()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            MockEngine e = new MockEngine(_output);
            t.BuildEngine = e;

            t.Assemblies = new ITaskItem[]
            {
                new TaskItem("A"),
                new TaskItem("B")
            };

            t.SearchPaths = new string[]
            {
                @"c:\Regress390219",
                @"c:\Regress390219\v1",
                @"c:\Regress390219\v2"
            };

            Execute(t);

            Assert.Equal(2, t.ResolvedDependencyFiles.Length);
            Assert.True(ContainsItem(t.ResolvedDependencyFiles, @"c:\Regress390219\v2\D.dll")); // "Expected to find assembly, but didn't."
            Assert.True(ContainsItem(t.ResolvedDependencyFiles, @"c:\Regress390219\v1\D.dll")); // "Expected to find assembly, but didn't."
            Assert.Empty(t.SuggestedRedirects);
            Assert.Equal(0, e.Warnings); // "Should only be no warning about suggested redirects."
        }

        // It is hard to write a test that will fail with the root cause of https://github.com/Microsoft/msbuild/issues/4002
        // because it requires reading real files from disk and we mock GetAssemblyName in RAR tests.
        //
        // So to add some coverage, simply check that we get a Culture=neutral when enumerating references of this test
        // assembly.
        [Fact]
        public void RealGetAssemblyNameIncludesCulture()
        {
            using (var info = new AssemblyInformation(Assembly.GetExecutingAssembly().Location))
            {
                foreach (var dependency in info.Dependencies)
                {
                    Assert.Contains("Culture=neutral", dependency.ToString());
                }
            }
        }
    }
}
