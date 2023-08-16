// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Sdk.Razor.Tests
{
    public class PackIntegrationTest : AspNetSdkTest
    {

        public PackIntegrationTest(ITestOutputHelper log) : base(log) { }

        [Fact]
        public void Pack_NoBuild_Works_IncludesAssembly()
        {
            var testAsset = "RazorClassLibrary";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset);

            var build = new BuildCommand(projectDirectory);
            build.Execute().Should().Pass();

            var pack = new MSBuildCommand(projectDirectory, "Pack");
            var result = pack.Execute("/p:NoBuild=true");

            result.Should().Pass();

            var outputPath = build.GetOutputDirectory(DefaultTfm, "Debug").ToString();

            new FileInfo(Path.Combine(outputPath, "ClassLibrary.dll")).Should().Exist();
            new FileInfo(Path.Combine(outputPath, "ClassLibrary.Views.dll")).Should().NotExist();

            result.Should().NuSpecContain(
                Path.Combine(projectDirectory.Path, "obj", "Debug", "ClassLibrary.1.0.0.nuspec"),
                $"<file src=\"{Path.Combine(outputPath, "ClassLibrary.dll")}\" " +
                $"target=\"{Path.Combine("lib", DefaultTfm, "ClassLibrary.dll")}\" />");

            result.Should().NuSpecDoesNotContain(
                Path.Combine(projectDirectory.Path, "obj", "Debug", "ClassLibrary.1.0.0.nuspec"),
                $"<file src=\"{Path.Combine(outputPath, "ClassLibrary.Views.dll")}\" " +
                $"target=\"{Path.Combine("lib", DefaultTfm, "ClassLibrary.Views.dll")}\" />");

            result.Should().NuSpecDoesNotContain(
                Path.Combine(projectDirectory.Path, "obj", "Debug", "ClassLibrary.1.0.0.nuspec"),
                $"<file src=\"{Path.Combine(outputPath, "ClassLibrary.Views.pdb")}\" " +
                $"target=\"{Path.Combine("lib", DefaultTfm, "ClassLibrary.Views.pdb")}\" />");

            result.Should().NuSpecDoesNotContain(
                Path.Combine(projectDirectory.Path, "obj", "Debug", "ClassLibrary.1.0.0.nuspec"),
                $@"<files include=""any/{DefaultTfm}/Views/Shared/_Layout.cshtml"" buildAction=""Content"" />");

            result.Should().NuPkgContain(
                Path.Combine(build.GetPackageDirectory().FullName, "ClassLibrary.1.0.0.nupkg"),
                Path.Combine("lib", DefaultTfm, "ClassLibrary.dll"));
        }
    }
}
