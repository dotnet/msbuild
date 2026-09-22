// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Construction;
using Microsoft.Build.Definition;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Exceptions;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.UnitTests;
using Microsoft.Win32;
using Shouldly;
using Xunit;
using static Microsoft.Build.Engine.UnitTests.TestComparers.ProjectInstanceModelTestComparers;

#nullable enable

namespace Microsoft.Build.UnitTests.BackEnd;

public sealed class ProjectInstanceSnapshotCacheAcceptance_Tests : IDisposable
{
    private const string ModeVariable = EvaluationCacheConfiguration.ModeEnvironmentVariable;
    private const string LegacyRecordVariable = "MSBUILDRECORDEVALUATIONINPUTS";
    private const string LegacySnapshotVariable = "MSBUILDENABLEPROJECTINSTANCESNAPSHOTCACHE";

    private readonly TestEnvironment _env = TestEnvironment.Create();
    private readonly string? _originalMode = Environment.GetEnvironmentVariable(ModeVariable);
    private readonly string? _originalRecord = Environment.GetEnvironmentVariable(LegacyRecordVariable);
    private readonly string? _originalSnapshot = Environment.GetEnvironmentVariable(LegacySnapshotVariable);

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(ModeVariable, _originalMode);
        Environment.SetEnvironmentVariable(LegacyRecordVariable, _originalRecord);
        Environment.SetEnvironmentVariable(LegacySnapshotVariable, _originalSnapshot);
        Traits.UpdateFromEnvironment();
        _env.Dispose();
    }

    [Fact]
    public void UnchangedCheckedHitMatchesFreshRichProjectAndBuildResult()
    {
        _env.SetEnvironmentVariable("MSBUILD_ACCEPTANCE_ENV", "environment");
        TransientTestFolder folder = _env.CreateFolder();
        _env.CreateFile(
            folder,
            "values.props",
            "<Project><PropertyGroup><Imported>imported</Imported></PropertyGroup></Project>");
        TransientTestFile project = _env.CreateFile(
            folder,
            "rich.proj",
            """
            <Project DefaultTargets="Build">
              <UsingTask TaskName="Message" AssemblyFile="$(MSBuildToolsPath)\Microsoft.Build.Tasks.Core.dll" />
              <Import Project="values.props" />
              <PropertyGroup>
                <DirectEnvironment>$([System.Environment]::GetEnvironmentVariable('MSBUILD_ACCEPTANCE_ENV'))</DirectEnvironment>
              </PropertyGroup>
              <ItemDefinitionGroup>
                <Compile><Kind>source</Kind></Compile>
              </ItemDefinitionGroup>
              <ItemGroup>
                <Compile Include="a.cs"><Flavor>$(Imported)-$(Configuration)-$(DirectEnvironment)</Flavor></Compile>
              </ItemGroup>
              <Target Name="Build" Returns="@(_TaskOutput)">
                <CreateItem Include="@(Compile)">
                  <Output TaskParameter="Include" ItemName="_TaskOutput" />
                </CreateItem>
                <Message Importance="High" Text="ACCEPTANCE|$(Imported)|$(Configuration)|$(DirectEnvironment)|@(_TaskOutput->'%(Filename):%(Kind):%(Flavor)')" />
              </Target>
            </Project>
            """);
        var globals = new Dictionary<string, string?> { ["Configuration"] = "Release" };
        SetMode(EvaluationCacheMode.SnapshotFileSystem);
        var logger = new MockLogger(verbosity: LoggerVerbosity.Diagnostic);
        using var manager = new BuildManager();
        var parameters = new BuildParameters
        {
            EvaluationCacheConfiguration = Traits.Instance.EvaluationCache,
            Loggers = [logger],
        };
        var request = new BuildRequestData(
            project.Path,
            globals,
            null,
            ["Build"],
            null,
            BuildRequestDataFlags.ProvideProjectStateAfterBuild);

        manager.Build(parameters, request).OverallResult.ShouldBe(BuildResultCode.Success);
        BuildResult hit = manager.Build(parameters, request);
        var cache = (ProjectInstanceSnapshotCache)((IBuildComponentHost)manager)
            .GetComponent(BuildComponentType.ProjectInstanceSnapshotCache);
        BuildResult fresh = BuildDisabled(project.Path, "Build", globals);

        hit.OverallResult.ShouldBe(fresh.OverallResult);
        hit.ResultsByTarget["Build"].ResultCode.ShouldBe(fresh.ResultsByTarget["Build"].ResultCode);
        hit.ResultsByTarget["Build"].Items.Select(item => item.ItemSpec)
            .ShouldBe(fresh.ResultsByTarget["Build"].Items.Select(item => item.ItemSpec));
        hit.ResultsByTarget["Build"].Items.ShouldHaveSingleItem()
            .GetMetadata("Flavor").ShouldBe("imported-Release-environment");
        AssertCoreSemanticStateEqual(
            fresh.ProjectStateAfterBuild.ShouldNotBeNull(),
            hit.ProjectStateAfterBuild.ShouldNotBeNull());
        logger.FullLog.ShouldContain(
            "ACCEPTANCE|imported|Release|environment|a:source:imported-Release-environment");
        cache.GetStatistics().ValidationAccepted.ShouldBe(1);
        cache.MaterializedEntries.ShouldBe(1);
    }

    [Theory]
    [InlineData(InputMutation.Root)]
    [InlineData(InputMutation.Import)]
    [InlineData(InputMutation.GlobAdd)]
    [InlineData(InputMutation.GlobDelete)]
    [InlineData(InputMutation.GlobRename)]
    [InlineData(InputMutation.MissingAppears)]
    [InlineData(InputMutation.DirectEnvironment)]
    [InlineData(InputMutation.ImportedEnvironment)]
    [InlineData(InputMutation.GlobalProperty)]
    public void ChangedInputMatchesDisabledFreshControl(InputMutation mutation)
    {
        string directVariable = $"MSBUILD_ACCEPTANCE_DIRECT_{Guid.NewGuid():N}";
        string importedVariable = $"MSBUILD_ACCEPTANCE_IMPORTED_{Guid.NewGuid():N}";
        _env.SetEnvironmentVariable(directVariable, "before-direct");
        _env.SetEnvironmentVariable(importedVariable, "before-imported");
        TransientTestFolder folder = _env.CreateFolder();
        TransientTestFile import = _env.CreateFile(
            folder,
            "values.props",
            "<Project><PropertyGroup><Imported>before</Imported></PropertyGroup></Project>");
        _env.CreateFile(folder, "one.cs", string.Empty);
        TransientTestFile? deleted = mutation == InputMutation.GlobDelete
            ? _env.CreateFile(folder, "two.cs", string.Empty)
            : null;
        string renameSource = Path.Combine(folder.Path, "rename-from.cs");
        if (mutation == InputMutation.GlobRename)
        {
            File.WriteAllText(renameSource, string.Empty);
        }
        string optionalImport = Path.Combine(folder.Path, "optional.props");
        TransientTestFile project = _env.CreateFile(
            folder,
            "mutations.proj",
            $"""
            <Project>
              <Import Project="values.props" />
              <Import Project="optional.props" Condition="Exists('optional.props')" />
              <PropertyGroup>
                <Root>before</Root>
                <DirectEnvironment>$([System.Environment]::GetEnvironmentVariable('{directVariable}'))</DirectEnvironment>
                <Value>$(Root)|$(Imported)|$(Optional)|$(Configuration)|$(DirectEnvironment)|$({importedVariable})</Value>
              </PropertyGroup>
              <ItemGroup><Compile Include="*.cs" /></ItemGroup>
            </Project>
            """);
        var initialGlobals = new Dictionary<string, string?> { ["Configuration"] = "Debug" };
        var currentGlobals = new Dictionary<string, string?>(initialGlobals);
        CheckedEvaluation checkedEvaluation = CreateChecked(project.Path, initialGlobals);
        _ = Load(checkedEvaluation, submissionId: 1);
        ProjectInstanceSnapshotCacheStatistics beforeMutation = checkedEvaluation.Cache.GetStatistics();
        beforeMutation.Count.ShouldBe(1);
        beforeMutation.StoredEntries.ShouldBe(1);
        beforeMutation.CacheMisses.ShouldBe(1);
        beforeMutation.CacheHits.ShouldBe(0);
        beforeMutation.MaterializedEntries.ShouldBe(0);

        switch (mutation)
        {
            case InputMutation.Root:
                WriteWithChangedMetadata(
                    project.Path,
                    File.ReadAllText(project.Path).Replace("<Root>before</Root>", "<Root>after-root</Root>"));
                checkedEvaluation.Parameters.ProjectRootElementCache.DiscardImplicitReferences();
                break;
            case InputMutation.Import:
                WriteWithChangedMetadata(
                    import.Path,
                    "<Project><PropertyGroup><Imported>after-import</Imported></PropertyGroup></Project>");
                checkedEvaluation.Parameters.ProjectRootElementCache.DiscardImplicitReferences();
                break;
            case InputMutation.GlobAdd:
                _env.CreateFile(folder, "added.cs", string.Empty);
                TouchDirectory(folder.Path);
                break;
            case InputMutation.GlobDelete:
                File.Delete(deleted!.Path);
                TouchDirectory(folder.Path);
                break;
            case InputMutation.GlobRename:
                File.Move(renameSource, Path.Combine(folder.Path, "rename-to.cs"));
                TouchDirectory(folder.Path);
                break;
            case InputMutation.MissingAppears:
                File.WriteAllText(
                    optionalImport,
                    "<Project><PropertyGroup><Optional>appeared</Optional></PropertyGroup></Project>");
                TouchDirectory(folder.Path);
                break;
            case InputMutation.DirectEnvironment:
                _env.SetEnvironmentVariable(directVariable, "after-direct");
                break;
            case InputMutation.ImportedEnvironment:
                _env.SetEnvironmentVariable(importedVariable, "after-imported");
                checkedEvaluation = RefreshCheckedEnvironment(checkedEvaluation);
                break;
            case InputMutation.GlobalProperty:
                currentGlobals["Configuration"] = "Release";
                break;
        }

        ProjectInstance candidate = Load(
            checkedEvaluation with { GlobalProperties = currentGlobals },
            submissionId: 2);
        ProjectInstance fresh = LoadDisabled(
            project.Path,
            currentGlobals,
            environmentOverrides: mutation == InputMutation.DirectEnvironment
                ? new Dictionary<string, string?> { [directVariable] = "before-direct" }
                : null);
        ProjectInstanceSnapshotCacheStatistics afterMutation = checkedEvaluation.Cache.GetStatistics();
        bool distinctKey = mutation is InputMutation.GlobalProperty or InputMutation.ImportedEnvironment;

        AssertCoreSemanticStateEqual(fresh, candidate);
        candidate.GetPropertyValue("Value").ShouldBe(fresh.GetPropertyValue("Value"));
        candidate.GetItems("Compile").Select(item => item.EvaluatedInclude)
            .ShouldBe(fresh.GetItems("Compile").Select(item => item.EvaluatedInclude), ignoreOrder: true);
        afterMutation.MaterializedEntries.ShouldBe(beforeMutation.MaterializedEntries);
        afterMutation.StoredEntries.ShouldBe(beforeMutation.StoredEntries + 1);
        afterMutation.Count.ShouldBe(distinctKey ? 2 : 1);
        afterMutation.CacheHits.ShouldBe(beforeMutation.CacheHits + (distinctKey ? 0 : 1));
        afterMutation.CacheMisses.ShouldBe(beforeMutation.CacheMisses + (distinctKey ? 1 : 0));
        afterMutation.ValidationRejections.ShouldBe(
            beforeMutation.ValidationRejections + (distinctKey ? 0 : 1));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void InvalidCurrentInputMatchesDisabledFailure(bool malformedRoot)
    {
        TransientTestFolder folder = _env.CreateFolder();
        TransientTestFile import = _env.CreateFile(folder, "required.props", "<Project />");
        TransientTestFile project = _env.CreateFile(
            folder,
            "invalid.proj",
            "<Project><Import Project=\"required.props\" /><PropertyGroup><Value>old</Value></PropertyGroup></Project>");
        CheckedEvaluation checkedEvaluation = CreateChecked(project.Path);
        _ = Load(checkedEvaluation, submissionId: 1);

        if (malformedRoot)
        {
            WriteWithChangedMetadata(project.Path, "<Project><PropertyGroup>");
        }
        else
        {
            File.Delete(import.Path);
        }
        checkedEvaluation.Parameters.ProjectRootElementCache.DiscardImplicitReferences();

        InvalidProjectFileException freshFailure = Should.Throw<InvalidProjectFileException>(
            () => LoadDisabled(project.Path));
        SetMode(EvaluationCacheMode.SnapshotFileSystem);
        InvalidProjectFileException checkedFailure = Should.Throw<InvalidProjectFileException>(
            () => Load(checkedEvaluation, submissionId: 2));

        checkedFailure.ErrorCode.ShouldBe(freshFailure.ErrorCode);
        checkedEvaluation.Cache.MaterializedEntries.ShouldBe(0);
    }

    [Fact]
    public void RestoreGeneratedImportMatchesDisabledFreshControl()
    {
        TransientTestFolder folder = _env.CreateFolder();
        TransientTestFile generated = _env.CreateFile(
            folder,
            "generated.props",
            "<Project><PropertyGroup><Generated>before</Generated></PropertyGroup></Project>");
        TransientTestFile project = _env.CreateFile(
            folder,
            "restore.proj",
            """
            <Project>
              <Import Project="generated.props" />
              <Target Name="Restore">
                <WriteLinesToFile File="$(MSBuildProjectDirectory)\generated.props" Overwrite="true"
                                  Lines="&lt;Project&gt;&lt;PropertyGroup&gt;&lt;Generated&gt;after&lt;/Generated&gt;&lt;/PropertyGroup&gt;&lt;/Project&gt;" />
                <Touch Files="$(MSBuildProjectDirectory)\generated.props" ForceTouch="true" />
              </Target>
              <Target Name="Build"><Message Importance="High" Text="GENERATED|$(Generated)" /></Target>
            </Project>
            """);
        File.SetLastWriteTimeUtc(generated.Path, DateTime.Now.AddMinutes(-1));
        SetMode(EvaluationCacheMode.SnapshotFileSystem);
        using var manager = new BuildManager();
        var parameters = new BuildParameters
        {
            EvaluationCacheConfiguration = Traits.Instance.EvaluationCache,
        };
        manager.BeginBuild(parameters);
        manager.BuildRequest(new BuildRequestData(
            project.Path,
            new Dictionary<string, string?>(),
            null,
            ["Restore"],
            null,
            BuildRequestDataFlags.ClearCachesAfterBuild)).OverallResult.ShouldBe(BuildResultCode.Success);
        manager.EndBuild();
        manager.BeginBuild(parameters);
        BuildResult candidateResult = manager.BuildRequest(new BuildRequestData(
            project.Path,
            new Dictionary<string, string?>(),
            null,
            ["Build"],
            null,
            BuildRequestDataFlags.ProvideProjectStateAfterBuild));
        manager.EndBuild();
        ProjectInstance candidate = candidateResult.ProjectStateAfterBuild.ShouldNotBeNull();
        var cache = (ProjectInstanceSnapshotCache)((IBuildComponentHost)manager)
            .GetComponent(BuildComponentType.ProjectInstanceSnapshotCache);

        BuildResult freshResult = BuildDisabled(project.Path, "Build");
        ProjectInstance fresh = freshResult.ProjectStateAfterBuild.ShouldNotBeNull();

        candidateResult.OverallResult.ShouldBe(BuildResultCode.Success);
        freshResult.OverallResult.ShouldBe(candidateResult.OverallResult);
        AssertCoreSemanticStateEqual(fresh, candidate);
        candidate.GetPropertyValue("Generated").ShouldBe("after");
        cache.MaterializedEntries.ShouldBe(0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CallerSuppliedStateBypassesCheckedCacheLikeDisabled(bool transferred)
    {
        TransientTestFile project = _env.CreateFile(
            "caller.proj",
            "<Project><PropertyGroup><Value>poisoned disk</Value></PropertyGroup></Project>");
        ProjectInstance checkedCaller = CreateCallerInstance(project.Path, "caller state");
        ProjectInstance disabledCaller = CreateCallerInstance(project.Path, "caller state");
        checkedCaller.TranslateEntireState = transferred;
        disabledCaller.TranslateEntireState = transferred;

        ProjectInstance checkedResult = BuildCaller(
            checkedCaller,
            EvaluationCacheMode.SnapshotFileSystem,
            out ProjectInstanceSnapshotCache? checkedCache);
        ProjectInstance disabledResult = BuildCaller(
            disabledCaller,
            EvaluationCacheMode.Disabled,
            out ProjectInstanceSnapshotCache? disabledCache);

        checkedResult.GetPropertyValue("Value").ShouldBe("caller state");
        new ProjectInstanceComparer().Equals(disabledResult, checkedResult).ShouldBeTrue();
        checkedCache.ShouldNotBeNull().Count.ShouldBe(0);
        checkedCache.CacheHits.ShouldBe(0);
        checkedCache.CacheMisses.ShouldBe(0);
        checkedCache.MaterializedEntries.ShouldBe(0);
        disabledCache.ShouldBeNull();
    }

    [Fact]
    public void PartialEvaluationHasSameRejectedRequestShapeWhenCheckedAndDisabled()
    {
        ProjectInstance CreatePartial()
        {
            ProjectCollection collection = _env.CreateProjectCollection().Collection;
            ProjectRootElement root = ProjectRootElement.Create(collection);
            root.AddProperty("Value", "partial");
            return ProjectInstance.FromProjectRootElement(
                root,
                new ProjectOptions
                {
                    EvaluationStage = ProjectEvaluationStage.Properties,
                    ProjectCollection = collection,
                });
        }

        SetMode(EvaluationCacheMode.SnapshotFileSystem);
        InvalidOperationException checkedFailure = Should.Throw<InvalidOperationException>(
            () => new BuildRequestData(CreatePartial(), ["Build"]));
        SetMode(EvaluationCacheMode.Disabled);
        InvalidOperationException disabledFailure = Should.Throw<InvalidOperationException>(
            () => new BuildRequestData(CreatePartial(), ["Build"]));

        checkedFailure.Message.ShouldBe(disabledFailure.Message);
    }

    [Fact]
    public void ProfileEvaluationUsesSeparateCheckedEntryAndMatchesDisabled()
    {
        SetMode(EvaluationCacheMode.SnapshotFileSystem);
        TransientTestFile project = _env.CreateFile(
            "profile.proj",
            "<Project><PropertyGroup><Value>profiled</Value></PropertyGroup><ItemGroup><Result Include=\"$(Value)\" /></ItemGroup><Target Name=\"Build\" Returns=\"@(Result)\" /></Project>");
        var logger = new MockLogger(verbosity: LoggerVerbosity.Diagnostic);
        using var manager = new BuildManager();
        var request = new BuildRequestData(
            project.Path,
            new Dictionary<string, string?>(),
            null,
            ["Build"],
            null,
            BuildRequestDataFlags.ProvideProjectStateAfterBuild);
        var baselineParameters = new BuildParameters
        {
            EvaluationCacheConfiguration = Traits.Instance.EvaluationCache,
            Loggers = [logger],
        };
        var profiledParameters = new BuildParameters
        {
            EvaluationCacheConfiguration = Traits.Instance.EvaluationCache,
            ProjectLoadSettings = ProjectLoadSettings.ProfileEvaluation,
            Loggers = [logger],
        };

        manager.Build(baselineParameters, request).OverallResult.ShouldBe(BuildResultCode.Success);
        BuildResult profiledFreshResult = manager.Build(profiledParameters, request);
        int evaluationsAfterProfiledFresh = logger.EvaluationFinishedEvents.Count;
        BuildResult cachedHitResult = manager.Build(profiledParameters, request);
        var cache = (ProjectInstanceSnapshotCache)((IBuildComponentHost)manager)
            .GetComponent(BuildComponentType.ProjectInstanceSnapshotCache);
        BuildResult disabledResult = BuildDisabled(
            project.Path,
            "Build",
            loadSettings: ProjectLoadSettings.ProfileEvaluation);

        profiledFreshResult.OverallResult.ShouldBe(BuildResultCode.Success);
        cachedHitResult.OverallResult.ShouldBe(BuildResultCode.Success);
        cachedHitResult.OverallResult.ShouldBe(disabledResult.OverallResult);
        cachedHitResult.ResultsByTarget["Build"].ResultCode.ShouldBe(
            disabledResult.ResultsByTarget["Build"].ResultCode);
        cachedHitResult.ResultsByTarget["Build"].Items.ShouldHaveSingleItem().ItemSpec.ShouldBe("profiled");
        cachedHitResult.ResultsByTarget["Build"].Items.Select(item => item.ItemSpec).ShouldBe(
            disabledResult.ResultsByTarget["Build"].Items.Select(item => item.ItemSpec));
        AssertCoreSemanticStateEqual(
            disabledResult.ProjectStateAfterBuild.ShouldNotBeNull(),
            cachedHitResult.ProjectStateAfterBuild.ShouldNotBeNull());
        evaluationsAfterProfiledFresh.ShouldBe(2);
        logger.EvaluationFinishedEvents.Last().ProfilerResult.ShouldNotBeNull()
            .ProfiledLocations.ShouldNotBeEmpty();
        logger.EvaluationFinishedEvents.Count.ShouldBe(evaluationsAfterProfiledFresh);
        cache.CacheMisses.ShouldBe(2);
        cache.GetStatistics().ValidationAccepted.ShouldBe(1);
        cache.MaterializedEntries.ShouldBe(1);
    }

    [Fact]
    public void UnsafeNonCacheableEntryIsRejectedWhenManagerSwitchesToCheckedMode()
    {
        TransientTestFile project = _env.CreateFile(
            "mode-boundary.proj",
            "<Project><PropertyGroup><Value>$([System.DateTime]::UtcNow.Ticks)</Value></PropertyGroup><Target Name=\"Build\" /></Project>");
        using var manager = new BuildManager();

        SetMode(EvaluationCacheMode.SnapshotUnsafe);
        BuildParameters unsafeParameters = new() { EvaluationCacheConfiguration = Traits.Instance.EvaluationCache };
        var request = new BuildRequestData(
            project.Path,
            new Dictionary<string, string?>(),
            null,
            ["Build"],
            null,
            BuildRequestDataFlags.ProvideProjectStateAfterBuild);
        manager.BeginBuild(unsafeParameters);
        BuildResult unsafeResult = manager.BuildRequest(request);
        unsafeResult.OverallResult.ShouldBe(BuildResultCode.Success);
        manager.EndBuild();
        var cache = (ProjectInstanceSnapshotCache)((IBuildComponentHost)manager)
            .GetComponent(BuildComponentType.ProjectInstanceSnapshotCache);
        cache.Count.ShouldBe(1);

        SetMode(EvaluationCacheMode.SnapshotFileSystem);
        BuildParameters checkedParameters = new() { EvaluationCacheConfiguration = Traits.Instance.EvaluationCache };
        manager.BeginBuild(checkedParameters);
        BuildResult checkedResult = manager.BuildRequest(request);
        checkedResult.OverallResult.ShouldBe(BuildResultCode.Success);
        manager.EndBuild();

        unsafeResult.ProjectStateAfterBuild!.GetPropertyValue("Value")
            .ShouldNotBe(checkedResult.ProjectStateAfterBuild!.GetPropertyValue("Value"));
        cache.MaterializedEntries.ShouldBe(0);
    }

    [Fact]
    public void UnconfiguredModeCreatesNoRecorderCacheLookupStorageOrStatus()
    {
        Environment.SetEnvironmentVariable(ModeVariable, null);
        Environment.SetEnvironmentVariable(LegacyRecordVariable, null);
        Environment.SetEnvironmentVariable(LegacySnapshotVariable, null);
        Traits.UpdateFromEnvironment();
        TransientTestFile project = _env.CreateFile(
            "default-off.proj",
            "<Project><Target Name=\"Build\" /></Project>");
        var logger = new MockLogger(verbosity: LoggerVerbosity.Diagnostic);
        using var manager = new BuildManager();
        var parameters = new BuildParameters { Loggers = [logger] };
        var request = new BuildRequestData(
            project.Path,
            new Dictionary<string, string?>(),
            null,
            ["Build"],
            null,
            BuildRequestDataFlags.ProvideProjectStateAfterBuild);

        BuildResult result = manager.Build(parameters, request);

        result.OverallResult.ShouldBe(BuildResultCode.Success);
        result.ProjectStateAfterBuild.ShouldNotBeNull();
        result.ProjectStateAfterBuild.EvaluationInputs.ShouldBeNull();
        logger.FullLog.ShouldNotContain("EvaluationCacheExperimentStatus|");
        parameters.ProjectInstanceSnapshotCache.ShouldBeNull();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BuildManagerReuseHitsWithoutSyntheticEvaluationEventsAndResetForcesFresh(bool multiThreaded)
    {
        SetMode(EvaluationCacheMode.SnapshotFileSystem);
        TransientTestFile project = _env.CreateFile(
            "manager-reuse.proj",
            "<Project><PropertyGroup><Value>stable</Value></PropertyGroup><Target Name=\"Build\" /></Project>");
        var logger = new MockLogger(verbosity: LoggerVerbosity.Diagnostic);
        using var manager = new BuildManager();
        var parameters = new BuildParameters
        {
            EvaluationCacheConfiguration = Traits.Instance.EvaluationCache,
            Loggers = [logger],
            MultiThreaded = multiThreaded,
        };
        var request = new BuildRequestData(
            project.Path,
            new Dictionary<string, string?>(),
            null,
            ["Build"],
            null,
            BuildRequestDataFlags.ProvideProjectStateAfterBuild);

        manager.Build(parameters, request).OverallResult.ShouldBe(BuildResultCode.Success);
        int evaluationsAfterFirstBuild = logger.EvaluationStartedEvents.Count;
        manager.Build(parameters, request).OverallResult.ShouldBe(BuildResultCode.Success);
        var cache = (ProjectInstanceSnapshotCache)((IBuildComponentHost)manager)
            .GetComponent(BuildComponentType.ProjectInstanceSnapshotCache);

        evaluationsAfterFirstBuild.ShouldBe(1);
        logger.EvaluationStartedEvents.Count.ShouldBe(evaluationsAfterFirstBuild);
        logger.EvaluationFinishedEvents.Count.ShouldBe(evaluationsAfterFirstBuild);
        cache.MaterializedEntries.ShouldBe(1);

        manager.ResetCaches();
        manager.Build(parameters, request).OverallResult.ShouldBe(BuildResultCode.Success);

        logger.EvaluationStartedEvents.Count.ShouldBe(evaluationsAfterFirstBuild + 1);
        logger.EvaluationFinishedEvents.Count.ShouldBe(evaluationsAfterFirstBuild + 1);
        cache.MaterializedEntries.ShouldBe(1);
    }

    [Fact]
    public async Task ConcurrentCheckedRequestsMatchFreshControl()
    {
        TransientTestFile project = _env.CreateFile(
            "concurrent.proj",
            "<Project><PropertyGroup><Value>stable</Value></PropertyGroup><ItemGroup><I Include=\"one\"><M>metadata</M></I></ItemGroup></Project>");
        ProjectInstance fresh = LoadDisabled(project.Path);
        SetMode(EvaluationCacheMode.SnapshotFileSystem);
        CheckedEvaluation checkedEvaluation = CreateChecked(project.Path);
        _ = Load(checkedEvaluation, submissionId: 1);

        Task<ProjectInstance>[] requests = Enumerable.Range(2, 8)
            .Select(id => Task.Run(() => Load(checkedEvaluation, id)))
            .ToArray();
        ProjectInstance[] results = await Task.WhenAll(requests);

        foreach (ProjectInstance result in results)
        {
            AssertCoreSemanticStateEqual(fresh, result);
        }
        checkedEvaluation.Cache.MaterializedEntries.ShouldBe(8);
    }

    [WindowsOnlyFact]
    [SupportedOSPlatform("windows")]
    public void RegistryDependentEvaluationIsFreshInCheckedMode()
    {
        string subKey = $@"Software\MSBuild_SnapshotAcceptance_{Guid.NewGuid():N}";
        using RegistryKey key = Registry.CurrentUser.CreateSubKey(subKey);
        try
        {
            key.SetValue("Value", "first");
            TransientTestFile project = _env.CreateFile(
                "registry.proj",
                $@"<Project><PropertyGroup><Value>$(Registry:HKEY_CURRENT_USER\{subKey}@Value)</Value></PropertyGroup></Project>");
            CheckedEvaluation checkedEvaluation = CreateChecked(project.Path);
            ProjectInstance first = Load(checkedEvaluation, submissionId: 1);
            key.SetValue("Value", "second");
            ProjectInstance second = Load(checkedEvaluation, submissionId: 2);
            ProjectInstance fresh = LoadDisabled(project.Path);

            first.GetPropertyValue("Value").ShouldBe("first");
            second.GetPropertyValue("Value").ShouldBe("second");
            AssertCoreSemanticStateEqual(fresh, second);
            checkedEvaluation.Cache.Count.ShouldBe(0);
            checkedEvaluation.Cache.MaterializedEntries.ShouldBe(0);
        }
        finally
        {
            Registry.CurrentUser.DeleteSubKeyTree(subKey, throwOnMissingSubKey: false);
        }
    }

    [Fact]
    public void LocalDefaultSdkResolverAllowsOrdinaryCheckedSdkHit()
    {
        string? originalSdksPath = Environment.GetEnvironmentVariable("MSBuildSDKsPath");
        try
        {
            TransientTestFolder sdkRoot = _env.CreateFolder();
            TransientTestFolder sdk = _env.CreateFolder(
                Path.Combine(sdkRoot.Path, "AcceptanceSdk", "Sdk"),
                createFolder: true);
            _env.CreateFile(
                sdk,
                "Sdk.props",
                "<Project><PropertyGroup><FromAcceptanceSdk>local sdk</FromAcceptanceSdk></PropertyGroup></Project>");
            _env.CreateFile(sdk, "Sdk.targets", "<Project />");
            _env.SetEnvironmentVariable("MSBuildSDKsPath", sdkRoot.Path);
            TransientTestFile project = _env.CreateFile(
                "real-sdk.proj",
                "<Project Sdk=\"AcceptanceSdk\"><Target Name=\"Build\" /></Project>");
            SetMode(EvaluationCacheMode.SnapshotFileSystem);
            using var manager = new BuildManager();

            BuildParameters firstParameters = new() { EvaluationCacheConfiguration = Traits.Instance.EvaluationCache };
            BuildResult first = manager.Build(
                firstParameters,
                new BuildRequestData(
                    project.Path,
                    new Dictionary<string, string?>(),
                    null,
                    ["Build"],
                    null,
                    BuildRequestDataFlags.ProvideProjectStateAfterBuild));
            BuildParameters secondParameters = new() { EvaluationCacheConfiguration = Traits.Instance.EvaluationCache };
            BuildResult second = manager.Build(
                secondParameters,
                new BuildRequestData(
                    project.Path,
                    new Dictionary<string, string?>(),
                    null,
                    ["Build"],
                    null,
                    BuildRequestDataFlags.ProvideProjectStateAfterBuild));
            var cache = (ProjectInstanceSnapshotCache)((IBuildComponentHost)manager)
                .GetComponent(BuildComponentType.ProjectInstanceSnapshotCache);

            first.ProjectStateAfterBuild!.GetPropertyValue("FromAcceptanceSdk").ShouldBe("local sdk");
            second.ProjectStateAfterBuild!.GetPropertyValue("FromAcceptanceSdk").ShouldBe("local sdk");
            cache.GetStatistics().ValidationAccepted.ShouldBe(1);
            cache.MaterializedEntries.ShouldBe(1);
        }
        finally
        {
            Environment.SetEnvironmentVariable("MSBuildSDKsPath", originalSdksPath);
        }
    }

    private CheckedEvaluation CreateChecked(
        string projectPath,
        IDictionary<string, string?>? globalProperties = null)
    {
        SetMode(EvaluationCacheMode.SnapshotFileSystem);
        EvaluationCacheConfiguration mode = Traits.Instance.EvaluationCache;
        var cache = new ProjectInstanceSnapshotCache();
        cache.ConfigureValidator(mode.ValidationPolicy);
        var parameters = new BuildParameters
        {
            EvaluationCacheConfiguration = mode,
            ProjectInstanceSnapshotCache = cache,
        };
        return new CheckedEvaluation(
            projectPath,
            cache,
            parameters,
            new MockHost(parameters),
            globalProperties ?? new Dictionary<string, string?>());
    }

    private static CheckedEvaluation RefreshCheckedEnvironment(CheckedEvaluation source)
    {
        SetMode(EvaluationCacheMode.SnapshotFileSystem);
        var parameters = new BuildParameters
        {
            EvaluationCacheConfiguration = Traits.Instance.EvaluationCache,
            ProjectInstanceSnapshotCache = source.Cache,
            ProjectLoadSettings = source.Parameters.ProjectLoadSettings,
        };
        return source with
        {
            Parameters = parameters,
            Host = new MockHost(parameters),
        };
    }

    private static ProjectInstance Load(CheckedEvaluation evaluation, int submissionId)
    {
        SetMode(EvaluationCacheMode.SnapshotFileSystem);
        BuildRequestConfiguration configuration = CreateConfiguration(
            evaluation.ProjectPath,
            evaluation.Parameters,
            evaluation.GlobalProperties);
        configuration.LoadProjectIntoConfiguration(
            evaluation.Host,
            BuildRequestDataFlags.None,
            submissionId,
            nodeId: 1);
        return configuration.Project;
    }

    private static ProjectInstance LoadDisabled(
        string projectPath,
        IDictionary<string, string?>? globalProperties = null,
        ProjectLoadSettings loadSettings = ProjectLoadSettings.Default,
        IDictionary<string, string?>? environmentOverrides = null)
    {
        SetMode(EvaluationCacheMode.Disabled);
        var parameters = new BuildParameters
        {
            EvaluationCacheConfiguration = Traits.Instance.EvaluationCache,
            ProjectLoadSettings = loadSettings,
        };
        if (environmentOverrides is not null)
        {
            foreach (KeyValuePair<string, string?> environmentOverride in environmentOverrides)
            {
                parameters.EnvironmentPropertiesInternal[environmentOverride.Key] =
                    ProjectPropertyInstance.Create(
                        environmentOverride.Key,
                        environmentOverride.Value ?? string.Empty);
            }
        }
        parameters.ProjectInstanceSnapshotCache.ShouldBeNull();
        var host = new MockHost(parameters);
        BuildRequestConfiguration configuration = CreateConfiguration(
            projectPath,
            parameters,
            globalProperties);
        configuration.LoadProjectIntoConfiguration(
            host,
            BuildRequestDataFlags.None,
            submissionId: 1000,
            nodeId: 1);
        configuration.Project.GetPropertyValue(ModeVariable)
            .ShouldBe(nameof(EvaluationCacheMode.Disabled));
        configuration.Project.EvaluationInputs.ShouldBeNull();
        return configuration.Project;
    }

    private static BuildResult BuildDisabled(
        string projectPath,
        string target,
        IDictionary<string, string?>? globalProperties = null,
        ProjectLoadSettings loadSettings = ProjectLoadSettings.Default)
    {
        SetMode(EvaluationCacheMode.Disabled);
        var parameters = new BuildParameters
        {
            EvaluationCacheConfiguration = Traits.Instance.EvaluationCache,
            ProjectLoadSettings = loadSettings,
        };
        parameters.ProjectInstanceSnapshotCache.ShouldBeNull();
        using var manager = new BuildManager();
        BuildResult result = manager.Build(
            parameters,
            new BuildRequestData(
                projectPath,
                globalProperties ?? new Dictionary<string, string?>(),
                null,
                [target],
                null,
                BuildRequestDataFlags.ProvideProjectStateAfterBuild));
        result.ProjectStateAfterBuild.ShouldNotBeNull()
            .GetPropertyValue(ModeVariable).ShouldBe(nameof(EvaluationCacheMode.Disabled));
        result.ProjectStateAfterBuild.EvaluationInputs.ShouldBeNull();
        return result;
    }

    private static void AssertCoreSemanticStateEqual(
        ProjectInstance expected,
        ProjectInstance actual)
    {
        expected.GetPropertyValue(ModeVariable).ShouldBe(nameof(EvaluationCacheMode.Disabled));
        expected.EvaluationInputs.ShouldBeNull();
        new ProjectInstanceComparer(
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ModeVariable })
            .Equals(expected, actual)
            .ShouldBeTrue();
    }

    private static ProjectInstance BuildCaller(
        ProjectInstance instance,
        EvaluationCacheMode mode,
        out ProjectInstanceSnapshotCache? cache)
    {
        SetMode(mode);
        var parameters = new BuildParameters
        {
            EvaluationCacheConfiguration = Traits.Instance.EvaluationCache,
            ProjectInstanceSnapshotCache = mode == EvaluationCacheMode.SnapshotFileSystem
                ? new ProjectInstanceSnapshotCache()
                : null,
        };
        parameters.ProjectInstanceSnapshotCache?.ConfigureValidator(
            Traits.Instance.EvaluationCache.ValidationPolicy);
        cache = parameters.ProjectInstanceSnapshotCache;
        using var manager = new BuildManager();
        BuildResult result = manager.Build(
            parameters,
            new BuildRequestData(
                instance,
                ["Build"],
                hostServices: null,
                BuildRequestDataFlags.ProvideProjectStateAfterBuild));
        result.OverallResult.ShouldBe(BuildResultCode.Success);
        return result.ProjectStateAfterBuild.ShouldNotBeNull();
    }

    private static ProjectInstance CreateCallerInstance(string path, string value)
    {
        using var collection = new ProjectCollection();
        ProjectRootElement root = ProjectRootElement.Create(collection);
        root.FullPath = path;
        root.AddProperty("Value", value);
        root.AddTarget("Build");
        return new ProjectInstance(root);
    }

    private static BuildRequestConfiguration CreateConfiguration(
        string projectPath,
        BuildParameters parameters,
        IDictionary<string, string?>? globalProperties)
    {
        var request = new BuildRequestData(
            projectPath,
            globalProperties ?? new Dictionary<string, string?>(),
            toolsVersion: null,
            [],
            hostServices: null,
            BuildRequestDataFlags.None);
        return new BuildRequestConfiguration(request, parameters.DefaultToolsVersion);
    }

    private static void WriteWithChangedMetadata(string path, string contents)
    {
        DateTime timestamp = File.GetLastWriteTimeUtc(path);
        File.WriteAllText(path, contents);
        File.SetLastWriteTimeUtc(path, timestamp.AddSeconds(10));
    }

    private static void TouchDirectory(string path)
    {
        DateTime timestamp = Directory.GetLastWriteTimeUtc(path);
        Directory.SetLastWriteTimeUtc(path, timestamp.AddSeconds(10));
    }

    private static void SetMode(EvaluationCacheMode mode)
    {
        Environment.SetEnvironmentVariable(ModeVariable, mode.ToString());
        Traits.UpdateFromEnvironment();
    }

    public enum InputMutation
    {
        Root,
        Import,
        GlobAdd,
        GlobDelete,
        GlobRename,
        MissingAppears,
        DirectEnvironment,
        ImportedEnvironment,
        GlobalProperty,
    }

    private sealed record CheckedEvaluation(
        string ProjectPath,
        ProjectInstanceSnapshotCache Cache,
        BuildParameters Parameters,
        MockHost Host,
        IDictionary<string, string?> GlobalProperties);
}
