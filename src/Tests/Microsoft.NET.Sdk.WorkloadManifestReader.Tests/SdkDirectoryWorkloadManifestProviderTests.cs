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
using Microsoft.NET.Sdk.WorkloadManifestReader;
using Microsoft.NET.TestFramework;

using Xunit;
using Xunit.Abstractions;

namespace ManifestReaderTests
{

    public class SdkDirectoryWorkloadManifestProviderTests : SdkTest
    {
        private string? _testDirectory;
        private string? _manifestDirectory;
        private string? _fakeDotnetRootDirectory;

        public SdkDirectoryWorkloadManifestProviderTests(ITestOutputHelper logger) : base(logger)
        {
        }

        [MemberNotNull("_testDirectory", "_manifestDirectory", "_fakeDotnetRootDirectory")]
        void Initialize([CallerMemberName] string? testName = null, string? identifier = null)
        {
            _testDirectory = _testAssetsManager.CreateTestDirectory(testName, identifier).Path;
            _fakeDotnetRootDirectory = Path.Combine(_testDirectory, "dotnet");
            _manifestDirectory = Path.Combine(_fakeDotnetRootDirectory, "sdk-manifests", "5.0.100");
            Directory.CreateDirectory(_manifestDirectory);
        }

        [Fact]
        public void ItShouldReturnListOfManifestFiles()
        {
            Initialize();

            string androidManifestFileContent = "Android";
            string iosManifestFileContent = "iOS";
            Directory.CreateDirectory(Path.Combine(_manifestDirectory, "Android"));
            File.WriteAllText(Path.Combine(_manifestDirectory, "Android", "WorkloadManifest.json"), androidManifestFileContent);
            Directory.CreateDirectory(Path.Combine(_manifestDirectory, "iOS"));
            File.WriteAllText(Path.Combine(_manifestDirectory, "iOS", "WorkloadManifest.json"), iosManifestFileContent);

            var sdkDirectoryWorkloadManifestProvider
                = new SdkDirectoryWorkloadManifestProvider(sdkRootPath: _fakeDotnetRootDirectory, sdkVersion: "5.0.100", userProfileDir: null);

            GetManifestContents(sdkDirectoryWorkloadManifestProvider)
                .Should()
                .BeEquivalentTo(iosManifestFileContent, androidManifestFileContent);
        }

        [Fact]
        public void GivenSDKVersionItShouldReturnListOfManifestFilesForThisVersionBand()
        {
            Initialize();

            string androidManifestFileContent = "Android";
            Directory.CreateDirectory(Path.Combine(_manifestDirectory, "Android"));
            File.WriteAllText(Path.Combine(_manifestDirectory, "Android", "WorkloadManifest.json"), androidManifestFileContent);

            var sdkDirectoryWorkloadManifestProvider
                = new SdkDirectoryWorkloadManifestProvider(sdkRootPath: _fakeDotnetRootDirectory, sdkVersion: "5.0.100", userProfileDir: null);

            GetManifestContents(sdkDirectoryWorkloadManifestProvider)
                .Should()
                .BeEquivalentTo(androidManifestFileContent);
        }

        [Fact]
        public void GivenNoManifestDirectoryItShouldReturnEmpty()
        {
            Initialize();

            var sdkDirectoryWorkloadManifestProvider
                = new SdkDirectoryWorkloadManifestProvider(sdkRootPath: _fakeDotnetRootDirectory, sdkVersion: "5.0.100", userProfileDir: null);
            sdkDirectoryWorkloadManifestProvider.GetManifests().Should().BeEmpty();
        }

        [Fact]
        public void GivenNoManifestJsonFileInDirectoryItShouldThrow()
        {
            Initialize();

            Directory.CreateDirectory(Path.Combine(_manifestDirectory, "Android"));

            var sdkDirectoryWorkloadManifestProvider
                = new SdkDirectoryWorkloadManifestProvider(sdkRootPath: _fakeDotnetRootDirectory, sdkVersion: "5.0.100", userProfileDir: null);

            Action a = () => sdkDirectoryWorkloadManifestProvider.GetManifests().Select(m => {
                using (m.OpenManifestStream()) { }
                return true;
            }).ToList();

            a.ShouldThrow<FileNotFoundException>();
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
            File.WriteAllText(Path.Combine(additionalManifestDirectory, sdkVersion, "Android", "WorkloadManifest.json"), "AndroidContent");

            //  Manifest in default directory
            Directory.CreateDirectory(Path.Combine(_manifestDirectory, "iOS"));
            File.WriteAllText(Path.Combine(_manifestDirectory, "iOS", "WorkloadManifest.json"), "iOSContent");


            var sdkDirectoryWorkloadManifestProvider
                = new SdkDirectoryWorkloadManifestProvider(sdkRootPath: _fakeDotnetRootDirectory, sdkVersion: sdkVersion, environmentMock.GetEnvironmentVariable, userProfileDir: null);

            GetManifestContents(sdkDirectoryWorkloadManifestProvider)
                .Should()
                .BeEquivalentTo("AndroidContent", "iOSContent");
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
            File.WriteAllText(Path.Combine(additionalManifestDirectory, sdkVersion, "Android", "WorkloadManifest.json"), "OverridingAndroidContent");

            //  Manifest in default directory
            Directory.CreateDirectory(Path.Combine(_manifestDirectory, "Android"));
            File.WriteAllText(Path.Combine(_manifestDirectory, "Android", "WorkloadManifest.json"), "OverriddenAndroidContent");

            var sdkDirectoryWorkloadManifestProvider
                = new SdkDirectoryWorkloadManifestProvider(sdkRootPath: _fakeDotnetRootDirectory, sdkVersion: sdkVersion, environmentMock.GetEnvironmentVariable, userProfileDir: null);

            GetManifestContents(sdkDirectoryWorkloadManifestProvider)
                .Should()
                .BeEquivalentTo("OverridingAndroidContent");

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
            Directory.CreateDirectory(Path.Combine(_manifestDirectory, "iOS"));
            File.WriteAllText(Path.Combine(_manifestDirectory, "iOS", "WorkloadManifest.json"), "iOSContent");

            Directory.CreateDirectory(Path.Combine(_manifestDirectory, "Android"));
            File.WriteAllText(Path.Combine(_manifestDirectory, "Android", "WorkloadManifest.json"), "DefaultAndroidContent");

            //  Manifests in first additional directory
            Directory.CreateDirectory(Path.Combine(additionalManifestDirectory1, sdkVersion, "Android"));
            File.WriteAllText(Path.Combine(additionalManifestDirectory1, sdkVersion, "Android", "WorkloadManifest.json"), "AndroidContent1");

            //  Manifests in second additional directory
            Directory.CreateDirectory(Path.Combine(additionalManifestDirectory2, sdkVersion, "Android"));
            File.WriteAllText(Path.Combine(additionalManifestDirectory2, sdkVersion, "Android", "WorkloadManifest.json"), "AndroidContent2");

            Directory.CreateDirectory(Path.Combine(additionalManifestDirectory2, sdkVersion, "Test"));
            File.WriteAllText(Path.Combine(additionalManifestDirectory2, sdkVersion, "Test", "WorkloadManifest.json"), "TestContent2");

            var sdkDirectoryWorkloadManifestProvider
                = new SdkDirectoryWorkloadManifestProvider(sdkRootPath: _fakeDotnetRootDirectory, sdkVersion: sdkVersion, environmentMock.GetEnvironmentVariable, userProfileDir: null);

            GetManifestContents(sdkDirectoryWorkloadManifestProvider)
                .Should()
                .BeEquivalentTo("AndroidContent1", "iOSContent", "TestContent2");
         
        }

        [Fact]
        public void IfTestHookFolderDoesNotExistItShouldBeIgnored()
        {
            Initialize();

            var additionalManifestDirectory = Path.Combine(_testDirectory, "AdditionalManifests");
                
            var environmentMock = new EnvironmentMock();
            environmentMock.Add(EnvironmentVariableNames.WORKLOAD_MANIFEST_ROOTS, additionalManifestDirectory);

            //  Manifest in default directory
            Directory.CreateDirectory(Path.Combine(_manifestDirectory, "Android"));
            File.WriteAllText(Path.Combine(_manifestDirectory, "Android", "WorkloadManifest.json"), "AndroidContent");

            var sdkDirectoryWorkloadManifestProvider
                = new SdkDirectoryWorkloadManifestProvider(sdkRootPath: _fakeDotnetRootDirectory, sdkVersion: "5.0.100", environmentMock.GetEnvironmentVariable, userProfileDir: null);

            GetManifestContents(sdkDirectoryWorkloadManifestProvider)
                .Should()
                .BeEquivalentTo("AndroidContent");
         
        }

        [Fact]
        public void ItShouldIgnoreOutdatedManifestIds()
        {
            Initialize();

            Directory.CreateDirectory(Path.Combine(_manifestDirectory, "iOS"));
            File.WriteAllText(Path.Combine(_manifestDirectory, "iOS", "WorkloadManifest.json"), "iOSContent");
            Directory.CreateDirectory(Path.Combine(_manifestDirectory, "Microsoft.NET.Workload.Android"));
            File.WriteAllText(Path.Combine(_manifestDirectory, "Microsoft.NET.Workload.Android", "WorkloadManifest.json"), "iOSContent");

            var sdkDirectoryWorkloadManifestProvider
                = new SdkDirectoryWorkloadManifestProvider(sdkRootPath: _fakeDotnetRootDirectory, sdkVersion: "5.0.100", userProfileDir: null);

            GetManifestContents(sdkDirectoryWorkloadManifestProvider)
                .Should()
                .BeEquivalentTo("iOSContent");
        }

        [Fact]
        public void ItShouldFallbackWhenFeatureBandHasNoManifests()
        {
            var testDirectory = _testAssetsManager.CreateTestDirectory().Path;
            var fakeDotnetRootDirectory = Path.Combine(testDirectory, "dotnet");

            // Write 4.0.100 manifests-> android only
            CreateMockManifest(fakeDotnetRootDirectory, "4.0.100", "Android");

            // Write 5.0.100 manifests-> ios and android
            CreateMockManifest(fakeDotnetRootDirectory, "5.0.100", "Android");
            CreateMockManifest(fakeDotnetRootDirectory, "5.0.100", "iOS");

            // Write 6.0.100 manifests-> ios only
            CreateMockManifest(fakeDotnetRootDirectory, "6.0.100", "iOS");

            // Write 7.0.100 manifests-> ios and android
            CreateMockManifest(fakeDotnetRootDirectory, "7.0.100", "Android");
            CreateMockManifest(fakeDotnetRootDirectory, "7.0.100", "iOS");

            var knownWorkloadsFilePath = Path.Combine(fakeDotnetRootDirectory, "sdk", "6.0.100", "IncludedWorkloadManifests.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(knownWorkloadsFilePath)!);
            File.WriteAllText(knownWorkloadsFilePath, "Android\niOS");

            var sdkDirectoryWorkloadManifestProvider
                = new SdkDirectoryWorkloadManifestProvider(sdkRootPath: fakeDotnetRootDirectory, sdkVersion: "6.0.100", userProfileDir: null);

            GetManifestContents(sdkDirectoryWorkloadManifestProvider)
                .Should()
                .BeEquivalentTo("6.0.100/iOS", "5.0.100/Android");
        }

        [Fact]
        public void ItShouldFallbackWhenPreviewFeatureBandHasNoManifests()
        {
            var testDirectory = _testAssetsManager.CreateTestDirectory().Path;
            var fakeDotnetRootDirectory = Path.Combine(testDirectory, "dotnet");

            // Write 4.0.100 manifests-> android only
            CreateMockManifest(fakeDotnetRootDirectory, "4.0.100", "iOS");

            // Write 5.0.100 manifests-> android
            CreateMockManifest(fakeDotnetRootDirectory, "5.0.100", "Android");

            // Write 6.0.100-preview.2 manifests-> ios only
            CreateMockManifest(fakeDotnetRootDirectory, "6.0.100-preview.2", "iOS");

            // Write 7.0.100 manifests-> ios and android
            CreateMockManifest(fakeDotnetRootDirectory, "7.0.100", "Android");
            CreateMockManifest(fakeDotnetRootDirectory, "7.0.100", "iOS");

            var prev4Version = "6.0.100-preview.4.12345";
            var knownWorkloadsFilePath = Path.Combine(fakeDotnetRootDirectory, "sdk", prev4Version, "IncludedWorkloadManifests.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(knownWorkloadsFilePath)!);
            File.WriteAllText(knownWorkloadsFilePath, "Android\niOS");

            var sdkDirectoryWorkloadManifestProvider
                = new SdkDirectoryWorkloadManifestProvider(sdkRootPath: fakeDotnetRootDirectory, sdkVersion: prev4Version, userProfileDir: null);

            GetManifestContents(sdkDirectoryWorkloadManifestProvider)
                .Should()
                .BeEquivalentTo("6.0.100-preview.2/iOS", "5.0.100/Android");
        }

        [Fact]
        public void ItShouldRollForwardToNonPrereleaseWhenPreviewFeatureBandHasNoManifests()
        {
            var testDirectory = _testAssetsManager.CreateTestDirectory().Path;
            var fakeDotnetRootDirectory = Path.Combine(testDirectory, "dotnet");

            // Write 4.0.100 manifests-> android only
            CreateMockManifest(fakeDotnetRootDirectory, "4.0.100", "Android");

            // Write 5.0.100 manifests-> ios and android
            CreateMockManifest(fakeDotnetRootDirectory, "5.0.100", "Android");
            CreateMockManifest(fakeDotnetRootDirectory, "5.0.100", "iOS");

            // Write 6.0.100-preview.4 manifests-> ios only
            CreateMockManifest(fakeDotnetRootDirectory, "6.0.100-preview.4", "iOS");

            // Write 6.0.100 manifests-> android
            CreateMockManifest(fakeDotnetRootDirectory, "6.0.100", "Android");

            var prev4Version = "6.0.100-preview.4.12345";
            var knownWorkloadsFilePath = Path.Combine(fakeDotnetRootDirectory, "sdk", prev4Version, "IncludedWorkloadManifests.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(knownWorkloadsFilePath)!);
            File.WriteAllText(knownWorkloadsFilePath, "Android\niOS");

            var sdkDirectoryWorkloadManifestProvider
                = new SdkDirectoryWorkloadManifestProvider(sdkRootPath: fakeDotnetRootDirectory, sdkVersion: prev4Version, userProfileDir: null);

            GetManifestContents(sdkDirectoryWorkloadManifestProvider)
                .Should()
                .BeEquivalentTo("6.0.100-preview.4/iOS", "6.0.100/Android");
        }

        private void CreateMockManifest(string rootDir, string version, string manifestId)
        {
            var manifestDirectory = Path.Combine(rootDir, "sdk-manifests", version);
            if (!Directory.Exists(manifestDirectory))
            {
                Directory.CreateDirectory(manifestDirectory);
            }
            if (!Directory.Exists(Path.Combine(manifestDirectory, manifestId)))
            {
                Directory.CreateDirectory(Path.Combine(manifestDirectory, manifestId));
            }
            File.WriteAllText(Path.Combine(manifestDirectory, manifestId, "WorkloadManifest.json"), $"{version}/{manifestId}");
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
            File.WriteAllText(Path.Combine(manifestDirectory6, "iOS", "WorkloadManifest.json"), "iOS-6.0.100");

            var knownWorkloadsFilePath = Path.Combine(fakeDotnetRootDirectory, "sdk", "6.0.100", "IncludedWorkloadManifests.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(knownWorkloadsFilePath)!);
            File.WriteAllText(knownWorkloadsFilePath, "Android\niOS");

            var sdkDirectoryWorkloadManifestProvider
                = new SdkDirectoryWorkloadManifestProvider(sdkRootPath: fakeDotnetRootDirectory, sdkVersion: "6.0.100", userProfileDir: null);

            GetManifestContents(sdkDirectoryWorkloadManifestProvider)
                .Should()
                .BeEquivalentTo("iOS-6.0.100");

        }

        private IEnumerable<string> GetManifestContents(SdkDirectoryWorkloadManifestProvider manifestProvider)
        {
            return manifestProvider.GetManifests().Select(manifest => new StreamReader(manifest.OpenManifestStream()).ReadToEnd());
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
