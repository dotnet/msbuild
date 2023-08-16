// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Sdk.Razor.Tool.Tests
{
    public class MetadataCacheTest : SdkTest
    {
        public MetadataCacheTest(ITestOutputHelper log) : base(log) { }

        [Fact]
        public void GetMetadata_AddsToCache()
        {
            // Arrange
            var directory = _testAssetsManager.CreateTestDirectory();
            var metadataCache = new MetadataCache();
            var assemblyFilePath = LoaderTestResources.Delta.WriteToFile(directory.Path, "Delta.dll");

            // Act
            var result = metadataCache.GetMetadata(assemblyFilePath);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(1, metadataCache.Cache.Count);
        }

        [Fact]
        public void GetMetadata_UsesCache()
        {
            // Arrange
            var directory = _testAssetsManager.CreateTestDirectory();
            var metadataCache = new MetadataCache();
            var assemblyFilePath = LoaderTestResources.Delta.WriteToFile(directory.Path, "Delta.dll");

            // Act 1
            var result = metadataCache.GetMetadata(assemblyFilePath);

            // Assert 1
            Assert.NotNull(result);
            Assert.Equal(1, metadataCache.Cache.Count);

            // Act 2
            var cacheResult = metadataCache.GetMetadata(assemblyFilePath);

            // Assert 2
            Assert.Same(result, cacheResult);
            Assert.Equal(1, metadataCache.Cache.Count);
        }

        [Fact]
        public void GetMetadata_MultipleFiles_ReturnsDifferentResultsAndAddsToCache()
        {
            // Arrange
            var directory = _testAssetsManager.CreateTestDirectory();
            var metadataCache = new MetadataCache();
            var assemblyFilePath1 = LoaderTestResources.Delta.WriteToFile(directory.Path, "Delta.dll");
            var assemblyFilePath2 = LoaderTestResources.Gamma.WriteToFile(directory.Path, "Gamma.dll");

            // Act
            var result1 = metadataCache.GetMetadata(assemblyFilePath1);
            var result2 = metadataCache.GetMetadata(assemblyFilePath2);

            // Assert
            Assert.NotSame(result1, result2);
            Assert.Equal(2, metadataCache.Cache.Count);
        }

        [Fact]
        public void GetMetadata_ReplacesCache_IfFileTimestampChanged()
        {
            // Arrange
            var directory = _testAssetsManager.CreateTestDirectory();
            var metadataCache = new MetadataCache();
            var assemblyFilePath = LoaderTestResources.Delta.WriteToFile(directory.Path, "Delta.dll");

            // Act 1
            var result = metadataCache.GetMetadata(assemblyFilePath);

            // Assert 1
            Assert.NotNull(result);
            var entry = Assert.Single(metadataCache.Cache.TestingEnumerable);
            Assert.Same(result, entry.Value.Metadata);

            // Act 2
            // Update the timestamp of the file
            File.SetLastWriteTimeUtc(assemblyFilePath, File.GetLastWriteTimeUtc(assemblyFilePath).AddSeconds(1));
            var cacheResult = metadataCache.GetMetadata(assemblyFilePath);

            // Assert 2
            Assert.NotSame(result, cacheResult);
            entry = Assert.Single(metadataCache.Cache.TestingEnumerable);
            Assert.Same(cacheResult, entry.Value.Metadata);
        }
    }
}
