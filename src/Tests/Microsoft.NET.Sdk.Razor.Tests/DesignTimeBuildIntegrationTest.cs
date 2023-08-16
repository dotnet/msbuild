// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.AspNetCore.Razor.Design.IntegrationTests
{
    public class DesignTimeBuildIntegrationTest : AspNetSdkTest
    {
        public DesignTimeBuildIntegrationTest(ITestOutputHelper log) : base(log) { }

        [Fact]
        public void DesignTimeBuild_DoesNotRunRazorTargets()
        {
            var testAsset = "RazorSimpleMvc";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset);

            // Using Compile here instead of CompileDesignTime because the latter is only defined when using
            // the VS targets. This is a close enough simulation for an SDK project
            var command = new MSBuildCommand(Log, "Compile", projectDirectory.Path);
            command.Execute("/clp:PerformanceSummary /p:DesignTimeBuild=true")
                .Should()
                .Pass()
                // .And.HaveStdOutContaining("_GenerateRazorAssemblyInfo")
                .And.NotHaveStdOutContaining("RazorCoreGenerate")
                .And.NotHaveStdOutContaining("RazorCoreCompile");

            var outputPath = command.GetOutputDirectory(DefaultTfm, "Debug").ToString();

            new FileInfo(Path.Combine(outputPath, "SimpleMvc.dll")).Should().NotExist();
            new FileInfo(Path.Combine(outputPath, "SimpleMvc.pdb")).Should().NotExist();
            new FileInfo(Path.Combine(outputPath, "SimpleMvc.Views.dll")).Should().NotExist();
            new FileInfo(Path.Combine(outputPath, "SimpleMvc.Views.pdb")).Should().NotExist();
        }

        [Fact(Skip = "Skipping until https://github.com/dotnet/aspnetcore/issues/28825 is resolved.")]
        public void RazorGenerateDesignTime_ReturnsRazorGenerateWithTargetPath()
        {
            var testAsset = "RazorSimpleMvc";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset);

            var command = new MSBuildCommand(Log, "RazorGenerateDesignTime;_IntrospectRazorGenerateWithTargetPath", projectDirectory.Path);
            var result = command.Execute();
            result.Should().Pass();

            var filePaths = new string[]
            {
                Path.Combine("Views", "Home", "About.cshtml"),
                Path.Combine("Views", "Home", "Contact.cshtml"),
                Path.Combine("Views", "Home", "Index.cshtml"),
                Path.Combine("Views", "Shared", "_Layout.cshtml"),
                Path.Combine("Views", "Shared", "_ValidationScriptsPartial.cshtml"),
                Path.Combine("Views", "_ViewImports.cshtml"),
                Path.Combine("Views", "_ViewStart.cshtml"),
            };

            var razorIntermediateOutputPath = Path.Combine(
                command.GetBaseIntermediateDirectory().ToString(), "Razor");

            foreach (var filePath in filePaths)
            {
                result.Should().HaveStdOutContaining(
                    $@"RazorGenerateWithTargetPath: {filePath} {filePath} {Path.Combine("obj", "Debug", DefaultTfm, "Razor", filePath + ".g.cs")}");
            }
        }

        [Fact]
        public void RazorGenerateComponentDesignTime_ReturnsRazorComponentWithTargetPath()
        {
            var testAsset = "RazorComponentLibrary";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset);

            var command = new MSBuildCommand(Log, "RazorGenerateComponentDesignTime;_IntrospectRazorComponentWithTargetPath", projectDirectory.Path);
            var result = command.Execute();
            result.Should().Pass();

            var filePaths = new string[]
            {
                Path.Combine("GenericComponent.razor"),
                Path.Combine("MyComponent.razor"),
            };

            foreach (var filePath in filePaths)
            {
                result.Should().HaveStdOutContaining(
                    $@"RazorComponentWithTargetPath: {filePath} {filePath} {Path.Combine("obj", "Debug", "netstandard2.0", "Razor", filePath + ".g.cs")} {Path.Combine("obj", "Debug", "netstandard2.0", "RazorDeclaration", filePath + ".g.cs")}");
            }
        }
    }
}
