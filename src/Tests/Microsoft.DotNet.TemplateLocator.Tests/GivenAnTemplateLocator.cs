// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.DotNetSdkResolver;

namespace Microsoft.DotNet.TemplateLocator.Tests
{
    public class GivenAnTemplateLocator : SdkTest
    {
        private readonly TemplateLocator _resolver;
        private readonly string _manifestDirectory;
        private readonly string _fakeDotnetRootDirectory;

        public GivenAnTemplateLocator(ITestOutputHelper logger) : base(logger)
        {
            _resolver = new TemplateLocator(Environment.GetEnvironmentVariable, null, VSSettings.Ambient, null, null);
            _fakeDotnetRootDirectory =
                Path.Combine(TestContext.Current.TestExecutionDirectory, Path.GetRandomFileName());

            var fakeSdkDirectory = Path.Combine(_fakeDotnetRootDirectory, "sdk", "5.0.102");
            Directory.CreateDirectory(fakeSdkDirectory);
            var fakeRuntimeIdentifierChainPath = Path.Combine(fakeSdkDirectory, "NETCoreSdkRuntimeIdentifierChain.txt");
            File.WriteAllLines(fakeRuntimeIdentifierChainPath,
                               new[] { "win-x64", "win", "any", "base" });

            _manifestDirectory = Path.Combine(_fakeDotnetRootDirectory, "sdk-manifests", "5.0.100");
            Directory.CreateDirectory(_manifestDirectory);
            
        }

        [Fact]
        public void ItShouldReturnListOfTemplates()
        {
            Directory.CreateDirectory(Path.Combine(_manifestDirectory, "Android"));
            File.Copy(Path.Combine("Manifests", "AndroidWorkloadManifest.json"),
                Path.Combine(_manifestDirectory, "Android", "WorkloadManifest.json"));
            // the nupkg need to exist to be considered installed
            string templatePacksDirectory = Path.Combine(_fakeDotnetRootDirectory, "template-packs");
            Directory.CreateDirectory(templatePacksDirectory);
            string templateNupkgPath = Path.Combine(templatePacksDirectory, "xamarin.android.templates.1.0.3.nupkg");
            File.WriteAllText(templateNupkgPath, "");

            var result = _resolver.GetDotnetSdkTemplatePackages("5.0.102", _fakeDotnetRootDirectory, userProfileDir: null);

            result.First().Path.Should().Be(templateNupkgPath);
            result.First().TemplatePackageId.Should().Be("Xamarin.Android.Templates");
            result.First().TemplateVersion.Should().Be("1.0.3");

            result.Should().HaveCount(1);
        }

        [Fact]
        public void GivenNoSdkToBondItShouldReturnEmpty()
        {
            Directory.CreateDirectory(Path.Combine(_manifestDirectory, "Android"));
            File.Copy(Path.Combine("Manifests", "AndroidWorkloadManifest.json"),
                Path.Combine(_manifestDirectory, "Android", "WorkloadManifest.json"));

            var result = _resolver.GetDotnetSdkTemplatePackages("5.1.100", _fakeDotnetRootDirectory, userProfileDir: null);
            result.Should().BeEmpty();
        }

        [Fact]
        public void GivenNoManifestDirectoryItShouldReturnEmpty()
        {
            var fakeDotnetRootDirectory =
                Path.Combine(TestContext.Current.TestExecutionDirectory, Path.GetRandomFileName());
            var result = _resolver.GetDotnetSdkTemplatePackages("5.0.102", fakeDotnetRootDirectory, userProfileDir: null);
            result.Should().BeEmpty();
        }
    }
}
