// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Sdk.Razor.Tests
{
    public class MvcBuildIntegrationTest50 : MvcBuildIntegrationTestLegacy
    {
        public MvcBuildIntegrationTest50(ITestOutputHelper log) : base(log) {}

        public override string TestProjectName => "SimpleMvc50";
        public override string TargetFramework => "net5.0";

        [Fact]
        public void BuildComponents_ErrorInGeneratedCode_ReportsMSBuildError_OnIncrementalBuild()
        {
            var testAsset = "RazorMvcWithComponents";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset, overrideTfm: TargetFramework);
                
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

                var intermediateOutputPath = build.GetIntermediateDirectory(TargetFramework, "Debug").ToString();

                // Compilation failed without creating the views assembly
                new FileInfo(Path.Combine(intermediateOutputPath, "MvcWithComponents.dll")).Should().NotExist();
                new FileInfo(Path.Combine(intermediateOutputPath, "MvcWithComponents.Views.dll")).Should().NotExist();

                // File with error does not get written to disk.
                new FileInfo(Path.Combine(intermediateOutputPath, "RazorComponents", "Views", "Shared", "NavMenu.razor.g.cs")).Should().NotExist();
            }
        }

        [Fact]
        public void IncrementalBuild_WithP2P_WorksWhenBuildProjectReferencesIsDisabled()
        {
            // Simulates building the same way VS does by setting BuildProjectReferences=false.
            // With this flag, the only target called is GetCopyToOutputDirectoryItems on the referenced project.
            // We need to ensure that we continue providing Razor binaries and symbols as files to be copied over.
            var testAsset = "RazorAppWithP2PReference";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset, overrideTfm: TargetFramework);
            
            var build = new BuildCommand(projectDirectory, "AppWithP2PReference");
            build.Execute().Should().Pass();

            string outputPath = build.GetOutputDirectory(TargetFramework).FullName;

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

        [CoreMSBuildOnlyFact]
        public void CshtmlCss_InNET5App_DoesNotProduceErrors()
        {
            // Regression test for https://github.com/dotnet/aspnetcore/issues/39526
            var testAsset = $"Razor{TestProjectName}";
            var project = CreateAspNetSdkTestAsset(testAsset);
            var scopedCssPath = Path.Combine(project.Path, "wwwroot", "Views", "Home", "Index.cshtml.css");
            Directory.CreateDirectory(Path.GetDirectoryName(scopedCssPath));
            File.WriteAllText(scopedCssPath, "Nothing to see here");

            // Build
            var build = new BuildCommand(project);
            build.Execute().Should().Pass();
        }
    }
}
