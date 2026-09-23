// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Build.BackEnd;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.BackEnd.SdkResolution;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Evaluation.Context;
using Microsoft.Build.Exceptions;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.UnitTests;
using Shouldly;
using Xunit;
using InternalUtilities = Microsoft.Build.Internal.Utilities;
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

        ProjectInstanceSnapshotValidationResult result =
            RejectingProjectInstanceSnapshotValidator.Instance.Validate(key, entry);

        result.ShouldBe(ProjectInstanceSnapshotValidationResult.Invalid);
        default(ProjectInstanceSnapshotValidationResult)
            .ShouldBe(ProjectInstanceSnapshotValidationResult.Invalid);
        ProjectInstanceSnapshotValidationResult.Valid
            .ShouldNotBe(ProjectInstanceSnapshotValidationResult.Invalid);
    }

    [Fact]
    public void BuildParametersClonePreservesCacheButTranslationDoesNot()
    {
        var cache = new ProjectInstanceSnapshotCache();
        var parameters = new BuildParameters
        {
            ProjectInstanceSnapshotCache = cache,
        };

        BuildParameters clone = parameters.Clone();

        clone.ProjectInstanceSnapshotCache.ShouldBeSameAs(cache);

        ((ITranslatable)parameters).Translate(TranslationHelpers.GetWriteTranslator());
        BuildParameters translated =
            BuildParameters.FactoryForDeserialization(TranslationHelpers.GetReadTranslator());
        translated.ProjectInstanceSnapshotCache.ShouldBeNull();
    }

    [Fact]
    public void SuccessfulEvaluationStoresSnapshotEntry()
    {
        using TestEnvironment env = TestEnvironment.Create();
        TransientTestFile project = env.CreateFile(
            "project.proj",
            "<Project><PropertyGroup><Value>stored</Value></PropertyGroup></Project>");
        var cache = new ProjectInstanceSnapshotCache();
        var parameters = new BuildParameters
        {
            ProjectInstanceSnapshotCache = cache,
        };
        var host = new MockHost(parameters);
        var requestData = new BuildRequestData(
            project.Path,
            new Dictionary<string, string?>(),
            toolsVersion: null,
            [],
            hostServices: null,
            BuildRequestDataFlags.None);
        var configuration = new BuildRequestConfiguration(requestData, parameters.DefaultToolsVersion);

        configuration.LoadProjectIntoConfiguration(
            host,
            BuildRequestDataFlags.None,
            submissionId: 1,
            nodeId: 1);

        Toolset requestToolset = parameters.GetToolset(configuration.Project.ToolsVersion);
        var key = new ProjectInstanceSnapshotCacheKey(
            project.Path,
            configuration.Project.ToolsVersion,
            configuration.ExplicitToolsVersionSpecified,
            requestToolset?.GenerateSubToolsetVersionUsingVisualStudioVersion(
                new Dictionary<string, string>(),
                visualStudioVersionFromSolution: 0),
            ProjectLoadSettings.Default,
            new Dictionary<string, string>());
        cache.TryGet(key, out ProjectInstanceSnapshotCacheEntry? entry).ShouldBeTrue();
        entry.ShouldNotBeNull();
        entry.ValidationData.ShouldBeSameAs(EmptyProjectInstanceSnapshotValidationData.Instance);
        cache.CacheMisses.ShouldBe(1);
        cache.StoredEntries.ShouldBe(1);
    }

    [Fact]
    public void RecordEvaluatedItemElementsBypassesSnapshotCache()
    {
        using TestEnvironment env = TestEnvironment.Create();
        TransientTestFile project = env.CreateFile(
            "project.proj",
            "<Project><ItemGroup><Compile Include=\"Program.cs\" /></ItemGroup></Project>");
        var cache = new ProjectInstanceSnapshotCache();
        var parameters = new BuildParameters
        {
            ProjectInstanceSnapshotCache = cache,
            ProjectLoadSettings = ProjectLoadSettings.RecordEvaluatedItemElements,
        };
        var host = new MockHost(parameters);
        BuildRequestConfiguration configuration =
            CreateFileConfiguration(project.Path, parameters);

        configuration.LoadProjectIntoConfiguration(
            host,
            BuildRequestDataFlags.None,
            submissionId: 1,
            nodeId: 1);

        configuration.Project.EvaluatedItemElements.ShouldNotBeEmpty();
        cache.Count.ShouldBe(0);
        cache.CacheHits.ShouldBe(0);
        cache.CacheMisses.ShouldBe(0);
        cache.StoredEntries.ShouldBe(0);
    }

    [Fact]
    public void RejectingValidatorReevaluatesAndReplacesEntry()
    {
        using TestEnvironment env = TestEnvironment.Create();
        TransientTestFile project = env.CreateFile(
            "project.proj",
            "<Project><PropertyGroup><Value>stored</Value></PropertyGroup></Project>");
        var cache = new ProjectInstanceSnapshotCache();
        var parameters = new BuildParameters
        {
            ProjectInstanceSnapshotCache = cache,
        };
        var host = new MockHost(parameters);

        BuildRequestConfiguration first = CreateFileConfiguration(project.Path, parameters);
        first.LoadProjectIntoConfiguration(host, BuildRequestDataFlags.None, submissionId: 1, nodeId: 1);

        BuildRequestConfiguration second = CreateFileConfiguration(project.Path, parameters);
        second.LoadProjectIntoConfiguration(host, BuildRequestDataFlags.None, submissionId: 2, nodeId: 1);

        cache.CacheHits.ShouldBe(1);
        cache.CacheMisses.ShouldBe(1);
        cache.ValidationRejections.ShouldBe(1);
        cache.MaterializedEntries.ShouldBe(0);
        cache.StoredEntries.ShouldBe(2);
        cache.Count.ShouldBe(1);
    }

    [Fact]
    public void AcceptingTestValidatorMaterializesWithoutReevaluation()
    {
        using TestEnvironment env = TestEnvironment.Create();
        TransientTestFile project = env.CreateFile(
            "project.proj",
            "<Project><PropertyGroup><Value>stored</Value></PropertyGroup></Project>");
        var cache = new ProjectInstanceSnapshotCache();
        var parameters = new BuildParameters
        {
            ProjectInstanceSnapshotCache = cache,
        };
        var host = new MockHost(parameters);

        BuildRequestConfiguration first = CreateFileConfiguration(project.Path, parameters);
        first.LoadProjectIntoConfiguration(host, BuildRequestDataFlags.None, submissionId: 1, nodeId: 1);

        var validator = new AcceptingTestValidator();
        cache.Validator = validator;
        BuildRequestConfiguration second = CreateFileConfiguration(project.Path, parameters);
        second.LoadProjectIntoConfiguration(host, BuildRequestDataFlags.None, submissionId: 2, nodeId: 1);

        second.Project.GetPropertyValue("Value").ShouldBe("stored");
        validator.Calls.ShouldBe(1);
        cache.CacheHits.ShouldBe(1);
        cache.CacheMisses.ShouldBe(1);
        cache.ValidationRejections.ShouldBe(0);
        cache.MaterializedEntries.ShouldBe(1);
        cache.StoredEntries.ShouldBe(1);
    }

    [Fact]
    public void DistinctGlobalPropertiesDoNotShareSnapshotEntries()
    {
        using TestEnvironment env = TestEnvironment.Create();
        TransientTestFile project = env.CreateFile(
            "project.proj",
            "<Project><PropertyGroup><Value>$(Configuration)</Value></PropertyGroup></Project>");
        var cache = new ProjectInstanceSnapshotCache
        {
            Validator = new AcceptingTestValidator(),
        };
        var parameters = new BuildParameters
        {
            ProjectInstanceSnapshotCache = cache,
        };
        var host = new MockHost(parameters);

        BuildRequestConfiguration debug = CreateFileConfiguration(
            project.Path,
            parameters,
            new Dictionary<string, string?>
            {
                ["Configuration"] = "Debug",
            });
        debug.LoadProjectIntoConfiguration(host, BuildRequestDataFlags.None, submissionId: 1, nodeId: 1);

        BuildRequestConfiguration release = CreateFileConfiguration(
            project.Path,
            parameters,
            new Dictionary<string, string?>
            {
                ["Configuration"] = "Release",
            });
        release.LoadProjectIntoConfiguration(host, BuildRequestDataFlags.None, submissionId: 2, nodeId: 1);

        debug.Project.GetPropertyValue("Value").ShouldBe("Debug");
        release.Project.GetPropertyValue("Value").ShouldBe("Release");
        cache.CacheHits.ShouldBe(0);
        cache.CacheMisses.ShouldBe(2);
        cache.MaterializedEntries.ShouldBe(0);
        cache.StoredEntries.ShouldBe(2);
        cache.Count.ShouldBe(2);

        BuildRequestConfiguration repeatedDebug = CreateFileConfiguration(
            project.Path,
            parameters,
            new Dictionary<string, string?>
            {
                ["Configuration"] = "Debug",
            });
        repeatedDebug.LoadProjectIntoConfiguration(host, BuildRequestDataFlags.None, submissionId: 3, nodeId: 1);

        repeatedDebug.Project.GetPropertyValue("Value").ShouldBe("Debug");
        cache.CacheHits.ShouldBe(1);
        cache.CacheMisses.ShouldBe(2);
        cache.MaterializedEntries.ShouldBe(1);
        cache.StoredEntries.ShouldBe(2);
        cache.Count.ShouldBe(2);
    }

    [Fact]
    public void ValidatorFailureFallsBackToReevaluation()
    {
        using TestEnvironment env = TestEnvironment.Create();
        TransientTestFile project = env.CreateFile(
            "project.proj",
            "<Project><PropertyGroup><Value>stored</Value></PropertyGroup></Project>");
        var cache = new ProjectInstanceSnapshotCache();
        var parameters = new BuildParameters
        {
            ProjectInstanceSnapshotCache = cache,
        };
        var host = new MockHost(parameters);

        BuildRequestConfiguration first = CreateFileConfiguration(project.Path, parameters);
        first.LoadProjectIntoConfiguration(host, BuildRequestDataFlags.None, submissionId: 1, nodeId: 1);

        cache.Validator = new ThrowingTestValidator();
        BuildRequestConfiguration second = CreateFileConfiguration(project.Path, parameters);
        second.LoadProjectIntoConfiguration(host, BuildRequestDataFlags.None, submissionId: 2, nodeId: 1);

        cache.CacheHits.ShouldBe(1);
        cache.ValidationRejections.ShouldBe(1);
        cache.MaterializedEntries.ShouldBe(0);
        cache.StoredEntries.ShouldBe(2);
    }

    [Fact]
    public void FailedEvaluationDoesNotStoreSnapshotEntry()
    {
        using TestEnvironment env = TestEnvironment.Create();
        TransientTestFile project = env.CreateFile(
            "invalid.proj",
            "<Project><PropertyGroup>");
        var cache = new ProjectInstanceSnapshotCache();
        var parameters = new BuildParameters
        {
            ProjectInstanceSnapshotCache = cache,
        };
        var host = new MockHost(parameters);
        var requestData = new BuildRequestData(
            project.Path,
            new Dictionary<string, string?>(),
            toolsVersion: null,
            [],
            hostServices: null,
            BuildRequestDataFlags.None);
        var configuration = new BuildRequestConfiguration(requestData, parameters.DefaultToolsVersion);

        Should.Throw<InvalidProjectFileException>(() =>
            configuration.LoadProjectIntoConfiguration(
                host,
                BuildRequestDataFlags.None,
                submissionId: 1,
                nodeId: 1));

        cache.Count.ShouldBe(0);
        cache.StoredEntries.ShouldBe(0);
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
    public void CancellationFromValidatorIsNotConvertedToFreshEvaluation()
    {
        using TestEnvironment env = TestEnvironment.Create();
        TransientTestFile project = env.CreateFile(
            "project.proj",
            "<Project><PropertyGroup><Value>stored</Value></PropertyGroup></Project>");
        var cache = new ProjectInstanceSnapshotCache();
        var parameters = new BuildParameters
        {
            ProjectInstanceSnapshotCache = cache,
        };
        var host = new MockHost(parameters);

        CreateFileConfiguration(project.Path, parameters).LoadProjectIntoConfiguration(
            host, BuildRequestDataFlags.None, submissionId: 1, nodeId: 1);
        cache.Validator = new CancelingTestValidator();

        Should.Throw<OperationCanceledException>(() =>
            CreateFileConfiguration(project.Path, parameters).LoadProjectIntoConfiguration(
                host, BuildRequestDataFlags.None, submissionId: 2, nodeId: 1));
    }

    [Fact]
    public void BuildAbortFromValidatorIsNotConvertedToFreshEvaluation()
    {
        using TestEnvironment env = TestEnvironment.Create();
        TransientTestFile project = env.CreateFile("project.proj", "<Project />");
        var cache = new ProjectInstanceSnapshotCache();
        var parameters = new BuildParameters
        {
            ProjectInstanceSnapshotCache = cache,
        };
        var host = new MockHost(parameters);

        CreateFileConfiguration(project.Path, parameters).LoadProjectIntoConfiguration(
            host, BuildRequestDataFlags.None, submissionId: 1, nodeId: 1);
        cache.Validator = new AbortingTestValidator();

        Should.Throw<BuildAbortedException>(() =>
            CreateFileConfiguration(project.Path, parameters).LoadProjectIntoConfiguration(
                host, BuildRequestDataFlags.None, submissionId: 2, nodeId: 1));
    }

    [Fact]
    public void NestedCancellationFromValidatorIsNotConvertedToFreshEvaluation()
    {
        using TestEnvironment env = TestEnvironment.Create();
        TransientTestFile project = env.CreateFile("project.proj", "<Project />");
        var cache = new ProjectInstanceSnapshotCache();
        var parameters = new BuildParameters
        {
            ProjectInstanceSnapshotCache = cache,
        };
        var host = new MockHost(parameters);

        CreateFileConfiguration(project.Path, parameters).LoadProjectIntoConfiguration(
            host, BuildRequestDataFlags.None, submissionId: 1, nodeId: 1);
        var expected = new InvalidOperationException(
            "Wrapped cancellation.",
            new OperationCanceledException("Validation canceled."));
        cache.Validator = new ExceptionThrowingTestValidator(expected);

        Should.Throw<InvalidOperationException>(() =>
            CreateFileConfiguration(project.Path, parameters).LoadProjectIntoConfiguration(
                host, BuildRequestDataFlags.None, submissionId: 2, nodeId: 1))
            .ShouldBeSameAs(expected);
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
    public void ComponentSurvivesBuildManagerBuildCycle()
    {
        using var buildManager = new BuildManager();
        var host = (IBuildComponentHost)buildManager;
        var first =
            (ProjectInstanceSnapshotCache)host.GetComponent(BuildComponentType.ProjectInstanceSnapshotCache);
        ProjectInstanceSnapshotCacheKey key = EmptyKey("BuildCycle.csproj");
        ProjectInstanceSnapshotCacheEntry entry = CreateEntry("build-cycle");
        first.AddOrReplace(key, entry).ShouldBeTrue();

        buildManager.BeginBuild(new BuildParameters());
        buildManager.EndBuild();

        var second =
            (ProjectInstanceSnapshotCache)host.GetComponent(BuildComponentType.ProjectInstanceSnapshotCache);
        second.ShouldBeSameAs(first);
        second.TryGet(key, out ProjectInstanceSnapshotCacheEntry? found).ShouldBeTrue();
        found.ShouldBeSameAs(entry);
    }

    [Fact]
    public void PublicResetCachesClearsSnapshotCache()
    {
        using var buildManager = new BuildManager();
        var cache = (ProjectInstanceSnapshotCache)((IBuildComponentHost)buildManager)
            .GetComponent(BuildComponentType.ProjectInstanceSnapshotCache);
        cache.AddOrReplace(EmptyKey("Reset.csproj"), CreateEntry("reset")).ShouldBeTrue();

        buildManager.ResetCaches();

        cache.Count.ShouldBe(0);
        cache.CurrentSizeBytes.ShouldBe(0);
    }

    [Fact]
    public void PublicResetCachesDoesNotCreateUnconfiguredSnapshotCache()
    {
        using var buildManager = new BuildManager();
        int creations = 0;
        ((IBuildComponentHost)buildManager).RegisterFactory(
            BuildComponentType.ProjectInstanceSnapshotCache,
            type =>
            {
                creations++;
                return ProjectInstanceSnapshotCache.CreateComponent(type);
            });

        buildManager.ResetCaches();

        creations.ShouldBe(0);
    }

    [Fact]
    public void BeginBuildFlowsCacheOnlyWhenFeatureIsEnabled()
    {
        const string VariableName = "MSBUILDENABLEPROJECTINSTANCESNAPSHOTCACHE";
        string? originalValue = Environment.GetEnvironmentVariable(VariableName);

        try
        {
            Environment.SetEnvironmentVariable(VariableName, null);
            Traits.UpdateFromEnvironment();
            using (var disabledBuildManager = new BuildManager())
            {
                disabledBuildManager.BeginBuild(new BuildParameters());
                ((IBuildComponentHost)disabledBuildManager)
                    .BuildParameters
                    .ProjectInstanceSnapshotCache.ShouldBeNull();
                disabledBuildManager.EndBuild();
            }

            Environment.SetEnvironmentVariable(VariableName, "1");
            Traits.UpdateFromEnvironment();
            using var enabledBuildManager = new BuildManager();
            enabledBuildManager.BeginBuild(new BuildParameters());
            ProjectInstanceSnapshotCache enabledCache = ((IBuildComponentHost)enabledBuildManager)
                .BuildParameters
                .ProjectInstanceSnapshotCache;
            enabledCache.ShouldNotBeNull();
            enabledCache.BuildsServed.ShouldBe(1);
            enabledBuildManager.EndBuild();

            enabledBuildManager.BeginBuild(new BuildParameters());
            ((IBuildComponentHost)enabledBuildManager)
                .BuildParameters
                .ProjectInstanceSnapshotCache.ShouldBeSameAs(enabledCache);
            enabledCache.BuildsServed.ShouldBe(2);
            enabledBuildManager.EndBuild();
        }
        finally
        {
            Environment.SetEnvironmentVariable(VariableName, originalValue);
            Traits.UpdateFromEnvironment();
        }
    }

    [Fact]
    public void EndBuildLogsCurrentSnapshotCacheStatistics()
    {
        const string ModeVariable = EvaluationCacheConfiguration.ModeEnvironmentVariable;
        string? originalMode = Environment.GetEnvironmentVariable(ModeVariable);
        try
        {
            Environment.SetEnvironmentVariable(ModeVariable, nameof(EvaluationCacheMode.SnapshotUnsafe));
            Traits.UpdateFromEnvironment();
            var logger = new MockLogger(verbosity: LoggerVerbosity.Diagnostic);
            using var buildManager = new BuildManager();
            buildManager.BeginBuild(new BuildParameters
            {
                Loggers = [logger],
            });

            ProjectInstanceSnapshotCache cache = ((IBuildComponentHost)buildManager)
                .BuildParameters
                .ProjectInstanceSnapshotCache;
            cache.NotifyCacheLookup(false);
            cache.AddOrReplace(EmptyKey("Status.csproj"), CreateEntry("status")).ShouldBeTrue();

            logger.FullLog.ShouldNotContain("Project instance snapshot cache:");

            buildManager.EndBuild();

            string firstStatus = logger.FullLog
                .Split([Environment.NewLine], StringSplitOptions.RemoveEmptyEntries)
                .Single(line => line.StartsWith("EvaluationCacheExperimentStatus|", StringComparison.Ordinal));
            string[] keys = firstStatus
                .Split('|')
                .Skip(1)
                .Select(token => token.Substring(0, token.IndexOf('=')))
                .ToArray();
            keys.ShouldBe(new[]
            {
                "Version", "Mode", "ConfigurationValid", "ProcessId", "BuildManagerId", "BuildsServed",
                "Entries", "CurrentSizeBytes", "MaximumSizeBytes", "FreshEvaluations", "RecordedEvaluations",
                "NonCacheableEvaluations", "CacheHits", "CacheMisses", "ValidationAttempts", "ValidationAccepted",
                "ValidationRejected", "ValidationErrors", "MaterializedEntries", "StoredEntries", "EvictedEntries",
                "OversizedRejections", "Fallbacks",
            });
            firstStatus.ShouldContain("|Version=1|Mode=SnapshotUnsafe|ConfigurationValid=True|");
            firstStatus.ShouldMatch(@"\|ProcessId=[1-9][0-9]*\|BuildManagerId=[0-9a-f]{32}\|BuildsServed=1\|");
            firstStatus.ShouldContain("|Entries=1|");
            firstStatus.ShouldContain("|StoredEntries=1|");

            buildManager.BeginBuild(new BuildParameters { Loggers = [logger] });
            buildManager.EndBuild();

            string[] statuses = logger.FullLog
                .Split([Environment.NewLine], StringSplitOptions.RemoveEmptyEntries)
                .Where(line => line.StartsWith("EvaluationCacheExperimentStatus|", StringComparison.Ordinal))
                .ToArray();
            statuses.Length.ShouldBe(2);
            statuses[1].ShouldContain("|BuildsServed=2|");
            string identityPrefix = firstStatus.Substring(
                firstStatus.IndexOf("|ProcessId=", StringComparison.Ordinal),
                firstStatus.IndexOf("|BuildsServed=", StringComparison.Ordinal)
                    - firstStatus.IndexOf("|ProcessId=", StringComparison.Ordinal));
            statuses[1].ShouldContain(identityPrefix);
        }
        finally
        {
            Environment.SetEnvironmentVariable(ModeVariable, originalMode);
            Traits.UpdateFromEnvironment();
        }
    }

    [Fact]
    public void BeginBuildHonorsInProcessFeatureOverride()
    {
        const string VariableName = "MSBUILDENABLEPROJECTINSTANCESNAPSHOTCACHE";
        string? originalValue = Environment.GetEnvironmentVariable(VariableName);

        try
        {
            Environment.SetEnvironmentVariable(VariableName, "1");
            Traits.UpdateFromEnvironment();
            Traits.ProjectInstanceSnapshotCacheEnabledOverride = false;

            using (var disabledBuildManager = new BuildManager())
            {
                disabledBuildManager.BeginBuild(new BuildParameters());
                ((IBuildComponentHost)disabledBuildManager)
                    .BuildParameters
                    .ProjectInstanceSnapshotCache.ShouldBeNull();
                disabledBuildManager.EndBuild();
            }

            Traits.ProjectInstanceSnapshotCacheEnabledOverride = true;
            using var enabledBuildManager = new BuildManager();
            enabledBuildManager.BeginBuild(new BuildParameters());
            ((IBuildComponentHost)enabledBuildManager)
                .BuildParameters
                .ProjectInstanceSnapshotCache.ShouldNotBeNull();
            enabledBuildManager.EndBuild();
        }
        finally
        {
            Traits.ProjectInstanceSnapshotCacheEnabledOverride = null;
            Environment.SetEnvironmentVariable(VariableName, originalValue);
            Traits.UpdateFromEnvironment();
        }
    }

    [Fact]
    public void ExplicitModeOverridesLegacySwitchesAndInvalidModeFailsClosed()
    {
        const string ModeVariable = EvaluationCacheConfiguration.ModeEnvironmentVariable;
        const string RecordVariable = "MSBUILDRECORDEVALUATIONINPUTS";
        const string SnapshotVariable = "MSBUILDENABLEPROJECTINSTANCESNAPSHOTCACHE";
        string? originalMode = Environment.GetEnvironmentVariable(ModeVariable);
        string? originalRecord = Environment.GetEnvironmentVariable(RecordVariable);
        string? originalSnapshot = Environment.GetEnvironmentVariable(SnapshotVariable);

        try
        {
            Environment.SetEnvironmentVariable(RecordVariable, "1");
            Environment.SetEnvironmentVariable(SnapshotVariable, "1");
            Environment.SetEnvironmentVariable(ModeVariable, nameof(EvaluationCacheMode.Disabled));
            Traits.UpdateFromEnvironment();

            EvaluationCacheConfiguration disabled = Traits.Instance.EvaluationCache;
            disabled.Mode.ShouldBe(EvaluationCacheMode.Disabled);
            disabled.ConfigurationValid.ShouldBeTrue();
            disabled.RecordInputs.ShouldBeFalse();
            disabled.EnableSnapshotCache.ShouldBeFalse();

            Environment.SetEnvironmentVariable(ModeVariable, "not-a-mode");
            Traits.UpdateFromEnvironment();

            EvaluationCacheConfiguration invalid = Traits.Instance.EvaluationCache;
            invalid.Mode.ShouldBe(EvaluationCacheMode.Disabled);
            invalid.ConfigurationValid.ShouldBeFalse();
            invalid.RecordInputs.ShouldBeFalse();
            invalid.EnableSnapshotCache.ShouldBeFalse();

            Environment.SetEnvironmentVariable(ModeVariable, "snapshotfilesystem");
            Traits.UpdateFromEnvironment();

            EvaluationCacheConfiguration caseInsensitive = Traits.Instance.EvaluationCache;
            caseInsensitive.Mode.ShouldBe(EvaluationCacheMode.SnapshotFileSystem);
            caseInsensitive.ConfigurationValid.ShouldBeTrue();
            caseInsensitive.RecordInputs.ShouldBeTrue();
            caseInsensitive.EnableSnapshotCache.ShouldBeTrue();

            Environment.SetEnvironmentVariable(ModeVariable, "1");
            Traits.UpdateFromEnvironment();
            Traits.Instance.EvaluationCache.ConfigurationValid.ShouldBeFalse();

            Environment.SetEnvironmentVariable(ModeVariable, null);
            Traits.UpdateFromEnvironment();

            EvaluationCacheConfiguration legacy = Traits.Instance.EvaluationCache;
            legacy.HasExplicitMode.ShouldBeFalse();
            legacy.RecordInputs.ShouldBeTrue();
            legacy.EnableSnapshotCache.ShouldBeTrue();
            legacy.ValidationPolicy.ShouldBe(EvaluationCacheValidationPolicy.Reject);
        }
        finally
        {
            Environment.SetEnvironmentVariable(ModeVariable, originalMode);
            Environment.SetEnvironmentVariable(RecordVariable, originalRecord);
            Environment.SetEnvironmentVariable(SnapshotVariable, originalSnapshot);
            Traits.UpdateFromEnvironment();
        }
    }

    [Fact]
    public void SnapshotFileSystemReusesCurrentInputsAndRejectsChangedRoot()
    {
        const string ModeVariable = EvaluationCacheConfiguration.ModeEnvironmentVariable;
        string? originalMode = Environment.GetEnvironmentVariable(ModeVariable);
        try
        {
            Environment.SetEnvironmentVariable(
                ModeVariable,
                nameof(EvaluationCacheMode.SnapshotFileSystem));
            Traits.UpdateFromEnvironment();

            using TestEnvironment env = TestEnvironment.Create();
            TransientTestFile project = env.CreateFile(
                "project.proj",
                "<Project><PropertyGroup><Value>first</Value></PropertyGroup></Project>");
            EvaluationCacheConfiguration mode = Traits.Instance.EvaluationCache;
            var cache = new ProjectInstanceSnapshotCache();
            cache.ConfigureValidator(mode.ValidationPolicy);
            var parameters = new BuildParameters
            {
                EvaluationCacheConfiguration = mode,
                ProjectInstanceSnapshotCache = cache,
            };
            var host = new MockHost(parameters);

            BuildRequestConfiguration first = CreateFileConfiguration(project.Path, parameters);
            first.LoadProjectIntoConfiguration(host, BuildRequestDataFlags.None, submissionId: 1, nodeId: 1);
            BuildRequestConfiguration second = CreateFileConfiguration(project.Path, parameters);
            second.LoadProjectIntoConfiguration(host, BuildRequestDataFlags.None, submissionId: 2, nodeId: 1);

            File.WriteAllText(
                project.Path,
                "<Project><PropertyGroup><Value>changed</Value></PropertyGroup></Project>");
            File.SetLastWriteTimeUtc(project.Path, DateTime.UtcNow.AddSeconds(2));
            BuildRequestConfiguration third = CreateFileConfiguration(project.Path, parameters);
            third.LoadProjectIntoConfiguration(host, BuildRequestDataFlags.None, submissionId: 3, nodeId: 1);

            second.Project.GetPropertyValue("Value").ShouldBe("first");
            third.Project.GetPropertyValue("Value").ShouldBe("changed");
            ProjectInstanceSnapshotCacheStatistics statistics = cache.GetStatistics();
            statistics.FreshEvaluations.ShouldBe(2);
            statistics.RecordedEvaluations.ShouldBe(2);
            statistics.CacheHits.ShouldBe(2);
            statistics.CacheMisses.ShouldBe(1);
            statistics.ValidationAttempts.ShouldBe(2);
            statistics.ValidationAccepted.ShouldBe(1);
            statistics.ValidationRejections.ShouldBe(1);
            statistics.MaterializedEntries.ShouldBe(1);
            statistics.StoredEntries.ShouldBe(2);
        }
        finally
        {
            Environment.SetEnvironmentVariable(ModeVariable, originalMode);
            Traits.UpdateFromEnvironment();
        }
    }

    [Fact]
    public void SnapshotFileSystemRevalidatesSdkAndReusesResultForFreshFallback()
    {
        const string ModeVariable = EvaluationCacheConfiguration.ModeEnvironmentVariable;
        string? originalMode = Environment.GetEnvironmentVariable(ModeVariable);
        try
        {
            Environment.SetEnvironmentVariable(
                ModeVariable,
                nameof(EvaluationCacheMode.SnapshotFileSystem));
            Traits.UpdateFromEnvironment();

            using TestEnvironment env = TestEnvironment.Create();
            TransientTestFolder firstSdk = env.CreateFolder();
            TransientTestFolder secondSdk = env.CreateFolder();
            env.CreateFile(firstSdk, "Sdk.props", "<Project><PropertyGroup><SdkValue>first</SdkValue></PropertyGroup></Project>");
            env.CreateFile(firstSdk, "Sdk.targets", "<Project />");
            env.CreateFile(secondSdk, "Sdk.props", "<Project><PropertyGroup><SdkValue>second</SdkValue></PropertyGroup></Project>");
            env.CreateFile(secondSdk, "Sdk.targets", "<Project />");
            TransientTestFile project = env.CreateFile(
                "project.proj",
                "<Project Sdk=\"TestSdk\"><PropertyGroup><Value>$(SdkValue)</Value></PropertyGroup></Project>");
            EvaluationCacheConfiguration mode = Traits.Instance.EvaluationCache;
            var cache = new ProjectInstanceSnapshotCache();
            cache.ConfigureValidator(mode.ValidationPolicy);
            var parameters = new BuildParameters
            {
                EvaluationCacheConfiguration = mode,
                ProjectInstanceSnapshotCache = cache,
            };
            var resolver = new ChangingSdkResolverService(firstSdk.Path);
            var host = new MockHost(parameters)
            {
                SdkResolverService = resolver,
            };

            BuildRequestConfiguration first = CreateFileConfiguration(project.Path, parameters);
            first.LoadProjectIntoConfiguration(host, BuildRequestDataFlags.None, submissionId: 1, nodeId: 1);
            int initialCalls = resolver.Calls;
            BuildRequestConfiguration unchanged = CreateFileConfiguration(project.Path, parameters);
            unchanged.LoadProjectIntoConfiguration(host, BuildRequestDataFlags.None, submissionId: 2, nodeId: 1);
            resolver.Path = secondSdk.Path;

            BuildRequestConfiguration changed = CreateFileConfiguration(project.Path, parameters);
            changed.LoadProjectIntoConfiguration(host, BuildRequestDataFlags.None, submissionId: 3, nodeId: 1);

            first.Project.GetPropertyValue("Value").ShouldBe("first");
            unchanged.Project.GetPropertyValue("Value").ShouldBe("first");
            changed.Project.GetPropertyValue("Value").ShouldBe("second");
            resolver.Calls.ShouldBe(initialCalls + 2);
            cache.GetStatistics().ValidationAccepted.ShouldBe(1);
            cache.ValidationRejections.ShouldBe(1);
            cache.MaterializedEntries.ShouldBe(1);
        }
        finally
        {
            Environment.SetEnvironmentVariable(ModeVariable, originalMode);
            Traits.UpdateFromEnvironment();
        }
    }

    [Fact]
    public void SdkValidationDiagnosticsAreDeferredAndReplayedOnceOnFreshFallback()
    {
        const string ModeVariable = EvaluationCacheConfiguration.ModeEnvironmentVariable;
        string? originalMode = Environment.GetEnvironmentVariable(ModeVariable);
        try
        {
            Environment.SetEnvironmentVariable(
                ModeVariable,
                nameof(EvaluationCacheMode.SnapshotFileSystem));
            Traits.UpdateFromEnvironment();

            using TestEnvironment env = TestEnvironment.Create();
            TransientTestFolder sdk = env.CreateFolder();
            env.CreateFile(sdk, "Sdk.props", "<Project />");
            env.CreateFile(sdk, "Sdk.targets", "<Project />");
            TransientTestFile project = env.CreateFile("project.proj", "<Project Sdk=\"TestSdk\" />");
            EvaluationCacheConfiguration mode = Traits.Instance.EvaluationCache;
            var cache = new ProjectInstanceSnapshotCache();
            cache.ConfigureValidator(mode.ValidationPolicy);
            var parameters = new BuildParameters
            {
                EvaluationCacheConfiguration = mode,
                ProjectInstanceSnapshotCache = cache,
            };
            var messages = new List<string>();
            var resolver = new ChangingSdkResolverService(sdk.Path);
            var host = new MockHost(parameters)
            {
                LoggingService = new MockLoggingService(messages.Add),
                SdkResolverService = resolver,
            };

            CreateFileConfiguration(project.Path, parameters).LoadProjectIntoConfiguration(
                host, BuildRequestDataFlags.None, submissionId: 1, nodeId: 1);
            int initialCalls = resolver.Calls;
            resolver.Message = "resolver diagnostic";
            CreateFileConfiguration(project.Path, parameters).LoadProjectIntoConfiguration(
                host, BuildRequestDataFlags.None, submissionId: 2, nodeId: 1);

            resolver.Calls.ShouldBe(initialCalls + 1);
            messages.Count(message => message == resolver.Message).ShouldBe(1);
            cache.ValidationRejections.ShouldBe(1);
            cache.Count.ShouldBe(0);
        }
        finally
        {
            Environment.SetEnvironmentVariable(ModeVariable, originalMode);
            Traits.UpdateFromEnvironment();
        }
    }

    [Fact]
    public void SnapshotFileSystemDoesNotStoreEvaluationThatLoggedWarning()
    {
        const string ModeVariable = EvaluationCacheConfiguration.ModeEnvironmentVariable;
        string? originalMode = Environment.GetEnvironmentVariable(ModeVariable);
        try
        {
            Environment.SetEnvironmentVariable(
                ModeVariable,
                nameof(EvaluationCacheMode.SnapshotFileSystem));
            Traits.UpdateFromEnvironment();

            using TestEnvironment env = TestEnvironment.Create();
            TransientTestFile project = env.CreateFile(
                "project.proj",
                "<Project><Import Project=\"project.proj\" /></Project>");
            EvaluationCacheConfiguration mode = Traits.Instance.EvaluationCache;
            var cache = new ProjectInstanceSnapshotCache();
            cache.ConfigureValidator(mode.ValidationPolicy);
            var parameters = new BuildParameters
            {
                EvaluationCacheConfiguration = mode,
                ProjectInstanceSnapshotCache = cache,
            };
            var host = new MockHost(parameters);

            BuildRequestConfiguration configuration = CreateFileConfiguration(project.Path, parameters);
            configuration.LoadProjectIntoConfiguration(
                host, BuildRequestDataFlags.None, submissionId: 1, nodeId: 1);

            configuration.Project.EvaluationInputs.NonCacheable
                .ShouldBe(NonCacheableReason.EvaluationDiagnostics);
            cache.Count.ShouldBe(0);
            cache.StoredEntries.ShouldBe(0);
        }
        finally
        {
            Environment.SetEnvironmentVariable(ModeVariable, originalMode);
            Traits.UpdateFromEnvironment();
        }
    }

    [Fact]
    public void SnapshotFileSystemDoesNotStoreIgnoredFailedSdkResolution()
    {
        const string ModeVariable = EvaluationCacheConfiguration.ModeEnvironmentVariable;
        string? originalMode = Environment.GetEnvironmentVariable(ModeVariable);
        try
        {
            Environment.SetEnvironmentVariable(
                ModeVariable,
                nameof(EvaluationCacheMode.SnapshotFileSystem));
            Traits.UpdateFromEnvironment();

            using TestEnvironment env = TestEnvironment.Create();
            TransientTestFile project = env.CreateFile("project.proj", "<Project Sdk=\"MissingSdk\" />");
            EvaluationCacheConfiguration mode = Traits.Instance.EvaluationCache;
            var cache = new ProjectInstanceSnapshotCache();
            cache.ConfigureValidator(mode.ValidationPolicy);
            var parameters = new BuildParameters
            {
                EvaluationCacheConfiguration = mode,
                ProjectInstanceSnapshotCache = cache,
            };
            var resolver = new ChangingSdkResolverService(env.CreateFolder().Path)
            {
                Success = false,
            };
            var host = new MockHost(parameters)
            {
                SdkResolverService = resolver,
            };

            BuildRequestConfiguration first = CreateFileConfiguration(project.Path, parameters);
            first.LoadProjectIntoConfiguration(
                host,
                BuildRequestDataFlags.IgnoreMissingEmptyAndInvalidImports,
                submissionId: 1,
                nodeId: 1);
            int initialCalls = resolver.Calls;
            BuildRequestConfiguration second = CreateFileConfiguration(project.Path, parameters);
            second.LoadProjectIntoConfiguration(
                host,
                BuildRequestDataFlags.IgnoreMissingEmptyAndInvalidImports,
                submissionId: 2,
                nodeId: 1);

            first.Project.EvaluationInputs.NonCacheable.ShouldBe(NonCacheableReason.FailedSdkResolution);
            second.Project.EvaluationInputs.NonCacheable.ShouldBe(NonCacheableReason.FailedSdkResolution);
            resolver.Calls.ShouldBeGreaterThan(initialCalls);
            cache.Count.ShouldBe(0);
            cache.MaterializedEntries.ShouldBe(0);
        }
        finally
        {
            Environment.SetEnvironmentVariable(ModeVariable, originalMode);
            Traits.UpdateFromEnvironment();
        }
    }

    [Fact]
    public void SnapshotFileSystemRejectsDirtyExplicitRoot()
    {
        const string ModeVariable = EvaluationCacheConfiguration.ModeEnvironmentVariable;
        string? originalMode = Environment.GetEnvironmentVariable(ModeVariable);
        try
        {
            Environment.SetEnvironmentVariable(
                ModeVariable,
                nameof(EvaluationCacheMode.SnapshotFileSystem));
            Traits.UpdateFromEnvironment();

            using TestEnvironment env = TestEnvironment.Create();
            TransientTestFile project = env.CreateFile(
                "project.proj",
                "<Project><PropertyGroup><Value>disk</Value></PropertyGroup></Project>");
            EvaluationCacheConfiguration mode = Traits.Instance.EvaluationCache;
            var cache = new ProjectInstanceSnapshotCache();
            cache.ConfigureValidator(mode.ValidationPolicy);
            var parameters = new BuildParameters
            {
                EvaluationCacheConfiguration = mode,
                ProjectInstanceSnapshotCache = cache,
            };
            var host = new MockHost(parameters);

            CreateFileConfiguration(project.Path, parameters).LoadProjectIntoConfiguration(
                host, BuildRequestDataFlags.None, submissionId: 1, nodeId: 1);
            ProjectRootElement root = ProjectRootElement.Open(
                project.Path,
                parameters.ProjectRootElementCache,
                isExplicitlyLoaded: true,
                preserveFormatting: null);
            root.Properties.Single().Value = "memory";
            root.HasUnsavedChanges.ShouldBeTrue();
            parameters.ProjectRootElementCache.TryGet(project.Path).ShouldBeSameAs(root);
            BuildRequestConfiguration second = CreateFileConfiguration(project.Path, parameters);
            second.LoadProjectIntoConfiguration(
                host, BuildRequestDataFlags.None, submissionId: 2, nodeId: 1);

            second.Project.GetPropertyValue("Value").ShouldBe("memory");
            second.Project.EvaluationInputs.NonCacheable.ShouldBe(NonCacheableReason.InMemoryProject);
            cache.MaterializedEntries.ShouldBe(0);
            cache.ValidationRejections.ShouldBe(1);
        }
        finally
        {
            Environment.SetEnvironmentVariable(ModeVariable, originalMode);
            Traits.UpdateFromEnvironment();
        }
    }

    [Fact]
    public void SnapshotFileSystemRejectsDirtyExplicitImport()
    {
        const string ModeVariable = EvaluationCacheConfiguration.ModeEnvironmentVariable;
        string? originalMode = Environment.GetEnvironmentVariable(ModeVariable);
        try
        {
            Environment.SetEnvironmentVariable(
                ModeVariable,
                nameof(EvaluationCacheMode.SnapshotFileSystem));
            Traits.UpdateFromEnvironment();

            using TestEnvironment env = TestEnvironment.Create();
            TransientTestFile import = env.CreateFile(
                "import.props",
                "<Project><PropertyGroup><Imported>disk</Imported></PropertyGroup></Project>");
            TransientTestFile project = env.CreateFile(
                "project.proj",
                "<Project><Import Project=\"import.props\" /></Project>");
            EvaluationCacheConfiguration mode = Traits.Instance.EvaluationCache;
            var cache = new ProjectInstanceSnapshotCache();
            cache.ConfigureValidator(mode.ValidationPolicy);
            var parameters = new BuildParameters
            {
                EvaluationCacheConfiguration = mode,
                ProjectInstanceSnapshotCache = cache,
            };
            var host = new MockHost(parameters);

            CreateFileConfiguration(project.Path, parameters).LoadProjectIntoConfiguration(
                host, BuildRequestDataFlags.None, submissionId: 1, nodeId: 1);
            ProjectRootElement importedRoot = ProjectRootElement.Open(
                import.Path,
                parameters.ProjectRootElementCache,
                isExplicitlyLoaded: true,
                preserveFormatting: null);
            importedRoot.Properties.Single().Value = "memory";
            importedRoot.HasUnsavedChanges.ShouldBeTrue();
            parameters.ProjectRootElementCache.TryGet(import.Path).ShouldBeSameAs(importedRoot);
            BuildRequestConfiguration second = CreateFileConfiguration(project.Path, parameters);
            second.LoadProjectIntoConfiguration(
                host, BuildRequestDataFlags.None, submissionId: 2, nodeId: 1);

            second.Project.GetPropertyValue("Imported").ShouldBe("memory");
            second.Project.EvaluationInputs.NonCacheable.ShouldBe(NonCacheableReason.InMemoryProject);
            cache.MaterializedEntries.ShouldBe(0);
            cache.ValidationRejections.ShouldBe(1);
        }
        finally
        {
            Environment.SetEnvironmentVariable(ModeVariable, originalMode);
            Traits.UpdateFromEnvironment();
        }
    }

    [Fact]
    public void SnapshotFileSystemRejectsUnsavedCachedRootForMissingImport()
    {
        const string ModeVariable = EvaluationCacheConfiguration.ModeEnvironmentVariable;
        string? originalMode = Environment.GetEnvironmentVariable(ModeVariable);
        try
        {
            Environment.SetEnvironmentVariable(
                ModeVariable,
                nameof(EvaluationCacheMode.SnapshotFileSystem));
            Traits.UpdateFromEnvironment();

            using TestEnvironment env = TestEnvironment.Create();
            string missingImport = Path.Combine(env.CreateFolder().Path, "missing.props");
            TransientTestFile project = env.CreateFile(
                "project.proj",
                $"<Project><Import Project=\"{missingImport}\" /></Project>");
            EvaluationCacheConfiguration mode = Traits.Instance.EvaluationCache;
            var cache = new ProjectInstanceSnapshotCache();
            cache.ConfigureValidator(mode.ValidationPolicy);
            var parameters = new BuildParameters
            {
                EvaluationCacheConfiguration = mode,
                ProjectInstanceSnapshotCache = cache,
            };
            var host = new MockHost(parameters);

            CreateFileConfiguration(project.Path, parameters).LoadProjectIntoConfiguration(
                host,
                BuildRequestDataFlags.IgnoreMissingEmptyAndInvalidImports,
                submissionId: 1,
                nodeId: 1);
            ProjectRootElement inMemoryImport = ProjectRootElement.Create(
                parameters.ProjectRootElementCache,
                NewProjectFileOptions.None);
            inMemoryImport.FullPath = missingImport;
            inMemoryImport.AddProperty("Imported", "memory");
            parameters.ProjectRootElementCache.AddEntry(inMemoryImport);

            BuildRequestConfiguration second = CreateFileConfiguration(project.Path, parameters);
            second.LoadProjectIntoConfiguration(
                host,
                BuildRequestDataFlags.IgnoreMissingEmptyAndInvalidImports,
                submissionId: 2,
                nodeId: 1);

            second.Project.GetPropertyValue("Imported").ShouldBe("memory");
            cache.MaterializedEntries.ShouldBe(0);
            cache.ValidationRejections.ShouldBe(1);
        }
        finally
        {
            Environment.SetEnvironmentVariable(ModeVariable, originalMode);
            Traits.UpdateFromEnvironment();
        }
    }

    [Fact]
    public void SnapshotFileSystemRejectsCachedRootForRecordedMissingImport()
    {
        const string ModeVariable = EvaluationCacheConfiguration.ModeEnvironmentVariable;
        string? originalMode = Environment.GetEnvironmentVariable(ModeVariable);
        try
        {
            Environment.SetEnvironmentVariable(
                ModeVariable,
                nameof(EvaluationCacheMode.SnapshotFileSystem));
            Traits.UpdateFromEnvironment();

            using TestEnvironment env = TestEnvironment.Create();
            string missingImport = Path.Combine(env.CreateFolder().Path, "missing.props");
            TransientTestFile project = env.CreateFile(
                "project.proj",
                $"<Project><Import Project=\"{missingImport}\" /></Project>");
            EvaluationCacheConfiguration mode = Traits.Instance.EvaluationCache;
            var cache = new ProjectInstanceSnapshotCache();
            cache.ConfigureValidator(mode.ValidationPolicy);
            var parameters = new BuildParameters
            {
                EvaluationCacheConfiguration = mode,
                ProjectInstanceSnapshotCache = cache,
            };
            var host = new MockHost(parameters);

            CreateFileConfiguration(project.Path, parameters).LoadProjectIntoConfiguration(
                host,
                BuildRequestDataFlags.IgnoreMissingEmptyAndInvalidImports,
                submissionId: 1,
                nodeId: 1);
            File.WriteAllText(
                missingImport,
                "<Project><PropertyGroup><Imported>cached root</Imported></PropertyGroup></Project>");
            ProjectRootElement cachedImport = ProjectRootElement.Open(
                missingImport,
                parameters.ProjectRootElementCache,
                isExplicitlyLoaded: true,
                preserveFormatting: null);
            cachedImport.HasUnsavedChanges.ShouldBeFalse();
            cachedImport.FileLengthWhenRead.ShouldNotBeNull();
            File.Delete(missingImport);

            BuildRequestConfiguration second = CreateFileConfiguration(project.Path, parameters);
            second.LoadProjectIntoConfiguration(
                host,
                BuildRequestDataFlags.IgnoreMissingEmptyAndInvalidImports,
                submissionId: 2,
                nodeId: 1);

            second.Project.GetPropertyValue("Imported").ShouldBe("cached root");
            cache.MaterializedEntries.ShouldBe(0);
            cache.ValidationRejections.ShouldBe(1);
        }
        finally
        {
            Environment.SetEnvironmentVariable(ModeVariable, originalMode);
            Traits.UpdateFromEnvironment();
        }
    }

    [Fact]
    public void SnapshotFileSystemRejectsCachedRootWithoutFileProvenance()
    {
        const string ModeVariable = EvaluationCacheConfiguration.ModeEnvironmentVariable;
        string? originalMode = Environment.GetEnvironmentVariable(ModeVariable);
        try
        {
            Environment.SetEnvironmentVariable(
                ModeVariable,
                nameof(EvaluationCacheMode.SnapshotFileSystem));
            Traits.UpdateFromEnvironment();

            using TestEnvironment env = TestEnvironment.Create();
            TransientTestFile project = env.CreateFile(
                "project.proj",
                "<Project><PropertyGroup><Value>disk</Value></PropertyGroup></Project>");
            EvaluationCacheConfiguration mode = Traits.Instance.EvaluationCache;
            var cache = new ProjectInstanceSnapshotCache();
            cache.ConfigureValidator(mode.ValidationPolicy);
            var parameters = new BuildParameters
            {
                EvaluationCacheConfiguration = mode,
                ProjectInstanceSnapshotCache = cache,
            };
            var host = new MockHost(parameters);

            CreateFileConfiguration(project.Path, parameters).LoadProjectIntoConfiguration(
                host, BuildRequestDataFlags.None, submissionId: 1, nodeId: 1);
            ProjectRootElement writerOnly = ProjectRootElement.Create(
                parameters.ProjectRootElementCache,
                NewProjectFileOptions.None);
            writerOnly.FullPath = project.Path;
            writerOnly.AddProperty("Value", "memory");
            using var writer = new StringWriter();
            writerOnly.Save(writer);
            writerOnly.FileLengthWhenRead.ShouldBeNull();
            parameters.ProjectRootElementCache.AddEntry(writerOnly);

            BuildRequestConfiguration second = CreateFileConfiguration(project.Path, parameters);
            second.LoadProjectIntoConfiguration(
                host, BuildRequestDataFlags.None, submissionId: 2, nodeId: 1);

            second.Project.GetPropertyValue("Value").ShouldBe("memory");
            second.Project.EvaluationInputs.NonCacheable.ShouldBe(NonCacheableReason.RecorderFailure);
            cache.MaterializedEntries.ShouldBe(0);
            cache.ValidationRejections.ShouldBe(1);
        }
        finally
        {
            Environment.SetEnvironmentVariable(ModeVariable, originalMode);
            Traits.UpdateFromEnvironment();
        }
    }

    [Fact]
    public void SnapshotUnsafeMaterializesWithoutValidation()
    {
        const string ModeVariable = EvaluationCacheConfiguration.ModeEnvironmentVariable;
        string? originalMode = Environment.GetEnvironmentVariable(ModeVariable);
        try
        {
            Environment.SetEnvironmentVariable(
                ModeVariable,
                nameof(EvaluationCacheMode.SnapshotUnsafe));
            Traits.UpdateFromEnvironment();

            using TestEnvironment env = TestEnvironment.Create();
            TransientTestFile project = env.CreateFile(
                "project.proj",
                "<Project><PropertyGroup><Value>stored</Value></PropertyGroup></Project>");
            EvaluationCacheConfiguration mode = Traits.Instance.EvaluationCache;
            var cache = new ProjectInstanceSnapshotCache();
            cache.ConfigureValidator(mode.ValidationPolicy);
            var parameters = new BuildParameters
            {
                EvaluationCacheConfiguration = mode,
                ProjectInstanceSnapshotCache = cache,
            };
            var host = new MockHost(parameters);

            BuildRequestConfiguration first = CreateFileConfiguration(project.Path, parameters);
            first.LoadProjectIntoConfiguration(
                host, BuildRequestDataFlags.None, submissionId: 1, nodeId: 1);
            BuildRequestConfiguration second = CreateFileConfiguration(project.Path, parameters);
            second.LoadProjectIntoConfiguration(
                host, BuildRequestDataFlags.None, submissionId: 2, nodeId: 1);

            second.Project.GetPropertyValue("Value").ShouldBe("stored");
            second.Project.EvaluationInputs.ShouldBeSameAs(
                first.Project.EvaluationInputs);
            ProjectInstanceSnapshotCacheStatistics statistics = cache.GetStatistics();
            statistics.FreshEvaluations.ShouldBe(1);
            statistics.RecordedEvaluations.ShouldBe(1);
            statistics.CacheHits.ShouldBe(1);
            statistics.CacheMisses.ShouldBe(1);
            statistics.ValidationAttempts.ShouldBe(0);
            statistics.MaterializedEntries.ShouldBe(1);
            statistics.StoredEntries.ShouldBe(1);
        }
        finally
        {
            Environment.SetEnvironmentVariable(ModeVariable, originalMode);
            Traits.UpdateFromEnvironment();
        }
    }

    [Fact]
    public void SnapshotUnsafeAdmitsFrozenNonCacheableManifest()
    {
        const string ModeVariable = EvaluationCacheConfiguration.ModeEnvironmentVariable;
        string? originalMode = Environment.GetEnvironmentVariable(ModeVariable);
        try
        {
            Environment.SetEnvironmentVariable(
                ModeVariable,
                nameof(EvaluationCacheMode.SnapshotUnsafe));
            Traits.UpdateFromEnvironment();

            using TestEnvironment env = TestEnvironment.Create();
            TransientTestFile project = env.CreateFile(
                "project.proj",
                "<Project><PropertyGroup><Now>$([System.DateTime]::Now)</Now></PropertyGroup></Project>");
            EvaluationCacheConfiguration mode = Traits.Instance.EvaluationCache;
            var cache = new ProjectInstanceSnapshotCache();
            cache.ConfigureValidator(mode.ValidationPolicy);
            var parameters = new BuildParameters
            {
                EvaluationCacheConfiguration = mode,
                ProjectInstanceSnapshotCache = cache,
            };
            var host = new MockHost(parameters);

            BuildRequestConfiguration first = CreateFileConfiguration(project.Path, parameters);
            first.LoadProjectIntoConfiguration(host, BuildRequestDataFlags.None, submissionId: 1, nodeId: 1);
            BuildRequestConfiguration second = CreateFileConfiguration(project.Path, parameters);
            second.LoadProjectIntoConfiguration(host, BuildRequestDataFlags.None, submissionId: 2, nodeId: 1);

            first.Project.EvaluationInputs.NonCacheable.ShouldBe(
                NonCacheableReason.VolatilePropertyFunction);
            second.Project.EvaluationInputs.ShouldBeSameAs(
                first.Project.EvaluationInputs);
            cache.StoredEntries.ShouldBe(1);
            cache.MaterializedEntries.ShouldBe(1);
        }
        finally
        {
            Environment.SetEnvironmentVariable(ModeVariable, originalMode);
            Traits.UpdateFromEnvironment();
        }
    }

    [Fact]
    public void SnapshotFileSystemRejectsNonCacheableManifestAdmission()
    {
        const string ModeVariable = EvaluationCacheConfiguration.ModeEnvironmentVariable;
        string? originalMode = Environment.GetEnvironmentVariable(ModeVariable);
        try
        {
            Environment.SetEnvironmentVariable(
                ModeVariable,
                nameof(EvaluationCacheMode.SnapshotFileSystem));
            Traits.UpdateFromEnvironment();

            using TestEnvironment env = TestEnvironment.Create();
            TransientTestFile project = env.CreateFile(
                "project.proj",
                "<Project><PropertyGroup><Now>$([System.DateTime]::Now)</Now></PropertyGroup></Project>");
            EvaluationCacheConfiguration mode = Traits.Instance.EvaluationCache;
            var cache = new ProjectInstanceSnapshotCache();
            cache.ConfigureValidator(mode.ValidationPolicy);
            var parameters = new BuildParameters
            {
                EvaluationCacheConfiguration = mode,
                ProjectInstanceSnapshotCache = cache,
            };
            var host = new MockHost(parameters);

            CreateFileConfiguration(project.Path, parameters).LoadProjectIntoConfiguration(
                host, BuildRequestDataFlags.None, submissionId: 1, nodeId: 1);

            cache.Count.ShouldBe(0);
            cache.StoredEntries.ShouldBe(0);
            cache.GetStatistics().NonCacheableEvaluations.ShouldBe(1);
        }
        finally
        {
            Environment.SetEnvironmentVariable(ModeVariable, originalMode);
            Traits.UpdateFromEnvironment();
        }
    }

    [Fact]
    public void RecordModeRecordsWithoutSnapshotLookupOrAdmission()
    {
        const string ModeVariable = EvaluationCacheConfiguration.ModeEnvironmentVariable;
        string? originalMode = Environment.GetEnvironmentVariable(ModeVariable);
        try
        {
            Environment.SetEnvironmentVariable(ModeVariable, nameof(EvaluationCacheMode.Record));
            Traits.UpdateFromEnvironment();

            using TestEnvironment env = TestEnvironment.Create();
            TransientTestFile project = env.CreateFile("project.proj", "<Project />");
            EvaluationCacheConfiguration mode = Traits.Instance.EvaluationCache;
            var cache = new ProjectInstanceSnapshotCache();
            var parameters = new BuildParameters
            {
                EvaluationCacheConfiguration = mode,
                ProjectInstanceSnapshotCache = cache,
            };
            var host = new MockHost(parameters);

            CreateFileConfiguration(project.Path, parameters).LoadProjectIntoConfiguration(
                host, BuildRequestDataFlags.None, submissionId: 1, nodeId: 1);
            CreateFileConfiguration(project.Path, parameters).LoadProjectIntoConfiguration(
                host, BuildRequestDataFlags.None, submissionId: 2, nodeId: 1);

            ProjectInstanceSnapshotCacheStatistics statistics = cache.GetStatistics();
            statistics.FreshEvaluations.ShouldBe(2);
            statistics.RecordedEvaluations.ShouldBe(2);
            statistics.CacheHits.ShouldBe(0);
            statistics.CacheMisses.ShouldBe(0);
            statistics.MaterializedEntries.ShouldBe(0);
            statistics.StoredEntries.ShouldBe(0);
            statistics.Count.ShouldBe(0);
            BuildRequestConfiguration casing = CreateFileConfiguration(
                project.Path,
                parameters,
                new Dictionary<string, string?>
                {
                    ["configuration"] = "Debug",
                });
            casing.LoadProjectIntoConfiguration(
                host, BuildRequestDataFlags.None, submissionId: 3, nodeId: 1);
            casing.Project.EvaluationInputs.Key.GlobalProperties.ShouldContain(
                "CONFIGURATION=Debug\0");
        }
        finally
        {
            Environment.SetEnvironmentVariable(ModeVariable, originalMode);
            Traits.UpdateFromEnvironment();
        }
    }

    [Fact]
    public void XmlToolsVersionSelectsCurrentCustomToolsetEvidence()
    {
        const string ModeVariable = EvaluationCacheConfiguration.ModeEnvironmentVariable;
        const string LegacyToolsVersionVariable = "MSBUILDLEGACYDEFAULTTOOLSVERSION";
        string? originalMode = Environment.GetEnvironmentVariable(ModeVariable);
        string? originalLegacyToolsVersion =
            Environment.GetEnvironmentVariable(LegacyToolsVersionVariable);
        try
        {
            Environment.SetEnvironmentVariable(
                ModeVariable,
                nameof(EvaluationCacheMode.SnapshotFileSystem));
            Environment.SetEnvironmentVariable(LegacyToolsVersionVariable, "1");
            Traits.UpdateFromEnvironment();
            InternalUtilities.RefreshInternalEnvironmentValues();

            using TestEnvironment env = TestEnvironment.Create();
            TransientTestFile project = env.CreateFile(
                "project.proj",
                "<Project ToolsVersion=\"Custom\"><PropertyGroup><Value>$(Contoso)</Value></PropertyGroup></Project>");
            using var collection = new ProjectCollection();
            Toolset current = collection.GetToolset(collection.DefaultToolsVersion);
            collection.AddToolset(new Toolset(
                "Custom",
                current.ToolsPath,
                new Dictionary<string, string> { ["Contoso"] = "first" },
                collection,
                msbuildOverrideTasksPath: null));
            EvaluationCacheConfiguration mode = Traits.Instance.EvaluationCache;
            var cache = new ProjectInstanceSnapshotCache();
            cache.ConfigureValidator(mode.ValidationPolicy);
            var parameters = new BuildParameters(collection)
            {
                EvaluationCacheConfiguration = mode,
                ProjectInstanceSnapshotCache = cache,
            };
            var host = new MockHost(parameters);

            BuildRequestConfiguration first = CreateFileConfiguration(project.Path, parameters);
            first.LoadProjectIntoConfiguration(host, BuildRequestDataFlags.None, submissionId: 1, nodeId: 1);

            collection.RemoveToolset("Custom").ShouldBeTrue();
            collection.AddToolset(new Toolset(
                "Custom",
                current.ToolsPath,
                new Dictionary<string, string> { ["Contoso"] = "second" },
                collection,
                msbuildOverrideTasksPath: null));
            parameters = new BuildParameters(collection)
            {
                EvaluationCacheConfiguration = mode,
                ProjectInstanceSnapshotCache = cache,
            };
            host = new MockHost(parameters);
            BuildRequestConfiguration second = CreateFileConfiguration(project.Path, parameters);
            second.LoadProjectIntoConfiguration(host, BuildRequestDataFlags.None, submissionId: 2, nodeId: 1);

            first.Project.ToolsVersion.ShouldBe("Custom");
            second.Project.ToolsVersion.ShouldBe("Custom");
            first.Project.GetPropertyValue("Value").ShouldBe("first");
            second.Project.GetPropertyValue("Value").ShouldBe("second");
            cache.CacheHits.ShouldBe(0);
            cache.CacheMisses.ShouldBe(2);
            cache.StoredEntries.ShouldBe(2);
        }
        finally
        {
            Environment.SetEnvironmentVariable(ModeVariable, originalMode);
            Environment.SetEnvironmentVariable(
                LegacyToolsVersionVariable,
                originalLegacyToolsVersion);
            Traits.UpdateFromEnvironment();
            InternalUtilities.RefreshInternalEnvironmentValues();
        }
    }

    [Fact]
    public void GlobalPropertyNameCasingMatchesAtLookupAndValidation()
    {
        const string ModeVariable = EvaluationCacheConfiguration.ModeEnvironmentVariable;
        string? originalMode = Environment.GetEnvironmentVariable(ModeVariable);
        try
        {
            Environment.SetEnvironmentVariable(
                ModeVariable,
                nameof(EvaluationCacheMode.SnapshotFileSystem));
            Traits.UpdateFromEnvironment();

            using TestEnvironment env = TestEnvironment.Create();
            TransientTestFile project = env.CreateFile(
                "project.proj",
                "<Project><PropertyGroup><Value>$(Configuration)</Value></PropertyGroup></Project>");
            EvaluationCacheConfiguration mode = Traits.Instance.EvaluationCache;
            var cache = new ProjectInstanceSnapshotCache();
            cache.ConfigureValidator(mode.ValidationPolicy);
            var parameters = new BuildParameters
            {
                EvaluationCacheConfiguration = mode,
                ProjectInstanceSnapshotCache = cache,
            };
            var host = new MockHost(parameters);

            BuildRequestConfiguration first = CreateFileConfiguration(
                project.Path,
                parameters,
                new Dictionary<string, string?> { ["Configuration"] = "Debug" });
            first.LoadProjectIntoConfiguration(host, BuildRequestDataFlags.None, submissionId: 1, nodeId: 1);
            BuildRequestConfiguration second = CreateFileConfiguration(
                project.Path,
                parameters,
                new Dictionary<string, string?> { ["configuration"] = "Debug" });
            second.LoadProjectIntoConfiguration(host, BuildRequestDataFlags.None, submissionId: 2, nodeId: 1);

            second.Project.GetPropertyValue("Value").ShouldBe("Debug");
            cache.CacheHits.ShouldBe(1);
            cache.GetStatistics().ValidationAccepted.ShouldBe(1);
            cache.MaterializedEntries.ShouldBe(1);
        }
        finally
        {
            Environment.SetEnvironmentVariable(ModeVariable, originalMode);
            Traits.UpdateFromEnvironment();
        }
    }

    [Fact]
    public void MaterializationFailureDoesNotChangeValidationCounters()
    {
        using TestEnvironment env = TestEnvironment.Create();
        TransientTestFile project = env.CreateFile("project.proj", "<Project />");
        var cache = new ProjectInstanceSnapshotCache
        {
            Validator = new AcceptingTestValidator(),
        };
        ProjectInstanceSnapshotCacheKey key = CreateKey(
            project.Path,
            "Current",
            ProjectLoadSettings.Default,
            new Dictionary<string, string>());
        cache.AddOrReplace(key, CreateEntryWithToolsVersion("Custom")).ShouldBeTrue();
        var parameters = new BuildParameters
        {
            ProjectInstanceSnapshotCache = cache,
        };
        var host = new MockHost(parameters);

        CreateFileConfiguration(project.Path, parameters).LoadProjectIntoConfiguration(
            host, BuildRequestDataFlags.None, submissionId: 1, nodeId: 1);

        ProjectInstanceSnapshotCacheStatistics statistics = cache.GetStatistics();
        statistics.ValidationAttempts.ShouldBe(0);
        statistics.ValidationAccepted.ShouldBe(0);
        statistics.ValidationRejections.ShouldBe(0);
        statistics.ValidationErrors.ShouldBe(0);
        statistics.MaterializedEntries.ShouldBe(0);
        statistics.Fallbacks.ShouldBe(1);
    }
    [Fact]
    public void InvalidModeLogsDiagnosticAndDisablesCache()
    {
        const string ModeVariable = EvaluationCacheConfiguration.ModeEnvironmentVariable;
        string? originalMode = Environment.GetEnvironmentVariable(ModeVariable);
        try
        {
            Environment.SetEnvironmentVariable(ModeVariable, "invalid");
            Traits.UpdateFromEnvironment();
            var logger = new MockLogger(verbosity: LoggerVerbosity.Diagnostic);
            using var buildManager = new BuildManager();

            buildManager.BeginBuild(new BuildParameters { Loggers = [logger] });
            ((IBuildComponentHost)buildManager)
                .BuildParameters
                .ProjectInstanceSnapshotCache.ShouldBeNull();
            buildManager.EndBuild();

            logger.FullLog.ShouldContain(
                "The value \"invalid\" of MSBUILDEVALUATIONCACHEMODE is invalid.");
            logger.FullLog.ShouldContain(
                "EvaluationCacheExperimentStatus|Version=1|Mode=Disabled|ConfigurationValid=False|");
        }
        finally
        {
            Environment.SetEnvironmentVariable(ModeVariable, originalMode);
            Traits.UpdateFromEnvironment();
        }
    }

    [Fact]
    public void UnconfiguredBuildDoesNotLogStatusButExplicitDisabledDoes()
    {
        const string ModeVariable = EvaluationCacheConfiguration.ModeEnvironmentVariable;
        const string RecordVariable = "MSBUILDRECORDEVALUATIONINPUTS";
        const string SnapshotVariable = "MSBUILDENABLEPROJECTINSTANCESNAPSHOTCACHE";
        string? originalMode = Environment.GetEnvironmentVariable(ModeVariable);
        string? originalRecord = Environment.GetEnvironmentVariable(RecordVariable);
        string? originalSnapshot = Environment.GetEnvironmentVariable(SnapshotVariable);
        try
        {
            Environment.SetEnvironmentVariable(ModeVariable, null);
            Environment.SetEnvironmentVariable(RecordVariable, null);
            Environment.SetEnvironmentVariable(SnapshotVariable, null);
            Traits.UpdateFromEnvironment();

            var defaultLogger = new MockLogger(verbosity: LoggerVerbosity.Diagnostic);
            using (var defaultBuildManager = new BuildManager())
            {
                defaultBuildManager.BeginBuild(new BuildParameters { Loggers = [defaultLogger] });
                defaultBuildManager.EndBuild();
            }

            defaultLogger.FullLog.ShouldNotContain("EvaluationCacheExperimentStatus|");

            Environment.SetEnvironmentVariable(
                ModeVariable,
                nameof(EvaluationCacheMode.Disabled));
            Traits.UpdateFromEnvironment();
            var explicitLogger = new MockLogger(verbosity: LoggerVerbosity.Diagnostic);
            using var explicitBuildManager = new BuildManager();
            explicitBuildManager.BeginBuild(new BuildParameters { Loggers = [explicitLogger] });
            explicitBuildManager.EndBuild();

            explicitLogger.FullLog.ShouldContain(
                "EvaluationCacheExperimentStatus|Version=1|Mode=Disabled|ConfigurationValid=True|");
        }
        finally
        {
            Environment.SetEnvironmentVariable(ModeVariable, originalMode);
            Environment.SetEnvironmentVariable(RecordVariable, originalRecord);
            Environment.SetEnvironmentVariable(SnapshotVariable, originalSnapshot);
            Traits.UpdateFromEnvironment();
        }
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

    private static BuildRequestConfiguration CreateFileConfiguration(
        string projectPath,
        BuildParameters parameters,
        IDictionary<string, string?>? globalProperties = null)
    {
        var requestData = new BuildRequestData(
            projectPath,
            globalProperties ?? new Dictionary<string, string?>(),
            toolsVersion: null,
            [],
            hostServices: null,
            BuildRequestDataFlags.None);
        return new BuildRequestConfiguration(requestData, parameters.DefaultToolsVersion);
    }

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

    private static ProjectInstanceSnapshotCacheEntry CreateEntryWithToolsVersion(
        string toolsVersion)
    {
        using var collection = new ProjectCollection();
        Toolset current = collection.GetToolset(collection.DefaultToolsVersion);
        collection.AddToolset(new Toolset(
            toolsVersion,
            current.ToolsPath,
            collection,
            msbuildOverrideTasksPath: null));
        using var projectFromString = new ProjectRootElementFromString(
            $"<Project ToolsVersion=\"{toolsVersion}\" />",
            collection);
        return new ProjectInstanceSnapshotCacheEntry(
            ProjectInstanceSnapshot.Create(
                new ProjectInstance(
                    projectFromString.Project,
                    new Dictionary<string, string>(),
                    toolsVersion,
                    collection)),
            EmptyProjectInstanceSnapshotValidationData.Instance);
    }

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
        internal int Calls { get; private set; }

        public ProjectInstanceSnapshotValidationResult Validate(
            ProjectInstanceSnapshotCacheKey key,
            ProjectInstanceSnapshotCacheEntry entry)
        {
            Calls++;
            return ProjectInstanceSnapshotValidationResult.Valid;
        }
    }

    private sealed class ThrowingTestValidator : IProjectInstanceSnapshotValidator
    {
        public ProjectInstanceSnapshotValidationResult Validate(
            ProjectInstanceSnapshotCacheKey key,
            ProjectInstanceSnapshotCacheEntry entry) =>
            throw new InvalidOperationException("Validation failed.");
    }

    private sealed class ExceptionThrowingTestValidator(Exception exception) : IProjectInstanceSnapshotValidator
    {
        public ProjectInstanceSnapshotValidationResult Validate(
            ProjectInstanceSnapshotCacheKey key,
            ProjectInstanceSnapshotCacheEntry entry) =>
            throw exception;
    }

    private sealed class CancelingTestValidator : IProjectInstanceSnapshotValidator
    {
        public ProjectInstanceSnapshotValidationResult Validate(
            ProjectInstanceSnapshotCacheKey key,
            ProjectInstanceSnapshotCacheEntry entry) =>
            throw new OperationCanceledException("Validation canceled.");
    }

    private sealed class AbortingTestValidator : IProjectInstanceSnapshotValidator
    {
        public ProjectInstanceSnapshotValidationResult Validate(
            ProjectInstanceSnapshotCacheKey key,
            ProjectInstanceSnapshotCacheEntry entry) =>
            throw new BuildAbortedException("Validation aborted.");
    }

    private sealed class ChangingSdkResolverService : ISdkResolverService, IBuildComponent
    {
        internal ChangingSdkResolverService(string path)
        {
            Path = path;
        }

        internal int Calls { get; private set; }
        internal string Path { get; set; }
        internal string? Message { get; set; }
        internal bool Success { get; set; } = true;
        public Action<INodePacket> SendPacket => _ => { };
        public bool IsNodeShutDown { get; set; }

        public void ClearCache(int submissionId)
        {
        }

        public void ClearCaches()
        {
        }

        public SdkResult ResolveSdk(
            int submissionId,
            SdkReference sdk,
            LoggingContext loggingContext,
            ElementLocation sdkReferenceLocation,
            string solutionPath,
            string projectPath,
            bool interactive,
            bool isRunningInVisualStudio,
            bool failOnUnresolvedSdk)
        {
            Calls++;
            if (Message is not null)
            {
                loggingContext.LogSdkMessage(MessageImportance.Low, Message);
            }

            return Success
                ? new SdkResult(sdk, Path, "1.0", warnings: null)
                : new SdkResult(sdk, errors: ["not found"], warnings: null);
        }

        public void InitializeComponent(IBuildComponentHost host)
        {
        }

        public void ShutdownComponent() => IsNodeShutDown = true;
    }
}
