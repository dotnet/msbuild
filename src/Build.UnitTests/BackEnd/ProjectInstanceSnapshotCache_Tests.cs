// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Exceptions;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.UnitTests;
using Shouldly;
using Xunit;

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
        cache.CurrentSizeBytes.ShouldBe(second.RetainedSizeBytes);

        cache.Clear();
        cache.TryGet(secondKey, out _).ShouldBeFalse();
        cache.Count.ShouldBe(0);
        cache.CurrentSizeBytes.ShouldBe(0);
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

        Toolset requestToolset = parameters.GetToolset(configuration.ToolsVersion);
        var key = new ProjectInstanceSnapshotCacheKey(
            project.Path,
            configuration.ToolsVersion,
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
        var cache = new ProjectInstanceSnapshotCache(entry.RetainedSizeBytes * 2L);
        ProjectInstanceSnapshotCacheKey firstKey = EmptyKey("First.csproj");
        ProjectInstanceSnapshotCacheKey secondKey = EmptyKey("Second.csproj");
        ProjectInstanceSnapshotCacheKey thirdKey = EmptyKey("Third.csproj");

        cache.AddOrReplace(firstKey, entry).ShouldBeTrue();
        cache.AddOrReplace(secondKey, entry).ShouldBeTrue();
        cache.TryGet(firstKey, out _).ShouldBeTrue();

        cache.AddOrReplace(thirdKey, entry).ShouldBeTrue();

        cache.TryGet(firstKey, out _).ShouldBeTrue();
        cache.TryGet(secondKey, out _).ShouldBeFalse();
        cache.TryGet(thirdKey, out _).ShouldBeTrue();
        cache.Count.ShouldBe(2);
        cache.CurrentSizeBytes.ShouldBe(entry.RetainedSizeBytes * 2L);
    }

    [Fact]
    public void ReplacementUpdatesSizeAndRecency()
    {
        ProjectInstanceSnapshotCacheEntry small = CreateEntry("small");
        ProjectInstanceSnapshotCacheEntry large =
            CreateEntry("large", validationDataSizeBytes: 128);
        var cache = new ProjectInstanceSnapshotCache(
            small.RetainedSizeBytes + large.RetainedSizeBytes);
        ProjectInstanceSnapshotCacheKey firstKey = EmptyKey("First.csproj");
        ProjectInstanceSnapshotCacheKey secondKey = EmptyKey("Second.csproj");
        ProjectInstanceSnapshotCacheKey thirdKey = EmptyKey("Third.csproj");

        cache.AddOrReplace(firstKey, small).ShouldBeTrue();
        cache.AddOrReplace(secondKey, small).ShouldBeTrue();
        cache.AddOrReplace(firstKey, large).ShouldBeTrue();
        cache.AddOrReplace(thirdKey, small).ShouldBeTrue();

        cache.TryGet(firstKey, out ProjectInstanceSnapshotCacheEntry? found).ShouldBeTrue();
        found.ShouldBeSameAs(large);
        cache.TryGet(secondKey, out _).ShouldBeFalse();
        cache.TryGet(thirdKey, out _).ShouldBeTrue();
        cache.Count.ShouldBe(2);
        cache.CurrentSizeBytes.ShouldBe(large.RetainedSizeBytes + small.RetainedSizeBytes);
    }

    [Fact]
    public void OversizedSnapshotIsNotCachedAndRemovesExistingEntry()
    {
        ProjectInstanceSnapshotCacheEntry small = CreateEntry("small");
        ProjectInstanceSnapshotCacheEntry oversized =
            CreateEntry(
                "oversized",
                validationDataSizeBytes: small.RetainedSizeBytes + 1);
        var cache = new ProjectInstanceSnapshotCache(small.RetainedSizeBytes);
        ProjectInstanceSnapshotCacheKey retainedKey = EmptyKey("Retained.csproj");
        ProjectInstanceSnapshotCacheKey oversizedKey = EmptyKey("Oversized.csproj");

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
        try
        {
            Traits.ProjectInstanceSnapshotCacheEnabledOverride = true;
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

            logger.FullLog.ShouldContain("Project instance snapshot cache: build 1, 1 entries");
            logger.FullLog.ShouldContain(", 1 stores, 0 hits, 1 misses,");
        }
        finally
        {
            Traits.ProjectInstanceSnapshotCacheEnabledOverride = null;
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
}
