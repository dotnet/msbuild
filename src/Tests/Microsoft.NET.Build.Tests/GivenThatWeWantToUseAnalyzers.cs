// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToUseAnalyzers : SdkTest
    {
        public GivenThatWeWantToUseAnalyzers(ITestOutputHelper log) : base(log)
        {
        }

        [Theory]
        [InlineData("WebApp", false)]
        [InlineData("WebApp", true)]
        [InlineData("WebApp", null)]
        public void It_resolves_requestdelegategenerator_correctly(string testAssetName, bool? isEnabled)
        {
            var asset = _testAssetsManager
                .CopyTestAsset(testAssetName, identifier: isEnabled.ToString())
                .WithSource()
                .WithProjectChanges(project =>
                {
                    if (isEnabled != null)
                    {
                        var ns = project.Root.Name.Namespace;
                        project.Root.Add(new XElement(ns + "PropertyGroup", new XElement("EnableRequestDelegateGenerator", isEnabled)));
                    }
                });

            VerifyRequestDelegateGeneratorIsUsed(asset, isEnabled);
            VerifyInterceptorsFeatureProperties(asset, isEnabled, "Microsoft.AspNetCore.Http.Generated");
        }

        [Theory]
        [InlineData("WebApp", false)]
        [InlineData("WebApp", true)]
        [InlineData("WebApp", null)]
        public void It_resolves_configbindinggenerator_correctly(string testAssetName, bool? isEnabled)
        {
            var asset = _testAssetsManager
                .CopyTestAsset(testAssetName, identifier: isEnabled.ToString())
                .WithSource()
                .WithProjectChanges(project =>
                {
                    if (isEnabled != null)
                    {
                        var ns = project.Root.Name.Namespace;
                        project.Root.Add(new XElement(ns + "PropertyGroup", new XElement("EnableConfigurationBindingGenerator", isEnabled)));
                    }
                });

            VerifyConfigBindingGeneratorIsUsed(asset, isEnabled);
            VerifyInterceptorsFeatureProperties(asset, isEnabled, "Microsoft.Extensions.Configuration.Binder.SourceGeneration");
        }

        [Fact]
        public void It_enables_requestdelegategenerator_and_configbindinggenerator_for_PublishAot()
        {
            var asset = _testAssetsManager
                .CopyTestAsset("WebApp")
                .WithSource()
                .WithProjectChanges(project =>
                {
                    var ns = project.Root.Name.Namespace;
                    project.Root.Add(new XElement(ns + "PropertyGroup", new XElement("PublishAot", "true")));
                });

            VerifyRequestDelegateGeneratorIsUsed(asset, expectEnabled: true);
            VerifyConfigBindingGeneratorIsUsed(asset, expectEnabled: true);
            VerifyInterceptorsFeatureProperties(asset, expectEnabled: true, "Microsoft.AspNetCore.Http.Generated", "Microsoft.Extensions.Configuration.Binder.SourceGeneration");
        }

        [Fact]
        public void It_enables_requestdelegategenerator_and_configbindinggenerator_for_PublishTrimmed()
        {
            var asset = _testAssetsManager
                .CopyTestAsset("WebApp")
                .WithSource()
                .WithProjectChanges(project =>
                {
                    var ns = project.Root.Name.Namespace;
                    project.Root.Add(new XElement(ns + "PropertyGroup", new XElement("PublishTrimmed", "true")));
                });

            VerifyRequestDelegateGeneratorIsUsed(asset, expectEnabled: true);
            VerifyConfigBindingGeneratorIsUsed(asset, expectEnabled: true);
            VerifyInterceptorsFeatureProperties(asset, expectEnabled: true, "Microsoft.AspNetCore.Http.Generated", "Microsoft.Extensions.Configuration.Binder.SourceGeneration");
        }

        private void VerifyGeneratorIsUsed(TestAsset asset, bool? expectEnabled, string generatorName)
        {
            var command = new GetValuesCommand(
                Log,
                asset.Path,
                ToolsetInfo.CurrentTargetFramework,
                "Analyzer",
                GetValuesCommand.ValueType.Item);

            command
                .WithWorkingDirectory(asset.Path)
                .Execute()
                .Should().Pass();

            var analyzers = command.GetValues();

            Assert.Equal(expectEnabled ?? false, analyzers.Any(analyzer => analyzer.Contains(generatorName)));
        }

        private void VerifyRequestDelegateGeneratorIsUsed(TestAsset asset, bool? expectEnabled)
            => VerifyGeneratorIsUsed(asset, expectEnabled, "Microsoft.AspNetCore.Http.RequestDelegateGenerator.dll");

        private void VerifyConfigBindingGeneratorIsUsed(TestAsset asset, bool? expectEnabled)
            => VerifyGeneratorIsUsed(asset, expectEnabled, "Microsoft.Extensions.Configuration.Binder.SourceGeneration.dll");

        private void VerifyInterceptorsFeatureProperties(TestAsset asset, bool? expectEnabled, params string[] expectedNamespaces)
        {
            var command = new GetValuesCommand(
                Log,
                asset.Path,
                ToolsetInfo.CurrentTargetFramework,
                "InterceptorsPreviewNamespaces",
                GetValuesCommand.ValueType.Property);

            command
                .WithWorkingDirectory(asset.Path)
                .Execute()
                .Should().Pass();

            var namespaces = command.GetValues();

            Assert.Equal(expectEnabled ?? false, expectedNamespaces.All(expectedNamespace => namespaces.Contains(expectedNamespace)));
        }

        [Theory]
        [InlineData("C#", "AppWithLibrary")]
        [InlineData("VB", "AppWithLibraryVB")]
        [InlineData("F#", "AppWithLibraryFS")]
        public void It_resolves_analyzers_correctly(string language, string testAssetName)
        {
            var asset = _testAssetsManager
                .CopyTestAsset(testAssetName, identifier: language)
                .WithSource()
                .WithProjectChanges(project =>
                {
                    var ns = project.Root.Name.Namespace;
                    project.Root.Add(
                        new XElement(ns + "ItemGroup",
                            new XElement(ns + "PackageReference",
                                new XAttribute("Include", "Microsoft.DependencyValidation.Analyzers"),
                                new XAttribute("Version", "0.9.0")),
                            new XElement(ns + "PackageReference",
                                new XAttribute("Include", "Microsoft.CodeQuality.Analyzers"),
                                new XAttribute("Version", "2.6.0"))));
                });

            var command = new GetValuesCommand(
                Log,
                Path.Combine(asset.Path, "TestApp"),
                ToolsetInfo.CurrentTargetFramework,
                "Analyzer",
                GetValuesCommand.ValueType.Item);

            command
                .WithWorkingDirectory(asset.Path)
                .Execute()
                .Should().Pass();

            var analyzers = command.GetValues();

            switch (language)
            {
                case "C#":
                    analyzers.Select(x => GetPackageAndPath(x)).Should().BeEquivalentTo(new[]
                            {
                                ("microsoft.net.sdk", (string) null, "analyzers/Microsoft.CodeAnalysis.CSharp.NetAnalyzers.dll"),
                                ("microsoft.net.sdk", (string)null, "analyzers/Microsoft.CodeAnalysis.NetAnalyzers.dll"),
                                ("microsoft.netcore.app.ref", (string)null, "analyzers/dotnet/cs/System.Text.Json.SourceGeneration.dll"),
                                ("microsoft.netcore.app.ref", (string)null, "analyzers/dotnet/cs/System.Text.RegularExpressions.Generator.dll"),
                                ("microsoft.codequality.analyzers", "2.6.0", "analyzers/dotnet/cs/Microsoft.CodeQuality.Analyzers.dll"),
                                ("microsoft.codequality.analyzers", "2.6.0", "analyzers/dotnet/cs/Microsoft.CodeQuality.CSharp.Analyzers.dll"),
                                ("microsoft.dependencyvalidation.analyzers", "0.9.0", "analyzers/dotnet/Microsoft.DependencyValidation.Analyzers.dll"),
                                ("microsoft.netcore.app.ref", (string)null, "analyzers/dotnet/cs/Microsoft.Interop.LibraryImportGenerator.dll"),
                                ("microsoft.netcore.app.ref", (string)null, "analyzers/dotnet/cs/Microsoft.Interop.JavaScript.JSImportGenerator.dll"),
                                ("microsoft.netcore.app.ref", (string)null, "analyzers/dotnet/cs/Microsoft.Interop.SourceGeneration.dll"),
                                ("microsoft.netcore.app.ref", (string)null, "analyzers/dotnet/cs/Microsoft.Interop.ComInterfaceGenerator.dll")
                            }
                        );
                    break;

                case "VB":
                    analyzers.Select(x => GetPackageAndPath(x)).Should().BeEquivalentTo( new[]
                        {
                            ("microsoft.net.sdk", (string)null, "analyzers/Microsoft.CodeAnalysis.VisualBasic.NetAnalyzers.dll"),
                            ("microsoft.net.sdk", (string)null, "analyzers/Microsoft.CodeAnalysis.NetAnalyzers.dll"),
                            ("microsoft.codequality.analyzers", "2.6.0", "analyzers/dotnet/vb/Microsoft.CodeQuality.Analyzers.dll"),
                            ("microsoft.codequality.analyzers", "2.6.0", "analyzers/dotnet/vb/Microsoft.CodeQuality.VisualBasic.Analyzers.dll"),
                            ("microsoft.dependencyvalidation.analyzers", "0.9.0", "analyzers/dotnet/Microsoft.DependencyValidation.Analyzers.dll")
                        }
                        );
                    break;

                case "F#":
                    analyzers.Should().BeEmpty();
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(language));
            }
        }

        [Fact]
        public void It_resolves_multitargeted_analyzers()
        {
            var testProject = new TestProject()
            {
                TargetFrameworks = $"{ToolsetInfo.CurrentTargetFramework};net472"
            };

            //  Disable analyzers built in to the SDK so we can more easily test the ones coming from NuGet packages
            testProject.AdditionalProperties["EnableNETAnalyzers"] = "false";

            testProject.ProjectChanges.Add(project =>
            {
                var ns = project.Root.Name.Namespace;

                var itemGroup = XElement.Parse($@"
  <ItemGroup>
    <PackageReference Include=""System.Text.Json"" Version=""4.7.0"" Condition="" '$(TargetFramework)' == 'net472' "" />
    <PackageReference Include=""System.Text.Json"" Version=""6.0.0-preview.4.21253.7"" Condition="" '$(TargetFramework)' == '{ToolsetInfo.CurrentTargetFramework}' "" />
  </ItemGroup>");

                project.Root.Add(itemGroup);
            });

            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            List<(string package, string version, string path)> GetAnalyzersForTargetFramework(string targetFramework)
            {
                var getValuesCommand = new GetValuesCommand(testAsset,
                    valueName: "Analyzer",
                    GetValuesCommand.ValueType.Item,
                    targetFramework);

                getValuesCommand.DependsOnTargets = "ResolveLockFileAnalyzers";

                getValuesCommand.Execute("-p:TargetFramework=" + targetFramework).Should().Pass();

                return getValuesCommand.GetValues().Select(x => GetPackageAndPath(x)).ToList();
            }

            GetAnalyzersForTargetFramework(ToolsetInfo.CurrentTargetFramework).Should().BeEquivalentTo(new[] { ("system.text.json", "6.0.0-preview.4.21253.7", "analyzers/dotnet/cs/System.Text.Json.SourceGeneration.dll") });
            GetAnalyzersForTargetFramework("net472").Should().BeEmpty();
        }

        static readonly List<string> nugetRoots = new List<string>()
            {
                TestContext.Current.NuGetCachePath,
                Path.Combine(FileConstants.UserProfileFolder, ".dotnet", "NuGetFallbackFolder"),
                Path.Combine(TestContext.Current.ToolsetUnderTest.DotNetRoot, "packs")
            };

        static (string package, string version, string path) GetPackageAndPath(string absolutePath)
        {
            absolutePath = Path.GetFullPath(absolutePath);

            if (absolutePath.StartsWith(TestContext.Current.ToolsetUnderTest.SdksPath))
            {
                string path = absolutePath.Substring(TestContext.Current.ToolsetUnderTest.SdksPath.Length + 1)
                    .Replace(Path.DirectorySeparatorChar, '/');
                var components = path.Split(new char[] { '/' }, 2);
                string sdkName = components[0];
                string pathInSdk = components[1];
                return (sdkName.ToLowerInvariant(), null, pathInSdk);
            }

            foreach (var nugetRoot in nugetRoots)
            {
                if (absolutePath.StartsWith(nugetRoot + Path.DirectorySeparatorChar))
                {
                    string path = absolutePath.Substring(nugetRoot.Length + 1)
                        .Replace(Path.DirectorySeparatorChar, '/');
                    var components = path.Split(new char[] { '/' }, 3);
                    var packageName = components[0];
                    var packageVersion = components[1];
                    var pathInPackage = components[2];
                    //  Don't check package version for analyzers included in targeting pack, as the version changes during development
                    if (packageName.Equals("microsoft.netcore.app.ref", StringComparison.OrdinalIgnoreCase))
                    {
                        packageVersion = null;
                    }
                    return (packageName.ToLowerInvariant(), packageVersion, pathInPackage);
                }
            }

            throw new InvalidDataException("Expected path to be under a known root: " + absolutePath);
        }
    }
}
