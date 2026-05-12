// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Build.UnitTests
{
    /// <summary>
    /// Tests to verify that all resource strings used in code exist in .resx files.
    /// 
    /// This test suite helps prevent runtime errors where code references resource strings
    /// that don't exist in the corresponding .resx files. These issues typically manifest as:
    /// 1. Missing resource exceptions at runtime
    /// 2. Resources accessible from multiple code paths but only tested in one
    /// 3. Resources referenced in conditional compilation code that aren't in the main .resx
    /// 
    /// If these tests fail, it means:
    /// - New code is referencing a resource that doesn't exist - add the resource to the .resx file
    /// - A resource was deleted but code still references it - update the code
    /// - A resource is in the wrong .resx file - move it to the correct assembly's resources
    /// 
    /// Related issues:
    /// - https://github.com/dotnet/msbuild/issues/12334
    /// - https://github.com/dotnet/msbuild/issues/11515
    /// - https://github.com/dotnet/msbuild/issues/7218
    /// - https://github.com/dotnet/msbuild/issues/2997
    /// - https://github.com/dotnet/msbuild/issues/9150
    /// </summary>
    public class Resources_Tests
    {
        private readonly ITestOutputHelper _output;

        public Resources_Tests(ITestOutputHelper output)
        {
            _output = output;
        }

        /// <summary>
        /// Verifies that all resource strings referenced in Microsoft.Build assembly exist in the corresponding .resx files
        /// </summary>
        [Fact]
        public void AllReferencedResourcesExistInBuildAssembly()
        {
            VerifyResourcesForAssembly(
                "Microsoft.Build",
                Path.Combine(GetRepoRoot(), "src", "Build"),
                new[] { "Resources/Strings.resx" },
                new[] { "../Shared/Resources/Strings.shared.resx" });
        }

        /// <summary>
        /// Verifies that all resource strings referenced in Microsoft.Build.Tasks.Core assembly exist in the corresponding .resx files
        /// </summary>
        [Fact]
        public void AllReferencedResourcesExistInTasksAssembly()
        {
            VerifyResourcesForAssembly(
                "Microsoft.Build.Tasks.Core",
                Path.Combine(GetRepoRoot(), "src", "Tasks"),
                new[] { "Resources/Strings.resx" },
                new[] { "../Shared/Resources/Strings.shared.resx" });
        }

        /// <summary>
        /// Verifies that all resource strings referenced in Microsoft.Build.Utilities.Core assembly exist in the corresponding .resx files
        /// </summary>
        [Fact]
        public void AllReferencedResourcesExistInUtilitiesAssembly()
        {
            VerifyResourcesForAssembly(
                "Microsoft.Build.Utilities.Core",
                Path.Combine(GetRepoRoot(), "src", "Utilities"),
                new[] { "Resources/Strings.resx" },
                new[] { "../Shared/Resources/Strings.shared.resx" });
        }

        /// <summary>
        /// Verifies that all resource strings referenced in MSBuild assembly exist in the corresponding .resx files
        /// </summary>
        [Fact]
        public void AllReferencedResourcesExistInMSBuildAssembly()
        {
            VerifyResourcesForAssembly(
                "MSBuild",
                Path.Combine(GetRepoRoot(), "src", "MSBuild"),
                new[] { "Resources/Strings.resx" },
                new[] { "../Shared/Resources/Strings.shared.resx" });
        }

        // NOTE: To add verification for additional assemblies, follow this pattern:
        // [Fact]
        // public void AllReferencedResourcesExistInYourAssembly()
        // {
        //     VerifyResourcesForAssembly(
        //         "Your.Assembly.Name",
        //         Path.Combine(GetRepoRoot(), "src", "YourAssemblyFolder"),
        //         new[] { "Resources/Strings.resx" },  // Primary resources for this assembly
        //         new[] { "../Shared/Resources/Strings.shared.resx" });  // Shared resources
        // }

        private void VerifyResourcesForAssembly(
            string assemblyName,
            string sourceDirectory,
            string[] primaryResxPaths,
            string[] sharedResxPaths)
        {
            _output.WriteLine($"Verifying resources for {assemblyName}");
            _output.WriteLine($"Source directory: {sourceDirectory}");

            // Load all resource strings from .resx files
            var availableResources = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var resxPath in primaryResxPaths)
            {
                var fullPath = Path.Combine(sourceDirectory, resxPath);
                _output.WriteLine($"Loading primary resources from: {fullPath}");
                LoadResourcesFromResx(fullPath, availableResources);
            }

            foreach (var resxPath in sharedResxPaths)
            {
                var fullPath = Path.Combine(sourceDirectory, resxPath);
                _output.WriteLine($"Loading shared resources from: {fullPath}");
                LoadResourcesFromResx(fullPath, availableResources);
            }

            _output.WriteLine($"Total available resources: {availableResources.Count}");

            // Find all resource string references in source code
            var referencedResources = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var sourceFiles = Directory.GetFiles(sourceDirectory, "*.cs", SearchOption.AllDirectories)
                .Where(f => !f.Contains("/obj/") && !f.Contains("\\obj\\"))
                .Where(f => !f.Contains("/bin/") && !f.Contains("\\bin\\"))
                .ToList();

            _output.WriteLine($"Scanning {sourceFiles.Count} source files for resource references");

            foreach (var sourceFile in sourceFiles)
            {
                ExtractResourceReferences(sourceFile, referencedResources);
            }

            _output.WriteLine($"Total referenced resources: {referencedResources.Count}");

            // Find missing resources
            var missingResources = referencedResources.Except(availableResources, StringComparer.OrdinalIgnoreCase).ToList();

            if (missingResources.Any())
            {
                _output.WriteLine($"Missing resources ({missingResources.Count}):");
                foreach (var missing in missingResources.OrderBy(x => x))
                {
                    _output.WriteLine($"  - {missing}");
                }
            }

            // Assert that all referenced resources exist
            missingResources.ShouldBeEmpty($"The following resources are referenced in code but missing from .resx files in {assemblyName}: {string.Join(", ", missingResources)}");
        }

        private void LoadResourcesFromResx(string resxPath, HashSet<string> resources)
        {
            if (!File.Exists(resxPath))
            {
                _output.WriteLine($"WARNING: Resource file not found: {resxPath}");
                return;
            }

            var doc = XDocument.Load(resxPath);
            foreach (var dataElement in doc.Descendants("data"))
            {
                var name = dataElement.Attribute("name")?.Value;
                if (!string.IsNullOrEmpty(name))
                {
                    resources.Add(name!);
                }
            }
        }

        private void ExtractResourceReferences(string sourceFile, HashSet<string> resources)
        {
            var content = File.ReadAllText(sourceFile);

            // Skip files that are conditional compilation only (e.g., XamlTaskFactory which is .NETFramework-only)
            // These might reference resources that are intentionally not included in all builds
            // TODO: Consider handling this more elegantly by checking project file conditionals
            
            // Patterns to match resource method calls with string literal arguments
            var patterns = new[]
            {
                // ResourceUtilities.FormatResourceString*("ResourceName", ...)
                @"ResourceUtilities\.FormatResourceString[A-Za-z]*\s*\(\s*""([A-Z][^""]+)""\s*[,\)]",
                
                // ResourceUtilities.GetResourceString("ResourceName")
                @"ResourceUtilities\.GetResourceString\s*\(\s*""([A-Z][^""]+)""\s*\)",
                
                // Log.LogErrorWithCodeFromResources("ResourceName", ...)
                @"\.LogErrorWithCodeFromResources\s*\(\s*""([A-Z][^""]+)""\s*[,\)]",
                
                // Log.LogWarningWithCodeFromResources("ResourceName", ...)
                @"\.LogWarningWithCodeFromResources\s*\(\s*""([A-Z][^""]+)""\s*[,\)]",
                
                // ProjectErrorUtilities.ThrowInvalidProject(*location, "ResourceName", ...)
                @"ProjectErrorUtilities\.ThrowInvalid[A-Za-z]*\s*\([^,""]*,\s*""([A-Z][^""]+)""\s*[,\)]",
                
                // ProjectErrorUtilities.VerifyThrowInvalidProject(*location, "ResourceName", ...)
                @"ProjectErrorUtilities\.VerifyThrowInvalid[A-Za-z]*\s*\([^,""]*,\s*""([A-Z][^""]+)""\s*[,\)]",
                
                // AssemblyResources.GetString("ResourceName") - case where the resource name starts with uppercase
                @"AssemblyResources\.GetString\s*\(\s*""([A-Z][^""]+)""\s*\)",
            };

            foreach (var pattern in patterns)
            {
                var matches = Regex.Matches(content, pattern, RegexOptions.Multiline);
                foreach (Match match in matches)
                {
                    if (match.Groups.Count > 1)
                    {
                        var resourceName = match.Groups[1].Value;
                        // Resource names typically start with uppercase and don't contain braces or dollar signs
                        if (!resourceName.Contains("{") && 
                            !resourceName.Contains("$") && 
                            !resourceName.Contains(" ") &&
                            char.IsUpper(resourceName[0]))
                        {
                            resources.Add(resourceName);
                        }
                    }
                }
            }
        }

        private string GetRepoRoot()
        {
            // Start from the current directory and walk up until we find the repo root
            var currentDir = Directory.GetCurrentDirectory();
            while (currentDir != null && !File.Exists(Path.Combine(currentDir, "MSBuild.sln")))
            {
                currentDir = Directory.GetParent(currentDir)?.FullName;
            }

            return currentDir ?? throw new InvalidOperationException("Could not find repository root");
        }
    }
}
