// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if FEATURE_WIN32_REGISTRY

using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Tasks;
using Microsoft.Build.UnitTests;
using Microsoft.Build.Utilities;
using Shouldly;
using Xunit;

#nullable disable

namespace Microsoft.Build.Tasks.UnitTests
{
    /// <summary>
    /// Wave-gated empty/null handling tests for <see cref="AssemblyFoldersResolver"/>.
    /// The empty-DirectoryPath path is registry-driven so the tests use reflection to inject the
    /// static <c>AssemblyFolder.s_assemblyFolders</c> dictionary that the resolver enumerates.
    /// </summary>
    [Collection("AssemblyFolderStaticState")]
    public class AssemblyFoldersResolver_Tests : IDisposable
    {
        private static readonly FieldInfo s_assemblyFoldersField =
            typeof(AssemblyFolder).GetField("s_assemblyFolders", BindingFlags.NonPublic | BindingFlags.Static);

        private readonly TestEnvironment _env;
        private readonly object _originalAssemblyFolders;

        public AssemblyFoldersResolver_Tests(ITestOutputHelper testOutput)
        {
            _env = TestEnvironment.Create(testOutput);
            _originalAssemblyFolders = s_assemblyFoldersField.GetValue(null);
        }

        public void Dispose()
        {
            s_assemblyFoldersField.SetValue(null, _originalAssemblyFolders);
            ChangeWaves.ResetStateForTests();
            _env.Dispose();
        }

        [WindowsOnlyFact]
        public void EmptyAssemblyFolder_SilentlySkipped()
        {
            InjectAssemblyFolders(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "hklm\\Empty", string.Empty },
            });

            // Wave-on: empty entry is skipped before ResolveFromDirectory is reached, so
            // fileExists is never called and the resolver returns false.
            bool result = Resolve(fileExists: p => true);

            result.ShouldBeFalse();
        }

        [WindowsOnlyFact]
        public void EmptyAssemblyFolder_WaveDisabled_PassedThroughToResolveFromDirectory()
        {
            ChangeWaves.ResetStateForTests();
            _env.SetEnvironmentVariable("MSBUILDDISABLEFEATURESFROMVERSION", ChangeWaves.Wave18_8.ToString());
            BuildEnvironmentHelper.ResetInstance_ForUnitTestsOnly();

            InjectAssemblyFolders(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "hklm\\Empty", string.Empty },
            });

            // Wave-off: empty entry passes through to ResolveFromDirectory, which combines it
            // with TaskEnvironment.ProjectDirectory and calls fileExists. fileExists returns
            // true so the simple-name resolver path declares a match and the resolver returns true.
            bool result = Resolve(fileExists: p => true);

            result.ShouldBeTrue();
        }

        private static void InjectAssemblyFolders(Dictionary<string, string> folders)
        {
            s_assemblyFoldersField.SetValue(null, folders);
        }

        private static bool Resolve(FileExists fileExists)
        {
            var resolver = new AssemblyFoldersResolver(
                searchPathElement: "{AssemblyFolders}",
                getAssemblyName: path => throw new NotImplementedException(),
                fileExists: fileExists,
                getRuntimeVersion: path => throw new NotImplementedException(),
                targetedRuntimeVesion: Version.Parse("4.0.30319"),
                taskEnvironment: TaskEnvironmentHelper.CreateForTest());

            return resolver.Resolve(
                assemblyName: new AssemblyNameExtension("System"),
                sdkName: string.Empty,
                rawFileNameCandidate: null,
                isPrimaryProjectReference: true,
                isImmutableFrameworkReference: false,
                wantSpecificVersion: false,
                executableExtensions: new[] { ".dll", ".exe" },
                hintPath: string.Empty,
                // Empty key triggers the unkeyed path that returns all dictionary values without filtering empty.
                assemblyFolderKey: string.Empty,
                assembliesConsideredAndRejected: new List<ResolutionSearchLocation>(),
                foundPath: out _,
                userRequestedSpecificFile: out _);
        }
    }
}

#endif
