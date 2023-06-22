// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using System.Text.Json;
using Microsoft.NET.Sdk.WebAssembly;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;
using ResourceHashesByNameDictionary = System.Collections.Generic.Dictionary<string, string>;

namespace Microsoft.NET.Sdk.BlazorWebAssembly.Tests
{
    public abstract class WasmPublishIntegrationTestBase : AspNetSdkTest
    {
        public WasmPublishIntegrationTestBase(ITestOutputHelper log) : base(log) { }

        protected static void VerifyBootManifestHashes(TestAsset testAsset, string blazorPublishDirectory)
        {
            var bootManifestResolvedPath = Path.Combine(blazorPublishDirectory, "_framework", "blazor.boot.json");
            var bootManifestJson = File.ReadAllText(bootManifestResolvedPath);
            var bootManifest = JsonSerializer.Deserialize<BootJsonData>(bootManifestJson);

            VerifyBootManifestHashes(testAsset, blazorPublishDirectory, bootManifest.resources.assembly);
            VerifyBootManifestHashes(testAsset, blazorPublishDirectory, bootManifest.resources.runtime);

            if (bootManifest.resources.pdb != null)
            {
                VerifyBootManifestHashes(testAsset, blazorPublishDirectory, bootManifest.resources.pdb);
            }

            if (bootManifest.resources.satelliteResources != null)
            {
                foreach (var resourcesForCulture in bootManifest.resources.satelliteResources.Values)
                {
                    VerifyBootManifestHashes(testAsset, blazorPublishDirectory, resourcesForCulture);
                }
            }

            static void VerifyBootManifestHashes(TestAsset testAsset, string blazorPublishDirectory, ResourceHashesByNameDictionary resources)
            {
                foreach (var (name, hash) in resources)
                {
                    var relativePath = Path.Combine(blazorPublishDirectory, "_framework", name);
                    new FileInfo(Path.Combine(testAsset.TestRoot, relativePath)).Should().HashEquals(ParseWebFormattedHash(hash));
                }
            }

            static string ParseWebFormattedHash(string webFormattedHash)
            {
                Assert.StartsWith("sha256-", webFormattedHash);
                return webFormattedHash.Substring(7);
            }
        }
    }
}
