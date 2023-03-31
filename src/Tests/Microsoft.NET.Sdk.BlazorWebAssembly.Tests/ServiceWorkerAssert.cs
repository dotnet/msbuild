// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using FluentAssertions;
using Microsoft.NET.Sdk.BlazorWebAssembly;

namespace Microsoft.NET.Sdk.BlazorWebAssembly.Tests
{
    internal static class ServiceWorkerAssert
    {
        internal static void VerifyServiceWorkerFiles(TestAsset testAsset,
            string outputDirectory,
            string serviceWorkerPath,
            string serviceWorkerContent,
            string assetsManifestPath,
            string staticWebAssetsBasePath = "")
        {
            // Check the expected files are there
            var serviceWorkerResolvedPath = Path.Combine(testAsset.TestRoot, outputDirectory, staticWebAssetsBasePath, serviceWorkerPath);
            var assetsManifestResolvedPath = Path.Combine(testAsset.TestRoot, outputDirectory, staticWebAssetsBasePath, assetsManifestPath);

            // Check the service worker contains the expected content (which comes from the PublishedContent file)
            new FileInfo(serviceWorkerResolvedPath).Should().Contain(serviceWorkerContent);

            // Check the assets manifest version was added to the published service worker
            var assetsManifest = ReadServiceWorkerAssetsManifest(assetsManifestResolvedPath);
            new FileInfo(serviceWorkerResolvedPath).Should().Contain($"/* Manifest version: {assetsManifest.version} */");

            // Check the assets manifest contains correct entries for all static content we're publishing
            var resolvedPublishDirectory = Path.Combine(testAsset.TestRoot, outputDirectory);
            var outputFiles = Directory.GetFiles(resolvedPublishDirectory, "*", new EnumerationOptions { RecurseSubdirectories = true });
            var assetsManifestHashesByUrl = (IReadOnlyDictionary<string, string>)assetsManifest.assets.ToDictionary(x => x.url, x => x.hash);
            foreach (var filePath in outputFiles)
            {
                var relativePath = Path.GetRelativePath(resolvedPublishDirectory, filePath);

                // We don't list compressed files in the SWAM, as these are transparent to the client,
                // nor do we list the service worker itself or its assets manifest, as these don't need to be fetched in the same way
                if (IsCompressedFile(relativePath)
                    || string.Equals(relativePath, Path.Combine(staticWebAssetsBasePath, serviceWorkerPath), StringComparison.Ordinal)
                    || string.Equals(relativePath, Path.Combine(staticWebAssetsBasePath, assetsManifestPath), StringComparison.Ordinal))
                {
                    continue;
                }

                // Verify hash
                var fileUrl = relativePath.Replace('\\', '/');
                var expectedHash = ParseWebFormattedHash(assetsManifestHashesByUrl[fileUrl]);
                assetsManifestHashesByUrl.Keys.Should().Contain(fileUrl);
                new FileInfo(filePath).Should().HashEquals(expectedHash);
            }
        }

        private static string ParseWebFormattedHash(string webFormattedHash)
        {
            webFormattedHash.Should().StartWith("sha256-");
            return webFormattedHash.Substring(7);
        }

        private static bool IsCompressedFile(string path)
        {
            switch (Path.GetExtension(path))
            {
                case ".br":
                case ".gz":
                    return true;
                default:
                    return false;
            }
        }

        private static AssetsManifestFile ReadServiceWorkerAssetsManifest(string assetsManifestResolvedPath)
        {
            var jsContents = File.ReadAllText(assetsManifestResolvedPath);
            var jsonStart = jsContents.IndexOf('{');
            var jsonLength = jsContents.LastIndexOf('}') - jsonStart + 1;
            var json = jsContents.Substring(jsonStart, jsonLength);
            return JsonSerializer.Deserialize<AssetsManifestFile>(json);
        }
    }
}
