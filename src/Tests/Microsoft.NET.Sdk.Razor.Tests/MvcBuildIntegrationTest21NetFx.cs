// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.DependencyModel;

namespace Microsoft.NET.Sdk.Razor.Tests
{
    public class MvcBuildIntegrationTest21NetFx : AspNetSdkTest
    {
        private const string TestProjectName = "SimpleMvc21NetFx";
        private const string TargetFramework = "net462";
        public const string OutputFileName = TestProjectName + ".exe";
        public MvcBuildIntegrationTest21NetFx(ITestOutputHelper log) : base(log) { }

        [Fact]
        public virtual void Building_Project()
        {
            var testAsset = $"Razor{TestProjectName}";
            var project = CreateAspNetSdkTestAsset(testAsset);

            // Build
            var build = new BuildCommand(project);
            build.Execute().Should().Pass();

            var outputPath = build.GetOutputDirectory(TargetFramework, "Debug").ToString();
            var intermediateOutputPath = build.GetIntermediateDirectory(TargetFramework, "Debug").ToString();

            new FileInfo(Path.Combine(outputPath, OutputFileName)).Should().Exist();
            new FileInfo(Path.Combine(outputPath, $"{TestProjectName}.pdb")).Should().Exist();
            new FileInfo(Path.Combine(outputPath, $"{TestProjectName}.Views.dll")).Should().Exist();
            new FileInfo(Path.Combine(outputPath, $"{TestProjectName}.Views.pdb")).Should().Exist();

            // Verify RazorTagHelper works
            new FileInfo(Path.Combine(intermediateOutputPath, $"{TestProjectName}.TagHelpers.input.cache")).Should().Exist();
            new FileInfo(Path.Combine(intermediateOutputPath, $"{TestProjectName}.TagHelpers.output.cache")).Should().Exist();
            new FileInfo(
                Path.Combine(intermediateOutputPath, $"{TestProjectName}.TagHelpers.output.cache")).Should().Contain(
                @"""Name"":""SimpleMvc.SimpleTagHelper""");
        }

        [Fact]
        public virtual void BuildingProject_CopyToOutputDirectoryFiles()
        {
            var testAsset = $"Razor{TestProjectName}";
            var project = CreateAspNetSdkTestAsset(testAsset);

            // Build
            var build = new BuildCommand(project);
            build.Execute().Should().Pass();

            var outputPath = build.GetOutputDirectory(TargetFramework, "Debug").ToString();

            // No cshtml files should be in the build output directory
            new DirectoryInfo(Path.Combine(outputPath, "Views")).Should().NotExist();

            // For netfx projects, we also expect a refs folder to be present in the output directory
            new DirectoryInfo(Path.Combine(outputPath, "refs")).Should().Exist();
        }

        [Fact]
        public virtual void Publish_Project()
        {
            var testAsset = $"Razor{TestProjectName}";
            var project = CreateAspNetSdkTestAsset(testAsset);

            var publish = new PublishCommand(Log, project.TestRoot);
            publish.Execute().Should().Pass();

            var outputPath = publish.GetOutputDirectory(TargetFramework, "Debug").ToString();

            new FileInfo(Path.Combine(outputPath, OutputFileName)).Should().Exist();
            new FileInfo(Path.Combine(outputPath, $"{TestProjectName}.pdb")).Should().Exist();
            new FileInfo(Path.Combine(outputPath, $"{TestProjectName}.Views.dll")).Should().Exist();
            new FileInfo(Path.Combine(outputPath, $"{TestProjectName}.Views.pdb")).Should().Exist();

            // By default, the refs folder and .cshtml files will not be copied on publish. However for framework projects, the refs directory is required.
            new DirectoryInfo(Path.Combine(outputPath, "refs")).Should().NotExist();
            new DirectoryInfo(Path.Combine(outputPath, "Views")).Should().NotExist();
        }

        [Fact]
        public virtual void Publish_IncludesRefAssemblies_WhenCopyRefAssembliesToPublishDirectoryIsSet()
        {
            var testAsset = $"Razor{TestProjectName}";
            var project = CreateAspNetSdkTestAsset(testAsset);

            var publish = new PublishCommand(Log, project.TestRoot);
            publish.Execute("/p:CopyRefAssembliesToPublishDirectory=true").Should().Pass();

            var outputPath = publish.GetOutputDirectory(TargetFramework, "Debug").ToString();

            new FileInfo(Path.Combine(outputPath, "refs", "System.Threading.Tasks.Extensions.dll")).Should().Exist();
        }

        [Fact]
        public void Build_ProducesDepsFileWithCompilationContext_ButNoReferences()
        {
            var testAsset = $"Razor{TestProjectName}";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset);

            var customDefine = "AspNetSdkTest";
            var build = new BuildCommand(projectDirectory);
            build.Execute($"/p:DefineConstants={customDefine}").Should().Pass();

            var outputPath = build.GetOutputDirectory(TargetFramework, "Debug").ToString();

            var depsFile = new FileInfo(Path.Combine(outputPath, $"{TestProjectName}.deps.json"));
            depsFile.Should().Exist();
            var dependencyContext = ReadDependencyContext(depsFile.FullName);

            // Ensure some compile references exist
            var packageReference = dependencyContext.CompileLibraries.First(l => l.Name == "System.Runtime.CompilerServices.Unsafe");
            packageReference.Assemblies.Should().NotBeEmpty();

            var projectReference = dependencyContext.CompileLibraries.First(l => l.Name == TestProjectName);
            projectReference.Assemblies.Should().NotBeEmpty();

            dependencyContext.CompilationOptions.Defines.Should().Contain(customDefine);
        }

        private static DependencyContext ReadDependencyContext(string depsFilePath)
        {
            var reader = new DependencyContextJsonReader();
            using (var stream = File.OpenRead(depsFilePath))
            {
                return reader.Read(stream);
            }
        }
    }
}
