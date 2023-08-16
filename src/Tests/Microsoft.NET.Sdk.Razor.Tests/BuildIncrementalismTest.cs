// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Moq;

namespace Microsoft.NET.Sdk.Razor.Tests
{
    public class BuildIncrementalismTest : AspNetSdkTest
    {
        public BuildIncrementalismTest(ITestOutputHelper log) : base(log) { }


        [Fact(Skip = "https://github.com/dotnet/aspnetcore/issues/28780")]
        public void Build_ErrorInGeneratedCode_ReportsMSBuildError_OnIncrementalBuild()
        {
            var testAsset = "RazorSimpleMvc";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset);

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

        [Fact(Skip = "https://github.com/dotnet/aspnetcore/issues/28780")]
        public void BuildComponents_DoesNotRegenerateComponentDefinition_WhenDefinitionIsUnchanged()
        {
            var testAsset = "RazorMvcWithComponents";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset);

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

        [Fact(Skip = "https://github.com/dotnet/aspnetcore/issues/28780")]
        public void Build_TouchesUpToDateMarkerFile()
        {
            var testAsset = "RazorClassLibrary";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset);

            // Remove the components so that they don't interfere with these tests
            Directory.Delete(Path.Combine(projectDirectory.Path, "Components"), recursive: true);

            var build = new BuildCommand(projectDirectory);
            build.Execute()
                .Should()
                .Pass();

            string intermediateOutputPath = Path.Combine(build.GetBaseIntermediateDirectory().FullName, "Debug", DefaultTfm);

            var classLibraryDll = Path.Combine(intermediateOutputPath, "ClassLibrary.dll");
            var classLibraryViewsDll = Path.Combine(intermediateOutputPath, "ClassLibrary.Views.dll");
            var markerFile = Path.Combine(intermediateOutputPath, "ClassLibrary.csproj.CopyComplete"); ;

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
