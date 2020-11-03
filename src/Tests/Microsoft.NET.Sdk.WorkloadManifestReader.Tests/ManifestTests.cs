// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Microsoft.NET.Sdk.WorkloadManifestReader;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

using WorkloadSuggestionCandidate = Microsoft.NET.Sdk.WorkloadManifestReader.WorkloadSuggestionFinder.WorkloadSuggestionCandidate;

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

        [Fact]
        public static void WorkloadsSuggestionsArePermutedCorrectly()
        {
            static HashSet<WorkloadPackId> ConstructPackHash (params string[] packIds)
                => new HashSet<WorkloadPackId> (packIds.Select(id => new WorkloadPackId(id)));

            static HashSet<WorkloadDefinitionId> ConstructWorkloadHash (params string[] workloadIds)
                => new HashSet<WorkloadDefinitionId> (workloadIds.Select(id => new WorkloadDefinitionId(id)));

            static WorkloadSuggestionCandidate ConstructCandidate(string[] workloadIds, string[] packIds, string[] unsatisfiedPackIds)
                => new WorkloadSuggestionCandidate (ConstructWorkloadHash(workloadIds), ConstructPackHash(packIds), ConstructPackHash(unsatisfiedPackIds));

            //we're looking for suggestions with "pack1", "pack2", "pack3"
            var partialSuggestions = new List<WorkloadSuggestionCandidate>
            {
                ConstructCandidate(new[] { "workload1" }, new[] { "pack1" }, new[] { "pack2", "pack3" }),
                ConstructCandidate(new[] { "workload2" }, new[] { "pack1", "pack2" }, new[] { "pack3" }),
                ConstructCandidate(new[] { "workload3" }, new[] { "pack2" }, new[] { "pack1", "pack3" }),
                ConstructCandidate(new[] { "workload4" }, new[] { "pack3" }, new[] { "pack1", "pack2" }),
                ConstructCandidate(new[] { "workload5" }, new[] { "pack2", "pack3" }, new[] { "pack1" })
            };

            var completeSuggestions = WorkloadSuggestionFinder.GatherCompleteSuggestions(partialSuggestions);

            Assert.Equal(4, completeSuggestions.Count);

            static int CountMatchingSuggestions(HashSet<WorkloadSuggestionCandidate> suggestions, params string[] workloadIds)
            {
                int found = 0;
                foreach(var suggestion in suggestions)
                {
                    if (suggestion.Workloads.Count == workloadIds.Length)
                    {
                        if (workloadIds.All(id => suggestion.Workloads.Contains(new WorkloadDefinitionId(id))))
                        {
                            found++;
                        }
                    }
                }
                return found;
            }

            Assert.Equal(1, CountMatchingSuggestions(completeSuggestions, "workload1", "workload3", "workload4"));
            Assert.Equal(1, CountMatchingSuggestions(completeSuggestions, "workload1", "workload5"));
            Assert.Equal(1, CountMatchingSuggestions(completeSuggestions, "workload2", "workload4"));
            Assert.Equal(1, CountMatchingSuggestions(completeSuggestions, "workload2", "workload5"));
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
