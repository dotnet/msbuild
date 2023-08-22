// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Sdk.Razor.Tool.Tests
{
    public class DefaultExtensionAssemblyLoaderTest : SdkTest
    {
        public DefaultExtensionAssemblyLoaderTest(ITestOutputHelper log) : base(log) { }

        [Fact]
        public void LoadFromPath_CanLoadAssembly()
        {
            // Arrange
            var directory = _testAssetsManager.CreateTestDirectory();
            var alphaFilePath = LoaderTestResources.Alpha.WriteToFile(directory.Path, "Alpha.dll");

            var loader = new TestDefaultExtensionAssemblyLoader(Path.Combine(directory.Path, "shadow"));

            // Act
            var assembly = loader.LoadFromPath(alphaFilePath);

            // Assert
            Assert.NotNull(assembly);
        }

        [Fact]
        public void LoadFromPath_DoesNotAddDuplicates_AfterLoadingByName()
        {
            // Arrange
            var directory = _testAssetsManager.CreateTestDirectory();
            var alphaFilePath = LoaderTestResources.Alpha.WriteToFile(directory.Path, "Alpha.dll");
            var alphaFilePath2 = LoaderTestResources.Alpha.WriteToFile(directory.Path, "Alpha2.dll");

            var loader = new TestDefaultExtensionAssemblyLoader(Path.Combine(directory.Path, "shadow"));
            loader.AddAssemblyLocation(alphaFilePath);

            var assembly1 = loader.Load("Alpha");

            // Act
            var assembly2 = loader.LoadFromPath(alphaFilePath2);

            // Assert
            Assert.Same(assembly1, assembly2);
        }

        [Fact]
        public void LoadFromPath_DoesNotAddDuplicates_AfterLoadingByPath()
        {
            // Arrange
            var directory = _testAssetsManager.CreateTestDirectory();
            var alphaFilePath = LoaderTestResources.Alpha.WriteToFile(directory.Path, "Alpha.dll");
            var alphaFilePath2 = LoaderTestResources.Alpha.WriteToFile(directory.Path, "Alpha2.dll");

            var loader = new TestDefaultExtensionAssemblyLoader(Path.Combine(directory.Path, "shadow"));
            var assembly1 = loader.LoadFromPath(alphaFilePath);

            // Act
            var assembly2 = loader.LoadFromPath(alphaFilePath2);

            // Assert
            Assert.Same(assembly1, assembly2);

        }

        [Fact]
        public void Load_CanLoadAssemblyByName_AfterLoadingByPath()
        {
            // Arrange
            var directory = _testAssetsManager.CreateTestDirectory();
            var alphaFilePath = LoaderTestResources.Alpha.WriteToFile(directory.Path, "Alpha.dll");

            var loader = new TestDefaultExtensionAssemblyLoader(Path.Combine(directory.Path, "shadow"));
            var assembly1 = loader.LoadFromPath(alphaFilePath);

            // Act
            var assembly2 = loader.Load(assembly1.FullName);

            // Assert
            Assert.Same(assembly1, assembly2);
        }

        [Fact]
        public void LoadFromPath_WithDependencyPathsSpecified_CanLoadAssemblyDependencies()
        {
            // Arrange
            var directory = _testAssetsManager.CreateTestDirectory();
            var alphaFilePath = LoaderTestResources.Alpha.WriteToFile(directory.Path, "Alpha.dll");
            var betaFilePath = LoaderTestResources.Beta.WriteToFile(directory.Path, "Beta.dll");
            var gammaFilePath = LoaderTestResources.Gamma.WriteToFile(directory.Path, "Gamma.dll");
            var deltaFilePath = LoaderTestResources.Delta.WriteToFile(directory.Path, "Delta.dll");

            var loader = new TestDefaultExtensionAssemblyLoader(Path.Combine(directory.Path, "shadow"));
            loader.AddAssemblyLocation(gammaFilePath);
            loader.AddAssemblyLocation(deltaFilePath);

            // Act
            var alpha = loader.LoadFromPath(alphaFilePath);
            var beta = loader.LoadFromPath(betaFilePath);

            // Assert
            var builder = new StringBuilder();

            var a = alpha.CreateInstance("Alpha.A");
            a.GetType().GetMethod("Write").Invoke(a, new object[] { builder, "Test A" });

            var b = beta.CreateInstance("Beta.B");
            b.GetType().GetMethod("Write").Invoke(b, new object[] { builder, "Test B" });
            var expected = @"Delta: Gamma: Alpha: Test A
Delta: Gamma: Beta: Test B
";

            var actual = builder.ToString();

            Assert.Equal(expected, actual, ignoreLineEndingDifferences: true);
        }
    }
}
