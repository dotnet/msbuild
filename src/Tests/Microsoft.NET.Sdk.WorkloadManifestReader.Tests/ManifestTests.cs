// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Microsoft.NET.Sdk.WorkloadManifestReader;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace ManifestReaderTests
{
    public class ManifestTests
    {
        private const string fakeRootPath = "fakeRootPath";

        public static readonly string[] TEST_RUNTIME_IDENTIFIER_CHAIN = new[] { "win-x64", "win", "any", "base" };

        [Fact]
        public void ItCanDeserialize()
        {
            using (FileStream fsSource = new FileStream(Path.Combine("Manifests", "Sample.json"), FileMode.Open, FileAccess.Read))
            {
                var result = WorkloadManifestReader.ReadWorkloadManifest(fsSource);
                result.Version.Should().Be(5);
                var xamAndroidId = new WorkloadPackId("Xamarin.Android.Sdk");

                result.Packs[xamAndroidId].Id.Should().Be(xamAndroidId);
                result.Packs[xamAndroidId].IsAlias.Should().Be(false);
                result.Packs[xamAndroidId].Kind.Should().Be(WorkloadPackKind.Sdk);
                result.Packs[xamAndroidId].Version.Should().Be("8.4.7");
            }
        }

        [Fact]
        public void AliasedPackPath()
        {
            var manifestProvider = new FakeManifestProvider(Path.Combine("Manifests", "Sample.json"));
            var resolver = WorkloadResolver.CreateForTests(manifestProvider, fakeRootPath, TEST_RUNTIME_IDENTIFIER_CHAIN);

            resolver.ReplaceFilesystemChecksForTest(_ => true, _ => true);

            var buildToolsPack = resolver.GetInstalledWorkloadPacksOfKind(WorkloadPackKind.Sdk).FirstOrDefault(pack => pack.Id == "Xamarin.Android.BuildTools");

            buildToolsPack.Should().NotBeNull();
            buildToolsPack.Id.Should().Be("Xamarin.Android.BuildTools");
            buildToolsPack.Version.Should().Be("8.4.7");
            buildToolsPack.Path.Should().Be(Path.Combine(fakeRootPath, "packs", "Xamarin.Android.BuildTools.Win64Host", "8.4.7"));
        }

        [Fact]
        public void CanSuggestSimpleWorkload()
        {
            var manifestProvider = new FakeManifestProvider(Path.Combine("Manifests", "Sample.json"));
            var resolver = new WorkloadResolver(manifestProvider, fakeRootPath);

            FakeFileSystemChecksSoThesePackagesAppearInstalled(resolver, "Xamarin.Android.Sdk", "Xamarin.Android.BuildTools");

            resolver.ReplacePlatformIdsForTest(new[] { "win-x64", "*" });

            var suggestions = resolver.GetWorkloadSuggestionForMissingPacks(new[] { "Mono.Android.Sdk" });
            suggestions.Count().Should().Be(1);
            suggestions.First().Id.Should().Be("xamarin-android-build");
        }

        [Fact]
        public void CanSuggestTwoWorkloadsToFulfilTwoRequirements()
        {
            var manifestProvider = new FakeManifestProvider(Path.Combine("Manifests", "Sample.json"));
            var resolver = new WorkloadResolver(manifestProvider, fakeRootPath);

            FakeFileSystemChecksSoThesePackagesAppearInstalled(resolver,
                //xamarin-android-build is fully installed
                "Xamarin.Android.Sdk",
                "Xamarin.Android.BuildTools",
                "Xamarin.Android.Framework",
                "Xamarin.Android.Runtime",
                "Mono.Android.Sdk");

            resolver.ReplacePlatformIdsForTest(new[] { "win-x64", "*" });

            var suggestions = resolver.GetWorkloadSuggestionForMissingPacks(new[] { "Mono.Android.Runtime.x86", "Mono.Android.Runtime.Armv7a" });
            suggestions.Count().Should().Be(2);
            suggestions.Should().Contain(s => s.Id == "xamarin-android-build-armv7a");
            suggestions.Should().Contain(s => s.Id == "xamarin-android-build-x86");
        }

        [Fact]
        public void CanSuggestWorkloadThatFulfillsTwoRequirements()
        {
            var manifestProvider = new FakeManifestProvider(Path.Combine("Manifests", "Sample.json"));
            var resolver = new WorkloadResolver(manifestProvider, fakeRootPath);

            FakeFileSystemChecksSoThesePackagesAppearInstalled(resolver,
                //xamarin-android-build is fully installed
                "Xamarin.Android.Sdk",
                "Xamarin.Android.BuildTools",
                "Xamarin.Android.Framework",
                "Xamarin.Android.Runtime",
                "Mono.Android.Sdk");

            resolver.ReplacePlatformIdsForTest(new[] { "win-x64", "*" });

            var suggestions = resolver.GetWorkloadSuggestionForMissingPacks(new[] { "Xamarin.Android.Templates", "Xamarin.Android.LLVM.Aot.armv7a" });
            suggestions.Count().Should().Be(1);
            suggestions.First().Id.Should().Be("xamarin-android-complete");
        }

        private static void FakeFileSystemChecksSoThesePackagesAppearInstalled(WorkloadResolver resolver, params string[] ids)
        {
            var installedPacks = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach(var id in ids)
            {
                installedPacks.Add(id);
            }

            resolver.ReplaceFilesystemChecksForTest(
                fileName =>
                {
                    var versionDir = Path.GetDirectoryName(fileName);
                    var idDir = Path.GetDirectoryName(versionDir);
                    return installedPacks.Contains(Path.GetFileName(idDir));
                },
                dirName =>
                {
                    var idDir = Path.GetDirectoryName(dirName);
                    return installedPacks.Contains(Path.GetFileName(idDir));
                });
        }
    }
}
