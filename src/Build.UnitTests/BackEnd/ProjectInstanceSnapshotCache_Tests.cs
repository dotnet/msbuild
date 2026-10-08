// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Build.BackEnd;
using Microsoft.Build.BackEnd.SdkResolution;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Evaluation.Context;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.UnitTests;
using Shouldly;
using Xunit;
using SdkResult = Microsoft.Build.BackEnd.SdkResolution.SdkResult;

#nullable enable

namespace Microsoft.Build.UnitTests.BackEnd;

public sealed class ProjectInstanceSnapshotCache_Tests
{
    [Fact]
    public void EquivalentEvaluationIdentityProducesEqualKeys()
    {
        var firstProperties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Configuration"] = "Debug",
            ["TargetFramework"] = "net10.0",
        };
        var secondProperties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["targetframework"] = "net10.0",
            ["configuration"] = "Debug",
        };

        string projectPath = ProjectPath("App.csproj");
        var first = CreateKey(projectPath, "Current", ProjectLoadSettings.Default, firstProperties);
        var second = CreateKey(projectPath, "current", ProjectLoadSettings.Default, secondProperties);

        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void ProjectPathCasingFollowsFileSystemSemantics()
    {
        string projectPath = ProjectPath("App.csproj");
        ProjectInstanceSnapshotCacheKey first = CreateKey(
            projectPath,
            "Current",
            ProjectLoadSettings.Default,
            new Dictionary<string, string>());
        ProjectInstanceSnapshotCacheKey differentCase = CreateKey(
            projectPath.ToUpperInvariant(),
            "Current",
            ProjectLoadSettings.Default,
            new Dictionary<string, string>());

        if (FileUtilities.IsFileSystemCaseSensitive)
        {
            differentCase.ShouldNotBe(first);
        }
        else
        {
            differentCase.ShouldBe(first);
            differentCase.GetHashCode().ShouldBe(first.GetHashCode());
        }
    }

    [Theory]
    [InlineData("Release", "Current", ProjectLoadSettings.Default)]
    [InlineData("Debug", "17.0", ProjectLoadSettings.Default)]
    [InlineData("Debug", "Current", ProjectLoadSettings.IgnoreMissingImports)]
    public void DifferentEvaluationIdentityProducesDifferentKeys(
        string configuration,
        string toolsVersion,
        ProjectLoadSettings projectLoadSettings)
    {
        ProjectInstanceSnapshotCacheKey baseline = CreateKey(
            ProjectPath("App.csproj"),
            "Current",
            ProjectLoadSettings.Default,
            new Dictionary<string, string> { ["Configuration"] = "Debug" });
        ProjectInstanceSnapshotCacheKey different = CreateKey(
            ProjectPath("App.csproj"),
            toolsVersion,
            projectLoadSettings,
            new Dictionary<string, string> { ["Configuration"] = configuration });

        different.ShouldNotBe(baseline);
    }

    [Fact]
    public void ExplicitToolsVersionChangesIdentity()
    {
        ProjectInstanceSnapshotCacheKey implicitToolsVersion = CreateKey(
            ProjectPath("App.csproj"),
            "Current",
            ProjectLoadSettings.Default,
            new Dictionary<string, string>(),
            explicitToolsVersionSpecified: false);
        ProjectInstanceSnapshotCacheKey explicitToolsVersion = CreateKey(
            ProjectPath("App.csproj"),
            "Current",
            ProjectLoadSettings.Default,
            new Dictionary<string, string>(),
            explicitToolsVersionSpecified: true);

        explicitToolsVersion.ShouldNotBe(implicitToolsVersion);
    }

    [Fact]
    public void SubToolsetVersionChangesIdentity()
    {
        ProjectInstanceSnapshotCacheKey first = CreateKey(
            ProjectPath("App.csproj"),
            "Current",
            ProjectLoadSettings.Default,
            new Dictionary<string, string>(),
            subToolsetVersion: "15.0");
        ProjectInstanceSnapshotCacheKey second = CreateKey(
            ProjectPath("App.csproj"),
            "Current",
            ProjectLoadSettings.Default,
            new Dictionary<string, string>(),
            subToolsetVersion: "16.0");

        second.ShouldNotBe(first);
    }

    [Fact]
    public void HashUsesDeduplicatedGlobalProperties()
    {
        var duplicateNames = new Dictionary<string, string>
        {
            ["Configuration"] = "Debug",
            ["configuration"] = "Release",
        };
        ProjectInstanceSnapshotCacheKey duplicateKey = CreateKey(
            ProjectPath("App.csproj"),
            "Current",
            ProjectLoadSettings.Default,
            duplicateNames);
        ProjectInstanceSnapshotCacheKey canonicalKey = CreateKey(
            ProjectPath("App.csproj"),
            "Current",
            ProjectLoadSettings.Default,
            new Dictionary<string, string> { ["Configuration"] = "Release" });

        duplicateKey.ShouldBe(canonicalKey);
        duplicateKey.GetHashCode().ShouldBe(canonicalKey.GetHashCode());
    }

    [Fact]
    public void KeyCopiesGlobalProperties()
    {
        var properties = new Dictionary<string, string>
        {
            ["Configuration"] = "Debug",
        };
        ProjectInstanceSnapshotCacheKey key = CreateKey(
            ProjectPath("App.csproj"),
            "Current",
            ProjectLoadSettings.Default,
            properties);

        properties["Configuration"] = "Release";

        ProjectInstanceSnapshotCacheKey originalIdentity = CreateKey(
            ProjectPath("App.csproj"),
            "Current",
            ProjectLoadSettings.Default,
            new Dictionary<string, string> { ["Configuration"] = "Debug" });
        key.ShouldBe(originalIdentity);
    }

    [Fact]
    public void RequestContextAndCommandLineProvenanceChangeIdentity()
    {
        string projectPath = ProjectPath("App.csproj");
        var baseline = new ProjectInstanceSnapshotCacheKey(
            projectPath,
            "Current",
            explicitToolsVersionSpecified: false,
            subToolsetVersion: null,
            ProjectLoadSettings.Default,
            new Dictionary<string, string>(),
            interactive: false,
            maxNodeCount: 1,
            startupDirectory: ProjectPath("startup"),
            workingDirectory: ProjectPath("working"),
            culture: "en-US",
            uiCulture: "en-US",
            engineVersion: "1",
            disabledChangeWave: null,
            environmentFingerprint: 10,
            parserConfigurationFingerprint: 20,
            toolsetFingerprint: 30,
            commandLinePropertyNames: "Configuration\0");
        var different = new ProjectInstanceSnapshotCacheKey(
            projectPath,
            "Current",
            explicitToolsVersionSpecified: false,
            subToolsetVersion: null,
            ProjectLoadSettings.Default,
            new Dictionary<string, string>(),
            interactive: true,
            maxNodeCount: 1,
            startupDirectory: ProjectPath("startup"),
            workingDirectory: ProjectPath("working"),
            culture: "en-US",
            uiCulture: "en-US",
            engineVersion: "1",
            disabledChangeWave: null,
            environmentFingerprint: 10,
            parserConfigurationFingerprint: 20,
            toolsetFingerprint: 30,
            commandLinePropertyNames: "Configuration\0");
        var differentProvenance = new ProjectInstanceSnapshotCacheKey(
            projectPath,
            "Current",
            explicitToolsVersionSpecified: false,
            subToolsetVersion: null,
            ProjectLoadSettings.Default,
            new Dictionary<string, string>(),
            interactive: false,
            maxNodeCount: 1,
            startupDirectory: ProjectPath("startup"),
            workingDirectory: ProjectPath("working"),
            culture: "en-US",
            uiCulture: "en-US",
            engineVersion: "1",
            disabledChangeWave: null,
            environmentFingerprint: 10,
            parserConfigurationFingerprint: 20,
            toolsetFingerprint: 30,
            commandLinePropertyNames: "Other\0");

        different.ShouldNotBe(baseline);
        differentProvenance.ShouldNotBe(baseline);
        different.ShouldNotBe(baseline);
    }

    [Fact]
    public void AddOrReplaceStoresSnapshotByEquivalentKey()
    {
        var cache = new ProjectInstanceSnapshotCache();
        ProjectInstanceSnapshotCacheKey storedKey = CreateKey(
            ProjectPath("App.csproj"),
            "Current",
            ProjectLoadSettings.Default,
            new Dictionary<string, string> { ["Configuration"] = "Debug" });
        ProjectInstanceSnapshotCacheKey lookupKey = CreateKey(
            ProjectPath("App.csproj"),
            "current",
            ProjectLoadSettings.Default,
            new Dictionary<string, string> { ["configuration"] = "Debug" });
        ProjectInstanceSnapshotCacheEntry entry = CreateEntry("first");

        cache.AddOrReplace(storedKey, entry).ShouldBeTrue();

        cache.TryGet(lookupKey, out ProjectInstanceSnapshotCacheEntry? found).ShouldBeTrue();
        found.ShouldBeSameAs(entry);
        cache.Count.ShouldBe(1);
    }

    [Fact]
    public void AddOrReplaceReplacesEquivalentEntry()
    {
        var cache = new ProjectInstanceSnapshotCache();
        ProjectInstanceSnapshotCacheKey firstKey = CreateKey(
            ProjectPath("App.csproj"),
            "Current",
            ProjectLoadSettings.Default,
            new Dictionary<string, string> { ["Configuration"] = "Debug" });
        ProjectInstanceSnapshotCacheKey equivalentKey = CreateKey(
            ProjectPath("App.csproj"),
            "Current",
            ProjectLoadSettings.Default,
            new Dictionary<string, string> { ["configuration"] = "Debug" });
        ProjectInstanceSnapshotCacheEntry first = CreateEntry("first");
        ProjectInstanceSnapshotCacheEntry replacement = CreateEntry("replacement");

        cache.AddOrReplace(firstKey, first).ShouldBeTrue();
        cache.AddOrReplace(equivalentKey, replacement).ShouldBeTrue();

        cache.TryGet(firstKey, out ProjectInstanceSnapshotCacheEntry? found).ShouldBeTrue();
        found.ShouldBeSameAs(replacement);
        cache.Count.ShouldBe(1);
    }

    [Fact]
    public void RemoveAndClearDiscardEntries()
    {
        var cache = new ProjectInstanceSnapshotCache();
        ProjectInstanceSnapshotCacheKey firstKey = CreateKey(
            ProjectPath("First.csproj"),
            "Current",
            ProjectLoadSettings.Default,
            new Dictionary<string, string>());
        ProjectInstanceSnapshotCacheKey secondKey = CreateKey(
            ProjectPath("Second.csproj"),
            "Current",
            ProjectLoadSettings.Default,
            new Dictionary<string, string>());

        ProjectInstanceSnapshotCacheEntry first = CreateEntry("first");
        ProjectInstanceSnapshotCacheEntry second = CreateEntry("second");
        cache.AddOrReplace(firstKey, first).ShouldBeTrue();
        cache.AddOrReplace(secondKey, second).ShouldBeTrue();

        cache.Remove(firstKey).ShouldBeTrue();
        cache.TryGet(firstKey, out _).ShouldBeFalse();
        cache.Count.ShouldBe(1);
        cache.CurrentSizeBytes.ShouldBe(secondKey.RetainedSizeBytes + second.RetainedSizeBytes);

        cache.Clear();
        cache.TryGet(secondKey, out _).ShouldBeFalse();
        cache.Count.ShouldBe(0);
        cache.CurrentSizeBytes.ShouldBe(0);
    }

    [Fact]
    public void ConditionalRemoveDoesNotDeleteConcurrentReplacement()
    {
        var cache = new ProjectInstanceSnapshotCache();
        ProjectInstanceSnapshotCacheKey key = EmptyKey("Concurrent.csproj");
        ProjectInstanceSnapshotCacheEntry stale = CreateEntry("stale");
        ProjectInstanceSnapshotCacheEntry replacement = CreateEntry("replacement");
        cache.AddOrReplace(key, stale).ShouldBeTrue();
        cache.AddOrReplace(key, replacement).ShouldBeTrue();

        cache.Remove(key, stale).ShouldBeFalse();

        cache.TryGet(key, out ProjectInstanceSnapshotCacheEntry? retained).ShouldBeTrue();
        retained.ShouldBeSameAs(replacement);
    }

    [Fact]
    public void DefaultMaximumSizeIs256MiB()
    {
        var cache = new ProjectInstanceSnapshotCache();

        cache.MaximumSizeBytes.ShouldBe(256L * 1024 * 1024);
    }

    [Fact]
    public void CacheEntryIncludesSnapshotAndValidationDataSize()
    {
        ProjectInstanceSnapshot snapshot = CreateSnapshot("entry");
        var validationData = new TestValidationData(123);

        var entry = new ProjectInstanceSnapshotCacheEntry(snapshot, validationData);

        entry.Snapshot.ShouldBeSameAs(snapshot);
        entry.ValidationData.ShouldBeSameAs(validationData);
        entry.RetainedSizeBytes.ShouldBe(snapshot.EstimatedRetainedSizeBytes + 123L);
    }

    [Fact]
    public void CacheEntryRejectsNegativeValidationDataSize()
    {
        ProjectInstanceSnapshot snapshot = CreateSnapshot("entry");

        Should.Throw<ArgumentOutOfRangeException>(
            () => new ProjectInstanceSnapshotCacheEntry(snapshot, new TestValidationData(-1)));
    }

    [Fact]
    public void CacheEntrySizeSaturatesOnOverflow()
    {
        ProjectInstanceSnapshotCacheEntry entry =
            new(CreateSnapshot("entry"), new TestValidationData(long.MaxValue));

        entry.RetainedSizeBytes.ShouldBe(long.MaxValue);
    }

    [Fact]
    public void RejectingValidatorIsFailClosed()
    {
        ProjectInstanceSnapshotCacheKey key = EmptyKey("Rejected.csproj");
        ProjectInstanceSnapshotCacheEntry entry = CreateEntry("rejected");
        var cache = new ProjectInstanceSnapshotCache();
        var injectedValidator = new AcceptingTestValidator();

        ProjectInstanceSnapshotValidationResult result =
            RejectingProjectInstanceSnapshotValidator.Instance.Validate(key, entry);

        cache.Validator.ShouldBeSameAs(RejectingProjectInstanceSnapshotValidator.Instance);
        cache.Validator = injectedValidator;
        cache.Validator.ShouldBeSameAs(injectedValidator);
        result.ShouldBe(ProjectInstanceSnapshotValidationResult.Invalid);
        default(ProjectInstanceSnapshotValidationResult)
            .ShouldBe(ProjectInstanceSnapshotValidationResult.Invalid);
        ProjectInstanceSnapshotValidationResult.Valid
            .ShouldNotBe(ProjectInstanceSnapshotValidationResult.Invalid);
    }

    [Fact]
    public void ExceedingBoundEvictsLeastRecentlyUsedEntry()
    {
        ProjectInstanceSnapshotCacheEntry entry = CreateEntry("same-size");
        ProjectInstanceSnapshotCacheKey firstKey = EmptyKey("First.csproj");
        ProjectInstanceSnapshotCacheKey secondKey = EmptyKey("Second.csproj");
        ProjectInstanceSnapshotCacheKey thirdKey = EmptyKey("Third.csproj");
        var cache = new ProjectInstanceSnapshotCache(
            firstKey.RetainedSizeBytes
            + secondKey.RetainedSizeBytes
            + (entry.RetainedSizeBytes * 2L));

        cache.AddOrReplace(firstKey, entry).ShouldBeTrue();
        cache.AddOrReplace(secondKey, entry).ShouldBeTrue();
        cache.TryGet(firstKey, out _).ShouldBeTrue();

        cache.AddOrReplace(thirdKey, entry).ShouldBeTrue();

        cache.TryGet(firstKey, out _).ShouldBeTrue();
        cache.TryGet(secondKey, out _).ShouldBeFalse();
        cache.TryGet(thirdKey, out _).ShouldBeTrue();
        cache.Count.ShouldBe(2);
        cache.CurrentSizeBytes.ShouldBe(
            firstKey.RetainedSizeBytes
            + thirdKey.RetainedSizeBytes
            + (entry.RetainedSizeBytes * 2L));
    }

    [Fact]
    public void ReplacementUpdatesSizeAndRecency()
    {
        ProjectInstanceSnapshotCacheEntry small = CreateEntry("small");
        ProjectInstanceSnapshotCacheEntry large =
            CreateEntry("large", validationDataSizeBytes: 128);
        ProjectInstanceSnapshotCacheKey firstKey = EmptyKey("First.csproj");
        ProjectInstanceSnapshotCacheKey secondKey = EmptyKey("Second.csproj");
        ProjectInstanceSnapshotCacheKey thirdKey = EmptyKey("Third.csproj");
        long initialSize = firstKey.RetainedSizeBytes
            + secondKey.RetainedSizeBytes
            + (small.RetainedSizeBytes * 2L);
        long finalSize = firstKey.RetainedSizeBytes
            + thirdKey.RetainedSizeBytes
            + large.RetainedSizeBytes
            + small.RetainedSizeBytes;
        var cache = new ProjectInstanceSnapshotCache(Math.Max(initialSize, finalSize));

        cache.AddOrReplace(firstKey, small).ShouldBeTrue();
        cache.AddOrReplace(secondKey, small).ShouldBeTrue();
        cache.AddOrReplace(firstKey, large).ShouldBeTrue();
        cache.AddOrReplace(thirdKey, small).ShouldBeTrue();

        cache.TryGet(firstKey, out ProjectInstanceSnapshotCacheEntry? found).ShouldBeTrue();
        found.ShouldBeSameAs(large);
        cache.TryGet(secondKey, out _).ShouldBeFalse();
        cache.TryGet(thirdKey, out _).ShouldBeTrue();
        cache.Count.ShouldBe(2);
        cache.CurrentSizeBytes.ShouldBe(finalSize);
    }

    [Fact]
    public void OversizedSnapshotIsNotCachedAndRemovesExistingEntry()
    {
        ProjectInstanceSnapshotCacheEntry small = CreateEntry("small");
        ProjectInstanceSnapshotCacheEntry oversized =
            CreateEntry(
                "oversized",
                validationDataSizeBytes: small.RetainedSizeBytes + 1);
        ProjectInstanceSnapshotCacheKey retainedKey = EmptyKey("Retained.csproj");
        ProjectInstanceSnapshotCacheKey oversizedKey = EmptyKey("Oversized.csproj");
        var cache = new ProjectInstanceSnapshotCache(
            retainedKey.RetainedSizeBytes + small.RetainedSizeBytes);

        cache.AddOrReplace(retainedKey, small).ShouldBeTrue();
        cache.AddOrReplace(oversizedKey, oversized).ShouldBeFalse();

        cache.TryGet(retainedKey, out _).ShouldBeTrue();
        cache.TryGet(oversizedKey, out _).ShouldBeFalse();

        cache.AddOrReplace(retainedKey, oversized).ShouldBeFalse();
        cache.TryGet(retainedKey, out _).ShouldBeFalse();
        cache.Count.ShouldBe(0);
        cache.CurrentSizeBytes.ShouldBe(0);
    }

    [Fact]
    public void CacheBudgetIncludesKeyPayload()
    {
        ProjectInstanceSnapshotCacheEntry entry = CreateEntry("key-budget");
        var cache = new ProjectInstanceSnapshotCache(entry.RetainedSizeBytes + 512);
        ProjectInstanceSnapshotCacheKey key = CreateKey(
            ProjectPath("LargeKey.csproj"),
            "Current",
            ProjectLoadSettings.Default,
            new Dictionary<string, string>
            {
                ["LargeProperty"] = new string('x', 4096),
            });

        cache.AddOrReplace(key, entry).ShouldBeFalse();
        cache.Count.ShouldBe(0);
        cache.CurrentSizeBytes.ShouldBe(0);
    }

    [Fact]
    public void CacheBudgetIncludesSdkManifestPayload()
    {
        ProjectInstanceSnapshotCacheKey key = EmptyKey("SdkPayload.csproj");
        ProjectInstanceSnapshotCacheEntry baseline = CreateEntry("sdk-budget");
        var recorder = new EvaluationInputRecorder();
        var result = new SdkResult(
            new SdkReference("LargeSdk", null, null),
            ProjectPath("sdk"),
            "1.0",
            warnings: null,
            propertiesToAdd: new Dictionary<string, string>
            {
                ["LargeResolverProperty"] = new string('x', 4096),
            });
        recorder.RecordSdkResolution(
            result.SdkReference,
            result,
            ElementLocation.Create(ProjectPath("SdkPayload.csproj")),
            solutionPath: null,
            ProjectPath("SdkPayload.csproj"),
            interactive: false,
            isRunningInVisualStudio: false,
            failOnUnresolvedSdk: true);
        var entry = new ProjectInstanceSnapshotCacheEntry(
            baseline.Snapshot,
            new EvaluationInputsSnapshotValidationData(recorder.Freeze(key.ToEvaluationInputKey())));
        var cache = new ProjectInstanceSnapshotCache(
            key.RetainedSizeBytes + baseline.RetainedSizeBytes + 512);

        cache.AddOrReplace(key, entry).ShouldBeFalse();
        cache.CurrentSizeBytes.ShouldBe(0);
    }

    [Fact]
    public void CacheBudgetIncludesRegistryManifestPayload()
    {
        ProjectInstanceSnapshotCacheKey key = EmptyKey("RegistryPayload.csproj");
        ProjectInstanceSnapshotCacheEntry baseline = CreateEntry("registry-budget");
        var recorder = new EvaluationInputRecorder();
        recorder.RecordRegistryRead(
            new string('k', 4096),
            new string('v', 4096),
            new string('x', 4096),
            ImmutableArray.Create(new string('r', 4096)));
        var entry = new ProjectInstanceSnapshotCacheEntry(
            baseline.Snapshot,
            new EvaluationInputsSnapshotValidationData(recorder.Freeze(key.ToEvaluationInputKey())));
        var cache = new ProjectInstanceSnapshotCache(
            key.RetainedSizeBytes + baseline.RetainedSizeBytes + 512);

        cache.AddOrReplace(key, entry).ShouldBeFalse();
        cache.CurrentSizeBytes.ShouldBe(0);
    }

    [Fact]
    public void ComponentFactoryReturnsSingletonAndShutdownDetachesIt()
    {
        var host = new MockHost();
        var factories = new BuildComponentFactoryCollection(host);
        factories.RegisterDefaultFactories();
        factories.AddFactory(
            BuildComponentType.ProjectInstanceSnapshotCache,
            ProjectInstanceSnapshotCache.CreateComponent,
            BuildComponentFactoryCollection.CreationPattern.Singleton);

        ProjectInstanceSnapshotCache first =
            factories.GetComponent<ProjectInstanceSnapshotCache>(BuildComponentType.ProjectInstanceSnapshotCache);
        ProjectInstanceSnapshotCache second =
            factories.GetComponent<ProjectInstanceSnapshotCache>(BuildComponentType.ProjectInstanceSnapshotCache);
        first.ShouldBeSameAs(second);

        ProjectInstanceSnapshotCacheEntry entry = CreateEntry("cached");
        first.AddOrReplace(EmptyKey("Cached.csproj"), entry).ShouldBeTrue();

        factories.ShutdownComponents();

        first.Count.ShouldBe(0);
        first.CurrentSizeBytes.ShouldBe(0);
        ProjectInstanceSnapshotCache replacement =
            factories.GetComponent<ProjectInstanceSnapshotCache>(BuildComponentType.ProjectInstanceSnapshotCache);
        replacement.ShouldNotBeSameAs(first);
    }

    [Fact]
    public async Task ConcurrentOperationsKeepCacheStateConsistent()
    {
        ProjectInstanceSnapshotCacheEntry entry = CreateEntry("concurrent");
        var cache = new ProjectInstanceSnapshotCache(entry.RetainedSizeBytes * 8L);
        ProjectInstanceSnapshotCacheKey[] keys = Enumerable.Range(0, 16)
            .Select(index => EmptyKey($"Project{index}.csproj"))
            .ToArray();

        Task[] workers = Enumerable.Range(0, 8)
            .Select(worker => Task.Run(() =>
            {
                for (int iteration = 0; iteration < 500; iteration++)
                {
                    ProjectInstanceSnapshotCacheKey key = keys[(worker + iteration) % keys.Length];
                    switch (iteration % 5)
                    {
                        case 0:
                            cache.AddOrReplace(key, entry);
                            break;
                        case 1:
                            cache.TryGet(key, out _);
                            break;
                        case 2:
                            cache.Remove(key);
                            break;
                        case 3:
                            _ = cache.Count;
                            _ = cache.CurrentSizeBytes;
                            break;
                        default:
                            if (iteration % 127 == 0)
                            {
                                cache.Clear();
                            }
                            break;
                    }
                }
            }))
            .ToArray();

        await Task.WhenAll(workers);

        cache.CurrentSizeBytes.ShouldBeGreaterThanOrEqualTo(0);
        cache.CurrentSizeBytes.ShouldBeLessThanOrEqualTo(cache.MaximumSizeBytes);
        cache.Clear();
        cache.Count.ShouldBe(0);
        cache.CurrentSizeBytes.ShouldBe(0);
    }

    private static ProjectInstanceSnapshotCacheKey CreateKey(
        string projectFullPath,
        string toolsVersion,
        ProjectLoadSettings projectLoadSettings,
        IReadOnlyDictionary<string, string> globalProperties,
        bool explicitToolsVersionSpecified = false,
        string? subToolsetVersion = null) =>
        new(
            projectFullPath,
            toolsVersion,
            explicitToolsVersionSpecified,
            subToolsetVersion,
            projectLoadSettings,
            globalProperties);

    private static string ProjectPath(string fileName) =>
        Path.Combine(Path.GetTempPath(), "snapshot-cache-tests", fileName);

    private static ProjectInstanceSnapshotCacheKey EmptyKey(string fileName) =>
        CreateKey(
            ProjectPath(fileName),
            "Current",
            ProjectLoadSettings.Default,
            new Dictionary<string, string>());

    private static ProjectInstanceSnapshot CreateSnapshot(string value)
    {
        using var collection = new ProjectCollection();
        using var projectFromString = new ProjectRootElementFromString(
            $"<Project><PropertyGroup><Value>{value}</Value></PropertyGroup></Project>",
            collection);
        return ProjectInstanceSnapshot.Create(new ProjectInstance(projectFromString.Project));
    }

    private static ProjectInstanceSnapshotCacheEntry CreateEntry(
        string value,
        long validationDataSizeBytes = 0) =>
        new(CreateSnapshot(value), new TestValidationData(validationDataSizeBytes));

    private sealed class TestValidationData : IProjectInstanceSnapshotValidationData
    {
        internal TestValidationData(long retainedSizeBytes)
        {
            RetainedSizeBytes = retainedSizeBytes;
        }

        public long RetainedSizeBytes { get; }
    }

    private sealed class AcceptingTestValidator : IProjectInstanceSnapshotValidator
    {
        public ProjectInstanceSnapshotValidationResult Validate(
            ProjectInstanceSnapshotCacheKey key,
            ProjectInstanceSnapshotCacheEntry entry) =>
            ProjectInstanceSnapshotValidationResult.Valid;
    }
}
