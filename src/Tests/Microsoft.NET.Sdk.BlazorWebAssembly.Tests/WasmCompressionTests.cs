// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Sdk.BlazorWebAssembly.Tests
{
    public class WasmCompressionTests : AspNetSdkTest
    {
        public WasmCompressionTests(ITestOutputHelper log) : base(log) { }

        [Fact]
        public void Publish_UpdatesFilesWhenSourcesChange()
        {
            // Arrange
            var testAppName = "BlazorHosted";
            var testInstance = CreateAspNetSdkTestAsset(testAppName);

            var publishCommand = new PublishCommand(testInstance, "blazorhosted");
            publishCommand.Execute().Should().Pass();

            // Act
            var blazorHostedPublishDirectory = publishCommand.GetOutputDirectory().FullName;
            var mainAppDll = Path.Combine(blazorHostedPublishDirectory, "wwwroot", "_framework", "blazorwasm.wasm");
            var mainAppDllThumbPrint = FileThumbPrint.Create(mainAppDll);
            var mainAppCompressedDll = Path.Combine(blazorHostedPublishDirectory, "wwwroot", "_framework", "blazorwasm.wasm.br");
            var mainAppCompressedDllThumbPrint = FileThumbPrint.Create(mainAppCompressedDll);

            var blazorBootJson = Path.Combine(testInstance.TestRoot, publishCommand.GetOutputDirectory(DefaultTfm).ToString(), "wwwroot", "_framework", "blazor.boot.json");
            var blazorBootJsonThumbPrint = FileThumbPrint.Create(blazorBootJson);
            var blazorBootJsonCompressed = Path.Combine(testInstance.TestRoot, publishCommand.GetOutputDirectory(DefaultTfm).ToString(), "wwwroot", "_framework", "blazor.boot.json.br");
            var blazorBootJsonCompressedThumbPrint = FileThumbPrint.Create(blazorBootJsonCompressed);

            var programFile = Path.Combine(testInstance.TestRoot, "blazorwasm", "Program.cs");
            var programFileContents = File.ReadAllText(programFile);
            File.WriteAllText(programFile, programFileContents.Replace("args", "arguments"));

            // Assert
            publishCommand = new PublishCommand(Log, Path.Combine(testInstance.TestRoot, "blazorhosted"));
            publishCommand.Execute().Should().Pass();

            var newMainAppDllThumbPrint = FileThumbPrint.Create(mainAppDll);
            var newMainAppCompressedDllThumbPrint = FileThumbPrint.Create(mainAppCompressedDll);
            var newBlazorBootJsonThumbPrint = FileThumbPrint.Create(blazorBootJson);
            var newBlazorBootJsonCompressedThumbPrint = FileThumbPrint.Create(blazorBootJsonCompressed);

            Assert.NotEqual(mainAppDllThumbPrint, newMainAppDllThumbPrint);
            Assert.NotEqual(mainAppCompressedDllThumbPrint, newMainAppCompressedDllThumbPrint);

            Assert.NotEqual(blazorBootJsonThumbPrint, newBlazorBootJsonThumbPrint);
            Assert.NotEqual(blazorBootJsonCompressedThumbPrint, newBlazorBootJsonCompressedThumbPrint);
        }

        [Fact]
        public void Publish_WithoutLinkerAndCompression_UpdatesFilesWhenSourcesChange()
        {
            // Arrange
            var testAppName = "BlazorHosted";
            var testInstance = CreateAspNetSdkTestAsset(testAppName);

            var publishCommand = new PublishCommand(testInstance, "blazorhosted");
            publishCommand.Execute("/p:BlazorWebAssemblyEnableLinking=false").Should().Pass();

            // Act
            var publishDirectory = publishCommand.GetOutputDirectory(DefaultTfm).FullName;
            var mainAppDll = Path.Combine(publishDirectory, "wwwroot", "_framework", "blazorwasm.wasm");
            var mainAppDllThumbPrint = FileThumbPrint.Create(mainAppDll);

            var mainAppCompressedDll = Path.Combine(publishDirectory, "wwwroot", "_framework", "blazorwasm.wasm.br");
            var mainAppCompressedDllThumbPrint = FileThumbPrint.Create(mainAppCompressedDll);

            var programFile = Path.Combine(testInstance.TestRoot, "blazorwasm", "Program.cs");
            var programFileContents = File.ReadAllText(programFile);
            File.WriteAllText(programFile, programFileContents.Replace("args", "arguments"));

            // Assert
            publishCommand = new PublishCommand(Log, Path.Combine(testInstance.TestRoot, "blazorhosted"));
            publishCommand.Execute("/p:BlazorWebAssemblyEnableLinking=false").Should().Pass();

            var newMainAppDllThumbPrint = FileThumbPrint.Create(mainAppDll);
            var newMainAppCompressedDllThumbPrint = FileThumbPrint.Create(mainAppCompressedDll);

            Assert.NotEqual(mainAppDllThumbPrint, newMainAppDllThumbPrint);
            Assert.NotEqual(mainAppCompressedDllThumbPrint, newMainAppCompressedDllThumbPrint);
        }

        [Fact]
        public void Publish_WithLinkerAndCompression_IsIncremental()
        {
            // Arrange
            var testAppName = "BlazorHosted";
            var testInstance = CreateAspNetSdkTestAsset(testAppName);

            var publishCommand = new PublishCommand(Log, Path.Combine(testInstance.TestRoot, "blazorhosted"));
            publishCommand.WithWorkingDirectory(testInstance.TestRoot);
            publishCommand.Execute().Should().Pass();

            var buildOutputDirectory = publishCommand.GetOutputDirectory(DefaultTfm);

            // Act
            var compressedFilesFolder = Path.Combine(testInstance.TestRoot, "blazorwasm", "obj", "Debug", DefaultTfm, "compressed", "publish");
            var thumbPrint = FileThumbPrint.CreateFolderThumbprint(testInstance, compressedFilesFolder);

            // Assert
            for (var i = 0; i < 3; i++)
            {
                var buildCommand = new BuildCommand(testInstance, "blazorhosted");
                buildCommand.Execute().Should().Pass();

                var newThumbPrint = FileThumbPrint.CreateFolderThumbprint(testInstance, compressedFilesFolder);
                Assert.Equal(thumbPrint.Count, newThumbPrint.Count);
                for (var j = 0; j < thumbPrint.Count; j++)
                {
                    Assert.Equal(thumbPrint[j], newThumbPrint[j]);
                }
            }
        }

        [Fact]
        public void Publish_WithoutLinkerAndCompression_IsIncremental()
        {
            // Arrange
            var testAppName = "BlazorHosted";
            var testInstance = CreateAspNetSdkTestAsset(testAppName);

            var publishCommand = new PublishCommand(Log, Path.Combine(testInstance.TestRoot, "blazorhosted"));
            publishCommand.Execute("/p:BlazorWebAssemblyEnableLinking=false")
                .Should().Pass();

            var buildOutputDirectory = publishCommand.GetOutputDirectory(DefaultTfm);

            // Act
            var compressedFilesFolder = Path.Combine(testInstance.TestRoot, "blazorwasm", "obj", "Debug", DefaultTfm, "compressed", "publish");
            var thumbPrint = FileThumbPrint.CreateFolderThumbprint(testInstance, compressedFilesFolder);

            // Assert
            for (var i = 0; i < 3; i++)
            {
                var buildCommand = new BuildCommand(testInstance, "blazorhosted");
                buildCommand.Execute("/p:BlazorWebAssemblyEnableLinking=false").Should().Pass();

                var newThumbPrint = FileThumbPrint.CreateFolderThumbprint(testInstance, compressedFilesFolder);
                Assert.Equal(thumbPrint.Count, newThumbPrint.Count);
                for (var j = 0; j < thumbPrint.Count; j++)
                {
                    Assert.Equal(thumbPrint[j], newThumbPrint[j]);
                }
            }
        }

        [Fact]
        public void Publish_CompressesAllFrameworkFiles()
        {
            // Arrange
            var testAppName = "BlazorWasmWithLibrary";
            var testInstance = CreateAspNetSdkTestAsset(testAppName);

            var publishCommand = new PublishCommand(testInstance, "blazorwasm");
            publishCommand.WithWorkingDirectory(testInstance.TestRoot);
            publishCommand.Execute().Should().Pass();

            var extensions = new[] { ".wasm", ".js", ".pdb", ".wasm", ".map", ".json", ".dat" };

            // Act
            var publishOutputDirectory = publishCommand.GetOutputDirectory(DefaultTfm).ToString();
            var frameworkFilesPath = Path.Combine(publishOutputDirectory, "wwwroot", "_framework");

            // Assert
            foreach (var file in Directory.EnumerateFiles(frameworkFilesPath, "*", new EnumerationOptions { RecurseSubdirectories = true, }))
            {
                var extension = Path.GetExtension(file);
                if (extension != ".br" && extension != ".gz")
                {
                    Assert.True(File.Exists($"{file}.gz"), $"Expected file {$"{file}.gz"} to exist, but it did not.");
                    Assert.True(File.Exists($"{file}.br"), $"Expected file {$"{file}.br"} to exist, but it did not.");
                }
            }
        }
    }
}
