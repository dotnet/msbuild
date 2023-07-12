// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using FluentAssertions;
using Microsoft.DotNet.Cli;
using Microsoft.NET.Sdk.Localization;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using Microsoft.NET.TestFramework;

using Xunit;
using Xunit.Abstractions;

namespace ManifestReaderTests
{

    public class SdkDirectoryWorkloadManifestProviderTests : SdkTest
    {
        private string? _testDirectory;
        private string? _manifestRoot;
        private string? _manifestVersionBandDirectory;
        private string? _fakeDotnetRootDirectory;

        public SdkDirectoryWorkloadManifestProviderTests(ITestOutputHelper logger) : base(logger)
        {
        }

        [MemberNotNull("_testDirectory", "_manifestRoot", "_manifestVersionBandDirectory", "_fakeDotnetRootDirectory")]
        void Initialize(string featureBand = "5.0.100", [CallerMemberName] string? testName = null, string? identifier = null)
        {
            _testDirectory = _testAssetsManager.CreateTestDirectory(testName, identifier).Path;
            _fakeDotnetRootDirectory = Path.Combine(_testDirectory, "dotnet");
            _manifestRoot = Path.Combine(_fakeDotnetRootDirectory, "sdk-manifests");
            _manifestVersionBandDirectory = Path.Combine(_manifestRoot, featureBand);
            Directory.CreateDirectory(_manifestVersionBandDirectory);
        }

        [Fact]
        public void ItShouldReturnListOfManifestFiles()
        {
            Initialize();

            string androidManifestFileContent = "Android";
            string iosManifestFileContent = "iOS";
            Directory.CreateDirectory(Path.Combine(_manifestVersionBandDirectory, "Android"));
            File.WriteAllText(Path.Combine(_manifestVersionBandDirectory, "Android", "WorkloadManifest.json"), androidManifestFileContent);
            Directory.CreateDirectory(Path.Combine(_manifestVersionBandDirectory, "iOS"));
            File.WriteAllText(Path.Combine(_manifestVersionBandDirectory, "iOS", "WorkloadManifest.json"), iosManifestFileContent);

            var sdkDirectoryWorkloadManifestProvider
                = new SdkDirectoryWorkloadManifestProvider(sdkRootPath: _fakeDotnetRootDirectory, sdkVersion: "5.0.100", userProfileDir: null, globalJsonPath: null);

            GetManifestContents(sdkDirectoryWorkloadManifestProvider)
                .Should()
                .BeEquivalentTo(iosManifestFileContent, androidManifestFileContent);
        }

        [Fact]
        public void GivenSDKVersionItShouldReturnListOfManifestFilesForThisVersionBand()
        {
            Initialize();

            string androidManifestFileContent = "Android";
            Directory.CreateDirectory(Path.Combine(_manifestVersionBandDirectory, "Android"));
            File.WriteAllText(Path.Combine(_manifestVersionBandDirectory, "Android", "WorkloadManifest.json"), androidManifestFileContent);

            var sdkDirectoryWorkloadManifestProvider
                = new SdkDirectoryWorkloadManifestProvider(sdkRootPath: _fakeDotnetRootDirectory, sdkVersion: "5.0.100", userProfileDir: null, globalJsonPath: null);

            GetManifestContents(sdkDirectoryWorkloadManifestProvider)
                .Should()
                .BeEquivalentTo(androidManifestFileContent);
        }

        [Fact]
        public void GivenNoManifestDirectoryItShouldReturnEmpty()
        {
            Initialize();

            var sdkDirectoryWorkloadManifestProvider
                = new SdkDirectoryWorkloadManifestProvider(sdkRootPath: _fakeDotnetRootDirectory, sdkVersion: "5.0.100", userProfileDir: null, globalJsonPath: null);
            sdkDirectoryWorkloadManifestProvider.GetManifests().Should().BeEmpty();
        }

        [Fact]
        public void GivenNoManifestJsonFileInDirectoryItShouldIgnoreIt()
        {
            Initialize();

            Directory.CreateDirectory(Path.Combine(_manifestVersionBandDirectory, "Android"));

            var sdkDirectoryWorkloadManifestProvider
                = new SdkDirectoryWorkloadManifestProvider(sdkRootPath: _fakeDotnetRootDirectory, sdkVersion: "5.0.100", userProfileDir: null, globalJsonPath: null);

            sdkDirectoryWorkloadManifestProvider.GetManifests()
                .Should()
                .BeEmpty();
        }

        [Fact]
        public void ItReturnsLatestManifestVersion()
        {
            Initialize();

            CreateMockManifest(_manifestRoot, "5.0.100-preview.5", "ios", "11.0.3", true);

            CreateMockManifest(_manifestRoot, "5.0.100", "ios", "11.0.1", true);
            CreateMockManifest(_manifestRoot, "5.0.100", "ios", "11.0.2", true);
            CreateMockManifest(_manifestRoot, "5.0.100", "ios", "11.0.2-rc.1", true);

            var sdkDirectoryWorkloadManifestProvider
                = new SdkDirectoryWorkloadManifestProvider(sdkRootPath: _fakeDotnetRootDirectory, sdkVersion: "5.0.100", userProfileDir: null, globalJsonPath: null);

            GetManifestContents(sdkDirectoryWorkloadManifestProvider)
                .Should()
                .BeEquivalentTo("ios: 11.0.2/5.0.100");
        }

        [Fact]
        public void ItPrefersManifestsInSubfolders()
        {
            Initialize();

            CreateMockManifest(_manifestRoot, "5.0.100", "ios", "11.0.1", true);
            CreateMockManifest(_manifestRoot, "5.0.100", "ios", "11.0.2", true);

            //  Even though this manifest has a higher version, it is not in a versioned subfolder so the other manifests will be preferred
            //  In real use, we would expect the manifest outside the versioned subfolders to be a lower version
            CreateMockManifest(_manifestRoot, "5.0.100", "ios", "12.0.1", false);

            var sdkDirectoryWorkloadManifestProvider
                = new SdkDirectoryWorkloadManifestProvider(sdkRootPath: _fakeDotnetRootDirectory, sdkVersion: "5.0.100", userProfileDir: null, globalJsonPath: null);

            GetManifestContents(sdkDirectoryWorkloadManifestProvider)
                .Should()
                .BeEquivalentTo("ios: 11.0.2/5.0.100");
        }

        [Fact]
        public void ItFallsBackToLatestManifestVersion()
        {
            Initialize("8.0.200");

            var knownWorkloadsFilePath = Path.Combine(_fakeDotnetRootDirectory, "sdk", "8.0.100", "IncludedWorkloadManifests.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(knownWorkloadsFilePath)!);
            File.WriteAllText(knownWorkloadsFilePath, "android\nios");

            CreateMockManifest(_manifestRoot, "8.0.100", "android", "33.0.0", true);
            CreateMockManifest(_manifestRoot, "8.0.100", "android", "33.0.1", true);

            CreateMockManifest(_manifestRoot, "7.0.400", "android", "32.0.1", true);

            CreateMockManifest(_manifestRoot, "7.0.400", "ios", "17.0.1", true);
            CreateMockManifest(_manifestRoot, "7.0.400", "ios", "18.0.1", true);

            var sdkDirectoryWorkloadManifestProvider
                = new SdkDirectoryWorkloadManifestProvider(sdkRootPath: _fakeDotnetRootDirectory, sdkVersion: "8.0.100", userProfileDir: null, globalJsonPath: null);

            GetManifestContents(sdkDirectoryWorkloadManifestProvider)
                .Should()
                .BeEquivalentTo("android: 33.0.1/8.0.100",
                                "ios: 18.0.1/7.0.400");
        }

        [Fact]
        public void ItUsesManifestsFromWorkloadSet()
        {
            Initialize("8.0.200");

            CreateMockManifest(_manifestRoot, "8.0.100", "ios", "11.0.1", true);
            CreateMockManifest(_manifestRoot, "8.0.100", "ios", "11.0.2", true);
            CreateMockManifest(_manifestRoot, "8.0.200", "ios", "12.0.1", true);

            CreateMockManifest(_manifestRoot, "8.0.200", "android", "33.0.1", true);
            CreateMockManifest(_manifestRoot, "8.0.200", "android", "33.0.2-rc.1", true);
            CreateMockManifest(_manifestRoot, "8.0.200", "android", "33.0.2", true);

            CreateMockManifest(_manifestRoot, "8.0.200-rc.1", "maui", "15.0.1-preview.123", true);
            CreateMockManifest(_manifestRoot, "8.0.200-rc.2", "maui", "15.0.1-rc.456", true);
            CreateMockManifest(_manifestRoot, "8.0.200", "maui", "15.0.1", true);


            CreateMockWorkloadSet(_manifestRoot, "8.0.200", "8.0.200", """
                {
                  "ios": "11.0.2/8.0.100",
                  "android": "33.0.2-rc.1/8.0.200",
                  "maui": "15.0.1-rc.456/8.0.200-rc.2"
                }
                """);

            var sdkDirectoryWorkloadManifestProvider
                = new SdkDirectoryWorkloadManifestProvider(sdkRootPath: _fakeDotnetRootDirectory, sdkVersion: "8.0.200", userProfileDir: null, globalJsonPath: null);

            GetManifestContents(sdkDirectoryWorkloadManifestProvider)
                .Should()
                .BeEquivalentTo("ios: 11.0.2/8.0.100", "android: 33.0.2-rc.1/8.0.200", "maui: 15.0.1-rc.456/8.0.200-rc.2");
        }


        [Fact]
        public void WorkloadSetCanHaveTrailingCommasInJson()
        {
            Initialize("8.0.200");

            CreateMockManifest(_manifestRoot, "8.0.100", "ios", "11.0.2", true);
            CreateMockManifest(_manifestRoot, "8.0.200", "android", "33.0.2-rc.1", true);
            CreateMockManifest(_manifestRoot, "8.0.200-rc.2", "maui", "15.0.1-rc.456", true);

            CreateMockWorkloadSet(_manifestRoot, "8.0.200", "8.0.200", """
                {
                "ios": "11.0.2/8.0.100",
                "android": "33.0.2-rc.1/8.0.200",
                "maui": "15.0.1-rc.456/8.0.200-rc.2",
                }
                """);

            var sdkDirectoryWorkloadManifestProvider
                = new SdkDirectoryWorkloadManifestProvider(sdkRootPath: _fakeDotnetRootDirectory, sdkVersion: "8.0.200", userProfileDir: null, globalJsonPath: null);

            GetManifestContents(sdkDirectoryWorkloadManifestProvider)
                .Should()
                .BeEquivalentTo("ios: 11.0.2/8.0.100", "android: 33.0.2-rc.1/8.0.200", "maui: 15.0.1-rc.456/8.0.200-rc.2");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void ItUsesLatestWorkloadSet(bool globalJsonExists)
        {
            Initialize("8.0.200", identifier: globalJsonExists.ToString());

            string? globalJsonPath;
            if (globalJsonExists)
            {
                globalJsonPath = Path.Combine(_testDirectory, "global.json");
                File.WriteAllText(globalJsonPath, """
                    {
                        "sdk": {
                            "version": "1.0.42",
                        },
                        "msbuild-sdks": {
                            "Microsoft.DotNet.Arcade.Sdk": "7.0.0-beta.23254.2",
                        }
                    }
                    """);
            }
            else
            {
                globalJsonPath = null;
            }

            CreateMockManifest(_manifestRoot, "8.0.100", "ios", "11.0.1", true);
            CreateMockManifest(_manifestRoot, "8.0.100", "ios", "11.0.2", true);
            CreateMockManifest(_manifestRoot, "8.0.200", "ios", "12.0.1", true);

            CreateMockWorkloadSet(_manifestRoot, "8.0.200", "8.0.201", """
    {
      "ios": "11.0.2/8.0.100"
    }
    """);

            CreateMockWorkloadSet(_manifestRoot, "8.0.200", "8.0.202", """
    {
      "ios": "12.0.1/8.0.200"
    }
    """);

            var sdkDirectoryWorkloadManifestProvider
                = new SdkDirectoryWorkloadManifestProvider(sdkRootPath: _fakeDotnetRootDirectory, sdkVersion: "8.0.200", userProfileDir: null, globalJsonPath: globalJsonPath);

            GetManifestContents(sdkDirectoryWorkloadManifestProvider)
                .Should()
                .BeEquivalentTo("ios: 12.0.1/8.0.200");
        }

        [Fact]
        public void ItUsesLatestManifestThatIsNotInWorkloadSet()
        {
            Initialize("8.0.200");

            CreateMockManifest(_manifestRoot, "8.0.100", "ios", "11.0.1", true);
            CreateMockManifest(_manifestRoot, "8.0.100", "ios", "11.0.2", true);
            CreateMockManifest(_manifestRoot, "8.0.200", "ios", "12.0.1", true);

            CreateMockManifest(_manifestRoot, "8.0.200", "android", "33.0.1", true);
            CreateMockManifest(_manifestRoot, "8.0.200", "android", "33.0.2-rc.1", true);
            CreateMockManifest(_manifestRoot, "8.0.200", "android", "33.0.2", true);

            CreateMockManifest(_manifestRoot, "8.0.200-rc.1", "maui", "15.0.1-preview.123", true);
            CreateMockManifest(_manifestRoot, "8.0.200-rc.2", "maui", "15.0.1-rc.456", true);
            CreateMockManifest(_manifestRoot, "8.0.200", "maui", "15.0.1", true);
            CreateMockManifest(_manifestRoot, "8.0.200", "maui", "15.0.2", true);

            CreateMockWorkloadSet(_manifestRoot, "8.0.200", "8.0.200", """
                {
                  "ios": "11.0.2/8.0.100",
                  "android": "33.0.2-rc.1/8.0.200"
                }
                """);

            var sdkDirectoryWorkloadManifestProvider
                = new SdkDirectoryWorkloadManifestProvider(sdkRootPath: _fakeDotnetRootDirectory, sdkVersion: "8.0.200", userProfileDir: null, globalJsonPath: null);

            GetManifestContents(sdkDirectoryWorkloadManifestProvider)
                .Should()
                .BeEquivalentTo("ios: 11.0.2/8.0.100", "android: 33.0.2-rc.1/8.0.200", "maui: 15.0.2/8.0.200");
        }

        [Fact]
        public void ItFallsBackForManifestNotInWorkloadSet()
        {
            Initialize("8.0.200");

            var knownWorkloadsFilePath = Path.Combine(_fakeDotnetRootDirectory, "sdk", "8.0.201", "IncludedWorkloadManifests.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(knownWorkloadsFilePath)!);
            File.WriteAllText(knownWorkloadsFilePath, "android\nios\nmaui");

            CreateMockManifest(_manifestRoot, "8.0.100", "ios", "11.0.1", true);
            CreateMockManifest(_manifestRoot, "8.0.100", "ios", "11.0.2", true);
            CreateMockManifest(_manifestRoot, "8.0.200", "ios", "12.0.1", true);

            CreateMockManifest(_manifestRoot, "8.0.100", "android", "33.0.1", true);
            CreateMockManifest(_manifestRoot, "8.0.100", "android", "33.0.2-rc.1", true);
            CreateMockManifest(_manifestRoot, "8.0.100", "android", "33.0.2", true);

            CreateMockManifest(_manifestRoot, "8.0.200-rc.1", "maui", "15.0.1-preview.123", true);
            CreateMockManifest(_manifestRoot, "8.0.200-rc.2", "maui", "15.0.1-rc.456", true);

            CreateMockWorkloadSet(_manifestRoot, "8.0.200", "8.0.201", """
                {
                  "ios": "11.0.2/8.0.100"
                }
                """);

            var sdkDirectoryWorkloadManifestProvider
                = new SdkDirectoryWorkloadManifestProvider(sdkRootPath: _fakeDotnetRootDirectory, sdkVersion: "8.0.201", userProfileDir: null, globalJsonPath: null);

            GetManifestContents(sdkDirectoryWorkloadManifestProvider)
                .Should()
                .BeEquivalentTo("ios: 11.0.2/8.0.100", "android: 33.0.2/8.0.100", "maui: 15.0.1-rc.456/8.0.200-rc.2");
        }

        [Fact]
        public void ItThrowsIfWorkloadSetHasInvalidVersion()
        {
            Initialize("8.0.200");

            CreateMockManifest(_manifestRoot, "8.0.100", "ios", "11.0.1", true);
            CreateMockManifest(_manifestRoot, "8.0.100", "ios", "11.0.2", true);
            CreateMockManifest(_manifestRoot, "8.0.200", "ios", "12.0.1", true);

            CreateMockWorkloadSet(_manifestRoot, "8.0.200", "8.0.200.1", """
                {
                    "ios": "11.0.2/8.0.100"
                }
                """);

            Assert.Throws<FormatException>(() => new SdkDirectoryWorkloadManifestProvider(sdkRootPath: _fakeDotnetRootDirectory, sdkVersion: "8.0.200", userProfileDir: null, globalJsonPath: null));
        }

        [Fact]
        public void ItThrowsIfManifestFromWorkloadSetIsNotFound()
        {
            Initialize("8.0.200");

            CreateMockManifest(_manifestRoot, "8.0.100", "ios", "11.0.1", true);
            CreateMockManifest(_manifestRoot, "8.0.100", "ios", "11.0.2", true);
            CreateMockManifest(_manifestRoot, "8.0.200", "ios", "12.0.1", true);

            CreateMockWorkloadSet(_manifestRoot, "8.0.200", "8.0.200", """
                {
                  "ios": "12.0.2/8.0.200"
                }
                """);
            var sdkDirectoryWorkloadManifestProvider
                = new SdkDirectoryWorkloadManifestProvider(sdkRootPath: _fakeDotnetRootDirectory, sdkVersion: "8.0.200", userProfileDir: null, globalJsonPath: null);

            Assert.Throws<FileNotFoundException>(() => GetManifestContents(sdkDirectoryWorkloadManifestProvider).ToList());
        }

        [Fact]
        public void WorkloadSetCanIncludeMultipleJsonFiles()
        {
            Initialize("8.0.200");

            CreateMockManifest(_manifestRoot, "8.0.100", "ios", "11.0.1", true);
            CreateMockManifest(_manifestRoot, "8.0.100", "ios", "11.0.2", true);
            CreateMockManifest(_manifestRoot, "8.0.200", "ios", "12.0.1", true);

            CreateMockManifest(_manifestRoot, "8.0.200", "android", "33.0.1", true);
            CreateMockManifest(_manifestRoot, "8.0.200", "android", "33.0.2-rc.1", true);
            CreateMockManifest(_manifestRoot, "8.0.200", "android", "33.0.2", true);


            var workloadSetDirectory = Path.Combine(_manifestRoot, "8.0.200", "workloadsets", "8.0.200");
            Directory.CreateDirectory(workloadSetDirectory);
            File.WriteAllText(Path.Combine(workloadSetDirectory, "1.workloadset.json"), """
                {
                  "ios": "11.0.2/8.0.100"
                }
                """);
            File.WriteAllText(Path.Combine(workloadSetDirectory, "2.workloadset.json"), """
                {
                  "android": "33.0.2-rc.1/8.0.200"
                }
                """);

            var sdkDirectoryWorkloadManifestProvider
                = new SdkDirectoryWorkloadManifestProvider(sdkRootPath: _fakeDotnetRootDirectory, sdkVersion: "8.0.200", userProfileDir: null, globalJsonPath: null);

            GetManifestContents(sdkDirectoryWorkloadManifestProvider)
                .Should()
                .BeEquivalentTo("ios: 11.0.2/8.0.100", "android: 33.0.2-rc.1/8.0.200");
        }

        [Fact]
        public void ItThrowsExceptionIfWorkloadSetJsonFilesHaveDuplicateManifests()
        {
            Initialize("8.0.200");

            CreateMockManifest(_manifestRoot, "8.0.100", "ios", "11.0.1", true);
            CreateMockManifest(_manifestRoot, "8.0.100", "ios", "11.0.2", true);
            CreateMockManifest(_manifestRoot, "8.0.200", "ios", "12.0.1", true);

            CreateMockManifest(_manifestRoot, "8.0.200", "android", "33.0.1", true);
            CreateMockManifest(_manifestRoot, "8.0.200", "android", "33.0.2-rc.1", true);
            CreateMockManifest(_manifestRoot, "8.0.200", "android", "33.0.2", true);


            var workloadSetDirectory = Path.Combine(_manifestRoot, "8.0.200", "workloadsets", "8.0.200");
            Directory.CreateDirectory(workloadSetDirectory);
            File.WriteAllText(Path.Combine(workloadSetDirectory, "1.workloadset.json"), """
                {
                    "ios": "11.0.2/8.0.100"
                }
                """);
            File.WriteAllText(Path.Combine(workloadSetDirectory, "2.workloadset.json"), """
                {
                  "android": "33.0.2-rc.1/8.0.200",
                  "ios": "11.0.2/8.0.100"
                }
                """);

            Assert.Throws<ArgumentException>(() =>
                new SdkDirectoryWorkloadManifestProvider(sdkRootPath: _fakeDotnetRootDirectory, sdkVersion: "8.0.200", userProfileDir: null, globalJsonPath: null));
        }

        [Fact]
        public void ItUsesWorkloadSetFromGlobalJson()
        {
            Initialize("8.0.200");

            string? globalJsonPath = Path.Combine(_testDirectory, "global.json");
            File.WriteAllText(globalJsonPath, """
            {
                "sdk": {
                    "version": "8.0.200",
                    "workloadversion": "8.0.201"
                },
                "msbuild-sdks": {
                    "Microsoft.DotNet.Arcade.Sdk": "7.0.0-beta.23254.2",
                }
            }
            """);

            CreateMockManifest(_manifestRoot, "8.0.100", "ios", "11.0.1", true);
            CreateMockManifest(_manifestRoot, "8.0.100", "ios", "11.0.2", true);
            CreateMockManifest(_manifestRoot, "8.0.200", "ios", "12.0.1", true);

            CreateMockWorkloadSet(_manifestRoot, "8.0.200", "8.0.201", """
{
  "ios": "11.0.2/8.0.100"
}
""");

            CreateMockWorkloadSet(_manifestRoot, "8.0.200", "8.0.202", """
{
  "ios": "12.0.1/8.0.200"
}
""");

            var sdkDirectoryWorkloadManifestProvider
                = new SdkDirectoryWorkloadManifestProvider(sdkRootPath: _fakeDotnetRootDirectory, sdkVersion: "8.0.200", userProfileDir: null, globalJsonPath: globalJsonPath);

            GetManifestContents(sdkDirectoryWorkloadManifestProvider)
                .Should()
                .BeEquivalentTo("ios: 11.0.2/8.0.100");
        }

        [Fact]
        public void ItFailsIfWorkloadSetFromGlobalJsonIsNotInstalled()
        {
            Initialize("8.0.200");

            string? globalJsonPath = Path.Combine(_testDirectory, "global.json");
            File.WriteAllText(globalJsonPath, """
            {
                "sdk": {
                    "version": "8.0.200",
                    "workloadversion": "8.0.201"
                },
                "msbuild-sdks": {
                    "Microsoft.DotNet.Arcade.Sdk": "7.0.0-beta.23254.2",
                }
            }
            """);

            CreateMockManifest(_manifestRoot, "8.0.200", "ios", "12.0.1", true);

            CreateMockWorkloadSet(_manifestRoot, "8.0.200", "8.0.202", """
{
  "ios": "12.0.1/8.0.200"
}
""");

            var ex = Assert.Throws<FileNotFoundException>(() => new SdkDirectoryWorkloadManifestProvider(sdkRootPath: _fakeDotnetRootDirectory, sdkVersion: "8.0.200", userProfileDir: null, globalJsonPath: globalJsonPath));
            ex.Message.Should().Be(string.Format(Strings.WorkloadVersionFromGlobalJsonNotFound, "8.0.201", globalJsonPath));
        }

        [Fact]
        public void ItFailsIfGlobalJsonIsMalformed()
        {
            Initialize("8.0.200");

            string? globalJsonPath = Path.Combine(_testDirectory, "global.json");
            File.WriteAllText(globalJsonPath, """
            {
                "sdk": {
                    "workloadversion": [ "8.0.202" ]
                }
            }
            """);

            CreateMockManifest(_manifestRoot, "8.0.200", "ios", "12.0.1", true);

            CreateMockWorkloadSet(_manifestRoot, "8.0.200", "8.0.202", """
{
  "ios": "12.0.1/8.0.200"
}
""");

            var ex = Assert.Throws<SdkDirectoryWorkloadManifestProvider.JsonFormatException>(
                () => new SdkDirectoryWorkloadManifestProvider(sdkRootPath: _fakeDotnetRootDirectory, sdkVersion: "8.0.200", userProfileDir: null, globalJsonPath: globalJsonPath));
        }

        [Fact]
        public void ItUsesWorkloadSetFromInstallState()
        {
            Initialize("8.0.200");

            CreateMockManifest(_manifestRoot, "8.0.100", "ios", "11.0.1", true);
            CreateMockManifest(_manifestRoot, "8.0.100", "ios", "11.0.2", true);
            CreateMockManifest(_manifestRoot, "8.0.200", "ios", "12.0.1", true);

            CreateMockWorkloadSet(_manifestRoot, "8.0.200", "8.0.201", """
{
  "ios": "11.0.2/8.0.100"
}
""");

            CreateMockWorkloadSet(_manifestRoot, "8.0.200", "8.0.202", """
{
  "ios": "12.0.1/8.0.200"
}
""");
            CreateMockInstallState("8.0.200", 
                """
                {
                    "workloadVersion": "8.0.201"
                }
                """);


            var sdkDirectoryWorkloadManifestProvider
                = new SdkDirectoryWorkloadManifestProvider(sdkRootPath: _fakeDotnetRootDirectory, sdkVersion: "8.0.200", userProfileDir: null, globalJsonPath: null);

            GetManifestContents(sdkDirectoryWorkloadManifestProvider)
                .Should()
                .BeEquivalentTo("ios: 11.0.2/8.0.100");
        }

        [Fact]
        public void ItFailsIfWorkloadSetFromInstallStateIsNotInstalled()
        {
            Initialize("8.0.200");

            CreateMockManifest(_manifestRoot, "8.0.100", "ios", "11.0.1", true);
            CreateMockManifest(_manifestRoot, "8.0.100", "ios", "11.0.2", true);
            CreateMockManifest(_manifestRoot, "8.0.200", "ios", "12.0.1", true);

            CreateMockWorkloadSet(_manifestRoot, "8.0.200", "8.0.201", """
{
  "ios": "11.0.2/8.0.100"
}
""");

            CreateMockWorkloadSet(_manifestRoot, "8.0.200", "8.0.202", """
{
  "ios": "12.0.1/8.0.200"
}
""");
            var installStatePath = CreateMockInstallState("8.0.200",
                """
                {
                    "workloadVersion": "8.0.203"
                }
                """);


            var ex = Assert.Throws<FileNotFoundException>(
                () => new SdkDirectoryWorkloadManifestProvider(sdkRootPath: _fakeDotnetRootDirectory, sdkVersion: "8.0.200", userProfileDir: null, globalJsonPath: null));

            ex.Message.Should().Be(string.Format(Strings.WorkloadVersionFromInstallStateNotFound, "8.0.203", installStatePath));
        }

        [Fact]
        public void ItFailsIfManifestFromWorkloadSetFromInstallStateIsNotInstalled()
        {
            Initialize("8.0.200");

            CreateMockManifest(_manifestRoot, "8.0.200", "ios", "12.0.1", true);

            CreateMockWorkloadSet(_manifestRoot, "8.0.200", "8.0.201", """
{
  "ios": "11.0.2/8.0.100"
}
""");

            CreateMockWorkloadSet(_manifestRoot, "8.0.200", "8.0.202", """
{
  "ios": "12.0.1/8.0.200"
}
""");
            var installStatePath = CreateMockInstallState("8.0.200",
                """
                {
                    "workloadVersion": "8.0.201"
                }
                """);

            var sdkDirectoryWorkloadManifestProvider
                = new SdkDirectoryWorkloadManifestProvider(sdkRootPath: _fakeDotnetRootDirectory, sdkVersion: "8.0.200", userProfileDir: null, globalJsonPath: null);

            var ex = Assert.Throws<FileNotFoundException>(() => sdkDirectoryWorkloadManifestProvider.GetManifests().ToList());

            ex.Message.Should().Be(string.Format(Strings.ManifestFromWorkloadSetNotFound, "ios: 11.0.2/8.0.100", "8.0.201"));
        }

        [Fact]
        public void ItUsesWorkloadManifestFromInstallState()
        {
            Initialize("8.0.200");

            CreateMockManifest(_manifestRoot, "8.0.100", "ios", "11.0.1", true);
            CreateMockManifest(_manifestRoot, "8.0.100", "ios", "11.0.2", true);
            CreateMockManifest(_manifestRoot, "8.0.200", "ios", "12.0.1", true);

            CreateMockWorkloadSet(_manifestRoot, "8.0.200", "8.0.201", """
{
  "ios": "11.0.2/8.0.100"
}
""");

            CreateMockWorkloadSet(_manifestRoot, "8.0.200", "8.0.202", """
{
  "ios": "12.0.1/8.0.200"
}
""");
            CreateMockInstallState("8.0.200",
                """
                {
                    "manifests": {
                        "ios": "11.0.1/8.0.100",
                    }
                }
                """);


            var sdkDirectoryWorkloadManifestProvider
                = new SdkDirectoryWorkloadManifestProvider(sdkRootPath: _fakeDotnetRootDirectory, sdkVersion: "8.0.200", userProfileDir: null, globalJsonPath: null);

            GetManifestContents(sdkDirectoryWorkloadManifestProvider)
                .Should()
                .BeEquivalentTo("ios: 11.0.1/8.0.100");
        }

        [Fact]
        public void ItFailsIfManifestFromInstallStateIsNotInstalled()
        {
            Initialize("8.0.200");

            CreateMockManifest(_manifestRoot, "8.0.100", "ios", "11.0.1", true);
            CreateMockManifest(_manifestRoot, "8.0.100", "ios", "11.0.2", true);
            CreateMockManifest(_manifestRoot, "8.0.200", "ios", "12.0.1", true);

            CreateMockWorkloadSet(_manifestRoot, "8.0.200", "8.0.201", """
{
  "ios": "11.0.2/8.0.100"
}
""");

            CreateMockWorkloadSet(_manifestRoot, "8.0.200", "8.0.202", """
{
  "ios": "12.0.1/8.0.200"
}
""");
            var installStatePath = CreateMockInstallState("8.0.200",
                """
                {
                    "manifests": {
                        "ios": "12.0.2/8.0.200",
                    }
                }
                """);

            var sdkDirectoryWorkloadManifestProvider
                = new SdkDirectoryWorkloadManifestProvider(sdkRootPath: _fakeDotnetRootDirectory, sdkVersion: "8.0.200", userProfileDir: null, globalJsonPath: null);

            var ex = Assert.Throws<FileNotFoundException>(() => sdkDirectoryWorkloadManifestProvider.GetManifests().ToList());

            ex.Message.Should().Be(string.Format(Strings.ManifestFromInstallStateNotFound, "ios: 12.0.2/8.0.200", installStatePath));
        }

        [Fact]
        public void ItUsesWorkloadSetAndManifestFromInstallState()
        {
            Initialize("8.0.200");

            CreateMockManifest(_manifestRoot, "8.0.200", "tizen", "8.0.0", true);
            CreateMockManifest(_manifestRoot, "8.0.200", "tizen", "8.0.1", true);

            CreateMockManifest(_manifestRoot, "8.0.100", "ios", "11.0.1", true);
            CreateMockManifest(_manifestRoot, "8.0.100", "ios", "11.0.2", true);
            CreateMockManifest(_manifestRoot, "8.0.200", "ios", "12.0.1", true);

            CreateMockWorkloadSet(_manifestRoot, "8.0.200", "8.0.201", """
{
  "ios": "11.0.2/8.0.100"
}
""");

            CreateMockWorkloadSet(_manifestRoot, "8.0.200", "8.0.202", """
{
  "ios": "12.0.1/8.0.200"
}
""");
            CreateMockInstallState("8.0.200",
                """
                {
                    "workloadVersion": "8.0.201",
                    "manifests": {
                        "tizen": "8.0.0/8.0.200",
                    }
                }
                """);


            var sdkDirectoryWorkloadManifestProvider
                = new SdkDirectoryWorkloadManifestProvider(sdkRootPath: _fakeDotnetRootDirectory, sdkVersion: "8.0.200", userProfileDir: null, globalJsonPath: null);

            GetManifestContents(sdkDirectoryWorkloadManifestProvider)
                .Should()
                .BeEquivalentTo("ios: 11.0.2/8.0.100", "tizen: 8.0.0/8.0.200");
        }

        [Fact]
        public void WorkloadManifestFromInstallStateOverridesWorkloadSetFromInstallState()
        {
            Initialize("8.0.200");

            CreateMockManifest(_manifestRoot, "8.0.100", "ios", "11.0.1", true);
            CreateMockManifest(_manifestRoot, "8.0.100", "ios", "11.0.2", true);
            CreateMockManifest(_manifestRoot, "8.0.200", "ios", "12.0.1", true);

            CreateMockWorkloadSet(_manifestRoot, "8.0.200", "8.0.201", """
{
  "ios": "11.0.2/8.0.100"
}
""");

            CreateMockWorkloadSet(_manifestRoot, "8.0.200", "8.0.202", """
{
  "ios": "12.0.1/8.0.200"
}
""");
            CreateMockInstallState("8.0.200",
                """
                {
                    "workloadVersion": "8.0.201",
                    "manifests": {
                        "ios": "11.0.1/8.0.100",
                    }
                }
                """);

            var sdkDirectoryWorkloadManifestProvider
                = new SdkDirectoryWorkloadManifestProvider(sdkRootPath: _fakeDotnetRootDirectory, sdkVersion: "8.0.200", userProfileDir: null, globalJsonPath: null);

            GetManifestContents(sdkDirectoryWorkloadManifestProvider)
                .Should()
                .BeEquivalentTo("ios: 11.0.1/8.0.100");
        }

        //  Falls back for manifest not in install state
        [Fact]
        public void ItFallsBackForManifestNotInInstallState()
        {
            Initialize("8.0.200");

            var knownWorkloadsFilePath = Path.Combine(_fakeDotnetRootDirectory, "sdk", "8.0.201", "IncludedWorkloadManifests.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(knownWorkloadsFilePath)!);
            File.WriteAllText(knownWorkloadsFilePath, "android\nios\nmaui");

            CreateMockManifest(_manifestRoot, "8.0.100", "ios", "11.0.1", true);
            CreateMockManifest(_manifestRoot, "8.0.100", "ios", "11.0.2", true);
            CreateMockManifest(_manifestRoot, "8.0.200", "ios", "12.0.1", true);

            CreateMockManifest(_manifestRoot, "8.0.100", "android", "33.0.1", true);
            CreateMockManifest(_manifestRoot, "8.0.100", "android", "33.0.2-rc.1", true);
            CreateMockManifest(_manifestRoot, "8.0.100", "android", "33.0.2", true);

            CreateMockInstallState("8.0.200",
                """
                {
                    "manifests": {
                        "ios": "12.0.1/8.0.200",
                    }
                }
                """);

            var sdkDirectoryWorkloadManifestProvider
                = new SdkDirectoryWorkloadManifestProvider(sdkRootPath: _fakeDotnetRootDirectory, sdkVersion: "8.0.201", userProfileDir: null, globalJsonPath: null);

            GetManifestContents(sdkDirectoryWorkloadManifestProvider)
                .Should()
                .BeEquivalentTo("ios: 12.0.1/8.0.200", "android: 33.0.2/8.0.100");
        }

        [Fact]
        public void GlobalJsonOverridesInstallState()
        {
            Initialize("8.0.200");

            string? globalJsonPath = Path.Combine(_testDirectory, "global.json");
            File.WriteAllText(globalJsonPath, """
            {
                "sdk": {
                    "version": "8.0.200",
                    "workloadversion": "8.0.201"
                },
                "msbuild-sdks": {
                    "Microsoft.DotNet.Arcade.Sdk": "7.0.0-beta.23254.2",
                }
            }
            """);

            CreateMockInstallState("8.0.200",
                """
                {
                    "workloadVersion": "8.0.202",
                }
                """);

            CreateMockManifest(_manifestRoot, "8.0.100", "ios", "11.0.1", true);
            CreateMockManifest(_manifestRoot, "8.0.100", "ios", "11.0.2", true);
            CreateMockManifest(_manifestRoot, "8.0.200", "ios", "12.0.1", true);

            CreateMockWorkloadSet(_manifestRoot, "8.0.200", "8.0.201", """
{
  "ios": "11.0.2/8.0.100"
}
""");

            CreateMockWorkloadSet(_manifestRoot, "8.0.200", "8.0.202", """
{
  "ios": "12.0.1/8.0.200"
}
""");

            var sdkDirectoryWorkloadManifestProvider
                = new SdkDirectoryWorkloadManifestProvider(sdkRootPath: _fakeDotnetRootDirectory, sdkVersion: "8.0.200", userProfileDir: null, globalJsonPath: globalJsonPath);

            GetManifestContents(sdkDirectoryWorkloadManifestProvider)
                .Should()
                .BeEquivalentTo("ios: 11.0.2/8.0.100");
        }

        [Fact]
        public void GlobalJsonWithoutWorkloadVersionDoesNotOverrideInstallState()
        {
            Initialize("8.0.200");

            string? globalJsonPath = Path.Combine(_testDirectory, "global.json");
            File.WriteAllText(globalJsonPath, "{}");

            CreateMockInstallState("8.0.200",
                """
                {
                    "workloadVersion": "8.0.200",
                }
                """);

            CreateMockManifest(_manifestRoot, "8.0.100", "ios", "11.0.1", true);
            CreateMockManifest(_manifestRoot, "8.0.100", "ios", "11.0.2", true);
            CreateMockManifest(_manifestRoot, "8.0.200", "ios", "12.0.1", true);

            CreateMockWorkloadSet(_manifestRoot, "8.0.200", "8.0.200", """
{
  "ios": "11.0.1/8.0.100"
}
""");

            CreateMockWorkloadSet(_manifestRoot, "8.0.200", "8.0.201", """
{
  "ios": "11.0.2/8.0.100"
}
""");

            CreateMockWorkloadSet(_manifestRoot, "8.0.200", "8.0.202", """
{
  "ios": "12.0.1/8.0.200"
}
""");

            var sdkDirectoryWorkloadManifestProvider
                = new SdkDirectoryWorkloadManifestProvider(sdkRootPath: _fakeDotnetRootDirectory, sdkVersion: "8.0.200", userProfileDir: null, globalJsonPath: globalJsonPath);

            GetManifestContents(sdkDirectoryWorkloadManifestProvider)
                .Should()
                .BeEquivalentTo("ios: 11.0.1/8.0.100");
        }

        [Fact]
        public void ItShouldReturnManifestsFromTestHook()
        {
            Initialize();

            string sdkVersion = "5.0.100";

            var additionalManifestDirectory = Path.Combine(_testDirectory, "AdditionalManifests");
            Directory.CreateDirectory(additionalManifestDirectory);

            var environmentMock = new EnvironmentMock();
            environmentMock.Add(EnvironmentVariableNames.WORKLOAD_MANIFEST_ROOTS, additionalManifestDirectory);

            //  Manifest in test hook directory
            Directory.CreateDirectory(Path.Combine(additionalManifestDirectory, sdkVersion, "Android"));
            File.WriteAllText(Path.Combine(additionalManifestDirectory, sdkVersion, "Android", "WorkloadManifest.json"), "Android: AndroidContent");

            //  Manifest in default directory
            Directory.CreateDirectory(Path.Combine(_manifestVersionBandDirectory, "iOS"));
            File.WriteAllText(Path.Combine(_manifestVersionBandDirectory, "iOS", "WorkloadManifest.json"), "iOS: iOSContent");


            var sdkDirectoryWorkloadManifestProvider
                = new SdkDirectoryWorkloadManifestProvider(sdkRootPath: _fakeDotnetRootDirectory, sdkVersion: sdkVersion, environmentMock.GetEnvironmentVariable, userProfileDir: null);

            GetManifestContents(sdkDirectoryWorkloadManifestProvider)
                .Should()
                .BeEquivalentTo("Android: AndroidContent", "iOS: iOSContent");
        }

        [Fact]
        public void ManifestFromTestHookShouldOverrideDefault()
        {
            Initialize();

            string sdkVersion = "5.0.100";

            var additionalManifestDirectory = Path.Combine(_testDirectory, "AdditionalManifests");
            Directory.CreateDirectory(additionalManifestDirectory);

            var environmentMock = new EnvironmentMock();
            environmentMock.Add(EnvironmentVariableNames.WORKLOAD_MANIFEST_ROOTS, additionalManifestDirectory);

            //  Manifest in test hook directory
            Directory.CreateDirectory(Path.Combine(additionalManifestDirectory, sdkVersion, "Android"));
            File.WriteAllText(Path.Combine(additionalManifestDirectory, sdkVersion, "Android", "WorkloadManifest.json"), "Android: OverridingAndroidContent");

            //  Manifest in default directory
            Directory.CreateDirectory(Path.Combine(_manifestVersionBandDirectory, "Android"));
            File.WriteAllText(Path.Combine(_manifestVersionBandDirectory, "Android", "WorkloadManifest.json"), "Android: OverriddenAndroidContent");

            var sdkDirectoryWorkloadManifestProvider
                = new SdkDirectoryWorkloadManifestProvider(sdkRootPath: _fakeDotnetRootDirectory, sdkVersion: sdkVersion, environmentMock.GetEnvironmentVariable, userProfileDir: null);

            GetManifestContents(sdkDirectoryWorkloadManifestProvider)
                .Should()
                .BeEquivalentTo("Android: OverridingAndroidContent");

        }

        [Fact]
        public void ItSupportsMultipleTestHookFolders()
        {
            Initialize();

            string sdkVersion = "5.0.100";

            var additionalManifestDirectory1 = Path.Combine(_testDirectory, "AdditionalManifests1");
            Directory.CreateDirectory(additionalManifestDirectory1);
            var additionalManifestDirectory2 = Path.Combine(_testDirectory, "AdditionalManifests2");
            Directory.CreateDirectory(additionalManifestDirectory2);

            var environmentMock = new EnvironmentMock();
            environmentMock.Add(EnvironmentVariableNames.WORKLOAD_MANIFEST_ROOTS, additionalManifestDirectory1 + Path.PathSeparator + additionalManifestDirectory2);


            //  Manifests in default directory
            Directory.CreateDirectory(Path.Combine(_manifestVersionBandDirectory, "iOS"));
            File.WriteAllText(Path.Combine(_manifestVersionBandDirectory, "iOS", "WorkloadManifest.json"), "iOS: iOSContent");

            Directory.CreateDirectory(Path.Combine(_manifestVersionBandDirectory, "Android"));
            File.WriteAllText(Path.Combine(_manifestVersionBandDirectory, "Android", "WorkloadManifest.json"), "Android: DefaultAndroidContent");

            //  Manifests in first additional directory
            Directory.CreateDirectory(Path.Combine(additionalManifestDirectory1, sdkVersion, "Android"));
            File.WriteAllText(Path.Combine(additionalManifestDirectory1, sdkVersion, "Android", "WorkloadManifest.json"), "Android: AndroidContent1");

            //  Manifests in second additional directory
            Directory.CreateDirectory(Path.Combine(additionalManifestDirectory2, sdkVersion, "Android"));
            File.WriteAllText(Path.Combine(additionalManifestDirectory2, sdkVersion, "Android", "WorkloadManifest.json"), "Android: AndroidContent2");

            Directory.CreateDirectory(Path.Combine(additionalManifestDirectory2, sdkVersion, "Test"));
            File.WriteAllText(Path.Combine(additionalManifestDirectory2, sdkVersion, "Test", "WorkloadManifest.json"), "Test: TestContent2");

            var sdkDirectoryWorkloadManifestProvider
                = new SdkDirectoryWorkloadManifestProvider(sdkRootPath: _fakeDotnetRootDirectory, sdkVersion: sdkVersion, environmentMock.GetEnvironmentVariable, userProfileDir: null);

            GetManifestContents(sdkDirectoryWorkloadManifestProvider)
                .Should()
                .BeEquivalentTo("Android: AndroidContent1", "iOS: iOSContent", "Test: TestContent2");
         
        }

        [Fact]
        public void IfTestHookFolderDoesNotExistItShouldBeIgnored()
        {
            Initialize();

            var additionalManifestDirectory = Path.Combine(_testDirectory, "AdditionalManifests");
                
            var environmentMock = new EnvironmentMock();
            environmentMock.Add(EnvironmentVariableNames.WORKLOAD_MANIFEST_ROOTS, additionalManifestDirectory);

            //  Manifest in default directory
            Directory.CreateDirectory(Path.Combine(_manifestVersionBandDirectory, "Android"));
            File.WriteAllText(Path.Combine(_manifestVersionBandDirectory, "Android", "WorkloadManifest.json"), "Android: AndroidContent");

            var sdkDirectoryWorkloadManifestProvider
                = new SdkDirectoryWorkloadManifestProvider(sdkRootPath: _fakeDotnetRootDirectory, sdkVersion: "5.0.100", environmentMock.GetEnvironmentVariable, userProfileDir: null);

            GetManifestContents(sdkDirectoryWorkloadManifestProvider)
                .Should()
                .BeEquivalentTo("Android: AndroidContent");
         
        }

        [Fact]
        public void ItShouldIgnoreOutdatedManifestIds()
        {
            Initialize();

            Directory.CreateDirectory(Path.Combine(_manifestVersionBandDirectory, "iOS"));
            File.WriteAllText(Path.Combine(_manifestVersionBandDirectory, "iOS", "WorkloadManifest.json"), "iOS: iOSContent");
            Directory.CreateDirectory(Path.Combine(_manifestVersionBandDirectory, "Microsoft.NET.Workload.Android"));
            File.WriteAllText(Path.Combine(_manifestVersionBandDirectory, "Microsoft.NET.Workload.Android", "WorkloadManifest.json"), "Microsoft.NET.Workload.Android: AndroidContent");

            var sdkDirectoryWorkloadManifestProvider
                = new SdkDirectoryWorkloadManifestProvider(sdkRootPath: _fakeDotnetRootDirectory, sdkVersion: "5.0.100", userProfileDir: null, globalJsonPath: null);

            GetManifestContents(sdkDirectoryWorkloadManifestProvider)
                .Should()
                .BeEquivalentTo("iOS: iOSContent");
        }

        [Fact]
        public void ItShouldFallbackWhenFeatureBandHasNoManifests()
        {
            Initialize("6.0.100");

            // Write 4.0.100 manifests-> android only
            CreateMockManifest(_manifestRoot, "4.0.100", "Android", "1");

            // Write 5.0.100 manifests-> ios and android
            CreateMockManifest(_manifestRoot, "5.0.100", "Android", "2");
            CreateMockManifest(_manifestRoot, "5.0.100", "iOS", "3");

            // Write 6.0.100 manifests-> ios only
            CreateMockManifest(_manifestRoot, "6.0.100", "iOS", "4");

            // Write 7.0.100 manifests-> ios and android
            CreateMockManifest(_manifestRoot, "7.0.100", "Android", "5");
            CreateMockManifest(_manifestRoot, "7.0.100", "iOS", "6");

            var knownWorkloadsFilePath = Path.Combine(_fakeDotnetRootDirectory, "sdk", "6.0.100", "IncludedWorkloadManifests.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(knownWorkloadsFilePath)!);
            File.WriteAllText(knownWorkloadsFilePath, "Android\niOS");

            var sdkDirectoryWorkloadManifestProvider
                = new SdkDirectoryWorkloadManifestProvider(sdkRootPath: _fakeDotnetRootDirectory, sdkVersion: "6.0.100", userProfileDir: null, globalJsonPath: null);

            GetManifestContents(sdkDirectoryWorkloadManifestProvider)
                .Should()
                .BeEquivalentTo("iOS: 4/6.0.100", "Android: 2/5.0.100");
        }

        [Fact]
        public void ItShouldFallbackWhenPreviewFeatureBandHasNoManifests()
        {
            Initialize("6.0.100");

            // Write 4.0.100 manifests-> android only
            CreateMockManifest(_manifestRoot, "4.0.100", "iOS", "1");

            // Write 5.0.100 manifests-> android
            CreateMockManifest(_manifestRoot, "5.0.100", "Android", "2");

            // Write 6.0.100-preview.2 manifests-> ios only
            CreateMockManifest(_manifestRoot, "6.0.100-preview.2", "iOS", "3");

            // Write 7.0.100 manifests-> ios and android
            CreateMockManifest(_manifestRoot, "7.0.100", "Android", "4");
            CreateMockManifest(_manifestRoot, "7.0.100", "iOS", " 5");

            var prev4Version = "6.0.100-preview.4.12345";
            var knownWorkloadsFilePath = Path.Combine(_fakeDotnetRootDirectory, "sdk", prev4Version, "IncludedWorkloadManifests.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(knownWorkloadsFilePath)!);
            File.WriteAllText(knownWorkloadsFilePath, "Android\niOS");

            var sdkDirectoryWorkloadManifestProvider
                = new SdkDirectoryWorkloadManifestProvider(sdkRootPath: _fakeDotnetRootDirectory, sdkVersion: prev4Version, userProfileDir: null, globalJsonPath: null);

            GetManifestContents(sdkDirectoryWorkloadManifestProvider)
                .Should()
                .BeEquivalentTo("iOS: 3/6.0.100-preview.2", "Android: 2/5.0.100");
        }

        [Fact]
        public void ItShouldRollForwardToNonPrereleaseWhenPreviewFeatureBandHasNoManifests()
        {
            Initialize("6.0.100");

            // Write 4.0.100 manifests-> android only
            CreateMockManifest(_manifestRoot, "4.0.100", "Android", "1");

            // Write 5.0.100 manifests-> ios and android
            CreateMockManifest(_manifestRoot, "5.0.100", "Android", "2");
            CreateMockManifest(_manifestRoot, "5.0.100", "iOS", "3");

            // Write 6.0.100-preview.4 manifests-> ios only
            CreateMockManifest(_manifestRoot, "6.0.100-preview.4", "iOS", "4");

            // Write 6.0.100 manifests-> android
            CreateMockManifest(_manifestRoot, "6.0.100", "Android", "5");

            var prev4Version = "6.0.100-preview.4.12345";
            var knownWorkloadsFilePath = Path.Combine(_fakeDotnetRootDirectory, "sdk", prev4Version, "IncludedWorkloadManifests.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(knownWorkloadsFilePath)!);
            File.WriteAllText(knownWorkloadsFilePath, "Android\niOS");

            var sdkDirectoryWorkloadManifestProvider
                = new SdkDirectoryWorkloadManifestProvider(sdkRootPath: _fakeDotnetRootDirectory, sdkVersion: prev4Version, userProfileDir: null, globalJsonPath: null);

            GetManifestContents(sdkDirectoryWorkloadManifestProvider)
                .Should()
                .BeEquivalentTo("iOS: 4/6.0.100-preview.4", "Android: 5/6.0.100");
        }

        [Fact]
        public void ItReturnsManifestsInOrderFromKnownWorkloadManifestsFile()
        {
            //  microsoft.net.workload.mono.toolchain.net6, microsoft.net.workload.mono.toolchain.net7, microsoft.net.workload.emscripten.net6, microsoft.net.workload.emscripten.net7

            var currentSdkVersion = "7.0.100";
            var fallbackWorkloadBand = "7.0.100-rc.2";

            Initialize(currentSdkVersion);

            CreateMockManifest(_manifestRoot, currentSdkVersion, "NotInIncudedWorkloadsFile", "1");
            CreateMockManifest(_manifestRoot, currentSdkVersion, "Microsoft.Net.Workload.Mono.Toolchain.net6", "2");
            CreateMockManifest(_manifestRoot, fallbackWorkloadBand, "Microsoft.Net.Workload.Mono.Toolchain.net7", "3");
            CreateMockManifest(_manifestRoot, fallbackWorkloadBand, "Microsoft.Net.Workload.Emscripten.net6", "4");
            CreateMockManifest(_manifestRoot, currentSdkVersion, "Microsoft.Net.Workload.Emscripten.net7", "5");

            var knownWorkloadsFilePath = Path.Combine(_fakeDotnetRootDirectory, "sdk", currentSdkVersion, "IncludedWorkloadManifests.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(knownWorkloadsFilePath)!);
            File.WriteAllText(knownWorkloadsFilePath, @"
Microsoft.Net.Workload.Mono.Toolchain.net6
Microsoft.Net.Workload.Mono.Toolchain.net7
Microsoft.Net.Workload.Emscripten.net6
Microsoft.Net.Workload.Emscripten.net7"
                .Trim());

            var sdkDirectoryWorkloadManifestProvider = new SdkDirectoryWorkloadManifestProvider(sdkRootPath: _fakeDotnetRootDirectory, sdkVersion: currentSdkVersion, userProfileDir: null, globalJsonPath: null);

            GetManifestContents(sdkDirectoryWorkloadManifestProvider)
                .Should()
                .Equal($"Microsoft.Net.Workload.Mono.Toolchain.net6: 2/{currentSdkVersion}",
                       $"Microsoft.Net.Workload.Mono.Toolchain.net7: 3/{fallbackWorkloadBand}",
                       $"Microsoft.Net.Workload.Emscripten.net6: 4/{fallbackWorkloadBand}",
                       $"Microsoft.Net.Workload.Emscripten.net7: 5/{currentSdkVersion}",
                       $"NotInIncudedWorkloadsFile: 1/{currentSdkVersion}");
        }

        private void CreateMockManifest(string manifestRoot, string featureBand, string manifestId, string manifestVersion, bool useVersionFolder = false, string? manifestContents = null)
        {
            var manifestDirectory = Path.Combine(manifestRoot, featureBand, manifestId);
            if (useVersionFolder)
            {
                manifestDirectory = Path.Combine(manifestDirectory, manifestVersion);
            }

            if (!Directory.Exists(manifestDirectory))
            {
                Directory.CreateDirectory(manifestDirectory);
            }

            if (manifestContents == null)
            {
                manifestContents = $"{manifestId}: {manifestVersion}/{featureBand}";
            }

            File.WriteAllText(Path.Combine(manifestDirectory, "WorkloadManifest.json"), manifestContents);
        }

        private void CreateMockWorkloadSet(string manifestRoot, string featureBand, string workloadSetVersion, string workloadSetContents)
        {
            var workloadSetDirectory = Path.Combine(manifestRoot, featureBand, "workloadsets", workloadSetVersion);
            if (!Directory.Exists(workloadSetDirectory))
            {
                Directory.CreateDirectory(workloadSetDirectory);
            }
            File.WriteAllText(Path.Combine(workloadSetDirectory, "workloadset.workloadset.json"), workloadSetContents);
        }

        private string CreateMockInstallState(string featureBand, string installStateContents)
        {
            var installStateFolder = Path.Combine(_fakeDotnetRootDirectory!, "metadata", "workloads", "8.0.200", "InstallState");
            Directory.CreateDirectory(installStateFolder);

            string installStatePath = Path.Combine(installStateFolder, "default.json");

            File.WriteAllText(installStatePath, installStateContents);

            return installStatePath;
        }

        [Fact]
        public void ItShouldIgnoreManifestsNotFoundInFallback()
        {
            var testDirectory = _testAssetsManager.CreateTestDirectory().Path;
            var fakeDotnetRootDirectory = Path.Combine(testDirectory, "dotnet");

            // Write 6.0.100 manifests-> ios only
            var manifestDirectory6 = Path.Combine(fakeDotnetRootDirectory, "sdk-manifests", "6.0.100");
            Directory.CreateDirectory(manifestDirectory6);
            Directory.CreateDirectory(Path.Combine(manifestDirectory6, "iOS"));
            File.WriteAllText(Path.Combine(manifestDirectory6, "iOS", "WorkloadManifest.json"), "iOS: iOS-6.0.100");

            var knownWorkloadsFilePath = Path.Combine(fakeDotnetRootDirectory, "sdk", "6.0.100", "KnownWorkloadManifests.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(knownWorkloadsFilePath)!);
            File.WriteAllText(knownWorkloadsFilePath, "Android\niOS");

            var sdkDirectoryWorkloadManifestProvider
                = new SdkDirectoryWorkloadManifestProvider(sdkRootPath: fakeDotnetRootDirectory, sdkVersion: "6.0.100", userProfileDir: null, globalJsonPath: null);

            GetManifestContents(sdkDirectoryWorkloadManifestProvider)
                .Should()
                .BeEquivalentTo("iOS: iOS-6.0.100");
        }

        [Fact]
        public void WorkloadResolverUsesManifestsFromWorkloadSet()
        {
            Initialize("8.0.200");

            string manifestContents1 = """
    {
        "version": "11.0.1",
        "workloads": {
            "ios": {
                "description": "iOS workload",
                "kind": "dev",
                "packs": [ "Microsoft.NET.iOS.Workload" ]
            },
        },
        "packs": {
            "Microsoft.NET.iOS.Workload" : {
                "kind": "sdk",
                "version": "1"
            }
        }
    }
    """;

            string manifestContents2 = """
    {
        "version": "11.0.2",
        "workloads": {
            "ios": {
                "description": "iOS workload",
                "kind": "dev",
                "packs": [ "Microsoft.NET.iOS.Workload" ]
            },
        },
        "packs": {
            "Microsoft.NET.iOS.Workload" : {
                "kind": "sdk",
                "version": "2"
            }
        }
    }
    """;

            string manifestContents3 = """
    {
        "version": "12.0.1",
        "workloads": {
            "ios": {
                "description": "iOS workload",
                "kind": "dev",
                "packs": [ "Microsoft.NET.iOS.Workload" ]
            },
        },
        "packs": {
            "Microsoft.NET.iOS.Workload" : {
                "kind": "sdk",
                "version": "3"
            }
        }
    }
    """;

            CreateMockManifest(_manifestRoot, "8.0.100", "ios", "11.0.1", true, manifestContents1);
            CreateMockManifest(_manifestRoot, "8.0.100", "ios", "11.0.2", true, manifestContents2);
            CreateMockManifest(_manifestRoot, "8.0.200", "ios", "12.0.1", true, manifestContents3);

            CreateMockWorkloadSet(_manifestRoot, "8.0.200", "8.0.200", """
                {
                  "ios": "11.0.2/8.0.100"
                }
                """);

            var sdkDirectoryWorkloadManifestProvider
                = new SdkDirectoryWorkloadManifestProvider(sdkRootPath: _fakeDotnetRootDirectory, sdkVersion: "8.0.200", userProfileDir: null, globalJsonPath: null);

            var workloadResolver = WorkloadResolver.CreateForTests(sdkDirectoryWorkloadManifestProvider, _fakeDotnetRootDirectory);

            var workloads = workloadResolver.GetAvailableWorkloads();
            workloads.Count().Should().Be(1);
            var expectedPackId = new WorkloadPackId("Microsoft.NET.iOS.Workload");
            workloadResolver.GetPacksInWorkload(workloads.Single().Id).Should().BeEquivalentTo(new[] { expectedPackId });
            var packInfo = workloadResolver.TryGetPackInfo(expectedPackId);
            packInfo.Should().NotBeNull();
            packInfo!.Version.Should().Be("2");

            workloadResolver.GetInstalledManifests().Count().Should().Be(1);
            var manifestInfo = workloadResolver.GetInstalledManifests().Single();
            manifestInfo.Id.Should().Be("ios");
            manifestInfo.Version.Should().Be("11.0.2");
            manifestInfo.ManifestFeatureBand.Should().Be("8.0.100");
            manifestInfo.ManifestDirectory.Should().Be(Path.Combine(_manifestRoot, "8.0.100", "ios", "11.0.2"));
        }

        private IEnumerable<string> GetManifestContents(SdkDirectoryWorkloadManifestProvider manifestProvider)
        {
            return manifestProvider.GetManifests().Select(manifest =>
                {
                    var contents = new StreamReader(manifest.OpenManifestStream()).ReadToEnd();

                    string manifestId = contents.Split(':')[0];
                    manifest.ManifestId.Should().Be(manifestId);

                    return contents;
                });
        }

        private class EnvironmentMock
        {
            Dictionary<string, string> _mockedEnvironmentVariables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            public void Add(string variable, string value)
            {
                _mockedEnvironmentVariables[variable] = value;
            }

            public string? GetEnvironmentVariable(string variable)
            {
                if (_mockedEnvironmentVariables.TryGetValue(variable, out string? value))
                {
                    return value;
                }
                return Environment.GetEnvironmentVariable(variable);
            }
        }
    }
}

#if NETFRAMEWORK
namespace System.Diagnostics.CodeAnalysis
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property, AllowMultiple = true, Inherited = false)]
    sealed class MemberNotNullAttribute : Attribute
    {
        public MemberNotNullAttribute(params string[] members)
        {
            Members = members;
        }

        public MemberNotNullAttribute(string member)
        {
            Members = new[] { member };
        }

        public string[] Members { get; }
    }
}
#endif
