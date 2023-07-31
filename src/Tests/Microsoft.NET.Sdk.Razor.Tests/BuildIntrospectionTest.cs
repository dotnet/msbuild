// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Sdk.Razor.Tests
{
    public class BuildIntrospectionTest : AspNetSdkTest
    {
        public BuildIntrospectionTest(ITestOutputHelper log) : base(log) {}

        [Fact]
        public void RazorSdk_AddsCshtmlFilesToUpToDateCheckInput()
        {
            var testAsset = "RazorSimpleMvc";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset);
            
            var build = new BuildCommand(projectDirectory);
            build.Execute("/t:_IntrospectUpToDateCheck")
                .Should()
                .Pass()
                .And.HaveStdOutContaining($"UpToDateCheckInput: {Path.Combine("Views", "Home", "Index.cshtml")}")
                .And.HaveStdOutContaining($"UpToDateCheckInput: {Path.Combine("Views", "_ViewStart.cshtml")}");
        }

        [Fact]
        public void UpToDateReloadFileTypes_Default()
        {
            var testAsset = "RazorSimpleMvc";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset);
            
            var build = new BuildCommand(projectDirectory);
            build.Execute("/t:_IntrospectUpToDateReloadFileTypes")
                .Should()
                .Pass()
                .And.HaveStdOutContaining("UpToDateReloadFileTypes: ;.cs;.razor;.resx;.cshtml");
        }

        [Fact]
        public void UpToDateReloadFileTypes_WithRuntimeCompilation()
        {
            var testAsset = "RazorSimpleMvc";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset)
                .WithProjectChanges(p =>
                {
                    var ns = p.Root.Name.Namespace;

                    var propertyGroup = new XElement(ns + "PropertyGroup");
                    p.Root.Add(propertyGroup);

                    propertyGroup.Add(new XElement(ns + "RazorUpToDateReloadFileTypes", @"$(RazorUpToDateReloadFileTypes.Replace('.cshtml', ''))"));
                });

            var build = new BuildCommand(projectDirectory);
            build.Execute("/t:_IntrospectUpToDateReloadFileTypes")
                .Should()
                .Pass()
                .And.HaveStdOutContaining("UpToDateReloadFileTypes: ;.cs;.razor;.resx;");
        }

        [Fact]
        public void UpToDateReloadFileTypes_WithwWorkAroundRemoved()
        {
            var testAsset = "RazorSimpleMvc";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset);
            
            var build = new BuildCommand(projectDirectory);
            build.Execute("/t:_IntrospectUpToDateReloadFileTypes")
                .Should()
                .Pass()
                .And.HaveStdOutContaining("UpToDateReloadFileTypes: ;.cs;.razor;.resx;.cshtml");
        }

        [Fact]
        public void UpToDateReloadFileTypes_WithRuntimeCompilationAndWorkaroundRemoved()
        {
            var testAsset = "RazorSimpleMvc";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset)
                .WithProjectChanges(p =>
                {
                    var ns = p.Root.Name.Namespace;

                    var propertyGroup = new XElement(ns + "PropertyGroup");
                    p.Root.Add(propertyGroup);

                    propertyGroup.Add(new XElement(ns + "RazorUpToDateReloadFileTypes", @"$(RazorUpToDateReloadFileTypes.Replace('.cshtml', ''))"));
                });

            var build = new BuildCommand(projectDirectory);
            build.Execute("/t:_IntrospectUpToDateReloadFileTypes", "/p:_RazorUpToDateReloadFileTypesAllowWorkaround=false")
                .Should()
                .Pass()
                .And.HaveStdOutContaining("UpToDateReloadFileTypes: ;.cs;.razor;.resx;");
        }

        [Fact]
        public void IntrospectRazorSdkWatchItems()
        {
            var testAsset = "RazorComponentApp";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset);

            var build = new MSBuildCommand(Log, "_IntrospectWatchItems", projectDirectory.Path);
            build.Execute()
                .Should()
                .Pass()
                .And.HaveStdOutContaining("Watch: Index.razor")
                .And.HaveStdOutContaining("Watch: Index.razor.css");
        }

        [Fact]
        public void IntrospectRazorDesignTimeTargets()
        {
            var expected1 = Path.Combine("Components", "App.razor");
            var expected2 = Path.Combine("Components", "Shared", "MainLayout.razor");
            var testAsset = "RazorComponentApp";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset);

            var build = new MSBuildCommand(Log, "_IntrospectRazorGenerateComponentDesignTime", projectDirectory.Path);
            build.Execute()
                .Should()
                .Pass()
                .And.HaveStdOutContaining($"RazorComponentWithTargetPath: App {expected1}")
                .And.HaveStdOutContaining($"RazorComponentWithTargetPath: MainLayout {expected2}");
        }
    }
}
