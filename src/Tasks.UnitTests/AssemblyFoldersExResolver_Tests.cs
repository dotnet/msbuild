// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if FEATURE_WIN32_REGISTRY

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Versioning;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Tasks;
using Microsoft.Build.UnitTests;
using Microsoft.Build.Utilities;
using Microsoft.Win32;
using Shouldly;
using Xunit;

#nullable disable

namespace Microsoft.Build.Tasks.UnitTests
{
    /// <summary>
    /// Wave-gated empty/null handling tests for <see cref="AssemblyFoldersExResolver"/>.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public class AssemblyFoldersExResolver_Tests : IDisposable
    {
        private readonly TestEnvironment _env;

        public AssemblyFoldersExResolver_Tests(ITestOutputHelper testOutput)
        {
            _env = TestEnvironment.Create(testOutput);
        }

        public void Dispose()
        {
            ChangeWaves.ResetStateForTests();
            _env.Dispose();
        }

        [WindowsOnlyFact]
        public void EmptyDirectoryPath_SilentlySkipped()
        {
            // Wave-on: empty entry is skipped before ResolveFromDirectory is reached, so
            // fileExists is never called and the resolver returns false.
            bool result = Resolve(fileExists: p => true);

            result.ShouldBeFalse();
        }

        [WindowsOnlyFact]
        public void EmptyDirectoryPath_WaveDisabled_PassedThroughToResolveFromDirectory()
        {
            ChangeWaves.ResetStateForTests();
            _env.SetEnvironmentVariable("MSBUILDDISABLEFEATURESFROMVERSION", ChangeWaves.Wave18_8.ToString());
            BuildEnvironmentHelper.ResetInstance_ForUnitTestsOnly();

            // Wave-off: empty entry passes through to ResolveFromDirectory, which combines it
            // with TaskEnvironment.ProjectDirectory and calls fileExists. fileExists returns
            // true so the simple-name resolver path declares a match and the resolver returns true.
            bool result = Resolve(fileExists: p => true);

            result.ShouldBeTrue();
        }

        private static bool Resolve(FileExists fileExists)
        {
            AssemblyFoldersExResolver resolver = new(
                searchPathElement: "{Registry:HKLM\\FakeRoot,v4.5,FakeSuffix}",
                getAssemblyName: path => throw new NotImplementedException(),
                fileExists: fileExists,
                getRegistrySubKeyNames: (hive, key) => Array.Empty<string>(),
                getRegistrySubKeyDefaultValue: (hive, key) => null,
                getRuntimeVersion: path => "v4.0.30319",
                openBaseKey: (hive, view) => null,
                targetedRuntimeVesion: Version.Parse("4.0.30319"),
                // X86 (non-MSIL non-None) lets the resolver short-circuit to "found" once
                // ResolveFromDirectory declares a match, without needing a real assembly on disk.
                targetProcessorArchitecture: System.Reflection.ProcessorArchitecture.X86,
                compareProcessorArchitecture: false,
                buildEngine: null,
                taskEnvironment: TaskEnvironmentHelper.CreateForTest());

            // Build an AssemblyFoldersEx with a single entry whose DirectoryPath is empty.
            AssemblyFoldersExInfo emptyInfo = new(
                hive: RegistryHive.LocalMachine,
                view: RegistryView.Default,
                registryKey: "FakeKey",
                directoryPath: string.Empty,
                targetFrameworkVersion: Version.Parse("4.5"));
            AssemblyFoldersEx assemblyFoldersEx = new(new[] { emptyInfo });
            AssemblyFoldersExCache cache = new(assemblyFoldersEx, fileExists, TaskEnvironmentHelper.CreateForTest());

            // Bypass LazyInitialize and inject our state directly.
            BindingFlags privateInstance = BindingFlags.NonPublic | BindingFlags.Instance;
            typeof(AssemblyFoldersExResolver).GetField("_isInitialized", privateInstance).SetValue(resolver, true);
            typeof(AssemblyFoldersExResolver).GetField("_wasMatch", privateInstance).SetValue(resolver, true);
            typeof(AssemblyFoldersExResolver).GetField("_assemblyFoldersCache", privateInstance).SetValue(resolver, cache);

            return resolver.Resolve(
                assemblyName: new AssemblyNameExtension("System"),
                sdkName: string.Empty,
                rawFileNameCandidate: null,
                isPrimaryProjectReference: true,
                isImmutableFrameworkReference: false,
                wantSpecificVersion: false,
                executableExtensions: new[] { ".dll", ".exe" },
                hintPath: string.Empty,
                assemblyFolderKey: string.Empty,
                assembliesConsideredAndRejected: new List<ResolutionSearchLocation>(),
                foundPath: out _,
                userRequestedSpecificFile: out _);
        }
    }
}

#endif
