// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Moq;

namespace Microsoft.NET.Sdk.Razor.Tool.Tests
{
    public class DefaultExtensionDependencyCheckerTest : SdkTest
    {
        public DefaultExtensionDependencyCheckerTest(ITestOutputHelper log) : base(log) {}

        [Fact]
        public void Check_ReturnsFalse_WithMissingDependency()
        {
            // Arrange
            var directory = _testAssetsManager.CreateTestDirectory();
            var output = new StringWriter();

            var alphaFilePath = LoaderTestResources.Alpha.WriteToFile(directory.Path, "Alpha.dll");

            var loader = new TestDefaultExtensionAssemblyLoader(Path.Combine(directory.Path, "shadow"));
            var checker = new DefaultExtensionDependencyChecker(loader, output, output);

            // Act
            var result = checker.Check(new[] { alphaFilePath, });

            // Assert
            Assert.False(result, "Check should not have passed: " + output.ToString());
        }

        [Fact]
        public void Check_ReturnsTrue_WithAllDependenciesProvided()
        {
            // Arrange
            var directory = _testAssetsManager.CreateTestDirectory();
            var output = new StringWriter();

            var alphaFilePath = LoaderTestResources.Alpha.WriteToFile(directory.Path, "Alpha.dll");
            var betaFilePath = LoaderTestResources.Beta.WriteToFile(directory.Path, "Beta.dll");
            var gammaFilePath = LoaderTestResources.Gamma.WriteToFile(directory.Path, "Gamma.dll");
            var deltaFilePath = LoaderTestResources.Delta.WriteToFile(directory.Path, "Delta.dll");

            var loader = new TestDefaultExtensionAssemblyLoader(Path.Combine(directory.Path, "shadow"));
            var checker = new DefaultExtensionDependencyChecker(loader, output, output);

            // Act
            var result = checker.Check(new[] { alphaFilePath, betaFilePath, gammaFilePath, deltaFilePath, });

            // Assert
            Assert.True(result, "Check should have passed: " + output.ToString());
            
        }

        [Fact]
        public void Check_ReturnsFalse_WhenAssemblyHasDifferentMVID()
        {
            // Arrange
            var directory = _testAssetsManager.CreateTestDirectory();
            var output = new StringWriter();

            // Load Beta.dll from the future Alpha.dll path to prime the assembly loader
            var alphaFilePath = LoaderTestResources.Beta.WriteToFile(directory.Path, "Alpha.dll");
            var betaFilePath = LoaderTestResources.Beta.WriteToFile(directory.Path, "Beta.dll");
            var gammaFilePath = LoaderTestResources.Gamma.WriteToFile(directory.Path, "Gamma.dll");
            var deltaFilePath = LoaderTestResources.Delta.WriteToFile(directory.Path, "Delta.dll");

            var loader = new TestDefaultExtensionAssemblyLoader(Path.Combine(directory.Path, "shadow"));
            var checker = new DefaultExtensionDependencyChecker(loader, output, output);

            // This will cause the loader to cache some inconsistent information.
            loader.LoadFromPath(alphaFilePath);
            LoaderTestResources.Alpha.WriteToFile(directory.Path, "Alpha.dll");

            // Act
            var result = checker.Check(new[] { alphaFilePath, gammaFilePath, deltaFilePath, });

            // Assert
            Assert.False(result, "Check should not have passed: " + output.ToString());
        }

        [Fact]
        public void Check_ReturnsFalse_WhenLoaderThrows()
        {
            // Arrange
            var directory = _testAssetsManager.CreateTestDirectory();
            var output = new StringWriter();
            
            var deltaFilePath = LoaderTestResources.Delta.WriteToFile(directory.Path, "Delta.dll");

            var loader = new Mock<ExtensionAssemblyLoader>();
            loader
                .Setup(l => l.LoadFromPath(It.IsAny<string>()))
                .Throws(new InvalidOperationException());
            var checker = new DefaultExtensionDependencyChecker(loader.Object, output, output);

            // Act
            var result = checker.Check(new[] { deltaFilePath, });

            // Assert
            Assert.False(result, "Check should not have passed: " + output.ToString());
        }
    }
}
