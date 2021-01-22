// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.Utilities;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Sdk.Razor.Tests
{
    public class BuildIncrementalismTest : RazorSdkTest
    {
        public BuildIncrementalismTest(ITestOutputHelper log) : base(log) {}

        [Fact]
        public void BuildIncremental_SimpleMvc_PersistsTargetInputFile()
        {
            // Arrange
            var thumbprintLookup = new Dictionary<string, FileThumbPrint>();

            // Act 1
            var testAsset = "RazorSimpleMvc";
            var projectDirectory = CreateRazorSdkTestAsset(testAsset);

            var build = new BuildCommand(projectDirectory);
            var result = build.Execute();

            var intermediateOutputPath = build.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();
            var filesToIgnore = new[]
            {
                // These files are generated on every build.
                Path.Combine(intermediateOutputPath, "SimpleMvc.csproj.CopyComplete"),
                Path.Combine(intermediateOutputPath, "SimpleMvc.csproj.FileListAbsolute.txt"),
            };

            var files = Directory.GetFiles(intermediateOutputPath).Where(p => !filesToIgnore.Contains(p));
            foreach (var file in files)
            {
                var thumbprint = FileThumbPrint.Create(file);
                thumbprintLookup[file] = thumbprint;
            }

            // Assert 1
            result.Should().Pass();

            // Act & Assert 2
            for (var i = 0; i < 2; i++)
            {
                // We want to make sure nothing changed between multiple incremental builds.
                using (var razorGenDirectoryLock = LockDirectory(Path.Combine(intermediateOutputPath, "Razor")))
                {
                    result = build.Execute();
                }

                result.Should().Pass();
                foreach (var file in files)
                {
                    var thumbprint = FileThumbPrint.Create(file);
                    Assert.Equal(thumbprintLookup[file], thumbprint);
                }
            }
        }

        [Fact]
        public void RazorGenerate_RegeneratesTagHelperInputs_IfFileChanges()
        {
            var testAsset = "RazorSimpleMvc";
            var projectDirectory = CreateRazorSdkTestAsset(testAsset);

            // Act - 1
            var build = new BuildCommand(projectDirectory);
            var result = build.Execute();

            var intermediateOutputPath = build.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();

            var expectedTagHelperCacheContent = @"""Name"":""SimpleMvc.SimpleTagHelper""";
            var file = Path.Combine(projectDirectory.Path, "SimpleTagHelper.cs");
            var tagHelperOutputCache = Path.Combine(intermediateOutputPath, "SimpleMvc.TagHelpers.output.cache");
            var generatedFile = Path.Combine(intermediateOutputPath, "Razor", "Views", "Home", "Index.cshtml.g.cs");

            // Assert - 1
            result.Should().Pass();
            new FileInfo(tagHelperOutputCache).Should().Contain(expectedTagHelperCacheContent);
            var fileThumbPrint = FileThumbPrint.Create(generatedFile);

            // Act - 2
            // Update the source content and build. We should expect the outputs to be regenerated.
            File.WriteAllText(file, string.Empty);
            build = new BuildCommand(projectDirectory);
            result = build.Execute();

            // Assert - 2
            result.Should().Pass();
            new FileInfo(tagHelperOutputCache).Should().NotContain(@"""Name"":""SimpleMvc.SimpleTagHelper""");
            var newThumbPrint = FileThumbPrint.Create(generatedFile);
            Assert.NotEqual(fileThumbPrint, newThumbPrint);
        }

        [Fact]
        public void Build_ErrorInGeneratedCode_ReportsMSBuildError_OnIncrementalBuild()
        {
            var testAsset = "RazorSimpleMvc";
            var projectDirectory = CreateRazorSdkTestAsset(testAsset);

            // Introducing a Razor semantic error
            var indexPage = Path.Combine(projectDirectory.Path, "Views", "Home", "Index.cshtml");
            File.WriteAllText(indexPage, "@{ // Unterminated code block");

            // Regular build
            VerifyError(projectDirectory);

            // Incremental build
            VerifyError(projectDirectory);

            void VerifyError(TestAsset privateDirectory)
            {
                var build = new BuildCommand(projectDirectory);
                var result = build.Execute();

                result.Should().Fail().And.HaveStdOutContaining("RZ1006");

                var intermediateOutputPath = build.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();

                // Compilation failed without creating the views assembly
                new FileInfo(Path.Combine(intermediateOutputPath, "SimpleMvc.dll")).Should().Exist();
                new FileInfo(Path.Combine(intermediateOutputPath, "SimpleMvc.Views.dll")).Should().NotExist();

                // File with error does not get written to disk.
                new FileInfo(Path.Combine(intermediateOutputPath, "Razor", "Views", "Home", "Index.cshtml.g.cs")).Should().NotExist();
            }
        }

        [Fact]
        public void BuildComponents_ErrorInGeneratedCode_ReportsMSBuildError_OnIncrementalBuild()
        {
            var testAsset = "RazorMvcWithComponents";
            var projectDirectory = CreateRazorSdkTestAsset(testAsset);
                
            // Introducing a Razor semantic error
            var indexPage = Path.Combine(projectDirectory.Path, "Views", "Shared", "NavMenu.razor");
            File.WriteAllText(indexPage, "@{ // Unterminated code block");

            // Regular build
            VerifyError(projectDirectory);

            // Incremental build
            VerifyError(projectDirectory);

            void VerifyError(TestAsset projectDirectory)
            {
                var build = new BuildCommand(projectDirectory);
                var result = build.Execute();

                result.Should().Fail().And.HaveStdOutContaining("RZ1006");

                var intermediateOutputPath = build.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();

                // Compilation failed without creating the views assembly
                new FileInfo(Path.Combine(intermediateOutputPath, "MvcWithComponents.dll")).Should().NotExist();
                new FileInfo(Path.Combine(intermediateOutputPath, "MvcWithComponents.Views.dll")).Should().NotExist();

                // File with error does not get written to disk.
                new FileInfo(Path.Combine(intermediateOutputPath, "RazorComponents", "Views", "Shared", "NavMenu.razor.g.cs")).Should().NotExist();
            }
        }

        [Fact]
        public void BuildComponents_DoesNotRegenerateComponentDefinition_WhenDefinitionIsUnchanged()
        {
            var testAsset = "RazorMvcWithComponents";
            var projectDirectory = CreateRazorSdkTestAsset(testAsset);

            // Act - 1
            var build = new BuildCommand(projectDirectory);

            var intermediateOutputPath = build.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();
            var outputPath = build.GetOutputDirectory(DefaultTfm, "Debug").ToString();

            var updatedContent = "Some content";
            var tagHelperOutputCache = Path.Combine(intermediateOutputPath, "MvcWithComponents.TagHelpers.output.cache");

            var generatedFile = Path.Combine(intermediateOutputPath, "Razor", "Views", "Shared", "NavMenu.razor.g.cs");
            var generatedDefinitionFile = Path.Combine(intermediateOutputPath, "RazorDeclaration", "Views", "Shared", "NavMenu.razor.g.cs");

            // Assert - 1
            var result = build.Execute();
            result.Should().Pass();

            var outputFile = Path.Combine(outputPath, "MvcWithComponents.dll");
            new FileInfo(outputFile).Should().Exist();
            var outputAssemblyThumbprint = FileThumbPrint.Create(outputFile);

            new FileInfo(generatedDefinitionFile).Should().Exist();
            var generatedDefinitionThumbprint = FileThumbPrint.Create(generatedDefinitionFile);
            new FileInfo(generatedFile).Should().Exist();
            var generatedFileThumbprint = FileThumbPrint.Create(generatedFile);

            new FileInfo(tagHelperOutputCache).Should().Exist();
            new FileInfo(tagHelperOutputCache).Should().Contain(@"""Name"":""MvcWithComponents.Views.Shared.NavMenu""");

            var definitionThumbprint = FileThumbPrint.Create(tagHelperOutputCache);

            // Act - 2
            var page = Path.Combine(projectDirectory.Path, "Views", "Shared", "NavMenu.razor");
            File.WriteAllText(page, updatedContent, Encoding.UTF8);
            File.SetLastWriteTimeUtc(page, File.GetLastWriteTimeUtc(page).AddSeconds(1));
            
            build = new BuildCommand(projectDirectory);
            result = build.Execute();

            // Assert - 2
            new FileInfo(generatedDefinitionFile).Should().Exist();
            // Definition file remains unchanged.
            Assert.Equal(generatedDefinitionThumbprint, FileThumbPrint.Create(generatedDefinitionFile));
            new FileInfo(generatedFile).Should().Exist();
            // Generated file should change and include the new content.
            Assert.NotEqual(generatedFileThumbprint, FileThumbPrint.Create(generatedFile));
            new FileInfo(generatedFile).Should().Contain(updatedContent);

            // TagHelper cache should remain unchanged.
            Assert.Equal(definitionThumbprint, FileThumbPrint.Create(tagHelperOutputCache));
        }

        [Fact]
        public void BuildComponents_RegeneratesComponentDefinition_WhenFilesChange()
        {
            var testAsset = "RazorMvcWithComponents";
            var projectDirectory = CreateRazorSdkTestAsset(testAsset);

            var build = new BuildCommand(projectDirectory);

            var intermediateOutputPath = build.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();
            var outputPath = build.GetOutputDirectory(DefaultTfm, "Debug").ToString();

            // Act - 1
            var updatedContent = "@code { [Parameter] public string AParameter { get; set; } }";
            var tagHelperOutputCache = Path.Combine(intermediateOutputPath, "MvcWithComponents.TagHelpers.output.cache");

            var generatedFile = Path.Combine(intermediateOutputPath, "Razor", "Views", "Shared", "NavMenu.razor.g.cs");
            var generatedDefinitionFile = Path.Combine(intermediateOutputPath, "RazorDeclaration", "Views", "Shared", "NavMenu.razor.g.cs");

            // Assert - 1
            var result = build.Execute();

            result.Should().Pass();
            var outputFile = Path.Combine(outputPath, "MvcWithComponents.dll");
            new FileInfo(outputFile).Should().Exist();
            var outputAssemblyThumbprint = FileThumbPrint.Create(outputFile);

            new FileInfo(generatedDefinitionFile).Should().Exist();
            var generatedDefinitionThumbprint = FileThumbPrint.Create(generatedDefinitionFile);
            new FileInfo(generatedFile).Should().Exist();
            var generatedFileThumbprint = FileThumbPrint.Create(generatedFile);

            new FileInfo(tagHelperOutputCache).Should().Exist();
            new FileInfo(tagHelperOutputCache).Should().Contain(@"""Name"":""MvcWithComponents.Views.Shared.NavMenu""");

            var definitionThumbprint = FileThumbPrint.Create(tagHelperOutputCache);

            // Act - 2
            var page = Path.Combine(projectDirectory.Path, "Views", "Shared", "NavMenu.razor");
            File.WriteAllText(page, updatedContent);

            build = new BuildCommand(projectDirectory);
            result = build.Execute();

            // Assert - 2
            new FileInfo(outputFile).Should().Exist();
            Assert.NotEqual(outputAssemblyThumbprint, FileThumbPrint.Create(outputFile));

            new FileInfo(generatedDefinitionFile).Should().Exist();
            Assert.NotEqual(generatedDefinitionThumbprint, FileThumbPrint.Create(generatedDefinitionFile));
            new FileInfo(generatedFile).Should().Exist();
            Assert.NotEqual(generatedFileThumbprint, FileThumbPrint.Create(generatedFile));

            new FileInfo(tagHelperOutputCache).Should().Exist();
            new FileInfo(tagHelperOutputCache).Should().Contain(@"""Name"":""MvcWithComponents.Views.Shared.NavMenu""");

            new FileInfo(tagHelperOutputCache).Should().Contain("AParameter");

            Assert.NotEqual(definitionThumbprint, FileThumbPrint.Create(tagHelperOutputCache));
        }

        [Fact]
        public void BuildComponents_DoesNotModifyFiles_IfFilesDoNotChange()
        {
            var testAsset = "RazorMvcWithComponents";
            var projectDirectory = CreateRazorSdkTestAsset(testAsset);

            var build = new BuildCommand(projectDirectory);

            var intermediateOutputPath = build.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();
            var outputPath = build.GetOutputDirectory(DefaultTfm, "Debug").ToString();

            // Act - 1
            var tagHelperOutputCache = Path.Combine(intermediateOutputPath, "MvcWithComponents.TagHelpers.output.cache");

            var file = Path.Combine(projectDirectory.Path, "Views", "Shared", "NavMenu.razor.g.cs");
            var generatedFile = Path.Combine(intermediateOutputPath, "Razor", "Views", "Shared", "NavMenu.razor.g.cs");
            var generatedDefinitionFile = Path.Combine(intermediateOutputPath, "RazorDeclaration", "Views", "Shared", "NavMenu.razor.g.cs");

            // Assert - 1
            var result = build.Execute();

            result.Should().Pass();
            var outputFile = Path.Combine(outputPath, "MvcWithComponents.dll");
            new FileInfo(outputFile).Should().Exist();
            var outputAssemblyThumbprint = FileThumbPrint.Create(outputFile);

            new FileInfo(generatedDefinitionFile).Should().Exist();
            var generatedDefinitionThumbprint = FileThumbPrint.Create(generatedDefinitionFile);
            new FileInfo(generatedFile).Should().Exist();
            var generatedFileThumbprint = FileThumbPrint.Create(generatedFile);

            new FileInfo(tagHelperOutputCache).Should().Exist();
            new FileInfo(tagHelperOutputCache).Should().Contain(@"""Name"":""MvcWithComponents.Views.Shared.NavMenu""");

            var definitionThumbprint = FileThumbPrint.Create(tagHelperOutputCache);

            // Act - 2
            result = build.Execute();

            // Assert - 2
            new FileInfo(outputFile).Should().Exist();
            Assert.Equal(outputAssemblyThumbprint, FileThumbPrint.Create(outputFile));

            new FileInfo(generatedDefinitionFile).Should().Exist();
            Assert.Equal(generatedDefinitionThumbprint, FileThumbPrint.Create(generatedDefinitionFile));
            new FileInfo(generatedFile).Should().Exist();
            Assert.Equal(generatedFileThumbprint, FileThumbPrint.Create(generatedFile));

            new FileInfo(tagHelperOutputCache).Should().Exist();
            new FileInfo(tagHelperOutputCache).Should().Contain(@"""Name"":""MvcWithComponents.Views.Shared.NavMenu""");

            Assert.Equal(definitionThumbprint, FileThumbPrint.Create(tagHelperOutputCache));
        }

        [Fact]
        public void IncrementalBuild_WithP2P_WorksWhenBuildProjectReferencesIsDisabled()
        {
            // Simulates building the same way VS does by setting BuildProjectReferences=false.
            // With this flag, the only target called is GetCopyToOutputDirectoryItems on the referenced project.
            // We need to ensure that we continue providing Razor binaries and symbols as files to be copied over.
            var testAsset = "RazorAppWithP2PReference";
            var projectDirectory = CreateRazorSdkTestAsset(testAsset);
            
            var build = new BuildCommand(projectDirectory, "AppWithP2PReference");
            build.Execute().Should().Pass();

            string outputPath = build.GetOutputDirectory(DefaultTfm).FullName;

            new FileInfo(Path.Combine(outputPath, "AppWithP2PReference.dll")).Should().Exist();
            new FileInfo(Path.Combine(outputPath, "AppWithP2PReference.Views.dll")).Should().Exist();
            new FileInfo(Path.Combine(outputPath, "ClassLibrary.dll")).Should().Exist();
            new FileInfo(Path.Combine(outputPath, "ClassLibrary.Views.dll")).Should().Exist();
            new FileInfo(Path.Combine(outputPath, "ClassLibrary.Views.pdb")).Should().Exist();

            var clean = new MSBuildCommand(Log, "Clean", build.FullPathProjectFile);
            clean.Execute("/p:BuildProjectReferences=false").Should().Pass();

            new FileInfo(Path.Combine(outputPath, "AppWithP2PReference.dll")).Should().NotExist();
            new FileInfo(Path.Combine(outputPath, "AppWithP2PReference.Views.dll")).Should().NotExist();
            new FileInfo(Path.Combine(outputPath, "ClassLibrary.dll")).Should().NotExist();
            new FileInfo(Path.Combine(outputPath, "ClassLibrary.Views.dll")).Should().NotExist();
            new FileInfo(Path.Combine(outputPath, "ClassLibrary.Views.pdb")).Should().NotExist();

            // dotnet msbuild /p:BuildProjectReferences=false
            build = new BuildCommand(projectDirectory, "AppWithP2PReference");
            build.Execute("/p:BuildProjectReferences=false").Should().Pass();

            new FileInfo(Path.Combine(outputPath, "AppWithP2PReference.dll")).Should().Exist();
            new FileInfo(Path.Combine(outputPath, "AppWithP2PReference.Views.dll")).Should().Exist();
            new FileInfo(Path.Combine(outputPath, "ClassLibrary.dll")).Should().Exist();
            new FileInfo(Path.Combine(outputPath, "ClassLibrary.Views.dll")).Should().Exist();
            new FileInfo(Path.Combine(outputPath, "ClassLibrary.Views.pdb")).Should().Exist();
        }

        [Fact]
        public void Build_TouchesUpToDateMarkerFile()
        {
            var testAsset = "RazorClassLibrary";
            var projectDirectory = CreateRazorSdkTestAsset(testAsset);

            // Remove the components so that they don't interfere with these tests
            Directory.Delete(Path.Combine(projectDirectory.Path, "Components"), recursive: true);

            var build = new BuildCommand(projectDirectory);
            build.Execute()
                .Should()
                .Pass();

            string intermediateOutputPath = Path.Combine(build.GetBaseIntermediateDirectory().FullName, "Debug", DefaultTfm);

            var classLibraryDll = Path.Combine(intermediateOutputPath, "ClassLibrary.dll");
            var classLibraryViewsDll = Path.Combine(intermediateOutputPath, "ClassLibrary.Views.dll");
            var markerFile = Path.Combine(intermediateOutputPath, "ClassLibrary.csproj.CopyComplete");;

            new FileInfo(classLibraryDll).Should().Exist();
            new FileInfo(classLibraryViewsDll).Should().Exist();
            new FileInfo(markerFile).Should().Exist();

            // Gather thumbprints before incremental build.
            var classLibraryThumbPrint = FileThumbPrint.Create(classLibraryDll);
            var classLibraryViewsThumbPrint = FileThumbPrint.Create(classLibraryViewsDll);
            var markerFileThumbPrint = FileThumbPrint.Create(markerFile);

            build = new BuildCommand(projectDirectory);
            build.Execute()
                .Should()
                .Pass();

            // Verify thumbprint file is unchanged between true incremental builds
            Assert.Equal(classLibraryThumbPrint, FileThumbPrint.Create(classLibraryDll));
            Assert.Equal(classLibraryViewsThumbPrint, FileThumbPrint.Create(classLibraryViewsDll));
            // In practice, this should remain unchanged. However, since our tests reference
            // binaries from other projects, this file gets updated by Microsoft.Common.targets
            Assert.NotEqual(markerFileThumbPrint, FileThumbPrint.Create(markerFile));

            // Change a cshtml file and verify ClassLibrary.Views.dll and marker file are updated
            File.AppendAllText(Path.Combine(projectDirectory.Path, "Views", "_ViewImports.cshtml"), Environment.NewLine);

            build = new BuildCommand(projectDirectory);
            build.Execute()
                .Should()
                .Pass();

            Assert.Equal(classLibraryThumbPrint, FileThumbPrint.Create(classLibraryDll));
            Assert.NotEqual(classLibraryViewsThumbPrint, FileThumbPrint.Create(classLibraryViewsDll));
            Assert.NotEqual(markerFileThumbPrint, FileThumbPrint.Create(markerFile));
        }

        private IDisposable LockDirectory(string directory)
        {
            var disposables = new List<IDisposable>();
            foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
            {
                disposables.Add(File.Open(file, FileMode.Open, FileAccess.Read, FileShare.None));
            }

            var disposable = new Mock<IDisposable>();
            disposable.Setup(d => d.Dispose())
                .Callback(() => disposables.ForEach(d => d.Dispose()));

            return disposable.Object;
        }
    }
}
