// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Runtime.Versioning;
using Microsoft.Build.Framework;
using Microsoft.Build.Tasks;
using Microsoft.Build.Tasks.Deployment.ManifestUtilities;
using Microsoft.Build.UnitTests;
using Microsoft.Build.Utilities;
using Shouldly;
using Xunit;
using ManifestAssemblyReference = Microsoft.Build.Tasks.Deployment.ManifestUtilities.AssemblyReference;

namespace Microsoft.Build.Tasks.UnitTests
{
    /// <summary>
    /// Tests verifying TaskEnvironment migration compatibility for manifest tasks.
    /// These tests focus on path handling changes from the migration.
    /// </summary>
    public class ManifestTaskEnvironmentTests
    {
        private readonly ITestOutputHelper _output;

        public ManifestTaskEnvironmentTests(ITestOutputHelper output) => _output = output;
        // Test 1: Empty ItemSpec - verifies exception handling matches pre-migration behavior
        // GetAbsolutePath throws on empty, but this flows through existing exception handling
        [Fact]
        public void CreateManifestResourceName_EmptyItemSpec_ShouldFail()
        {
            var engine = new MockEngine(_output);
            var task = new CreateCSharpManifestResourceName
            {
                BuildEngine = engine,
                ResourceFiles = new ITaskItem[] { new TaskItem("") },
                RootNamespace = "Test"
            };

            // On .NET Framework: returns false with logged error
            // On .NET Core+: throws ArgumentNullException (pre-existing behavior in Path.GetDirectoryName)
#if NETFRAMEWORK
            bool result = task.Execute();
            result.ShouldBeFalse();
#else
            Should.Throw<ArgumentNullException>(() => task.Execute());
#endif
        }

        // Test 2: Path with .. segments - critical test for canonicalization
        // GetAbsolutePath does NOT canonicalize, so we wrap with Path.GetFullPath where needed
        [Fact]
        public void CreateManifestResourceName_PathWithDotDot_ShouldResolve()
        {
            using var env = TestEnvironment.Create(_output);
            var folder = env.CreateFolder();
            var subFolder = Path.Combine(folder.Path, "sub");
            Directory.CreateDirectory(subFolder);
            
            var resxPath = Path.Combine(subFolder, "Test.resx");
            File.WriteAllText(resxPath, "<root></root>");
            
            var csPath = Path.Combine(subFolder, "Test.cs");
            File.WriteAllText(csPath, "namespace Test { class Test { } }");

            // Use path with .. segments - tests canonicalization
            var pathWithDotDot = Path.Combine(folder.Path, "sub", "..", "sub", "Test.resx");

            var task = new CreateCSharpManifestResourceName
            {
                BuildEngine = new MockEngine(_output),
                ResourceFiles = new ITaskItem[] { new TaskItem(pathWithDotDot) },
                RootNamespace = "Test",
                UseDependentUponConvention = true
            };

            bool result = task.Execute();
            result.ShouldBeTrue();
        }

        // Test 3: Forward slashes - tests path normalization
        [Fact]
        public void CreateManifestResourceName_ForwardSlashes_ShouldWork()
        {
            using var env = TestEnvironment.Create(_output);
            var folder = env.CreateFolder();
            var subFolder = Path.Combine(folder.Path, "Resources");
            Directory.CreateDirectory(subFolder);
            
            var resxPath = Path.Combine(subFolder, "Strings.resx");
            File.WriteAllText(resxPath, "<root></root>");

            // Replace backslashes with forward slashes
            var pathWithForwardSlashes = resxPath.Replace('\\', '/');

            var task = new CreateCSharpManifestResourceName
            {
                BuildEngine = new MockEngine(_output),
                ResourceFiles = new ITaskItem[] { new TaskItem(pathWithForwardSlashes) },
                RootNamespace = "Test"
            };

            bool result = task.Execute();
            result.ShouldBeTrue();
        }

        // Test 4: Mixed slashes - tests path normalization handles both
        [Fact]
        public void CreateManifestResourceName_MixedSlashes_ShouldWork()
        {
            using var env = TestEnvironment.Create(_output);
            var folder = env.CreateFolder();
            var subFolder = Path.Combine(folder.Path, "Sub", "Folder");
            Directory.CreateDirectory(subFolder);
            
            var resxPath = Path.Combine(subFolder, "Test.resx");
            File.WriteAllText(resxPath, "<root></root>");

            // Mix forward and back slashes
            var mixedPath = folder.Path + "/Sub\\Folder/Test.resx";

            var task = new CreateCSharpManifestResourceName
            {
                BuildEngine = new MockEngine(_output),
                ResourceFiles = new ITaskItem[] { new TaskItem(mixedPath) },
                RootNamespace = "Test"
            };

            bool result = task.Execute();
            result.ShouldBeTrue();
        }

        // Test 5: AddToWin32Manifest with null ApplicationManifest - tests graceful handling
        [Fact]
        public void AddToWin32Manifest_NullApplicationManifest_HandledGracefully()
        {
            using var env = TestEnvironment.Create(_output);
            var folder = env.CreateFolder();

            var task = new AddToWin32Manifest
            {
                BuildEngine = new MockEngine(_output),
                ApplicationManifest = null,
                OutputDirectory = folder.Path,
                SupportedArchitectures = "amd64"
            };

            // Null is treated as "no manifest" - should generate new one
            bool result = task.Execute();
            result.ShouldBeTrue();
        }

        // Test 6: Batch processing - one error should not abort remaining items
        [Fact]
        public void CreateManifestResourceName_BatchProcessing_ContinuesAfterError()
        {
            using var env = TestEnvironment.Create(_output);
            var folder = env.CreateFolder();
            
            // Create one valid resource
            var validPath = Path.Combine(folder.Path, "Valid.resx");
            File.WriteAllText(validPath, "<root></root>");
            
            // Create another valid resource
            var valid2Path = Path.Combine(folder.Path, "Valid2.resx");
            File.WriteAllText(valid2Path, "<root></root>");

            // Invalid: DependentUpon points to non-existent file
            var invalidItem = new TaskItem(validPath);
            invalidItem.SetMetadata("DependentUpon", "NonExistent.cs");

            var validItem = new TaskItem(valid2Path);

            var task = new CreateCSharpManifestResourceName
            {
                BuildEngine = new MockEngine(_output),
                ResourceFiles = new ITaskItem[] { invalidItem, validItem },
                RootNamespace = "Test"
            };

            // Should return false due to error, but should still process both items
            bool result = task.Execute();
            result.ShouldBeFalse();
            // The valid item should still have been processed successfully
            task.ManifestResourceNames[1].ShouldNotBeNull();
            task.ManifestResourceNames[1].ItemSpec.ShouldNotBeNullOrEmpty();
        }

        // Test 7: Deeply nested folder - tests path handling with many segments
        [Fact]
        public void CreateManifestResourceName_DeepNesting_ShouldWork()
        {
            using var env = TestEnvironment.Create(_output);
            var folder = env.CreateFolder();
            var deepFolder = Path.Combine(folder.Path, "a", "b", "c", "d", "e");
            Directory.CreateDirectory(deepFolder);
            
            var resxPath = Path.Combine(deepFolder, "Test.resx");
            File.WriteAllText(resxPath, "<root></root>");

            var task = new CreateCSharpManifestResourceName
            {
                BuildEngine = new MockEngine(_output),
                ResourceFiles = new ITaskItem[] { new TaskItem(resxPath) },
                RootNamespace = "Test"
            };

            bool result = task.Execute();
            result.ShouldBeTrue();
        }

        [WindowsOnlyTheory]
        [InlineData(true, 1)]
        [InlineData(false, 2)]
        [SupportedOSPlatform("windows")]
        public void GenerateApplicationManifest_ResolvesRelativeSentinelDependencyAgainstProjectDirectory(bool waveEnabled, int expectedReferenceCount)
        {
            using TestEnvironment env = TestEnvironment.Create(_output);
            env.SetEnvironmentVariable("MSBUILDDISABLEFEATURESFROMVERSION", waveEnabled ? null : ChangeWaves.Wave18_13.ToString());
            ChangeWaves.ResetStateForTests();

            string projectDirectory = env.CreateFolder().Path;
            string relativeDependencyPath = Path.Combine("sub", "System.Core.dll");
            string dependencyPath = Path.Combine(projectDirectory, relativeDependencyPath);
            string outputManifestPath = Path.Combine(projectDirectory, "app.manifest");

            Directory.CreateDirectory(Path.GetDirectoryName(dependencyPath)!);
            WriteSystemCoreAssembly(dependencyPath);
            env.SetCurrentDirectory(env.CreateFolder().Path);
            Environment.CurrentDirectory.ShouldNotBe(projectDirectory);

            var dependency = new TaskItem(relativeDependencyPath);
            dependency.SetMetadata("AssemblyType", "Managed");

            try
            {
                var task = new GenerateApplicationManifest
                {
                    AssemblyName = "TestApplication",
                    AssemblyVersion = "1.0.0.0",
                    BuildEngine = new MockEngine(_output),
                    Dependencies = [dependency],
                    OutputManifest = new TaskItem(outputManifestPath),
                    TargetFrameworkVersion = "v3.5",
                    TaskEnvironment = TaskEnvironment.CreateWithProjectDirectoryAndEnvironment(projectDirectory),
                };

                task.Execute().ShouldBeTrue();

                var manifest = (ApplicationManifest)ManifestReader.ReadManifest(outputManifestPath, preserveStream: false);
                ManifestAssemblyReference[] systemCoreReferences = manifest.AssemblyReferences
                    .Cast<ManifestAssemblyReference>()
                    .Where(reference => reference.AssemblyIdentity?.Name == "System.Core")
                    .ToArray();

                systemCoreReferences.Length.ShouldBe(expectedReferenceCount);
                if (waveEnabled)
                {
                    systemCoreReferences[0].IsPrerequisite.ShouldBeFalse();
                }
            }
            finally
            {
                ChangeWaves.ResetStateForTests();
            }
        }

        // Test 8: Path with spaces - tests no issues with space handling
        [Fact]
        public void CreateManifestResourceName_PathWithSpaces_ShouldWork()
        {
            using var env = TestEnvironment.Create(_output);
            var folder = env.CreateFolder();
            var spaceFolder = Path.Combine(folder.Path, "My Resources");
            Directory.CreateDirectory(spaceFolder);
            
            var resxPath = Path.Combine(spaceFolder, "My Strings.resx");
            File.WriteAllText(resxPath, "<root></root>");

            var task = new CreateCSharpManifestResourceName
            {
                BuildEngine = new MockEngine(_output),
                ResourceFiles = new ITaskItem[] { new TaskItem(resxPath) },
                RootNamespace = "Test"
            };

            bool result = task.Execute();
            result.ShouldBeTrue();
        }

        private static void WriteSystemCoreAssembly(string path)
        {
            var metadata = new MetadataBuilder();
            metadata.AddModule(
                generation: 0,
                metadata.GetOrAddString(Path.GetFileName(path)),
                metadata.GetOrAddGuid(Guid.NewGuid()),
                encId: default,
                encBaseId: default);
            metadata.AddAssembly(
                metadata.GetOrAddString("System.Core"),
                new Version(3, 5, 0, 0),
                culture: default,
                metadata.GetOrAddBlob(ParseHex(AssemblyRef.EcmaPublicKeyFull)),
                AssemblyFlags.PublicKey,
                System.Reflection.AssemblyHashAlgorithm.Sha1);
            metadata.AddTypeDefinition(
                TypeAttributes.NotPublic,
                @namespace: default,
                metadata.GetOrAddString("<Module>"),
                baseType: default,
                MetadataTokens.FieldDefinitionHandle(1),
                MetadataTokens.MethodDefinitionHandle(1));

            var peBuilder = new ManagedPEBuilder(
                PEHeaderBuilder.CreateLibraryHeader(),
                new MetadataRootBuilder(metadata),
                new BlobBuilder());
            var peImage = new BlobBuilder();
            peBuilder.Serialize(peImage);

            using FileStream stream = File.Create(path);
            peImage.WriteContentTo(stream);
        }

        private static byte[] ParseHex(string value)
        {
            var bytes = new byte[value.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = Convert.ToByte(value.Substring(i * 2, 2), 16);
            }

            return bytes;
        }
    }
}
