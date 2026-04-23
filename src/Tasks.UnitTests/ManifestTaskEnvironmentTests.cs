// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Tasks;
using Microsoft.Build.UnitTests;
using Microsoft.Build.Utilities;
using Shouldly;
using Xunit;

namespace Microsoft.Build.Tasks.UnitTests
{
    /// <summary>
    /// Tests verifying TaskEnvironment migration compatibility for manifest tasks.
    /// These tests focus on path handling changes from the migration.
    /// </summary>
    public class ManifestTaskEnvironmentTests
    {
        // Test 1: Empty ItemSpec - verifies exception handling matches pre-migration behavior
        // GetAbsolutePath throws on empty, but this flows through existing exception handling
        [Fact]
        public void CreateManifestResourceName_EmptyItemSpec_ShouldFail()
        {
            var engine = new MockEngine(true);
            var task = new CreateCSharpManifestResourceName
            {
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(),
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
            using var env = TestEnvironment.Create();
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
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(),
                BuildEngine = new MockEngine(true),
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
            using var env = TestEnvironment.Create();
            var folder = env.CreateFolder();
            var subFolder = Path.Combine(folder.Path, "Resources");
            Directory.CreateDirectory(subFolder);
            
            var resxPath = Path.Combine(subFolder, "Strings.resx");
            File.WriteAllText(resxPath, "<root></root>");

            // Replace backslashes with forward slashes
            var pathWithForwardSlashes = resxPath.Replace('\\', '/');

            var task = new CreateCSharpManifestResourceName
            {
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(),
                BuildEngine = new MockEngine(true),
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
            using var env = TestEnvironment.Create();
            var folder = env.CreateFolder();
            var subFolder = Path.Combine(folder.Path, "Sub", "Folder");
            Directory.CreateDirectory(subFolder);
            
            var resxPath = Path.Combine(subFolder, "Test.resx");
            File.WriteAllText(resxPath, "<root></root>");

            // Mix forward and back slashes
            var mixedPath = folder.Path + "/Sub\\Folder/Test.resx";

            var task = new CreateCSharpManifestResourceName
            {
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(),
                BuildEngine = new MockEngine(true),
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
            using var env = TestEnvironment.Create();
            var folder = env.CreateFolder();

            var task = new AddToWin32Manifest
            {
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(),
                BuildEngine = new MockEngine(true),
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
            using var env = TestEnvironment.Create();
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
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(),
                BuildEngine = new MockEngine(true),
                ResourceFiles = new ITaskItem[] { invalidItem, validItem },
                RootNamespace = "Test"
            };

            // Should return false due to error, but should still process both items
            bool result = task.Execute();
            result.ShouldBeFalse();
            // Both items should have manifest names assigned (even though task failed)
            task.ManifestResourceNames.Length.ShouldBe(2);
        }

        // Test 7: Deeply nested folder - tests path handling with many segments
        [Fact]
        public void CreateManifestResourceName_DeepNesting_ShouldWork()
        {
            using var env = TestEnvironment.Create();
            var folder = env.CreateFolder();
            var deepFolder = Path.Combine(folder.Path, "a", "b", "c", "d", "e");
            Directory.CreateDirectory(deepFolder);
            
            var resxPath = Path.Combine(deepFolder, "Test.resx");
            File.WriteAllText(resxPath, "<root></root>");

            var task = new CreateCSharpManifestResourceName
            {
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(),
                BuildEngine = new MockEngine(true),
                ResourceFiles = new ITaskItem[] { new TaskItem(resxPath) },
                RootNamespace = "Test"
            };

            bool result = task.Execute();
            result.ShouldBeTrue();
        }

        // Test 8: Path with spaces - tests no issues with space handling
        [Fact]
        public void CreateManifestResourceName_PathWithSpaces_ShouldWork()
        {
            using var env = TestEnvironment.Create();
            var folder = env.CreateFolder();
            var spaceFolder = Path.Combine(folder.Path, "My Resources");
            Directory.CreateDirectory(spaceFolder);
            
            var resxPath = Path.Combine(spaceFolder, "My Strings.resx");
            File.WriteAllText(resxPath, "<root></root>");

            var task = new CreateCSharpManifestResourceName
            {
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(),
                BuildEngine = new MockEngine(true),
                ResourceFiles = new ITaskItem[] { new TaskItem(resxPath) },
                RootNamespace = "Test"
            };

            bool result = task.Execute();
            result.ShouldBeTrue();
        }
    }
}
