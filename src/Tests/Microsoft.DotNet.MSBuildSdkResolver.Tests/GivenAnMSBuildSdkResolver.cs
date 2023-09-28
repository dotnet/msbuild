// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

extern alias sdkResolver;
using System.Runtime.CompilerServices;
using Microsoft.Build.Framework;
using Microsoft.DotNet.DotNetSdkResolver;
using Microsoft.DotNet.MSBuildSdkResolver;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Microsoft.DotNet.Cli.Utils.Tests
{
    public class GivenAnMSBuildSdkResolver : SdkTest
    {

        public GivenAnMSBuildSdkResolver(ITestOutputHelper logger) : base(logger)
        {
        }

        [Fact]
        public void ItHasCorrectNameAndPriority()
        {
            var resolver = new DotNetMSBuildSdkResolver();

            Assert.Equal(5000, resolver.Priority);
            Assert.Equal("Microsoft.DotNet.MSBuildSdkResolver", resolver.Name);
        }

        [Fact]
        public void ItDoesNotFindMSBuildSdkThatIsMissingFromLocatedNETCoreSdk()
        {
            var environment = new TestEnvironment(_testAssetsManager);
            var expected = environment.CreateSdkDirectory(ProgramFiles.X64, "Some.Test.Sdk", "99.99.97");
            environment.CreateMuxerAndAddToPath(ProgramFiles.X64);

            var resolver = environment.CreateResolver();
            var result = (MockResult)resolver.Resolve(
                new SdkReference("Some.Test.SdkThatDoesNotExist", null, null),
                new MockContext { ProjectFileDirectory = environment.TestDirectory },
                new MockFactory());

            result.Success.Should().BeFalse();
            result.Path.Should().BeNull();
            result.AdditionalPaths.Should().BeNull();
            result.Version.Should().BeNull();
            result.Warnings.Should().BeNullOrEmpty();
            result.Errors.Should().NotBeEmpty();
        }

        [Fact]
        public void ItFindsTheVersionSpecifiedInGlobalJson()
        {
            var environment = new TestEnvironment(_testAssetsManager);
            environment.CreateSdkDirectory(ProgramFiles.X64, "Some.Test.Sdk", "99.99.97");
            var expected = environment.CreateSdkDirectory(ProgramFiles.X64, "Some.Test.Sdk", "99.99.98");
            environment.CreateSdkDirectory(ProgramFiles.X64, "Some.Test.Sdk", "99.99.99");
            environment.CreateGlobalJson(environment.TestDirectory, "99.99.98");
            environment.CreateMuxerAndAddToPath(ProgramFiles.X64);

            var resolver = environment.CreateResolver();
            var result = (MockResult)resolver.Resolve(
                new SdkReference("Some.Test.Sdk", null, null),
                new MockContext { ProjectFileDirectory = environment.TestDirectory },
                new MockFactory());

            result.Success.Should().BeTrue($"No error expected. Error encountered: {string.Join(Environment.NewLine, result.Errors ?? new string[] { })}. Mocked Process Path: {environment.ProcessPath}. Mocked Path: {environment.PathEnvironmentVariable}");
            result.Path.Should().Be(expected.FullName);
            result.AdditionalPaths.Should().BeNull();
            result.Version.Should().Be("99.99.98");
            result.Warnings.Should().BeNullOrEmpty();
            result.Errors.Should().BeNullOrEmpty();
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void ItUsesProjectDirectoryIfSolutionFilePathIsNullOrWhitespace(string solutionFilePath)
        {
            const string version = "99.0.0";

            var environment = new TestEnvironment(_testAssetsManager, identifier: solutionFilePath ?? "NULL");
            environment.CreateSdkDirectory(ProgramFiles.X64, "Some.Test.Sdk", version);
            environment.CreateMuxerAndAddToPath(ProgramFiles.X64);

            var resolver = environment.CreateResolver();
            var result = (MockResult)resolver.Resolve(
                new SdkReference("Some.Test.Sdk", null, version),
                new MockContext { ProjectFileDirectory = environment.TestDirectory, SolutionFilePath = solutionFilePath },
                new MockFactory());

            result.Success.Should().BeTrue($"No error expected. Error encountered: {string.Join(Environment.NewLine, result.Errors ?? new string[] { })}. Mocked Process Path: {environment.ProcessPath}. Mocked Path: {environment.PathEnvironmentVariable}");
            result.Path.Should().StartWith(environment.TestDirectory.FullName);
            result.AdditionalPaths.Should().BeNull();
            result.Version.Should().Be(version);
            result.Warnings.Should().BeNullOrEmpty();
            result.Errors.Should().BeNullOrEmpty();
        }

        [Theory]
        [InlineData(null, null)]
        [InlineData("", null)]
        [InlineData("", "")]
        [InlineData(null, "")]
        public void ItUsesCurrentDirectoryIfSolutionFilePathAndProjectFilePathIsNullOrWhitespace(string solutionFilePath, string projectFilePath)
        {
            const string version = "99.0.0";

            var environment = new TestEnvironment(_testAssetsManager, identifier: $"{solutionFilePath ?? "NULL"}-{projectFilePath ?? "NULL"}");
            environment.CreateSdkDirectory(ProgramFiles.X64, "Some.Test.Sdk", version);
            environment.CreateMuxerAndAddToPath(ProgramFiles.X64);

            var resolver = environment.CreateResolver();
            var result = (MockResult)resolver.Resolve(
                new SdkReference("Some.Test.Sdk", null, version),
                new MockContext { ProjectFilePath = projectFilePath, SolutionFilePath = solutionFilePath },
                new MockFactory());

            result.Success.Should().BeTrue($"No error expected. Error encountered: {string.Join(Environment.NewLine, result.Errors ?? new string[] { })}. Mocked Process Path: {environment.ProcessPath}. Mocked Path: {environment.PathEnvironmentVariable}");
            result.Path.Should().StartWith(environment.TestDirectory.FullName);
            result.AdditionalPaths.Should().BeNull();
            result.Version.Should().Be(version);
            result.Warnings.Should().BeNullOrEmpty();
            result.Errors.Should().BeNullOrEmpty();
        }

        [Fact]
        public void ItReturnsNullIfTheVersionFoundDoesNotSatisfyTheMinVersion()
        {
            var environment = new TestEnvironment(_testAssetsManager);
            environment.CreateSdkDirectory(ProgramFiles.X64, "Some.Test.Sdk", "99.99.99");
            environment.CreateMuxerAndAddToPath(ProgramFiles.X64);

            var resolver = environment.CreateResolver();
            var result = (MockResult)resolver.Resolve(
                new SdkReference("Some.Test.Sdk", null, "999.99.99"),
                new MockContext { ProjectFileDirectory = environment.TestDirectory },
                new MockFactory());

            result.Success.Should().BeFalse();
            result.Path.Should().BeNull();
            result.AdditionalPaths.Should().BeNull();
            result.Version.Should().BeNull();
            result.Warnings.Should().BeNullOrEmpty();
            result.Errors.Should().Contain(string.Format(Strings.NETCoreSDKSmallerThanMinimumRequestedVersion, "99.99.99", "999.99.99"));
        }

        [Fact]
        public void ItReturnsNullWhenTheSDKRequiresAHigherVersionOfMSBuildThanAnyOneAvailable()
        {
            var environment = new TestEnvironment(_testAssetsManager);
            var expected =
                environment.CreateSdkDirectory(ProgramFiles.X64, "Some.Test.Sdk", "99.99.99", new Version(2, 0));
            environment.CreateMuxerAndAddToPath(ProgramFiles.X64);

            var resolver = environment.CreateResolver();
            var result = (MockResult)resolver.Resolve(
                new SdkReference("Some.Test.Sdk", null, "99.99.99"),
                new MockContext
                {
                    MSBuildVersion = new Version(1, 0),
                    ProjectFileDirectory = environment.TestDirectory
                },
                new MockFactory());

            result.Success.Should().BeFalse();
            result.Path.Should().BeNull();
            result.AdditionalPaths.Should().BeNull();
            result.Version.Should().BeNull();
            result.Warnings.Should().BeNullOrEmpty();
            result.Errors.Should().Contain(string.Format(Strings.MSBuildSmallerThanMinimumVersion, "99.99.99", "2.0", "1.0"));
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void ItReturnsHighestSdkAvailableThatIsCompatibleWithMSBuild(bool disallowPreviews)
        {
            var environment = new TestEnvironment(_testAssetsManager, identifier: disallowPreviews.ToString())
            {
                DisallowPrereleaseByDefault = disallowPreviews
            };

            var compatibleRtm = environment.CreateSdkDirectory(ProgramFiles.X64, "Some.Test.Sdk", "98.98.98", new Version(19, 0, 0, 0));
            var compatiblePreview = environment.CreateSdkDirectory(ProgramFiles.X64, "Some.Test.Sdk", "99.99.99-preview", new Version(20, 0, 0, 0));
            var incompatible = environment.CreateSdkDirectory(ProgramFiles.X64, "Some.Test.Sdk", "100.100.100", new Version(21, 0, 0, 0));

            environment.CreateMuxerAndAddToPath(ProgramFiles.X64);

            var resolver = environment.CreateResolver();
            var result = (MockResult)resolver.Resolve(
                new SdkReference("Some.Test.Sdk", null, null),
                new MockContext
                {
                    MSBuildVersion = new Version(20, 0, 0, 0),
                    ProjectFileDirectory = environment.TestDirectory,
                },
                new MockFactory());

            result.Success.Should().BeTrue($"No error expected. Error encountered: {string.Join(Environment.NewLine, result.Errors ?? new string[] { })}. Mocked Process Path: {environment.ProcessPath}. Mocked Path: {environment.PathEnvironmentVariable}");
            result.Path.Should().Be((disallowPreviews ? compatibleRtm : compatiblePreview).FullName);
            result.AdditionalPaths.Should().BeNull();
            result.PropertiesToAdd.Should().BeNull();
            result.Version.Should().Be(disallowPreviews ? "98.98.98" : "99.99.99-preview");
            result.Warnings.Should().BeNullOrEmpty();
            result.Errors.Should().BeNullOrEmpty();
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void ItDoesNotReturnHighestSdkAvailableThatIsCompatibleWithMSBuildWhenVersionInGlobalJsonCannotBeFoundOutsideOfVisualStudio(bool disallowPreviews)
        {
            var environment = new TestEnvironment(_testAssetsManager, callingMethod: "ItDoesNotReturnHighest___", identifier: disallowPreviews.ToString())
            {
                DisallowPrereleaseByDefault = disallowPreviews
            };

            var compatibleRtm = environment.CreateSdkDirectory(ProgramFiles.X64, "Some.Test.Sdk", "98.98.98", new Version(19, 0, 0, 0));
            var compatiblePreview = environment.CreateSdkDirectory(ProgramFiles.X64, "Some.Test.Sdk", "99.99.99-preview", new Version(20, 0, 0, 0));
            var incompatible = environment.CreateSdkDirectory(ProgramFiles.X64, "Some.Test.Sdk", "100.100.100", new Version(21, 0, 0, 0));

            environment.CreateMuxerAndAddToPath(ProgramFiles.X64);
            environment.CreateGlobalJson(environment.TestDirectory, "1.2.3");

            var resolver = environment.CreateResolver();
            var result = (MockResult)resolver.Resolve(
                new SdkReference("Some.Test.Sdk", null, null),
                new MockContext
                {
                    MSBuildVersion = new Version(20, 0, 0, 0),
                    ProjectFileDirectory = environment.TestDirectory,
                    IsRunningInVisualStudio = false
                },
                new MockFactory());

            result.Success.Should().BeFalse();
            result.Path.Should().BeNull();
            result.AdditionalPaths.Should().BeNull();
            result.PropertiesToAdd.Should().BeNull();
            result.Version.Should().BeNull();
            result.Warnings.Should().BeNullOrEmpty();
            result.Errors.Should().NotBeEmpty();
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void ItReturnsHighestSdkAvailableThatIsCompatibleWithMSBuildWhenVersionInGlobalJsonCannotBeFoundAndRunningInVisualStudio(bool disallowPreviews)
        {
            var environment = new TestEnvironment(_testAssetsManager, callingMethod: "ItReturnsHighest___", identifier: disallowPreviews.ToString())
            {
                DisallowPrereleaseByDefault = disallowPreviews
            };

            var compatibleRtm = environment.CreateSdkDirectory(ProgramFiles.X64, "Some.Test.Sdk", "98.98.98", new Version(19, 0, 0, 0));
            var compatiblePreview = environment.CreateSdkDirectory(ProgramFiles.X64, "Some.Test.Sdk", "99.99.99-preview", new Version(20, 0, 0, 0));
            var incompatible = environment.CreateSdkDirectory(ProgramFiles.X64, "Some.Test.Sdk", "100.100.100", new Version(21, 0, 0, 0));

            environment.CreateMuxerAndAddToPath(ProgramFiles.X64);
            environment.CreateGlobalJson(environment.TestDirectory, "1.2.3");

            var resolver = environment.CreateResolver();
            var result = (MockResult)resolver.Resolve(
                new SdkReference("Some.Test.Sdk", null, null),
                new MockContext
                {
                    MSBuildVersion = new Version(20, 0, 0, 0),
                    ProjectFileDirectory = environment.TestDirectory,
                    IsRunningInVisualStudio = true
                },
                new MockFactory());

            result.Success.Should().BeTrue($"No error expected. Error encountered: {string.Join(Environment.NewLine, result.Errors ?? new string[] { })}. Mocked Process Path: {environment.ProcessPath}. Mocked Path: {environment.PathEnvironmentVariable}");
            result.Path.Should().Be((disallowPreviews ? compatibleRtm : compatiblePreview).FullName);
            result.AdditionalPaths.Should().BeNull();
            result.PropertiesToAdd.Count.Should().Be(2);
            result.PropertiesToAdd.ContainsKey("SdkResolverHonoredGlobalJson");
            result.PropertiesToAdd.ContainsKey("SdkResolverGlobalJsonPath");
            result.PropertiesToAdd["SdkResolverHonoredGlobalJson"].Should().Be("false");
            result.Version.Should().Be(disallowPreviews ? "98.98.98" : "99.99.99-preview");
            result.Warnings.Should().BeEquivalentTo(new[] { "Unable to locate the .NET SDK version '1.2.3' as specified by global.json, please check that the specified version is installed." });
            result.Errors.Should().BeNullOrEmpty();
        }

        [Fact]
        public void ItReturnsNullWhenTheDefaultVSRequiredSDKVersionIsHigherThanTheSDKVersionAvailable()
        {
            var environment = new TestEnvironment(_testAssetsManager);
            var expected =
                environment.CreateSdkDirectory(ProgramFiles.X64, "Some.Test.Sdk", "1.0.1");
            environment.CreateMuxerAndAddToPath(ProgramFiles.X64);

            var resolver = environment.CreateResolver();
            var result = (MockResult)resolver.Resolve(
                new SdkReference("Some.Test.Sdk", null, "1.0.0"),
                new MockContext { ProjectFileDirectory = environment.TestDirectory },
                new MockFactory());

            result.Success.Should().BeFalse();
            result.Path.Should().BeNull();
            result.AdditionalPaths.Should().BeNull();
            result.Version.Should().BeNull();
            result.Warnings.Should().BeNullOrEmpty();
            result.Errors.Should().Contain(string.Format(Strings.NETCoreSDKSmallerThanMinimumVersionRequiredByVisualStudio, "1.0.1", "1.0.4"));
        }

        [Fact]
        public void ItReturnsNullWhenTheTheVSRequiredSDKVersionIsHigherThanTheSDKVersionAvailable()
        {
            var environment = new TestEnvironment(_testAssetsManager);
            var expected =
                environment.CreateSdkDirectory(ProgramFiles.X64, "Some.Test.Sdk", "1.0.1");
            environment.CreateMuxerAndAddToPath(ProgramFiles.X64);
            environment.CreateMinimumVSDefinedSDKVersionFile("2.0.0");

            var resolver = environment.CreateResolver();
            var result = (MockResult)resolver.Resolve(
                new SdkReference("Some.Test.Sdk", null, "1.0.0"),
                new MockContext { ProjectFileDirectory = environment.TestDirectory },
                new MockFactory());

            result.Success.Should().BeFalse();
            result.Path.Should().BeNull();
            result.AdditionalPaths.Should().BeNull();
            result.Version.Should().BeNull();
            result.Warnings.Should().BeNullOrEmpty();
            result.Errors.Should().Contain(string.Format(Strings.NETCoreSDKSmallerThanMinimumVersionRequiredByVisualStudio, "1.0.1", "2.0.0"));
        }

        [Fact]
        public void ItReturnsTheVersionIfItIsEqualToTheMinVersionAndTheVSDefinedMinVersion()
        {
            var environment = new TestEnvironment(_testAssetsManager);
            var expected = environment.CreateSdkDirectory(ProgramFiles.X64, "Some.Test.Sdk", "99.99.99");
            environment.CreateMuxerAndAddToPath(ProgramFiles.X64);
            environment.CreateMinimumVSDefinedSDKVersionFile("99.99.99");

            var resolver = environment.CreateResolver();
            var result = (MockResult)resolver.Resolve(
                new SdkReference("Some.Test.Sdk", null, "99.99.99"),
                new MockContext { ProjectFileDirectory = environment.TestDirectory },
                new MockFactory());

            result.Success.Should().BeTrue($"No error expected. Error encountered: {string.Join(Environment.NewLine, result.Errors ?? new string[] { })}. Mocked Process Path: {environment.ProcessPath}. Mocked Path: {environment.PathEnvironmentVariable}");
            result.Path.Should().Be(expected.FullName);
            result.AdditionalPaths.Should().BeNull();
            result.Version.Should().Be("99.99.99");
            result.Warnings.Should().BeNullOrEmpty();
            result.Errors.Should().BeNullOrEmpty();
        }

        [Fact]
        public void ItReturnsTheVersionIfItIsHigherThanTheMinVersionAndTheVSDefinedMinVersion()
        {
            var environment = new TestEnvironment(_testAssetsManager);
            var expected = environment.CreateSdkDirectory(ProgramFiles.X64, "Some.Test.Sdk", "999.99.99");
            environment.CreateMuxerAndAddToPath(ProgramFiles.X64);
            environment.CreateMinimumVSDefinedSDKVersionFile("999.99.98");

            var resolver = environment.CreateResolver();
            var result = (MockResult)resolver.Resolve(
                new SdkReference("Some.Test.Sdk", null, "99.99.99"),
                new MockContext { ProjectFileDirectory = environment.TestDirectory },
                new MockFactory());

            result.Success.Should().BeTrue($"No error expected. Error encountered: {string.Join(Environment.NewLine, result.Errors ?? new string[] { })}. Mocked Process Path: {environment.ProcessPath}. Mocked Path: {environment.PathEnvironmentVariable}");
            result.Path.Should().Be(expected.FullName);
            result.AdditionalPaths.Should().BeNull();
            result.Version.Should().Be("999.99.99");
            result.Warnings.Should().BeNullOrEmpty();
            result.Errors.Should().BeNullOrEmpty();
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void ItDisallowsPreviewsBasedOnDefault(bool disallowPreviewsByDefault)
        {
            var environment = new TestEnvironment(_testAssetsManager, identifier: disallowPreviewsByDefault.ToString());
            var rtm = environment.CreateSdkDirectory(ProgramFiles.X64, "Some.Test.Sdk", "10.0.0");
            var preview = environment.CreateSdkDirectory(ProgramFiles.X64, "Some.Test.Sdk", "11.0.0-preview1");
            var expected = disallowPreviewsByDefault ? rtm : preview;

            environment.CreateMuxerAndAddToPath(ProgramFiles.X64);
            environment.DisallowPrereleaseByDefault = disallowPreviewsByDefault;

            var resolver = environment.CreateResolver();
            var result = (MockResult)resolver.Resolve(
                new SdkReference("Some.Test.Sdk", null, null),
                new MockContext { ProjectFileDirectory = environment.TestDirectory },
                new MockFactory());

            result.Success.Should().BeTrue($"No error expected. Error encountered: {string.Join(Environment.NewLine, result.Errors ?? new string[] { })}. Mocked Process Path: {environment.ProcessPath}. Mocked Path: {environment.PathEnvironmentVariable}");
            result.Path.Should().Be(expected.FullName);
            result.AdditionalPaths.Should().BeNull();
            result.Version.Should().Be(disallowPreviewsByDefault ? "10.0.0" : "11.0.0-preview1");
            result.Warnings.Should().BeNullOrEmpty();
            result.Errors.Should().BeNullOrEmpty();
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void ItDisallowsPreviewsBasedOnFile(bool disallowPreviews)
        {
            var environment = new TestEnvironment(_testAssetsManager, identifier: disallowPreviews.ToString());
            var rtm = environment.CreateSdkDirectory(ProgramFiles.X64, "Some.Test.Sdk", "10.0.0");
            var preview = environment.CreateSdkDirectory(ProgramFiles.X64, "Some.Test.Sdk", "11.0.0-preview1");
            var expected = disallowPreviews ? rtm : preview;

            environment.CreateMuxerAndAddToPath(ProgramFiles.X64);
            environment.DisallowPrereleaseByDefault = !disallowPreviews;
            environment.CreateVSSettingsFile(disallowPreviews);

            var resolver = environment.CreateResolver();
            var result = (MockResult)resolver.Resolve(
                new SdkReference("Some.Test.Sdk", null, null),
                new MockContext { ProjectFileDirectory = environment.TestDirectory },
                new MockFactory());

            result.Success.Should().BeTrue($"No error expected. Error encountered: {string.Join(Environment.NewLine, result.Errors ?? new string[] { })}. Mocked Process Path: {environment.ProcessPath}. Mocked Path: {environment.PathEnvironmentVariable}");
            result.Path.Should().Be(expected.FullName);
            result.AdditionalPaths.Should().BeNull();
            result.Version.Should().Be(disallowPreviews ? "10.0.0" : "11.0.0-preview1");
            result.Warnings.Should().BeNullOrEmpty();
            result.Errors.Should().BeNullOrEmpty();
        }

        [Fact]
        public void ItObservesChangesToVSSettingsFile()
        {
            var environment = new TestEnvironment(_testAssetsManager);
            var rtm = environment.CreateSdkDirectory(ProgramFiles.X64, "Some.Test.Sdk", "10.0.0");
            var preview = environment.CreateSdkDirectory(ProgramFiles.X64, "Some.Test.Sdk", "11.0.0-preview1");

            environment.CreateMuxerAndAddToPath(ProgramFiles.X64);
            environment.CreateVSSettingsFile(disallowPreviews: true);
            var resolver = environment.CreateResolver();

            void Check(bool disallowPreviews, string message)
            {
                // check twice because file-up-to-date is a separate code path
                for (int i = 0; i < 2; i++)
                {

                    var result = (MockResult)resolver.Resolve(
                        new SdkReference("Some.Test.Sdk", null, null),
                        new MockContext { ProjectFileDirectory = environment.TestDirectory },
                        new MockFactory());

                    string m = $"{message} ({i})";
                    var expected = disallowPreviews ? rtm : preview;
                    result.Success.Should().BeTrue(m);
                    result.Path.Should().Be(expected.FullName, m);
                    result.AdditionalPaths.Should().BeNull();
                    result.Version.Should().Be(disallowPreviews ? "10.0.0" : "11.0.0-preview1", m);
                    result.Warnings.Should().BeNullOrEmpty(m);
                    result.Errors.Should().BeNullOrEmpty(m);
                }
            }

            environment.DeleteVSSettingsFile();
            Check(disallowPreviews: false, message: "default with no file");

            environment.CreateVSSettingsFile(disallowPreviews: true);
            Check(disallowPreviews: true, message: "file changed to disallow previews");

            environment.CreateVSSettingsFile(disallowPreviews: false);
            Check(disallowPreviews: false, message: "file changed to not disallow previews");

            environment.CreateVSSettingsFile(disallowPreviews: true);
            Check(disallowPreviews: true, message: "file changed back to disallow previews");

            environment.DeleteVSSettingsFile();
            Check(disallowPreviews: false, message: "file deleted to return to default");
        }

        [Fact]
        public void ItAllowsPreviewWhenGlobalJsonHasPreviewIrrespectiveOfSetting()
        {
            var environment = new TestEnvironment(_testAssetsManager);
            var rtm = environment.CreateSdkDirectory(ProgramFiles.X64, "Some.Test.Sdk", "10.0.0");
            var preview = environment.CreateSdkDirectory(ProgramFiles.X64, "Some.Test.Sdk", "11.0.0-preview1");

            environment.CreateMuxerAndAddToPath(ProgramFiles.X64);
            environment.DisallowPrereleaseByDefault = true;
            environment.CreateGlobalJson(environment.TestDirectory, "11.0.0-preview1");

            var resolver = environment.CreateResolver();
            var result = (MockResult)resolver.Resolve(
                new SdkReference("Some.Test.Sdk", null, null),
                new MockContext { ProjectFileDirectory = environment.TestDirectory },
                new MockFactory());

            result.Success.Should().BeTrue($"No error expected. Error encountered: {string.Join(Environment.NewLine, result.Errors ?? new string[] { })}. Mocked Process Path: {environment.ProcessPath}. Mocked Path: {environment.PathEnvironmentVariable}");
            result.Path.Should().Be(preview.FullName);
            result.AdditionalPaths.Should().BeNull();
            result.Version.Should().Be("11.0.0-preview1");
            result.Warnings.Should().BeNullOrEmpty();
            result.Errors.Should().BeNullOrEmpty();
        }

        [Fact]
        public void ItRespectsAmbientVSSettings()
        {
            // When run in test explorer in VS, this will actually locate the settings for the current VS instance
            // based on location of testhost executable. This gives us some coverage threw that path but we cannot
            // fix our expectations since the behavior will vary (by design) based on the current VS instance's settings.
            var vsSettings = VSSettings.Ambient;

            var environment = new TestEnvironment(_testAssetsManager);
            var rtm = environment.CreateSdkDirectory(ProgramFiles.X64, "Some.Test.Sdk", "10.0.0");
            var preview = environment.CreateSdkDirectory(ProgramFiles.X64, "Some.Test.Sdk", "11.0.0-preview1");
            var expected = vsSettings.DisallowPrerelease() ? rtm : preview;

            environment.CreateMuxerAndAddToPath(ProgramFiles.X64);

            var resolver = environment.CreateResolver(useAmbientSettings: true);
            var result = (MockResult)resolver.Resolve(
                new SdkReference("Some.Test.Sdk", null, null),
                new MockContext { ProjectFileDirectory = environment.TestDirectory },
                new MockFactory());

            result.Success.Should().BeTrue($"No error expected. Error encountered: {string.Join(Environment.NewLine, result.Errors ?? new string[] { })}. Mocked Process Path: {environment.ProcessPath}. Mocked Path: {environment.PathEnvironmentVariable}");
            result.Path.Should().Be(expected.FullName);
            result.AdditionalPaths.Should().BeNull();
            result.Version.Should().Be(vsSettings.DisallowPrerelease() ? "10.0.0" : "11.0.0-preview1");
            result.Warnings.Should().BeNullOrEmpty();
            result.Errors.Should().BeNullOrEmpty();
        }

        [Fact]
        public void GivenTemplateLocatorItCanResolveSdkVersion()
        {
            var environment = new TestEnvironment(_testAssetsManager);
            const string sdkVersion = "99.99.97";
            environment.CreateSdkDirectory(ProgramFiles.X64, "Some.Test.Sdk", sdkVersion);
            environment.CreateMuxerAndAddToPath(ProgramFiles.X64);

            var resolver = new TemplateLocator.TemplateLocator(
                environment.GetEnvironmentVariable,
                () => environment.ProcessPath,
                new sdkResolver::Microsoft.DotNet.DotNetSdkResolver.VSSettings(environment.VSSettingsFile?.FullName, environment.DisallowPrereleaseByDefault), null, null);
            resolver.TryGetDotnetSdkVersionUsedInVs("15.8", out var version).Should().BeTrue();

            version.Should().Be(sdkVersion);
        }

        private enum ProgramFiles
        {
            X64,
            X86,
            Default,
        }

        private sealed class TestEnvironment : SdkResolverContext
        {
            public string Muxer => RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "dotnet.exe" : "dotnet";

            public string PathEnvironmentVariable { get; set; }

            public string ProcessPath { get; set; }

            public DirectoryInfo TestDirectory { get; }
            public FileInfo VSSettingsFile { get; set; }
            public bool DisallowPrereleaseByDefault { get; set; }

            public TestEnvironment(TestAssetsManager testAssets, string identifier = "", [CallerMemberName] string callingMethod = "")
            {
                TestDirectory = new DirectoryInfo(testAssets.CreateTestDirectory(
                    //  Add FrameworkDescription to identifier so that instances of tests running on different frameworks
                    //  (.NET Core vs .NET Framework) don't conflict
                    identifier: identifier + RuntimeInformation.FrameworkDescription,
                    testName: callingMethod).Path);

                DeleteMinimumVSDefinedSDKVersionFile();

                PathEnvironmentVariable = string.Empty;
            }

            public SdkResolver CreateResolver(bool useAmbientSettings = false)
                => new DotNetMSBuildSdkResolver(
                    GetEnvironmentVariable,
                    // force current executable location to be the mocked dotnet executable location
                    () => ProcessPath,
                    useAmbientSettings
                        ? VSSettings.Ambient
                        : new VSSettings(VSSettingsFile?.FullName, DisallowPrereleaseByDefault));

            public DirectoryInfo GetProgramFilesDirectory(ProgramFiles programFiles)
                => new(Path.Combine(TestDirectory.FullName, $"ProgramFiles{programFiles}"));

            public DirectoryInfo CreateSdkDirectory(
                ProgramFiles programFiles,
                string sdkName,
                string sdkVersion,
                Version minimumMSBuildVersion = null)
            {
                var netSdkDirectory = Path.Combine(TestDirectory.FullName,
                    GetProgramFilesDirectory(programFiles).FullName,
                    "dotnet",
                    "sdk",
                    sdkVersion);

                new DirectoryInfo(netSdkDirectory).Create();

                //  hostfxr now checks for the existence of dotnet.dll in an SDK directory: https://github.com/dotnet/runtime/pull/89333
                //  So create that file
                var dotnetDllPath = Path.Combine(netSdkDirectory, "dotnet.dll");
                new FileInfo(dotnetDllPath).Create();


                var sdkDir = new DirectoryInfo(Path.Combine(netSdkDirectory,
                    "Sdks",
                    sdkName,
                    "Sdk"));

                sdkDir.Create();

                if (minimumMSBuildVersion != null)
                {
                    CreateMSBuildRequiredVersionFile(programFiles, sdkVersion, minimumMSBuildVersion);
                }

                return sdkDir;
            }

            public void CreateMuxerAndAddToPath(ProgramFiles programFiles)
            {
                var muxerDirectory =
                    new DirectoryInfo(Path.Combine(
                        TestDirectory.FullName, GetProgramFilesDirectory(programFiles).FullName, "dotnet"));

                ProcessPath = Path.Combine(muxerDirectory.FullName, Muxer);
                new FileInfo(ProcessPath).Create();

                PathEnvironmentVariable = $"{muxerDirectory}{Path.PathSeparator}{PathEnvironmentVariable}";
            }

            private void CreateMSBuildRequiredVersionFile(
                ProgramFiles programFiles,
                string sdkVersion,
                Version minimumMSBuildVersion)
            {
                if (minimumMSBuildVersion == null)
                {
                    minimumMSBuildVersion = new Version(1, 0);
                }

                var cliDirectory = new DirectoryInfo(Path.Combine(
                    TestDirectory.FullName,
                    GetProgramFilesDirectory(programFiles).FullName,
                    "dotnet",
                    "sdk",
                    sdkVersion));

                File.WriteAllText(
                    Path.Combine(cliDirectory.FullName, "minimumMSBuildVersion"),
                    minimumMSBuildVersion.ToString());
            }

            public void CreateGlobalJson(DirectoryInfo directory, string version)
                => File.WriteAllText(Path.Combine(directory.FullName, "global.json"),
                    $@"{{ ""sdk"": {{ ""version"":  ""{version}"" }} }}");

            public string GetEnvironmentVariable(string variable)
            {
                switch (variable)
                {
                    case "PATH":
                        return PathEnvironmentVariable;
                    default:
                        return null;
                }
            }

            public void CreateMinimumVSDefinedSDKVersionFile(string version)
            {
                File.WriteAllText(GetMinimumVSDefinedSDKVersionFilePath(), version);
            }

            private void DeleteMinimumVSDefinedSDKVersionFile()
            {
                File.Delete(GetMinimumVSDefinedSDKVersionFilePath());
            }

            private string GetMinimumVSDefinedSDKVersionFilePath()
            {
                string baseDirectory = AppContext.BaseDirectory;
                return Path.Combine(baseDirectory, "minimumVSDefinedSDKVersion");
            }

            public void CreateVSSettingsFile(bool disallowPreviews)
            {
                VSSettingsFile = new FileInfo(Path.Combine(TestDirectory.FullName, "sdk.txt"));

                // Guard against tests writing too fast for the up-to-date check
                // It happens more often on Unix due to https://github.com/dotnet/corefx/issues/12403
                var lastWriteTimeUtc = VSSettingsFile.Exists ? VSSettingsFile.LastWriteTimeUtc : DateTime.MinValue;
                for (int sleep = 10; sleep < 3000; sleep *= 2)
                {
                    File.WriteAllText(VSSettingsFile.FullName, $"UsePreviews={!disallowPreviews}");
                    VSSettingsFile.Refresh();

                    if (VSSettingsFile.LastWriteTimeUtc > lastWriteTimeUtc)
                    {
                        return;
                    }

                    Thread.Sleep(sleep);
                }

                throw new InvalidOperationException("LastWriteTime is not changing.");
            }

            public void DeleteVSSettingsFile()
            {
                VSSettingsFile.Delete();
            }
        }

        private sealed class MockContext : SdkResolverContext
        {
            public new string ProjectFilePath { get => base.ProjectFilePath; set => base.ProjectFilePath = value; }
            public new string SolutionFilePath { get => base.SolutionFilePath; set => base.SolutionFilePath = value; }
            public new Version MSBuildVersion { get => base.MSBuildVersion; set => base.MSBuildVersion = value; }
            public new bool IsRunningInVisualStudio { get => base.IsRunningInVisualStudio; set => base.IsRunningInVisualStudio = value; }

            public DirectoryInfo ProjectFileDirectory
            {
                get => new(Path.GetDirectoryName(ProjectFilePath));
                set => ProjectFilePath = Path.Combine(value.FullName, "test.csproj");
            }

            public override SdkLogger Logger { get; protected set; }

            public MockContext()
            {
                MSBuildVersion = new Version(15, 3, 0);
                Logger = new MockLogger();
            }
        }

        private sealed class MockFactory : SdkResultFactory
        {
            public override SdkResult IndicateFailure(IEnumerable<string> errors, IEnumerable<string> warnings = null)
                => new MockResult(success: false, path: null, version: null, warnings: warnings, errors: errors);

            public override SdkResult IndicateSuccess(string path, string version, IEnumerable<string> warnings = null)
                => new MockResult(success: true, path: path, version: version, warnings: warnings);

            public override SdkResult IndicateSuccess(string path, string version, IDictionary<string, string> propertiesToAdd, IDictionary<string, SdkResultItem> itemsToAdd, IEnumerable<string> warnings = null)
                => new MockResult(success: true, path: path, version: version, warnings: warnings, propertiesToAdd: propertiesToAdd, itemsToAdd: itemsToAdd);

            public override SdkResult IndicateSuccess(IEnumerable<string> paths, string version,
                IDictionary<string, string> propertiesToAdd = null, IDictionary<string, SdkResultItem> itemsToAdd = null,
                IEnumerable<string> warnings = null) => new MockResult(success: true, paths: paths, version: version, propertiesToAdd, itemsToAdd, warnings);
        }

        private sealed class MockResult : SdkResult
        {
            public MockResult(bool success, string path, string version, IEnumerable<string> warnings = null,
                IEnumerable<string> errors = null, IDictionary<string, string> propertiesToAdd = null, IDictionary<string, SdkResultItem> itemsToAdd = null)
            {
                Success = success;
                Path = path;
                Version = version;
                Warnings = warnings;
                Errors = errors;
                PropertiesToAdd = propertiesToAdd;
                ItemsToAdd = itemsToAdd;
            }

            public MockResult(bool success, IEnumerable<string> paths, string version,
                IDictionary<string, string> propertiesToAdd, IDictionary<string, SdkResultItem> itemsToAdd, IEnumerable<string> warnings)
            {
                Success = success;
                if (paths != null)
                {
                    var firstPath = paths.FirstOrDefault();
                    if (firstPath != null)
                    {
                        Path = firstPath;
                    }
                    if (paths.Count() > 1)
                    {
                        AdditionalPaths = paths.Skip(1).ToList();
                    }
                }
                Version = version;
                Warnings = warnings;
                PropertiesToAdd = propertiesToAdd;
                ItemsToAdd = itemsToAdd;
            }

            public override bool Success { get; protected set; }
            public override string Version { get; protected set; }
            public override string Path { get; protected set; }
            public override IList<string> AdditionalPaths { get; set; }
            public override IDictionary<string, string> PropertiesToAdd { get; protected set; }
            public override IDictionary<string, SdkResultItem> ItemsToAdd { get; protected set; }
            public IEnumerable<string> Errors { get; }
            public IEnumerable<string> Warnings { get; }
        }

        private sealed class MockLogger : SdkLogger
        {
            public override void LogMessage(string message, MessageImportance messageImportance = MessageImportance.Low)
            {

            }
        }
    }
}
