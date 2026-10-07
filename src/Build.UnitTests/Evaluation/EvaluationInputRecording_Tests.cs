// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Microsoft.Build.Construction;
using Microsoft.Build.Definition;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Evaluation.Context;
using Microsoft.Build.Execution;
using Microsoft.Build.FileSystem;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Shared.FileSystem;
using Microsoft.Build.Unittest;
using Microsoft.Win32;
using Shouldly;
using Xunit;
using InvalidProjectFileException = Microsoft.Build.Exceptions.InvalidProjectFileException;
using SdkResult = Microsoft.Build.BackEnd.SdkResolution.SdkResult;

namespace Microsoft.Build.UnitTests.Evaluation;

public sealed class EvaluationInputRecording_Tests : IDisposable
{
    private const string EnableVariable = "MSBUILDRECORDEVALUATIONINPUTS";

    private readonly ITestOutputHelper _output;
    private readonly TestEnvironment _env;
    private readonly TransientTestFolder _folder;

    public EvaluationInputRecording_Tests(ITestOutputHelper output)
    {
        _output = output;
        _env = TestEnvironment.Create(output);
        _folder = _env.CreateFolder(createFolder: true);
        SetRecording(enabled: true);
    }

    public void Dispose()
    {
        _env.Dispose();
        Traits.UpdateFromEnvironment();
    }

    [Fact]
    public void DisabledRecordingProducesNoInputs()
    {
        SetRecording(enabled: false);
        string project = CreateProject("""
            <Project>
              <PropertyGroup>
                <A>1</A>
              </PropertyGroup>
            </Project>
            """);

        ProjectInstance instance = ProjectInstance.FromFile(project, CreateOptions());

        instance.EvaluationInputs.ShouldBeNull();
        instance.GetPropertyValue("A").ShouldBe("1");
    }

    [Fact]
    public void RecordsRootProjectAsExistingFile()
    {
        string project = CreateProject("""
            <Project>
              <PropertyGroup>
                <Value>fresh</Value>
              </PropertyGroup>
            </Project>
            """);
        ProjectCollection collection = _env.CreateProjectCollection().Collection;
        ProjectRootElement cachedRoot = ProjectRootElement.Open(project, collection);
        var fileInfo = new FileInfo(project);

        cachedRoot.LastWriteTimeWhenReadUtc.ShouldBe(fileInfo.LastWriteTimeUtc);
        cachedRoot.FileLengthWhenRead.ShouldBe(fileInfo.Length);

        EvaluationInputs inputs = Evaluate(project, new ProjectOptions { ProjectCollection = collection });

        inputs.NonCacheable.ShouldBe(NonCacheableReason.None);
        inputs.Key.ProjectFullPath.ShouldBe(project);
        inputs.Key.Culture.ShouldBe(CultureInfo.CurrentCulture.Name);
        inputs.Key.ToolsPath.ShouldNotBeNullOrEmpty();
        FileDependency rootDependency = inputs.Files[project];
        rootDependency.Kind.ShouldBe(PathKind.File);
        rootDependency.LastWriteTimeUtc.ShouldBe(File.GetLastWriteTimeUtc(project));
        rootDependency.Length.ShouldBe(new FileInfo(project).Length);
        GC.KeepAlive(cachedRoot);
    }

    [Theory]
    [InlineData(nameof(EvaluationCacheMode.Record), true, false)]
    [InlineData(nameof(EvaluationCacheMode.Record), true, true)]
    [InlineData(nameof(EvaluationCacheMode.SnapshotUnsafe), false, false)]
    [InlineData(nameof(EvaluationCacheMode.SnapshotUnsafe), false, true)]
    [InlineData(nameof(EvaluationCacheMode.SnapshotFileSystem), false, false)]
    [InlineData(nameof(EvaluationCacheMode.SnapshotFileSystem), false, true)]
    [InlineData(nameof(EvaluationCacheMode.Disabled), false, false)]
    [InlineData(nameof(EvaluationCacheMode.Disabled), false, true)]
    public void RestoreRecordingRespectsModeAcrossEvaluationApis(string modeName, bool recordsInputs, bool useProject)
    {
        _env.SetEnvironmentVariable(EvaluationCacheConfiguration.ModeEnvironmentVariable, modeName);
        Traits.UpdateFromEnvironment();
        string path = CreateProject("<Project />");
        ProjectCollection collection = _env.CreateProjectCollection().Collection;
        var globals = new Dictionary<string, string>
        {
            [MSBuildConstants.MSBuildRestoreSessionId] = "restore-session",
        };

        EvaluationInputs? inputs = useProject
            ? new Project(path, globals, null, collection).EvaluationInputs
            : ProjectInstance.FromFile(path, new ProjectOptions
            {
                ProjectCollection = collection,
                GlobalProperties = globals,
            }).EvaluationInputs;

        if (recordsInputs)
        {
            inputs.ShouldNotBeNull().Key.GlobalProperties.ShouldContain("MSBUILDRESTORESESSIONID=restore-session\0");
        }
        else
        {
            inputs.ShouldBeNull();
        }
    }

    [Fact]
    public void EditingRootProjectInvalidates()
    {
        string project = CreateProject("<Project />");
        EvaluationInputs inputs = Evaluate(project);
        IsCurrent(inputs, out _).ShouldBeTrue();

        Touch(project, "<Project><PropertyGroup /></Project>");

        IsCurrent(inputs, out string? reason).ShouldBeFalse();
        reason.ShouldNotBeNull().ShouldContain(project);
    }

    [Fact]
    public void DeletingRecordedFileInvalidates()
    {
        string project = CreateProject("<Project />");
        EvaluationInputs inputs = Evaluate(project);
        IsCurrent(inputs, out _).ShouldBeTrue();

        File.Delete(project);

        IsCurrent(inputs, out string? reason).ShouldBeFalse();
        reason.ShouldBe(project);
    }

    [Fact]
    public void ChangedLengthWithPreservedTimestampInvalidates()
    {
        string project = CreateProject("<Project />");
        EvaluationInputs inputs = Evaluate(project);
        DateTime timestamp = inputs.Files[project].LastWriteTimeUtc;

        File.WriteAllText(project, "<Project><PropertyGroup /></Project>");
        File.SetLastWriteTimeUtc(project, timestamp);
        File.GetLastWriteTimeUtc(project).ShouldBe(timestamp);
        new FileInfo(project).Length.ShouldNotBe(inputs.Files[project].Length);

        IsCurrent(inputs, out string? reason).ShouldBeFalse();
        reason.ShouldBe(project);
    }

    [Fact]
    public void SameLengthAndTimestampContentChangeIsPrototypeBlindSpot()
    {
        string project = CreateProject(
            "<Project><PropertyGroup><Value>first</Value></PropertyGroup></Project>");
        EvaluationInputs inputs = Evaluate(project);
        DateTime timestamp = inputs.Files[project].LastWriteTimeUtc;

        File.WriteAllText(
            project,
            "<Project><PropertyGroup><Value>other</Value></PropertyGroup></Project>");
        File.SetLastWriteTimeUtc(project, timestamp);

        new FileInfo(project).Length.ShouldBe(inputs.Files[project].Length);
        IsCurrent(inputs, out _).ShouldBeTrue();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FileDirectoryKindReplacementInvalidates(bool initiallyDirectory)
    {
        string candidate = Path.Combine(_folder.Path, "candidate");
        if (initiallyDirectory)
        {
            Directory.CreateDirectory(candidate);
        }
        else
        {
            File.WriteAllText(candidate, string.Empty);
        }

        string project = CreateProject("""
            <Project>
              <PropertyGroup>
                <Found Condition="Exists('candidate')">true</Found>
              </PropertyGroup>
            </Project>
            """);
        EvaluationInputs inputs = Evaluate(project);
        inputs.Files[candidate].Kind.ShouldBe(initiallyDirectory ? PathKind.Directory : PathKind.File);
        DateTime timestamp = inputs.Files[candidate].LastWriteTimeUtc;
        IsCurrent(inputs, out _).ShouldBeTrue();

        if (initiallyDirectory)
        {
            Directory.Delete(candidate);
            File.WriteAllText(candidate, string.Empty);
            File.SetLastWriteTimeUtc(candidate, timestamp);
        }
        else
        {
            File.Delete(candidate);
            Directory.CreateDirectory(candidate);
            Directory.SetLastWriteTimeUtc(candidate, timestamp);
        }

        File.GetLastWriteTimeUtc(candidate).ShouldBe(timestamp);
        IsCurrent(inputs, out string? reason).ShouldBeFalse();
        reason.ShouldBe(candidate);
    }

    [Fact]
    public void ProjectCreatedInstancePreservesRecordedInputs()
    {
        ProjectCollection collection = _env.CreateProjectCollection().Collection;
        string projectPath = CreateProject("<Project />");
        var project = new Project(projectPath, globalProperties: null, toolsVersion: null, collection);

        ProjectInstance instance = project.CreateProjectInstance();

        instance.EvaluationInputs.ShouldBeSameAs(project.EvaluationInputs);
    }

    [Fact]
    public void ProjectInstanceConstructorPreservesRecordedInputs()
    {
        ProjectCollection collection = _env.CreateProjectCollection().Collection;
        string projectPath = CreateProject("<Project />");
        var project = new Project(
            projectPath,
            globalProperties: null,
            toolsVersion: null,
            collection,
            ProjectLoadSettings.RecordDuplicateButNotCircularImports);

        var instance = new ProjectInstance(project, ProjectInstanceSettings.Immutable);

        instance.EvaluationInputs.ShouldBeSameAs(project.EvaluationInputs);
    }

    [Fact]
    public void IgnoredMissingImportIsRecordedAsMissing()
    {
        string project = CreateProject("""
            <Project>
              <Import Project="missing.props" />
            </Project>
            """);
        ProjectOptions options = CreateOptions();
        options.LoadSettings = ProjectLoadSettings.IgnoreMissingImports;

        EvaluationInputs inputs = Evaluate(project, options);

        inputs.Files[Path.Combine(_folder.Path, "missing.props")].Kind.ShouldBe(PathKind.Missing);
    }

    [Fact]
    public void CreatingProbedMissingFileInvalidates()
    {
        string project = CreateProject("""
            <Project>
              <Import Project="optional.props" Condition="Exists('optional.props')" />
            </Project>
            """);
        string optional = Path.Combine(_folder.Path, "optional.props");
        EvaluationInputs inputs = Evaluate(project);
        inputs.Files[optional].Kind.ShouldBe(PathKind.Missing);
        IsCurrent(inputs, out _).ShouldBeTrue();

        File.WriteAllText(optional, "<Project />");

        IsCurrent(inputs, out string? reason).ShouldBeFalse();
        reason.ShouldBe(optional);
    }

    [Fact]
    public void GlobMembershipChangeInvalidates()
    {
        _env.CreateFile(_folder, "a.cs", string.Empty);
        string project = CreateProject("""
            <Project>
              <ItemGroup>
                <Compile Include="**/*.cs" />
              </ItemGroup>
            </Project>
            """);
        EvaluationInputs inputs = Evaluate(project);
        inputs.Files[_folder.Path].Kind.ShouldBe(PathKind.Directory);
        IsCurrent(inputs, out _).ShouldBeTrue();

        File.WriteAllText(Path.Combine(_env.CreateFolder(createFolder: true).Path, "outside.cs"), string.Empty);
        IsCurrent(inputs, out _).ShouldBeTrue();

        AddFile(_folder.Path, "b.cs");

        IsCurrent(inputs, out string? reason).ShouldBeFalse();
        reason.ShouldBe(_folder.Path);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExistenceProbeIgnoresMetadataChangesButStillRequiresThePath(bool directory)
    {
        string candidate = Path.Combine(_folder.Path, "candidate");
        if (directory)
        {
            Directory.CreateDirectory(candidate);
        }
        else
        {
            File.WriteAllText(candidate, "before");
        }

        string project = CreateProject("""
            <Project>
              <PropertyGroup>
                <Present Condition="Exists('candidate')">true</Present>
              </PropertyGroup>
            </Project>
            """);
        EvaluationInputs inputs = Evaluate(project);
        inputs.Files[candidate].RequiresMetadata.ShouldBeFalse();

        if (directory)
        {
            AddFile(candidate, "output.txt");
        }
        else
        {
            Touch(candidate, "a different file length");
        }

        IsCurrent(inputs, out string? reason).ShouldBeTrue(reason);
        ProjectInstance.FromFile(project, CreateOptions()).GetPropertyValue("Present").ShouldBe("true");

        if (directory)
        {
            Directory.Delete(candidate, recursive: true);
        }
        else
        {
            File.Delete(candidate);
        }

        IsCurrent(inputs, out reason).ShouldBeFalse();
        reason.ShouldBe(candidate);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void MetadataObservationIsNeverDowngradedToAProbe(bool directory, bool metadataFirst)
    {
        string candidate = Path.Combine(_folder.Path, "candidate");
        if (directory)
        {
            Directory.CreateDirectory(candidate);
        }
        else
        {
            File.WriteAllText(candidate, "before");
        }

        var recorder = new EvaluationInputRecorder();
        if (metadataFirst)
        {
            recorder.RecordPath(candidate);
        }
        recorder.RecordProbe(candidate, directory ? ProbeKind.Directory : ProbeKind.File, exists: true);
        recorder.RecordPath(candidate);
        recorder.RecordProbe(candidate, ProbeKind.FileOrDirectory, exists: true);
        EvaluationInputs inputs = recorder.Freeze(Evaluate(CreateProject("<Project />")).Key);
        inputs.Files[candidate].RequiresMetadata.ShouldBeTrue();
        IsCurrent(inputs, out _).ShouldBeTrue();

        if (directory)
        {
            AddFile(candidate, "new.txt");
        }
        else
        {
            Touch(candidate, "longer content");
        }

        IsCurrent(inputs, out string? reason).ShouldBeFalse();
        reason.ShouldBe(candidate);
    }

    [Fact]
    public void MetadataPromotionPreservesTheFirstObservation()
    {
        string file = _env.CreateFile(_folder, "input.txt", "first").Path;
        var recorder = new EvaluationInputRecorder();
        recorder.RecordProbe(file, ProbeKind.File, exists: true);
        Touch(file, "changed after the probe");
        recorder.RecordPath(file);
        EvaluationInputs inputs = recorder.Freeze(Evaluate(CreateProject("<Project />")).Key);

        inputs.Files[file].RequiresMetadata.ShouldBeTrue();
        inputs.Files[file].Length.ShouldBe(5);
        IsCurrent(inputs, out string? reason).ShouldBeFalse();
        reason.ShouldBe(file);
    }

    [Fact]
    public void DirectoryPropertyFunctionReadPromotesAnExistenceProbe()
    {
        string directory = Path.Combine(_folder.Path, "generated");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "before.txt"), string.Empty);
        string project = CreateProject("""
            <Project>
              <PropertyGroup>
                <Exists>$([System.IO.Directory]::Exists('$(MSBuildProjectDirectory)/generated'))</Exists>
                <Read>$([System.IO.Directory]::GetFiles('$(MSBuildProjectDirectory)/generated'))</Read>
              </PropertyGroup>
            </Project>
            """);
        EvaluationInputs inputs = Evaluate(project);
        inputs.IsCacheable.ShouldBeTrue(inputs.NonCacheableDetail);
        inputs.Files[directory].RequiresMetadata.ShouldBeTrue();
        IsCurrent(inputs, out _).ShouldBeTrue();

        AddFile(directory, "after.txt");

        IsCurrent(inputs, out string? reason).ShouldBeFalse();
        reason.ShouldBe(directory);
    }

    [Theory]
    [InlineData("GetLastWriteTimeUtc")]
    [InlineData("EnumerateFiles")]
    [InlineData("EnumerateDirectories")]
    [InlineData("EnumerateFileSystemEntries")]
    public void FileSystemDirectoryReadPromotesAnExistenceProbe(string operation)
    {
        string directory = Path.Combine(_folder.Path, "generated");
        Directory.CreateDirectory(directory);
        var recorder = new EvaluationInputRecorder();
        var fileSystem = new RecordingFileSystem(FileSystems.Default, recorder);
        fileSystem.DirectoryExists(directory).ShouldBeTrue();
        switch (operation)
        {
            case "GetLastWriteTimeUtc":
                _ = fileSystem.GetLastWriteTimeUtc(directory);
                break;
            case "EnumerateFiles":
                _ = fileSystem.EnumerateFiles(directory).ToArray();
                break;
            case "EnumerateDirectories":
                _ = fileSystem.EnumerateDirectories(directory).ToArray();
                break;
            case "EnumerateFileSystemEntries":
                _ = fileSystem.EnumerateFileSystemEntries(directory).ToArray();
                break;
        }
        EvaluationInputs inputs = recorder.Freeze(Evaluate(CreateProject("<Project />")).Key);
        inputs.Files[directory].RequiresMetadata.ShouldBeTrue();
        IsCurrent(inputs, out _).ShouldBeTrue();

        AddFile(directory, "new.txt");

        IsCurrent(inputs, out string? reason).ShouldBeFalse();
        reason.ShouldBe(directory);
    }

    [Fact]
    public void ConcurrentObservationsCannotLoseMetadataRequirements()
    {
        string directory = _env.CreateFolder(createFolder: true).Path;
        var recorder = new EvaluationInputRecorder();
        Parallel.For(0, 128, iteration =>
        {
            if (iteration % 3 == 0)
            {
                recorder.RecordProbe(directory, ProbeKind.Directory, exists: true);
            }
            else if (iteration % 3 == 1)
            {
                recorder.RecordPath(directory);
            }
            else
            {
                recorder.RecordGlobDirectory(directory);
            }
        });
        EvaluationInputs inputs = recorder.Freeze(Evaluate(CreateProject("<Project />")).Key);

        inputs.Files.Count.ShouldBe(1);
        inputs.Files[directory].RequiresMetadata.ShouldBeTrue();
        inputs.Files[directory].RequiresGlobValidation.ShouldBeTrue();
        IsCurrent(inputs, out _).ShouldBeTrue();
        AddFile(directory, "new.txt");
        IsCurrent(inputs, out _).ShouldBeFalse();
    }

    [Theory]
    [InlineData(EvaluationContext.SharingPolicy.Isolated, false)]
    [InlineData(EvaluationContext.SharingPolicy.SharedSDKCache, false)]
    [InlineData(EvaluationContext.SharingPolicy.Shared, false)]
    [InlineData(EvaluationContext.SharingPolicy.Shared, true)]
    public void ExcludedDirectoryMetadataChangesDoNotInvalidate(
        EvaluationContext.SharingPolicy policy,
        bool prewarmWithoutRecording)
    {
        string bin = Path.Combine(_folder.Path, "bin", "Debug", "net8.0");
        string obj = Path.Combine(_folder.Path, "obj", "Debug", "net8.0");
        Directory.CreateDirectory(bin);
        Directory.CreateDirectory(obj);
        _env.CreateFile(_folder, "input.txt", "included");
        string project = CreateProject("""
            <Project>
              <ItemGroup>
                <Asset Include="**/*" Exclude="bin/Debug/net8.0/**/*;obj/Debug/net8.0/**/*" />
              </ItemGroup>
            </Project>
            """);
        EvaluationContext context = EvaluationContext.Create(policy);
        ProjectOptions options = CreateOptions();
        options.EvaluationContext = context;
        if (prewarmWithoutRecording)
        {
            SetRecording(enabled: false);
            ProjectInstance.FromFile(project, options).EvaluationInputs.ShouldBeNull();
            SetRecording(enabled: true);
        }

        ProjectInstance first = ProjectInstance.FromFile(project, options);
        ProjectInstance cachedGlob = ProjectInstance.FromFile(project, options);
        EvaluationInputs firstInputs = first.EvaluationInputs.ShouldNotBeNull();
        EvaluationInputs replayedInputs = cachedGlob.EvaluationInputs.ShouldNotBeNull();
        EvaluationInputs[] observations = [firstInputs, replayedInputs];
        foreach (EvaluationInputs inputs in observations)
        {
            inputs.IsCacheable.ShouldBeTrue(inputs.NonCacheableDetail);
            inputs.Files[bin].RequiresMetadata.ShouldBeFalse();
            inputs.Files[obj].RequiresMetadata.ShouldBeFalse();
            inputs.Files[_folder.Path].RequiresMetadata.ShouldBeFalse();
            inputs.Files[_folder.Path].RequiresGlobValidation.ShouldBeTrue();
            IsCurrent(inputs, out _).ShouldBeTrue();
        }

        AddFile(bin, "generated.dll");
        AddFile(obj, "generated.cs");

        IsCurrent(firstInputs, out string? firstReason).ShouldBeTrue(firstReason);
        IsCurrent(replayedInputs, out string? replayedReason).ShouldBeTrue(replayedReason);
        ProjectInstance fresh = ProjectInstance.FromFile(project, CreateOptions());
        first.GetItems("Asset").Select(item => item.EvaluatedInclude)
            .ShouldBe(fresh.GetItems("Asset").Select(item => item.EvaluatedInclude), ignoreOrder: true);
        fresh.GetItems("Asset").Select(item => item.EvaluatedInclude)
            .ShouldBe(["input.txt", "test.proj"], ignoreOrder: true);
    }

    [Fact]
    public void AnIncludedGlobPromotesAnExcludedDirectoryProbe()
    {
        string generated = Path.Combine(_folder.Path, "obj", "Debug");
        Directory.CreateDirectory(generated);
        File.WriteAllText(Path.Combine(generated, "before.cs"), string.Empty);
        string project = CreateProject("""
            <Project>
              <ItemGroup>
                <Asset Include="**/*" Exclude="obj/Debug/**/*" />
                <Generated Include="obj/Debug/*.cs" />
              </ItemGroup>
            </Project>
            """);
        EvaluationInputs inputs = Evaluate(project);
        inputs.Files[generated].RequiresMetadata.ShouldBeFalse();
        inputs.Files[generated].RequiresGlobValidation.ShouldBeTrue();
        IsCurrent(inputs, out _).ShouldBeTrue();

        AddFile(generated, "after.cs");

        IsCurrent(inputs, out string? reason).ShouldBeFalse();
        reason.ShouldBe(generated);
        ProjectInstance.FromFile(project, CreateOptions()).GetItems("Generated").Count.ShouldBe(2);
    }

    [Theory]
    [InlineData(EvaluationContext.SharingPolicy.Isolated)]
    [InlineData(EvaluationContext.SharingPolicy.SharedSDKCache)]
    [InlineData(EvaluationContext.SharingPolicy.Shared)]
    public void GlobValidationIgnoresUnmatchedOutputChanges(EvaluationContext.SharingPolicy policy)
    {
        string bin = Path.Combine(_folder.Path, "bin", "Debug", "net8.0");
        string obj = Path.Combine(_folder.Path, "obj", "Debug", "net8.0");
        Directory.CreateDirectory(bin);
        Directory.CreateDirectory(obj);
        _env.CreateFile(_folder, "build.props", "<Project />");
        string project = CreateProject("""
            <Project>
              <ItemGroup>
                <None Include="Assets.*;GulpAssets.*;Assets/**;**/*.props;**/*.targets"
                      Exclude="obj/**/*.props;obj/**/*.targets" />
                <Asset Include="**/*" Exclude="bin/**;obj/**;**/*.props;**/*.targets" />
              </ItemGroup>
            </Project>
            """);
        ProjectOptions options = CreateOptions();
        options.EvaluationContext = EvaluationContext.Create(policy);
        ProjectInstance first = ProjectInstance.FromFile(project, options);
        EvaluationInputs inputs = first.EvaluationInputs.ShouldNotBeNull();
        EvaluationInputs replayed = Evaluate(project, options);

        inputs.Files[bin].RequiresMetadata.ShouldBeFalse();
        inputs.Files[bin].RequiresGlobValidation.ShouldBeTrue();
        inputs.Files[obj].RequiresMetadata.ShouldBeFalse();
        inputs.Files[obj].RequiresGlobValidation.ShouldBeTrue();
        inputs.Globs.ShouldNotBeEmpty();
        IsCurrent(inputs, out string? reason).ShouldBeTrue(reason);

        AddFile(bin, "output.dll");
        AddFile(obj, "output.dll");
        AddFile(obj, "excluded.props");

        IsCurrent(inputs, out reason).ShouldBeTrue(reason);
        IsCurrent(replayed, out reason).ShouldBeTrue(reason);
        ProjectInstance fresh = ProjectInstance.FromFile(project, CreateOptions());
        first.Items.Select(item => $"{item.ItemType}:{item.EvaluatedInclude}")
            .ShouldBe(fresh.Items.Select(item => $"{item.ItemType}:{item.EvaluatedInclude}"), ignoreOrder: true);

        AddFile(bin, "included.props");
        IsCurrent(inputs, out _).ShouldBeFalse();
        IsCurrent(replayed, out _).ShouldBeFalse();
        ProjectInstance.FromFile(project, CreateOptions()).GetItems("None").Count.ShouldBe(2);
    }

    [Theory]
    [InlineData("add")]
    [InlineData("remove")]
    [InlineData("rename")]
    [InlineData("subdirectory")]
    public void MatchingGlobMembershipChangesInvalidate(string mutation)
    {
        string directory = Path.Combine(_folder.Path, "files");
        Directory.CreateDirectory(directory);
        string existing = Path.Combine(directory, "before.props");
        File.WriteAllText(existing, "<Project />");
        string project = CreateProject("""
            <Project>
              <ItemGroup>
                <Input Include="files/**/*.props" />
              </ItemGroup>
            </Project>
            """);
        EvaluationInputs inputs = Evaluate(project);
        IsCurrent(inputs, out _).ShouldBeTrue();
        switch (mutation)
        {
            case "add":
                File.WriteAllText(Path.Combine(directory, "after.props"), "<Project />");
                break;
            case "remove":
                File.Delete(existing);
                break;
            case "rename":
                File.Move(existing, Path.Combine(directory, "renamed.props"));
                break;
            case "subdirectory":
                string child = Path.Combine(directory, "child");
                Directory.CreateDirectory(child);
                File.WriteAllText(Path.Combine(child, "after.props"), "<Project />");
                break;
        }
        Directory.SetLastWriteTimeUtc(directory, Directory.GetLastWriteTimeUtc(directory).AddSeconds(2));

        IsCurrent(inputs, out string? reason).ShouldBeFalse();
        reason.ShouldBe(directory);
    }

    [Fact]
    public void AnUnrelatedDirectoryChangeIsReplayedOnlyOnce()
    {
        (EvaluationInputs inputs, string directory) = EvaluateGlobDirectoryWithUnrelatedChange(DateTime.UtcNow.AddSeconds(-30));

        IsCurrent(inputs, out string? reason).ShouldBeTrue(reason);
        inputs.TryGetValidatedGlobDirectory(directory, out FileDependency validated).ShouldBeTrue();
        validated.LastWriteTimeUtc.ShouldBe(Directory.GetLastWriteTimeUtc(directory));

        // The remembered state is trusted without another replay, as an unchanged recorded timestamp always was.
        // A directory length that tracks its entries would make this change visible, so only forge where it does not.
        File.WriteAllText(Path.Combine(directory, "after.props"), "<Project />");
        Directory.SetLastWriteTimeUtc(directory, validated.LastWriteTimeUtc);
        EvaluationInputRecorder.TryStat(directory, out FileDependency forged).ShouldBeTrue();
        Assert.SkipUnless(forged.Length == validated.Length, "This file system reports a directory length that tracks its entries.");
        IsCurrent(inputs, out reason).ShouldBeTrue(reason);
    }

    [Theory]
    [InlineData("add")]
    [InlineData("remove")]
    [InlineData("rename")]
    public void AMatchingChangeAfterARememberedDirectoryStateInvalidates(string mutation)
    {
        (EvaluationInputs inputs, string directory) = EvaluateGlobDirectoryWithUnrelatedChange(DateTime.UtcNow.AddSeconds(-30));
        IsCurrent(inputs, out string? reason).ShouldBeTrue(reason);
        inputs.TryGetValidatedGlobDirectory(directory, out _).ShouldBeTrue();

        string existing = Path.Combine(directory, "before.props");
        switch (mutation)
        {
            case "add":
                AddFile(directory, "after.props");
                break;
            case "remove":
                File.Delete(existing);
                break;
            case "rename":
                File.Move(existing, Path.Combine(directory, "renamed.props"));
                break;
        }

        Directory.SetLastWriteTimeUtc(directory, DateTime.UtcNow.AddSeconds(-5));
        IsCurrent(inputs, out reason).ShouldBeFalse();
        reason.ShouldBe(directory);
    }

    [Fact]
    public void ADirectoryThatIsStillChangingIsNotRemembered()
    {
        (EvaluationInputs inputs, string directory) = EvaluateGlobDirectoryWithUnrelatedChange(DateTime.UtcNow);

        IsCurrent(inputs, out string? reason).ShouldBeTrue(reason);
        inputs.TryGetValidatedGlobDirectory(directory, out _).ShouldBeFalse();
    }

    [Fact]
    public void AReplayThatDoesNotMatchRemembersNothing()
    {
        (EvaluationInputs inputs, string directory) = EvaluateGlobDirectoryWithUnrelatedChange(DateTime.UtcNow.AddSeconds(-30));
        File.WriteAllText(Path.Combine(directory, "after.props"), "<Project />");
        Directory.SetLastWriteTimeUtc(directory, DateTime.UtcNow.AddSeconds(-30));

        IsCurrent(inputs, out string? reason).ShouldBeFalse();
        reason.ShouldBe(directory);
        inputs.TryGetValidatedGlobDirectory(directory, out _).ShouldBeFalse();
    }

    private (EvaluationInputs Inputs, string Directory) EvaluateGlobDirectoryWithUnrelatedChange(DateTime timestamp)
    {
        string directory = Path.Combine(_folder.Path, "files");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "before.props"), "<Project />");
        string project = CreateProject("""
            <Project>
              <ItemGroup>
                <Input Include="files/*.props" />
              </ItemGroup>
            </Project>
            """);
        EvaluationInputs inputs = Evaluate(project);
        File.WriteAllText(Path.Combine(directory, "unrelated.txt"), string.Empty);
        Directory.SetLastWriteTimeUtc(directory, timestamp);
        return (inputs, directory);
    }

    [Fact]
    public void CachedGlobResultsAreCheckedEvenWhenReplayedDirectoryStatsAreCurrent()
    {
        string directory = Path.Combine(_folder.Path, "files");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "before.props"), "<Project />");
        string project = CreateProject("""
            <Project>
              <ItemGroup>
                <Input Include="files/*.props" />
              </ItemGroup>
            </Project>
            """);
        ProjectOptions options = CreateOptions();
        options.EvaluationContext = EvaluationContext.Create(EvaluationContext.SharingPolicy.Shared);
        ProjectInstance.FromFile(project, options).GetItems("Input").Count.ShouldBe(1);
        AddFile(directory, "after.props");

        ProjectInstance replayed = ProjectInstance.FromFile(project, options);
        EvaluationInputs inputs = replayed.EvaluationInputs.ShouldNotBeNull();
        replayed.GetItems("Input").Count.ShouldBe(1);
        inputs.Globs.ShouldContain(glob => glob.FromCache);
        inputs.Files[directory].LastWriteTimeUtc.ShouldBe(Directory.GetLastWriteTimeUtc(directory));
        IsCurrent(inputs, out _).ShouldBeFalse();
        ProjectInstance.FromFile(project, CreateOptions()).GetItems("Input").Count.ShouldBe(2);
    }

    [Theory]
    [InlineData(EvaluationContext.SharingPolicy.Isolated)]
    [InlineData(EvaluationContext.SharingPolicy.SharedSDKCache)]
    public void EvaluationScopedGlobCachesKeepUnchangedValidationOnTheStatFastPath(EvaluationContext.SharingPolicy policy)
    {
        string directory = Path.Combine(_folder.Path, "files");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "before.props"), "<Project />");
        string project = CreateProject("""
            <Project>
              <ItemGroup>
                <First Include="files/*.props" />
                <Second Include="files/*.props" />
                <Other Include="files/*.targets" />
              </ItemGroup>
            </Project>
            """);
        ProjectOptions options = CreateOptions();
        options.EvaluationContext = EvaluationContext.Create(policy);
        EvaluationInputs inputs = Evaluate(project, options);

        inputs.Globs.Length.ShouldBeGreaterThanOrEqualTo(3);
        inputs.Globs.ShouldAllBe(glob => !glob.FromCache);
        IsCurrent(inputs, out _).ShouldBeTrue();
        AddFile(directory, "unrelated.dll");
        IsCurrent(inputs, out string? reason).ShouldBeTrue(reason);
        AddFile(directory, "changed.targets");
        IsCurrent(inputs, out _).ShouldBeFalse();
    }

    [Theory]
    [InlineData(EvaluationContext.SharingPolicy.Isolated)]
    [InlineData(EvaluationContext.SharingPolicy.SharedSDKCache)]
    public void PrewarmedNonSharedGlobCachesStillRequireResultValidation(EvaluationContext.SharingPolicy policy)
    {
        string directory = Path.Combine(_folder.Path, "files");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "before.props"), "<Project />");
        string project = CreateProject("""
            <Project>
              <ItemGroup>
                <Input Include="files/*.props" />
              </ItemGroup>
            </Project>
            """);
        EvaluationContext context = EvaluationContext.Create(policy);
        context.FileMatcher.GetFiles(_folder.Path, "files/*.props").FileList.Length.ShouldBe(1);
        AddFile(directory, "after.props");
        ProjectOptions options = CreateOptions();
        options.EvaluationContext = context;

        ProjectInstance instance = ProjectInstance.FromFile(project, options);
        EvaluationInputs inputs = instance.EvaluationInputs.ShouldNotBeNull();
        instance.GetItems("Input").Count.ShouldBe(1);
        inputs.Globs.ShouldContain(glob => glob.FromCache);
        IsCurrent(inputs, out _).ShouldBeFalse();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GlobObservationDoesNotWeakenDirectDirectoryMetadataReads(bool metadataFirst)
    {
        string directory = Path.Combine(_folder.Path, "files");
        Directory.CreateDirectory(directory);
        var recorder = new EvaluationInputRecorder();
        if (metadataFirst)
        {
            recorder.RecordPath(directory);
        }
        recorder.RecordGlobDirectory(directory);
        recorder.RecordPath(directory);
        recorder.RecordProbe(directory, ProbeKind.Directory, exists: true);
        EvaluationInputs inputs = recorder.Freeze(Evaluate(CreateProject("<Project />")).Key);

        inputs.Files[directory].RequiresMetadata.ShouldBeTrue();
        inputs.Files[directory].RequiresGlobValidation.ShouldBeTrue();
        Directory.SetLastWriteTimeUtc(directory, Directory.GetLastWriteTimeUtc(directory).AddSeconds(2));
        IsCurrent(inputs, out string? reason).ShouldBeFalse();
        reason.ShouldBe(directory);
    }

    [Fact]
    public void RecordedGlobOwnsItsInputsAndResults()
    {
        _env.CreateFile(_folder, "input.props", "<Project />");
        string[] files = ["input.props"];
        List<string> excludes = [];
        var recorder = new EvaluationInputRecorder();
        recorder.RecordGlobDirectory(_folder.Path);
        recorder.RecordGlob(new FileMatcher.GlobResultObservation(
            _folder.Path, "*.props", excludes, files,
            FileMatcherDriver.Legacy, FileMatcherCaseFolding.InvariantCulture,
            FromCache: true, Succeeded: true));
        EvaluationInputs inputs = recorder.Freeze(Evaluate(CreateProject("<Project />")).Key);
        files[0] = "changed.props";
        excludes.Add("**/*");

        IsCurrent(inputs, out string? reason).ShouldBeTrue(reason);
        inputs.Globs.Single().RetainedSizeBytes.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void FailedGlobExpansionIsNotCacheable()
    {
        var recorder = new EvaluationInputRecorder();
        recorder.RecordGlob(new FileMatcher.GlobResultObservation(
            _folder.Path, "*.props", null, ["*.props"],
            FileMatcherDriver.Legacy, FileMatcherCaseFolding.InvariantCulture,
            FromCache: false, Succeeded: false));
        EvaluationInputs inputs = recorder.Freeze(Evaluate(CreateProject("<Project />")).Key);

        inputs.NonCacheable.ShouldBe(NonCacheableReason.RecorderFailure);
        IsCurrent(inputs, out _).ShouldBeFalse();
    }

    [Fact]
    public void EmptyGlobDetectsItsFirstMatchingFile()
    {
        string directory = Path.Combine(_folder.Path, "empty");
        Directory.CreateDirectory(directory);
        string project = CreateProject("""
            <Project>
              <ItemGroup>
                <Input Include="empty/**/*.props" />
              </ItemGroup>
            </Project>
            """);
        EvaluationInputs inputs = Evaluate(project);
        inputs.Globs.ShouldNotBeEmpty();
        IsCurrent(inputs, out _).ShouldBeTrue();

        AddFile(directory, "unrelated.dll");
        IsCurrent(inputs, out string? reason).ShouldBeTrue(reason);
        AddFile(directory, "first.props");
        IsCurrent(inputs, out _).ShouldBeFalse();
    }

    [Fact]
    public void ChangedGlobDirectoryWithoutCapturedResultsFailsClosed()
    {
        string directory = _env.CreateFolder(createFolder: true).Path;
        var recorder = new EvaluationInputRecorder();
        recorder.RecordGlobDirectory(directory);
        EvaluationInputs inputs = recorder.Freeze(Evaluate(CreateProject("<Project />")).Key);
        IsCurrent(inputs, out _).ShouldBeTrue();
        AddFile(directory, "new.props");

        IsCurrent(inputs, out string? reason).ShouldBeFalse();
        reason.ShouldBe(directory);
    }

    [Fact]
    public void NearerFileAboveCandidateInvalidates()
    {
        TransientTestFolder child = _env.CreateFolder(Path.Combine(_folder.Path, "child"), createFolder: true);
        _env.CreateFile(_folder, "Marker.props", "<Project />");
        string project = _env.CreateFile(child, "test.proj", """
            <Project>
              <PropertyGroup>
                <Marker>$([MSBuild]::GetPathOfFileAbove('Marker.props'))</Marker>
              </PropertyGroup>
            </Project>
            """.Cleanup()).Path;
        string nearer = Path.Combine(child.Path, "Marker.props");
        EvaluationInputs inputs = Evaluate(project);
        inputs.Files[nearer].Kind.ShouldBe(PathKind.Missing);
        inputs.Files[Path.Combine(_folder.Path, "Marker.props")].Kind.ShouldBe(PathKind.File);

        File.WriteAllText(nearer, "<Project />");

        IsCurrent(inputs, out string? reason).ShouldBeFalse();
        reason.ShouldBe(nearer);
    }

    [Theory]
    [InlineData("FileExists", "optional.props")]
    [InlineData("DirectoryExists", "generated")]
    public void IntrinsicExistsProbeIsRecorded(string function, string name)
    {
        string project = CreateProject($"""
            <Project>
              <PropertyGroup>
                <Found>$([MSBuild]::{function}('$(MSBuildThisFileDirectory){name}'))</Found>
              </PropertyGroup>
            </Project>
            """);
        string probed = Path.Combine(_folder.Path, name);

        EvaluationInputs inputs = Evaluate(project);

        inputs.NonCacheable.ShouldBe(NonCacheableReason.None);
        inputs.Files[probed].Kind.ShouldBe(PathKind.Missing);
        Directory.CreateDirectory(probed);
        IsCurrent(inputs, out string? reason).ShouldBeFalse();
        reason.ShouldBe(probed);
    }

    [Fact]
    public void ExistsOnLoadedProjectIsRecorded()
    {
        // Only design-time evaluation consults the loaded projects, and only for import conditions; ProjectInstance
        // evaluation always asks the file system.
        ProjectCollection collection = _env.CreateProjectCollection().Collection;
        string loaded = _env.CreateFile(_folder, "loaded.proj", "<Project />").Path;
        ProjectRootElement loadedElement = ProjectRootElement.Open(loaded, collection);
        _env.CreateFile(_folder, "other.props", "<Project><PropertyGroup><HasOther>true</HasOther></PropertyGroup></Project>");
        string project = CreateProject("""
            <Project>
              <Import Project="other.props" Condition="Exists('$(MSBuildThisFileDirectory)loaded.proj')" />
            </Project>
            """);

        var evaluated = new Project(project, globalProperties: null, toolsVersion: null, collection);

        evaluated.GetPropertyValue("HasOther").ShouldBe("true");
        evaluated.EvaluationInputs.ShouldNotBeNull().Files[loaded].Kind.ShouldBe(PathKind.File);
        GC.KeepAlive(loadedElement);
    }

    [Fact]
    public void IgnoredEmptyImportIsRecordedWithItsLength()
    {
        string empty = _env.CreateFile(_folder, "empty.props", string.Empty).Path;
        string project = CreateProject("""
            <Project>
              <Import Project="empty.props" />
            </Project>
            """);
        ProjectOptions options = CreateOptions();
        options.LoadSettings = ProjectLoadSettings.IgnoreEmptyImports;
        EvaluationInputs inputs = Evaluate(project, options);
        inputs.Files[empty].ShouldBe(new FileDependency(PathKind.File, File.GetLastWriteTimeUtc(empty), 0));

        Touch(empty, "<Project />");

        IsCurrent(inputs, out string? reason).ShouldBeFalse();
        reason.ShouldBe(empty);
    }

    [Fact]
    public void IgnoredInvalidImportBecomingValidInvalidates()
    {
        string import = _env.CreateFile(_folder, "invalid.props", "<Project>").Path;
        string project = CreateProject("""
            <Project>
              <Import Project="invalid.props" />
            </Project>
            """);
        ProjectOptions options = CreateOptions();
        options.LoadSettings = ProjectLoadSettings.IgnoreInvalidImports;
        EvaluationInputs inputs = Evaluate(project, options);
        inputs.Files[import].RequiresMetadata.ShouldBeTrue();
        inputs.Files[import].Length.ShouldBe(new FileInfo(import).Length);
        IsCurrent(inputs, out _).ShouldBeTrue();

        Touch(import, "<Project><PropertyGroup><Imported>now-valid</Imported></PropertyGroup></Project>");

        IsCurrent(inputs, out string? reason).ShouldBeFalse();
        reason.ShouldBe(import);
        ProjectInstance.FromFile(project, CreateOptions()).GetPropertyValue("Imported").ShouldBe("now-valid");
    }

    [Fact]
    public void UnsavedProjectChangesAreNotCacheable()
    {
        ProjectCollection collection = _env.CreateProjectCollection().Collection;
        string project = CreateProject("<Project />");
        ProjectRootElement xml = ProjectRootElement.Open(project, collection);
        xml.AddProperty("Edited", "true");

        var instance = new ProjectInstance(xml, globalProperties: null, toolsVersion: null, collection);

        instance.EvaluationInputs.ShouldNotBeNull().NonCacheable.ShouldBe(NonCacheableReason.InMemoryProject);
    }

    [Fact]
    public void ProjectSourceWithoutAuthoritativeFileObservationIsNotCacheable()
    {
        ProjectCollection collection = _env.CreateProjectCollection().Collection;
        string project = CreateProject("<Project />");
        ProjectRootElement xml = ProjectRootElement.Create(collection);
        xml.FullPath = project;
        xml.AddProperty("Value", "in memory");
        using var writer = new StringWriter();
        xml.Save(writer);

        xml.HasUnsavedChanges.ShouldBeFalse();
        xml.FileLengthWhenRead.ShouldBeNull();

        var instance = new ProjectInstance(xml, globalProperties: null, toolsVersion: null, collection);
        EvaluationInputs inputs = instance.EvaluationInputs.ShouldNotBeNull();

        instance.GetPropertyValue("Value").ShouldBe("in memory");
        inputs.NonCacheable.ShouldBe(NonCacheableReason.RecorderFailure);
        inputs.NonCacheableDetail.ShouldBe(project);
    }

    [Fact]
    public void WriterOnlySaveClearsFileSourceProvenance()
    {
        ProjectCollection collection = _env.CreateProjectCollection().Collection;
        string project = CreateProject("""
            <Project>
              <PropertyGroup>
                <Value>disk</Value>
              </PropertyGroup>
            </Project>
            """);
        ProjectRootElement xml = ProjectRootElement.Open(project, collection);
        xml.Properties.Single().Value = "writer";
        using var writer = new StringWriter();

        xml.Save(writer);

        xml.HasUnsavedChanges.ShouldBeFalse();
        xml.FileLengthWhenRead.ShouldBeNull();
        File.ReadAllText(project).ShouldContain("<Value>disk</Value>");

        var instance = new ProjectInstance(xml, globalProperties: null, toolsVersion: null, collection);
        EvaluationInputs inputs = instance.EvaluationInputs.ShouldNotBeNull();

        instance.GetPropertyValue("Value").ShouldBe("writer");
        inputs.NonCacheable.ShouldBe(NonCacheableReason.RecorderFailure);
        inputs.NonCacheableDetail.ShouldBe(project);
    }

    [Fact]
    public void FailedReloadRestoresFileSourceProvenance()
    {
        ProjectCollection collection = _env.CreateProjectCollection().Collection;
        string project = CreateProject("""
            <Project>
              <PropertyGroup>
                <Value>cached</Value>
              </PropertyGroup>
            </Project>
            """);
        ProjectRootElement xml = ProjectRootElement.Open(project, collection);
        DateTime timestampWhenRead = xml.LastWriteTimeWhenReadUtc;
        long lengthWhenRead = xml.FileLengthWhenRead.ShouldNotBeNull();
        Touch(
            project,
            """
            <Project>
              <InvalidElement Attribute="replacement is deliberately longer" />
            </Project>
            """.Cleanup());
        File.GetLastWriteTimeUtc(project).ShouldNotBe(timestampWhenRead);
        new FileInfo(project).Length.ShouldNotBe(lengthWhenRead);

        Should.Throw<InvalidProjectFileException>(() => xml.ReloadFrom(project));

        xml.LastWriteTimeWhenReadUtc.ShouldBe(timestampWhenRead);
        xml.FileLengthWhenRead.ShouldBe(lengthWhenRead);
        var instance = new ProjectInstance(xml, globalProperties: null, toolsVersion: null, collection);
        EvaluationInputs inputs = instance.EvaluationInputs.ShouldNotBeNull();

        instance.GetPropertyValue("Value").ShouldBe("cached");
        inputs.NonCacheable.ShouldBe(NonCacheableReason.ConflictingObservation);
        inputs.NonCacheableDetail.ShouldBe(project);
    }

    [Fact]
    public void ProjectSourceChangedAfterItWasReadIsNotCacheable()
    {
        ProjectCollection collection = _env.CreateProjectCollection().Collection;
        string project = CreateProject("<Project />");
        ProjectRootElement xml = ProjectRootElement.Open(project, collection);
        Touch(project, "<Project><PropertyGroup /></Project>");

        var instance = new ProjectInstance(xml, globalProperties: null, toolsVersion: null, collection);

        instance.EvaluationInputs.ShouldNotBeNull().NonCacheable.ShouldBe(NonCacheableReason.ConflictingObservation);
    }

    [Fact]
    public void CachedProjectSourceWithChangedLengthAndPreservedTimestampIsNotCacheable()
    {
        ProjectCollection collection = _env.CreateProjectCollection().Collection;
        string project = CreateProject("""
            <Project>
              <PropertyGroup>
                <Value>cached</Value>
              </PropertyGroup>
            </Project>
            """);
        ProjectRootElement cachedRoot = ProjectRootElement.Open(project, collection);
        DateTime timestamp = cachedRoot.LastWriteTimeWhenReadUtc;
        long lengthWhenRead = cachedRoot.FileLengthWhenRead.ShouldNotBeNull();
        RewriteWithDifferentLengthPreservingTimestamp(
            project,
            """
            <Project>
              <PropertyGroup>
                <Value>replacement from disk is longer</Value>
              </PropertyGroup>
            </Project>
            """.Cleanup(),
            timestamp,
            lengthWhenRead);
        collection.ProjectRootElementCache.TryGet(project).ShouldBeSameAs(cachedRoot);

        ProjectInstance instance = ProjectInstance.FromFile(project, new ProjectOptions { ProjectCollection = collection });
        EvaluationInputs inputs = instance.EvaluationInputs.ShouldNotBeNull();

        instance.GetPropertyValue("Value").ShouldBe("cached");
        inputs.NonCacheable.ShouldBe(NonCacheableReason.ConflictingObservation);
        inputs.NonCacheableDetail.ShouldBe(project);
        inputs.Files[project].Length.ShouldBe(new FileInfo(project).Length);
        GC.KeepAlive(cachedRoot);
    }

    [Fact]
    public void CachedImportSourceWithChangedLengthAndPreservedTimestampIsNotCacheable()
    {
        ProjectCollection collection = _env.CreateProjectCollection().Collection;
        string import = _env.CreateFile(_folder, "cached.props", """
            <Project>
              <PropertyGroup>
                <ImportedValue>cached</ImportedValue>
              </PropertyGroup>
            </Project>
            """.Cleanup()).Path;
        string project = CreateProject("""
            <Project>
              <Import Project="cached.props" />
            </Project>
            """);
        ProjectRootElement cachedImport = ProjectRootElement.Open(import, collection);
        DateTime timestamp = cachedImport.LastWriteTimeWhenReadUtc;
        long lengthWhenRead = cachedImport.FileLengthWhenRead.ShouldNotBeNull();
        RewriteWithDifferentLengthPreservingTimestamp(
            import,
            """
            <Project>
              <PropertyGroup>
                <ImportedValue>replacement from disk is longer</ImportedValue>
              </PropertyGroup>
            </Project>
            """.Cleanup(),
            timestamp,
            lengthWhenRead);
        collection.ProjectRootElementCache.TryGet(import).ShouldBeSameAs(cachedImport);

        ProjectInstance instance = ProjectInstance.FromFile(project, new ProjectOptions { ProjectCollection = collection });
        EvaluationInputs inputs = instance.EvaluationInputs.ShouldNotBeNull();

        instance.GetPropertyValue("ImportedValue").ShouldBe("cached");
        inputs.NonCacheable.ShouldBe(NonCacheableReason.ConflictingObservation);
        inputs.NonCacheableDetail.ShouldBe(import);
        inputs.Files[import].Length.ShouldBe(new FileInfo(import).Length);
        GC.KeepAlive(cachedImport);
    }

    [Fact]
    public void FileReadPropertyFunctionRecordsTheFile()
    {
        string version = _env.CreateFile(_folder, "version.txt", "1.0").Path;
        string project = CreateProject("""
            <Project>
              <PropertyGroup>
                <Version>$([System.IO.File]::ReadAllText('$(MSBuildProjectDirectory)/version.txt'))</Version>
              </PropertyGroup>
            </Project>
            """);
        EvaluationInputs inputs = Evaluate(project);
        inputs.NonCacheable.ShouldBe(NonCacheableReason.None);
        inputs.Files[version].Kind.ShouldBe(PathKind.File);

        Touch(version, "2.0");

        IsCurrent(inputs, out string? reason).ShouldBeFalse();
        reason.ShouldBe(version);
    }

    [Fact]
    public void PropertyFunctionFileReadRecordsStateBeforeInvocation()
    {
        string path = _env.CreateFile(_folder, "version.txt", "1").Path;
        var recorder = new EvaluationInputRecorder();
        recorder.RecordPropertyFunctionInput(typeof(File), nameof(File.ReadAllText), isInstance: false, [path]);
        File.WriteAllText(path, "longer");
        recorder.RecordPropertyFunction(
            typeof(File),
            nameof(File.ReadAllText),
            isInstance: false,
            boundMethod: null,
            [path],
            result: "1");

        FileDependency observed = recorder.Freeze(Evaluate(CreateProject("<Project />")).Key).Files[path];
        observed.Length.ShouldBe(1);
    }

    [Fact]
    public void DirectEnvironmentReadNamesFollowPlatformCasing()
    {
        var recorder = new EvaluationInputRecorder();
        recorder.RecordEnvironmentRead("MSBUILD_TEST_CASE", "upper");
        recorder.RecordEnvironmentRead("msbuild_test_case", "lower");

        EvaluationInputs inputs = recorder.Freeze(Evaluate(CreateProject("<Project />")).Key);

        inputs.EnvironmentReads.Count.ShouldBe(NativeMethodsShared.IsWindows ? 1 : 2);
    }

    [Theory]
    [InlineData("second")]
    [InlineData(null)]
    public void EnvironmentReadIsRevalidated(string? currentValue)
    {
        _env.SetEnvironmentVariable("MSBUILD_TEST_INPUT", "first");
        string project = CreateProject("""
            <Project>
              <PropertyGroup>
                <Value>$([System.Environment]::GetEnvironmentVariable('MSBUILD_TEST_INPUT'))</Value>
              </PropertyGroup>
            </Project>
            """);
        EvaluationInputs inputs = Evaluate(project);
        inputs.EnvironmentReads["MSBUILD_TEST_INPUT"].ShouldBe("first");
        IsCurrent(inputs, out _).ShouldBeTrue();

        _env.SetEnvironmentVariable("MSBUILD_TEST_INPUT", currentValue);

        IsCurrent(inputs, out string? reason).ShouldBeFalse();
        reason.ShouldBe("MSBUILD_TEST_INPUT");
        inputs.EnvironmentReads["MSBUILD_TEST_INPUT"].ShouldBe("first");
    }

    [UnixOnlyFact]
    public void EnvironmentReadsWithDifferentCasingAreRevalidatedOnUnix()
    {
        _env.SetEnvironmentVariable("MSBUILD_TEST_CASE", "upper");
        _env.SetEnvironmentVariable("msbuild_test_case", "lower");
        string project = CreateProject("""
            <Project>
              <PropertyGroup>
                <Upper>$([System.Environment]::GetEnvironmentVariable('MSBUILD_TEST_CASE'))</Upper>
                <Lower>$([System.Environment]::GetEnvironmentVariable('msbuild_test_case'))</Lower>
              </PropertyGroup>
            </Project>
            """);
        EvaluationInputs inputs = Evaluate(project);

        inputs.EnvironmentReads.Count.ShouldBe(2);
        _env.SetEnvironmentVariable("MSBUILD_TEST_CASE", "changed");

        IsCurrent(inputs, out string? reason).ShouldBeFalse();
        reason.ShouldBe("MSBUILD_TEST_CASE");
    }

    [Theory]
    [InlineData("$([System.DateTime]::Now)")]
    [InlineData("$([System.Guid]::NewGuid())")]
    [InlineData("$([System.DateTime]::Parse('12:34'))")]
    [InlineData("$([System.Convert]::ToDateTime('12:34'))")]
    public void VolatilePropertyFunctionIsNotCacheable(string expression)
    {
        string project = CreateProject($"""
            <Project>
              <PropertyGroup>
                <Value>{expression}</Value>
              </PropertyGroup>
            </Project>
            """);

        EvaluationInputs inputs = Evaluate(project);

        inputs.NonCacheable.ShouldBe(NonCacheableReason.VolatilePropertyFunction);
        IsCurrent(inputs, out string? reason).ShouldBeFalse();
        reason.ShouldNotBeNull().ShouldContain(nameof(NonCacheableReason.VolatilePropertyFunction));
    }

    [Theory]
    [InlineData("$([System.IO.File]::GetCreationTime('$(MSBuildProjectFullPath)'))")]
    [InlineData("$([System.IO.File]::GetAttributes('$(MSBuildProjectFullPath)'))")]
    [InlineData("$([System.IO.Directory]::GetLastAccessTime('$(MSBuildProjectDirectory)'))")]
    public void ReadsOfFieldsTheManifestDoesNotHoldAreNotCacheable(string expression)
    {
        string project = CreateProject($"""
            <Project>
              <PropertyGroup>
                <Value>{expression}</Value>
              </PropertyGroup>
            </Project>
            """);

        Evaluate(project).NonCacheable.ShouldBe(NonCacheableReason.UnclassifiedPropertyFunction);
    }

    [Fact]
    public void PurePropertyFunctionsAreCacheable()
    {
        string project = CreateProject("""
            <Project>
              <PropertyGroup>
                <A>value</A>
                <B>$(A.ToUpper())_$([System.String]::Join('-', 'x', 'y'))_$([MSBuild]::Add(1, 2))</B>
              </PropertyGroup>
            </Project>
            """);

        EvaluationInputs inputs = Evaluate(project);

        inputs.NonCacheable.ShouldBe(NonCacheableReason.None);
    }

    [Fact]
    public void UnclassifiedPropertyFunctionIsNotCacheable()
    {
        string project = CreateProject("""
            <Project>
              <PropertyGroup>
                <Drives>$([System.Environment]::GetLogicalDrives())</Drives>
              </PropertyGroup>
            </Project>
            """);

        EvaluationInputs inputs = Evaluate(project);

        inputs.NonCacheable.ShouldBe(NonCacheableReason.UnclassifiedPropertyFunction);
        inputs.NonCacheableDetail.ShouldNotBeNull().ShouldContain("GetLogicalDrives");
    }

    [Fact]
    public void KnownFolderPropertyFunctionIsNotCacheable()
    {
        PropertyFunctionEffects.Classify(
            typeof(Environment),
            nameof(Environment.GetFolderPath),
            isInstance: false,
            boundMethod: null,
            arguments: []).ShouldBe(PropertyFunctionEffect.Unsupported);
    }

    [Theory]
    [InlineData("GetRelativePath")]
    [InlineData("GetTempPath")]
    public void CurrentDirectoryAndEnvironmentDependentPathFunctionsAreNotCacheable(string member)
    {
        PropertyFunctionEffects.Classify(
            typeof(Path),
            member,
            isInstance: false,
            boundMethod: null,
            arguments: []).ShouldBe(PropertyFunctionEffect.Unsupported);
    }

    [Fact]
    public void AllPropertyFunctionsSwitchIsNotCacheable()
    {
        // The AppContext switch overrides the variable when a previous test left it set, so honor whichever is active.
        const string switchName = "Microsoft.Build.EnableAllPropertyFunctions";
        bool switchWasSet = AppContext.TryGetSwitch(switchName, out bool original);
        _env.SetEnvironmentVariable("MSBUILDENABLEALLPROPERTYFUNCTIONS", "1");
        if (switchWasSet)
        {
            AppContext.SetSwitch(switchName, true);
        }

        try
        {
            EvaluationInputs inputs = Evaluate(CreateProject("<Project />"));

            inputs.NonCacheable.ShouldBe(NonCacheableReason.AllPropertyFunctionsEnabled);
        }
        finally
        {
            if (switchWasSet)
            {
                AppContext.SetSwitch(switchName, original);
            }
        }
    }

    [WindowsOnlyTheory]
    [InlineData("$(Registry:{0}@Value)")]
    [InlineData("$([MSBuild]::GetRegistryValue('{0}', 'Value'))")]
    [InlineData("$([MSBuild]::GetRegistryValueFromView('{0}', 'Value', null, RegistryView.Default, RegistryView.Registry32))")]
    [SupportedOSPlatform("windows")]
    public void RegistryReadsAreRecordedWithoutStoppingObservation(string expression)
    {
        TestRegistryKey registry = _env.WithTransientTestState(new TestRegistryKey());
        registry.Key.SetValue("Value", "first;value%", RegistryValueKind.String);
        _env.SetEnvironmentVariable("MSBUILD_TEST_AFTER_REGISTRY", "observed");
        string import = _env.CreateFile(_folder, "after.props", "<Project />").Path;
        expression = string.Format(CultureInfo.InvariantCulture, expression, registry.Key.Name);
        string project = CreateProject($"""
            <Project>
              <PropertyGroup>
                <First>{expression}</First>
                <Second>{expression}</Second>
                <After>$([System.Environment]::GetEnvironmentVariable('MSBUILD_TEST_AFTER_REGISTRY'))</After>
              </PropertyGroup>
              <Import Project="after.props" />
            </Project>
            """);

        ProjectInstance recorded = EvaluateWithAndWithoutRecording(project);
        EvaluationInputs inputs = recorded.EvaluationInputs.ShouldNotBeNull();

        recorded.GetPropertyValue("First").ShouldBe("first;value%");
        inputs.NonCacheable.ShouldBe(NonCacheableReason.RegistryRead);
        inputs.RegistryReads.Length.ShouldBe(2);
        foreach (RegistryRead read in inputs.RegistryReads)
        {
            read.KeyName.ShouldBe(registry.Key.Name);
            read.ValueName.ShouldBe("Value");
            read.Value.ShouldBe("first;value%");
            if (expression.Contains("GetRegistryValueFromView"))
            {
                read.RequestedViews.ShouldBe(["RegistryView.Default", "RegistryView.Registry32"]);
            }
            else
            {
                read.RequestedViews.ShouldBeEmpty();
            }
        }
        inputs.EnvironmentReads["MSBUILD_TEST_AFTER_REGISTRY"].ShouldBe("observed");
        inputs.Files.ContainsKey(import).ShouldBeTrue();

        registry.Key.SetValue("Value", "changed", RegistryValueKind.String);

        inputs.RegistryReads[0].Value.ShouldBe("first;value%");
        IsCurrent(inputs, out _).ShouldBeFalse();
    }

    [WindowsOnlyFact]
    [SupportedOSPlatform("windows")]
    public void MissingDefaultAndFallbackRegistryValuesAreRecorded()
    {
        TestRegistryKey registry = _env.WithTransientTestState(new TestRegistryKey());
        registry.Key.SetValue(string.Empty, "default", RegistryValueKind.String);
        registry.Key.SetValue("Empty", string.Empty, RegistryValueKind.String);
        string keyName = registry.Key.Name;
        string project = CreateProject($"""
            <Project>
              <PropertyGroup>
                <Default>$(Registry:{keyName})</Default>
                <Empty>$(Registry:{keyName}@Empty)</Empty>
                <Missing>$(Registry:{keyName}@Missing)</Missing>
                <MissingFunction>$([MSBuild]::GetRegistryValue('{keyName}', 'Missing'))</MissingFunction>
                <Fallback>$([MSBuild]::GetRegistryValue('{keyName}', 'Missing', 'fallback'))</Fallback>
                <ViewFallback>$([MSBuild]::GetRegistryValueFromView('{keyName}\MissingKey', 'Missing', 'view fallback', RegistryView.Default))</ViewFallback>
              </PropertyGroup>
            </Project>
            """);

        EvaluationInputs inputs = Evaluate(project);

        inputs.RegistryReads.Length.ShouldBe(6);
        inputs.RegistryReads[0].ValueName.ShouldBe(string.Empty);
        inputs.RegistryReads[0].Value.ShouldBe("default");
        inputs.RegistryReads[1].Value.ShouldBe(string.Empty);
        inputs.RegistryReads[2].Value.ShouldBeNull();
        inputs.RegistryReads[3].Value.ShouldBeNull();
        inputs.RegistryReads[4].Value.ShouldBe("fallback");
        inputs.RegistryReads[5].KeyName.ShouldBe(keyName + @"\MissingKey");
        inputs.RegistryReads[5].Value.ShouldBe("view fallback");
        inputs.NonCacheable.ShouldBe(NonCacheableReason.RegistryRead);
    }

    [WindowsOnlyTheory]
    [InlineData(RegistryValueKind.DWord)]
    [InlineData(RegistryValueKind.QWord)]
    [InlineData(RegistryValueKind.Binary)]
    [InlineData(RegistryValueKind.MultiString)]
    [InlineData(RegistryValueKind.ExpandString)]
    [SupportedOSPlatform("windows")]
    public void RegistryObservationsPreserveReturnedValueTypes(RegistryValueKind kind)
    {
        TestRegistryKey registry = _env.WithTransientTestState(new TestRegistryKey());
        _env.SetEnvironmentVariable("MSBUILD_TEST_REGISTRY_EXPANSION", "expanded");
        object value = kind switch
        {
            RegistryValueKind.DWord => 123,
            RegistryValueKind.QWord => 123456789123456789L,
            RegistryValueKind.Binary => (byte[])[1, 2, 3],
            RegistryValueKind.MultiString => (string[])["one;two", "three"],
            RegistryValueKind.ExpandString => "%MSBUILD_TEST_REGISTRY_EXPANSION%",
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
        registry.Key.SetValue("Value", value, kind);
        string project = CreateProject($"""
            <Project>
              <PropertyGroup>
                <Direct>$(Registry:{registry.Key.Name}@Value)</Direct>
                <Function>$([MSBuild]::GetRegistryValue('{registry.Key.Name}', 'Value'))</Function>
              </PropertyGroup>
            </Project>
            """);

        EvaluationInputs inputs = Evaluate(project);

        inputs.RegistryReads.Length.ShouldBe(2);
        foreach (RegistryRead read in inputs.RegistryReads)
        {
            if (value is byte[] bytes)
            {
                read.Value.ShouldBeOfType<ImmutableArray<byte>>().ToArray().ShouldBe(bytes);
            }
            else if (value is string[] strings)
            {
                read.Value.ShouldBeOfType<ImmutableArray<string>>().ToArray().ShouldBe(strings);
            }
            else
            {
                read.Value.ShouldBe(kind == RegistryValueKind.ExpandString ? "expanded" : value);
            }
        }
        inputs.NonCacheable.ShouldBe(NonCacheableReason.RegistryRead);
    }

    [Fact]
    public void RegistryObservationCopiesArraysAndPreservesRepeatedReads()
    {
        var recorder = new EvaluationInputRecorder();
        byte[] bytes = [1, 2];
        string[] strings = ["first", "second"];
        recorder.RecordRegistryRead("key", "bytes", bytes);
        recorder.RecordRegistryRead("key", "strings", strings);
        bytes[0] = 3;
        strings[0] = "changed";
        recorder.RecordRegistryRead("key", "bytes", bytes);
        EvaluationInputKey key = Evaluate(CreateProject("<Project />")).Key;
        EvaluationInputs inputs = recorder.Freeze(key);
        recorder.RecordRegistryRead("key", "after-freeze", "ignored");

        inputs.RegistryReads.Length.ShouldBe(3);
        inputs.RegistryReads[0].Value.ShouldBeOfType<ImmutableArray<byte>>().ToArray().ShouldBe([1, 2]);
        inputs.RegistryReads[1].Value.ShouldBeOfType<ImmutableArray<string>>().ToArray().ShouldBe(["first", "second"]);
        inputs.RegistryReads[2].Value.ShouldBeOfType<ImmutableArray<byte>>().ToArray().ShouldBe([3, 2]);
    }

    [WindowsOnlyFact]
    [SupportedOSPlatform("windows")]
    public void RegistryObservationUsesDecodedKeyAndValueNames()
    {
        TestRegistryKey registry = _env.WithTransientTestState(new TestRegistryKey());
        using RegistryKey nested = registry.Key.CreateSubKey("key@part");
        nested.SetValue("value@part", "value", RegistryValueKind.String);
        string expressionKey = nested.Name.Replace("@", "%40");
        string project = CreateProject($"""
            <Project>
              <PropertyGroup>
                <Value>$(Registry:{expressionKey}@value%40part)</Value>
              </PropertyGroup>
            </Project>
            """);

        RegistryRead read = Evaluate(project).RegistryReads.ShouldHaveSingleItem();

        read.KeyName.ShouldBe(nested.Name);
        read.ValueName.ShouldBe("value@part");
        read.Value.ShouldBe("value");
    }

    [WindowsOnlyFact]
    [SupportedOSPlatform("windows")]
    public void RegistryObservationUsesCoercedValueName()
    {
        TestRegistryKey registry = _env.WithTransientTestState(new TestRegistryKey());
        registry.Key.SetValue("123", "numbered value", RegistryValueKind.String);
        string project = CreateProject($"""
            <Project>
              <PropertyGroup>
                <Value>$([MSBuild]::GetRegistryValue('{registry.Key.Name}', $([System.Int32]::Parse('123')), 'fallback'))</Value>
              </PropertyGroup>
            </Project>
            """);

        ProjectInstance instance = EvaluateWithAndWithoutRecording(project);
        RegistryRead read = instance.EvaluationInputs.ShouldNotBeNull().RegistryReads.ShouldHaveSingleItem();

        instance.GetPropertyValue("Value").ShouldBe("numbered value");
        read.KeyName.ShouldBe(registry.Key.Name);
        read.ValueName.ShouldBe("123");
        read.Value.ShouldBe("numbered value");
    }

    [WindowsOnlyTheory]
    [InlineData("$([MSBuild]::GetRegistryValueFromView('{0}', 'Value', 'fallback'))", "fallback", false)]
    [InlineData("$([MSBuild]::GetRegistryValueFromView('{0}', 'Value', null))", null, false)]
    [InlineData("$([MSBuild]::GetRegistryValueFromView(null, null, 'fallback'))", "fallback", true)]
    [SupportedOSPlatform("windows")]
    public void RegistryRequestsWithoutExplicitViewsPreserveExistingBehavior(string expression, string? expected, bool nullKey)
    {
        TestRegistryKey registry = _env.WithTransientTestState(new TestRegistryKey());
        registry.Key.SetValue("Value", "stored", RegistryValueKind.String);
        expression = string.Format(CultureInfo.InvariantCulture, expression, registry.Key.Name);
        string project = CreateProject($"""
            <Project>
              <PropertyGroup>
                <Value>{expression}</Value>
              </PropertyGroup>
            </Project>
            """);

        ProjectInstance instance = EvaluateWithAndWithoutRecording(project);
        EvaluationInputs inputs = instance.EvaluationInputs.ShouldNotBeNull();
        RegistryRead read = inputs.RegistryReads.ShouldHaveSingleItem();

        instance.GetPropertyValue("Value").ShouldBe(expected ?? string.Empty);
        inputs.NonCacheable.ShouldBe(NonCacheableReason.RegistryRead);
        read.KeyName.ShouldBe(nullKey ? null : registry.Key.Name);
        read.ValueName.ShouldBe(nullKey ? string.Empty : "Value");
        read.Value.ShouldBe(expected);
        read.RequestedViews.ShouldBeEmpty();
    }

    [WindowsOnlyTheory]
    [InlineData(false)]
    [InlineData(true)]
    [SupportedOSPlatform("windows")]
    public void RegistryViewParamsArrayIsNotConfusedWithANestedArray(bool nested)
    {
        TestRegistryKey registry = _env.WithTransientTestState(new TestRegistryKey());
        registry.Key.SetValue("Value", "stored", RegistryValueKind.String);
        string views = "$([System.String]::Copy('RegistryView.Default;RegistryView.Registry32').Split(';'))";
        if (nested)
        {
            views += ", RegistryView.Registry64";
        }
        string project = CreateProject($"""
            <Project>
              <PropertyGroup>
                <Value>$([MSBuild]::GetRegistryValueFromView('{registry.Key.Name}', 'Value', null, {views}))</Value>
              </PropertyGroup>
            </Project>
            """);

        ProjectInstance instance = EvaluateWithAndWithoutRecording(project);
        EvaluationInputs inputs = instance.EvaluationInputs.ShouldNotBeNull();
        RegistryRead read = inputs.RegistryReads.ShouldHaveSingleItem();

        instance.GetPropertyValue("Value").ShouldBe("stored");
        inputs.NonCacheable.ShouldBe(NonCacheableReason.RegistryRead);
        read.Value.ShouldBe("stored");
        read.RequestedViews.ShouldBe(nested
            ? ["RegistryView.Registry64"]
            : ["RegistryView.Default", "RegistryView.Registry32"]);
    }

    [WindowsOnlyFact]
    [SupportedOSPlatform("windows")]
    public void IgnoredRegistryViewDoesNotInvokeToString()
    {
        TestRegistryKey registry = _env.WithTransientTestState(new TestRegistryKey());
        string project = CreateProject($"""
            <Project>
              <PropertyGroup>
                <Value>$([MSBuild]::GetRegistryValueFromView('{registry.Key.Name}', 'Missing', 'fallback', $([System.UriBuilder]::new('http://:x@localhost'))))</Value>
              </PropertyGroup>
            </Project>
            """);

        ProjectInstance instance = EvaluateWithAndWithoutRecording(project);
        EvaluationInputs inputs = instance.EvaluationInputs.ShouldNotBeNull();
        RegistryRead read = inputs.RegistryReads.ShouldHaveSingleItem();

        instance.GetPropertyValue("Value").ShouldBe("fallback");
        inputs.NonCacheable.ShouldBe(NonCacheableReason.RegistryRead);
        read.Value.ShouldBe("fallback");
        read.RequestedViews.ShouldBeEmpty();
    }

    [WindowsOnlyFact]
    [SupportedOSPlatform("windows")]
    public void RegistryFallbackIsCapturedBeforeChainedMutation()
    {
        TestRegistryKey registry = _env.WithTransientTestState(new TestRegistryKey());
        string project = CreateProject($"""
            <Project>
              <PropertyGroup>
                <Value>$([MSBuild]::GetRegistryValue('{registry.Key.Name}', 'Missing', $([System.String]::Copy('ab').ToCharArray())).SetValue($([System.Char]::Parse('z')), 0))</Value>
              </PropertyGroup>
            </Project>
            """);

        ProjectInstance instance = EvaluateWithAndWithoutRecording(project);
        RegistryRead read = instance.EvaluationInputs.ShouldNotBeNull().RegistryReads.ShouldHaveSingleItem();

        read.Value.ShouldBeOfType<ImmutableArray<char>>().ToArray().ShouldBe(['a', 'b']);
    }

    [WindowsOnlyFact]
    [SupportedOSPlatform("windows")]
    public void UnsupportedRegistryFallbackRejectsRecordingWithoutChangingEvaluation()
    {
        TestRegistryKey registry = _env.WithTransientTestState(new TestRegistryKey());
        string project = CreateProject($"""
            <Project>
              <PropertyGroup>
                <Value>$([MSBuild]::GetRegistryValue('{registry.Key.Name}', 'Missing', $([System.UriBuilder]::new('https://example.invalid/path'))))</Value>
              </PropertyGroup>
            </Project>
            """);

        ProjectInstance instance = EvaluateWithAndWithoutRecording(project);
        EvaluationInputs inputs = instance.EvaluationInputs.ShouldNotBeNull();

        inputs.NonCacheable.ShouldBe(NonCacheableReason.UnsupportedRegistryValue);
        inputs.RegistryReads.ShouldBeEmpty();
        IsCurrent(inputs, out _).ShouldBeFalse();
    }

    [Fact]
    public void SdkResolutionIsRecordedAndImportedFilesAreValidated()
    {
        TransientTestFolder sdkFolder = _env.CreateFolder(Path.Combine(_folder.Path, "sdk"), createFolder: true);
        string sdkProps = _env.CreateFile(sdkFolder, "Sdk.props", "<Project><PropertyGroup><FromSdk>props</FromSdk></PropertyGroup></Project>").Path;
        _env.CreateFile(sdkFolder, "Sdk.targets", "<Project />");
        string project = CreateProject("""
            <Project Sdk="TestSdk">
              <PropertyGroup>
                <A>$(FromSdk)</A>
              </PropertyGroup>
            </Project>
            """);
        var properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ResolverProperty"] = "original",
        };
        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ResolverMetadata"] = "original",
        };
        var item = new SdkResultItem("original-item", metadata);
        var items = new Dictionary<string, SdkResultItem>(StringComparer.OrdinalIgnoreCase)
        {
            ["ResolverItem"] = item,
        };
        var environment = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["RESOLVER_ENVIRONMENT"] = "original",
        };
        var recorded = new SdkResult(
            new SdkReference("TestSdk", null, null),
            sdkFolder.Path,
            "1.0",
            warnings: null,
            propertiesToAdd: properties,
            itemsToAdd: items,
            environmentVariablesToAdd: environment);
        ProjectOptions options = SdkUtilities.CreateProjectOptionsWithResolver(new SdkUtilities.ConfigurableMockSdkResolver(recorded));
        options.ProjectCollection = _env.CreateProjectCollection().Collection;

        EvaluationInputs inputs = Evaluate(project, options);
        var directRecorder = new EvaluationInputRecorder();
        directRecorder.RecordSdkResolution(
            recorded.SdkReference,
            recorded,
            ElementLocation.Create(project),
            solutionPath: null,
            project,
            interactive: false,
            isRunningInVisualStudio: false,
            failOnUnresolvedSdk: true);
        EvaluationInputs directlyRecorded = directRecorder.Freeze(inputs.Key);

        inputs.SdkResolutions.ShouldHaveSingleItem().Reference.Name.ShouldBe("TestSdk");
        inputs.Files[sdkProps].Kind.ShouldBe(PathKind.File);
        inputs.SdkResolutions[0].Result.Path.ShouldBe(recorded.Path);
        recorded.AdditionalPaths = [_folder.Path];
        properties["ResolverProperty"] = "changed";
        item.ItemSpec = "changed-item";
        metadata["ResolverMetadata"] = "changed";
        environment["RESOLVER_ENVIRONMENT"] = "changed";
        inputs.SdkResolutions[0].Result.AdditionalPaths.ShouldBeEmpty();
        inputs.SdkResolutions[0].Result.PropertiesToAdd["ResolverProperty"].ShouldBe("original");
        inputs.SdkResolutions[0].Result.ItemsToAdd["ResolverItem"].ItemSpec.ShouldBe("original-item");
        inputs.SdkResolutions[0].Result.ItemsToAdd["ResolverItem"].Metadata["ResolverMetadata"].ShouldBe("original");
        directlyRecorded.SdkResolutions[0].Result.EnvironmentVariablesToAdd["RESOLVER_ENVIRONMENT"].ShouldBe("original");
        IsCurrent(inputs, out _).ShouldBeTrue();

        Touch(sdkProps, "<Project><PropertyGroup><FromSdk>changed</FromSdk></PropertyGroup></Project>");

        IsCurrent(inputs, out string? reason).ShouldBeFalse();
        reason.ShouldBe(sdkProps);
    }

    [Theory]
    [InlineData("<Stamp Include=\"@(Compile->'%(ModifiedTime)')\" />")]
    [InlineData("<Stamp Include=\"@(Compile->ModifiedTime())\" />")]
    [InlineData("<Stamp Include=\"@(Compile)\"><Time>%(CreatedTime)</Time></Stamp>")]
    public void ItemTimestampMetadataIsNotCacheable(string stampItem)
    {
        _env.CreateFile(_folder, "a.cs", string.Empty);
        string project = CreateProject($"""
            <Project>
              <ItemGroup>
                <Compile Include="a.cs" />
                {stampItem}
              </ItemGroup>
            </Project>
            """);

        EvaluationInputs inputs = Evaluate(project);

        inputs.NonCacheable.ShouldBe(NonCacheableReason.ItemTimestampMetadata);
    }

    [Fact]
    public void NamesContainingTimestampModifiersAreCacheable()
    {
        string project = CreateProject("""
            <Project>
              <PropertyGroup>
                <LastModifiedTime>never</LastModifiedTime>
                <Value>$(LastModifiedTime)</Value>
              </PropertyGroup>
              <ItemGroup>
                <Compile Include="a.cs">
                  <CreatedTimeZone>UTC</CreatedTimeZone>
                </Compile>
                <Stamp Include="@(Compile->'%(CreatedTimeZone)')" />
              </ItemGroup>
            </Project>
            """);

        EvaluationInputs inputs = Evaluate(project);

        inputs.NonCacheable.ShouldBe(NonCacheableReason.None);
    }

    [WindowsOnlyFact]
    public void RecorderFailureMarksNonCacheableWithoutChangingEvaluation()
    {
        // Path.GetFullPath rejects paths over 32,767 characters on Windows; File.Exists just returns false.
        string project = CreateProject("""
            <Project>
              <PropertyGroup>
                <Name>a</Name>
                <LongPath>$(Name.PadLeft(40000, 'a'))</LongPath>
                <Found>$([System.IO.File]::Exists('$(LongPath)'))</Found>
              </PropertyGroup>
            </Project>
            """);

        ProjectInstance instance = ProjectInstance.FromFile(project, CreateOptions());

        instance.GetPropertyValue("Found").ShouldBe("False");
        instance.EvaluationInputs.ShouldNotBeNull().NonCacheable.ShouldBe(NonCacheableReason.RecorderFailure);
    }

    [Fact]
    public void ConcurrentEvaluationsOnSharedContextRecordIndependently()
    {
        const string xml = """
            <Project>
              <ItemGroup>
                <Compile Include="*.cs" />
              </ItemGroup>
            </Project>
            """;
        string first = CreateProject(xml, "first.proj");
        TransientTestFolder secondFolder = _env.CreateFolder(createFolder: true);
        string second = _env.CreateFile(secondFolder, "second.proj", xml.Cleanup()).Path;
        EvaluationContext shared = EvaluationContext.Create(EvaluationContext.SharingPolicy.Shared);
        ProjectCollection collection = _env.CreateProjectCollection().Collection;
        var instances = new ProjectInstance[2];

        Parallel.Invoke(
            () => instances[0] = ProjectInstance.FromFile(first, new ProjectOptions { ProjectCollection = collection, EvaluationContext = shared }),
            () => instances[1] = ProjectInstance.FromFile(second, new ProjectOptions { ProjectCollection = collection, EvaluationContext = shared }));

        EvaluationInputs firstInputs = instances[0].EvaluationInputs.ShouldNotBeNull();
        EvaluationInputs secondInputs = instances[1].EvaluationInputs.ShouldNotBeNull();
        firstInputs.Files.ContainsKey(first).ShouldBeTrue();
        firstInputs.Files.ContainsKey(_folder.Path).ShouldBeTrue();
        firstInputs.Files.ContainsKey(second).ShouldBeFalse();
        firstInputs.Files.ContainsKey(secondFolder.Path).ShouldBeFalse();
        secondInputs.Files.ContainsKey(second).ShouldBeTrue();
        secondInputs.Files.ContainsKey(first).ShouldBeFalse();
    }

    [Fact]
    public void InMemoryProjectIsNotCacheable()
    {
        ProjectCollection collection = _env.CreateProjectCollection().Collection;
        ProjectRootElement xml = ProjectRootElement.Create(collection);

        var instance = new ProjectInstance(xml, globalProperties: null, toolsVersion: null, collection);

        instance.EvaluationInputs.ShouldNotBeNull().NonCacheable.ShouldBe(NonCacheableReason.InMemoryProject);
    }

    [Fact]
    public void CommonTargetsProjectIsCacheableAndValidatesAsCurrent()
    {
        string project = CreateCommonTargetsProject();

        EvaluationInputs inputs = Evaluate(project);

        inputs.NonCacheable.ShouldBe(NonCacheableReason.None, inputs.NonCacheableDetail);
        inputs.Files.Count.ShouldBeGreaterThan(5);
        new[]
        {
            "fixture.defaults.props",
            "fixture.conditional.props",
            "fixture.items.props",
            "fixture.build.targets",
            "fixture.compile.targets",
            "fixture.prepare.targets",
        }.ShouldAllBe(path => inputs.Files.ContainsKey(Path.Combine(_folder.Path, path)));
        IsCurrent(inputs, out string? reason).ShouldBeTrue(reason);
        Evaluate(project).Files.Keys.ShouldBe(inputs.Files.Keys, ignoreOrder: true);
    }

    [Fact]
    public void RecordingDoesNotChangeEvaluationResult()
    {
        string project = CreateCommonTargetsProject();
        SetRecording(enabled: false);
        ProjectInstance plain = ProjectInstance.FromFile(project, CreateOptions());
        SetRecording(enabled: true);
        ProjectInstance recorded = ProjectInstance.FromFile(project, CreateOptions());

        plain.EvaluationInputs.ShouldBeNull();
        recorded.EvaluationInputs.ShouldNotBeNull();
        List<string> plainSnapshot = Snapshot(plain);
        List<string> recordedSnapshot = Snapshot(recorded);
        recordedSnapshot.Except(plainSnapshot).ShouldBeEmpty();
        plainSnapshot.Except(recordedSnapshot).ShouldBeEmpty();
    }

    [Fact]
    public void RecordingThroughASharedContextDoesNotChangeEvaluationResult()
    {
        // The recording copy shares the context's glob cache, so the second evaluation reuses the expansions and must still
        // record every traversed directory, replayed from the cache, as a dependency a new file invalidates.
        string project = CreateCommonTargetsProject();
        SetRecording(enabled: false);
        ProjectInstance plain = ProjectInstance.FromFile(project, CreateOptions());
        SetRecording(enabled: true);
        EvaluationContext shared = EvaluationContext.Create(EvaluationContext.SharingPolicy.Shared);
        ProjectInstance first = ProjectInstance.FromFile(project, new ProjectOptions { ProjectCollection = CreateOptions().ProjectCollection, EvaluationContext = shared });
        ProjectInstance second = ProjectInstance.FromFile(project, new ProjectOptions { ProjectCollection = CreateOptions().ProjectCollection, EvaluationContext = shared });

        List<string> plainSnapshot = Snapshot(plain);
        Snapshot(first).Except(plainSnapshot).ShouldBeEmpty();
        Snapshot(second).Except(plainSnapshot).ShouldBeEmpty();
        plainSnapshot.Except(Snapshot(second)).ShouldBeEmpty();
        EvaluationInputs firstInputs = first.EvaluationInputs.ShouldNotBeNull();
        EvaluationInputs secondInputs = second.EvaluationInputs.ShouldNotBeNull();
        secondInputs.NonCacheable.ShouldBe(NonCacheableReason.None);
        secondInputs.Files.Keys.Except(firstInputs.Files.Keys, StringComparer.OrdinalIgnoreCase).ShouldBeEmpty();
        firstInputs.Files.Keys.Except(secondInputs.Files.Keys, StringComparer.OrdinalIgnoreCase).ShouldBeEmpty();
        IsCurrent(secondInputs, out _).ShouldBeTrue();

        AddFile(_folder.Path, "Class2.cs");

        IsCurrent(secondInputs, out string? reason).ShouldBeFalse();
        reason.ShouldBe(_folder.Path);
    }

#if FEATURE_SYMLINK_TARGET
    [RequiresSymbolicLinksFact]
    public void LinkedFileIsNotCacheable()
    {
        string target = _env.CreateFile(_folder, "target.props", "<Project />").Path;
        string link = Path.Combine(_folder.Path, "linked.props");
        File.CreateSymbolicLink(link, target);
        string project = CreateProject("""
            <Project>
              <Import Project="linked.props" Condition="Exists('linked.props')" />
            </Project>
            """);

        EvaluationInputs inputs = Evaluate(project);

        inputs.NonCacheable.ShouldBe(NonCacheableReason.Link);
        inputs.NonCacheableDetail.ShouldBe(link);
    }

    [RequiresSymbolicLinksFact]
    public void LinkedDirectoryIsNotCacheable()
    {
        string target = _env.CreateFolder(Path.Combine(_folder.Path, "target"), createFolder: true).Path;
        File.WriteAllText(Path.Combine(target, "a.cs"), string.Empty);
        string link = Path.Combine(_folder.Path, "linked");
        Directory.CreateSymbolicLink(link, target);
        string project = CreateProject("""
            <Project>
              <ItemGroup>
                <Compile Include="linked\**\*.cs" />
              </ItemGroup>
            </Project>
            """);

        EvaluationInputs inputs = Evaluate(project);

        inputs.NonCacheable.ShouldBe(NonCacheableReason.Link);
        inputs.NonCacheableDetail.ShouldBe(link);
    }
#endif

    [Fact]
    public void ExpandEnvironmentVariablesRecordsEachReferencedVariable()
    {
        _env.SetEnvironmentVariable("MSBUILD_TEST_EXPAND_ROOT", "first");
        _env.SetEnvironmentVariable("MSBUILD_TEST_EXPAND_MISSING", null);
        string project = CreateProject("""
            <Project>
              <PropertyGroup>
                <Value>$([System.Environment]::ExpandEnvironmentVariables('%%MSBUILD_TEST_EXPAND_ROOT%%\src\%MSBUILD_TEST_EXPAND_MISSING%'))</Value>
              </PropertyGroup>
            </Project>
            """);

        EvaluationInputs inputs = Evaluate(project);

        inputs.NonCacheable.ShouldBe(NonCacheableReason.None);
        inputs.EnvironmentReads["MSBUILD_TEST_EXPAND_ROOT"].ShouldBe("first");
        inputs.EnvironmentReads["MSBUILD_TEST_EXPAND_MISSING"].ShouldBeNull();
        inputs.EnvironmentReads.Keys.ShouldBe(
            ["MSBUILD_TEST_EXPAND_ROOT", "MSBUILD_TEST_EXPAND_MISSING"],
            ignoreOrder: true);
        IsCurrent(inputs, out _).ShouldBeTrue();

        _env.SetEnvironmentVariable("MSBUILD_TEST_EXPAND_ROOT", "changed");

        IsCurrent(inputs, out string? reason).ShouldBeFalse();
        reason.ShouldBe("MSBUILD_TEST_EXPAND_ROOT");
    }

    [Fact]
    public void RecordedPathsShareOneStringAcrossEvaluations()
    {
        _env.CreateFile(_folder, "shared.props", "<Project />");
        string project = CreateProject("""
            <Project>
              <Import Project="shared.props" />
              <ItemGroup>
                <None Include="**/*.txt" />
              </ItemGroup>
            </Project>
            """);

        EvaluationInputs first = Evaluate(project);
        EvaluationInputs second = Evaluate(project);

        first.Files.Count.ShouldBeGreaterThan(1);
        foreach (string path in first.Files.Keys)
        {
            string counterpart = second.Files.Keys.Single(key => string.Equals(key, path, StringComparison.OrdinalIgnoreCase));
            ReferenceEquals(path, counterpart).ShouldBeTrue($"{path} was allocated again");
        }
    }

    [Fact]
    public void ItemExistsTransformRecordsProbes()
    {
        _env.CreateFile(_folder, "present.txt", "x");
        string missing = Path.Combine(_folder.Path, "missing.txt");
        string project = CreateProject("""
            <Project>
              <ItemGroup>
                <Candidate Include="present.txt;missing.txt" />
                <Kept Include="@(Candidate->Exists())" />
              </ItemGroup>
            </Project>
            """);

        ProjectInstance instance = ProjectInstance.FromFile(project, CreateOptions());
        EvaluationInputs inputs = instance.EvaluationInputs.ShouldNotBeNull();

        instance.GetItems("Kept").Select(item => item.EvaluatedInclude).ShouldBe(["present.txt"]);
        inputs.NonCacheable.ShouldBe(NonCacheableReason.None);
        inputs.Files[Path.Combine(_folder.Path, "present.txt")].Kind.ShouldBe(PathKind.File);
        inputs.Files[missing].Kind.ShouldBe(PathKind.Missing);
        IsCurrent(inputs, out _).ShouldBeTrue();

        File.WriteAllText(missing, string.Empty);

        IsCurrent(inputs, out string? reason).ShouldBeFalse();
        reason.ShouldBe(missing);
    }

    [Theory]
    [InlineData("Metadata('ModifiedTime')")]
    [InlineData("HasMetadata('CreatedTime')")]
    [InlineData("WithMetadataValue('AccessedTime', '')")]
    [InlineData("WithoutMetadataValue('ModifiedTime', '')")]
    [InlineData("AnyHaveMetadataValue('ModifiedTime', '')")]
    public void TransformsReadingTimestampMetadataByNameAreNotCacheable(string transform)
    {
        _env.CreateFile(_folder, "a.cs", "class A {}");
        string project = CreateProject($$"""
            <Project>
              <ItemGroup>
                <Compile Include="a.cs" />
                <Stamp Include="@(Compile->{{transform}})" />
              </ItemGroup>
            </Project>
            """);

        Evaluate(project).NonCacheable.ShouldBe(NonCacheableReason.ItemTimestampMetadata);
    }

    [Fact]
    public void MetadataReferenceWithWhitespaceReadingTimestampIsNotCacheable()
    {
        _env.CreateFile(_folder, "a.cs", "class A {}");
        string project = CreateProject("""
            <Project>
              <ItemGroup>
                <Compile Include="a.cs" />
                <Stamp Include="@(Compile->'%( ModifiedTime )')" />
              </ItemGroup>
            </Project>
            """);

        Evaluate(project).NonCacheable.ShouldBe(NonCacheableReason.ItemTimestampMetadata);
    }

    [Fact]
    public void RemoveMatchingOnTimestampMetadataIsNotCacheable()
    {
        _env.CreateFile(_folder, "a.cs", "class A {}");
        string project = CreateProject("""
            <Project>
              <ItemGroup>
                <Compile Include="a.cs" />
                <Old Include="a.cs" />
                <Compile Remove="@(Old)" MatchOnMetadata="ModifiedTime" />
              </ItemGroup>
            </Project>
            """);

        Evaluate(project).NonCacheable.ShouldBe(NonCacheableReason.ItemTimestampMetadata);
    }

    [Fact]
    public void ExistsOnLoadedProjectDeletedFromDiskIsNotCacheable()
    {
        ProjectCollection collection = _env.CreateProjectCollection().Collection;
        string loaded = _env.CreateFile(_folder, "loaded.props", "<Project />").Path;
        ProjectRootElement loadedElement = ProjectRootElement.Open(loaded, collection);
        File.Delete(loaded);
        collection.ProjectRootElementCache.TryGet(loaded).ShouldBeSameAs(loadedElement);
        _env.CreateFile(_folder, "other.props", "<Project><PropertyGroup><Loaded>true</Loaded></PropertyGroup></Project>");
        string project = CreateProject("""
            <Project>
              <Import Project="other.props" Condition="Exists('loaded.props')" />
            </Project>
            """);

        var evaluated = new Project(project, globalProperties: null, toolsVersion: null, collection);
        EvaluationInputs inputs = evaluated.EvaluationInputs.ShouldNotBeNull();

        evaluated.GetPropertyValue("Loaded").ShouldBe("true");
        inputs.NonCacheable.ShouldBe(NonCacheableReason.ConflictingObservation);
        inputs.NonCacheableDetail.ShouldBe(loaded);
        GC.KeepAlive(loadedElement);
    }

    [Fact]
    public void ToolLocationHelperProbingCallerPathsIsNotCacheable()
    {
        _env.CreateFile(_folder, "a.txt", "x");
        string project = CreateProject("""
            <Project>
              <PropertyGroup>
                <Root>$([Microsoft.Build.Utilities.ToolLocationHelper]::FindRootFolderWhereAllFilesExist('$(MSBuildProjectDirectory)', 'a.txt'))</Root>
              </PropertyGroup>
            </Project>
            """);

        EvaluationInputs inputs = Evaluate(project);

        inputs.NonCacheable.ShouldBe(NonCacheableReason.UnclassifiedPropertyFunction);
        inputs.NonCacheableDetail.ShouldNotBeNull().ShouldContain("FindRootFolderWhereAllFilesExist");
    }

    [Theory]
    [InlineData(@"custom\root")]
    [InlineData("custom/root")]
    [InlineData("refs")]
    public void ToolLocationHelperGivenARelativeRootIsNotCacheable(string root)
    {
        string project = CreateProject($"""
            <Project>
              <PropertyGroup>
                <Assemblies>$([Microsoft.Build.Utilities.ToolLocationHelper]::GetPathToReferenceAssemblies('.NETFramework', 'v4.7.2', '', '{root}'))</Assemblies>
              </PropertyGroup>
            </Project>
            """);

        ProjectInstance instance = EvaluateWithAndWithoutRecording(project);
        EvaluationInputs inputs = instance.EvaluationInputs.ShouldNotBeNull();

        inputs.NonCacheable.ShouldBe(NonCacheableReason.UnclassifiedPropertyFunction);
        inputs.NonCacheableDetail.ShouldNotBeNull().ShouldContain("GetPathToReferenceAssemblies");
    }

    [Fact]
    public void ToolLocationHelperGivenArrayRootsIsNotCacheable()
    {
        string project = CreateProject("""
            <Project>
              <PropertyGroup>
                <Roots>$(MSBuildProjectDirectory)</Roots>
                <Sdks>$([Microsoft.Build.Utilities.ToolLocationHelper]::GetTargetPlatformSdks($([System.String]::Copy('$(Roots)').Split(';')), ''))</Sdks>
              </PropertyGroup>
            </Project>
            """);

        ProjectInstance instance = EvaluateWithAndWithoutRecording(project);
        EvaluationInputs inputs = instance.EvaluationInputs.ShouldNotBeNull();

        inputs.NonCacheable.ShouldBe(NonCacheableReason.UnclassifiedPropertyFunction);
        inputs.NonCacheableDetail.ShouldNotBeNull().ShouldContain("GetTargetPlatformSdks");
    }

    [Fact]
    public void ThreadWorkingDirectoryIsTheWorkingDirectoryInTheKey()
    {
        // The in-process node gives each build thread its own working directory, which is what Path.GetFullPath resolves against.
        string project = CreateProject("<Project />");
        string? saved = FileUtilities.CurrentThreadWorkingDirectory;
        try
        {
            FileUtilities.CurrentThreadWorkingDirectory = _folder.Path;

            Evaluate(project).Key.WorkingDirectory.ShouldBe(_folder.Path);
        }
        finally
        {
            FileUtilities.CurrentThreadWorkingDirectory = saved;
        }
    }

    [Fact]
    public void RelativeIntrinsicProbeUsesThreadWorkingDirectory()
    {
        string probed = _env.CreateFile(_folder, "relative.txt", string.Empty).Path;
        string project = CreateProject("""
            <Project>
              <PropertyGroup>
                <Found>$([MSBuild]::FileExists('relative.txt'))</Found>
              </PropertyGroup>
            </Project>
            """);
        string? saved = FileUtilities.CurrentThreadWorkingDirectory;
        try
        {
            FileUtilities.CurrentThreadWorkingDirectory = _folder.Path;

            EvaluationInputs inputs = Evaluate(project);

            inputs.Files[probed].Kind.ShouldBe(PathKind.File);
        }
        finally
        {
            FileUtilities.CurrentThreadWorkingDirectory = saved;
        }
    }

    [Fact]
    public void RelativePathProbeUsesThreadWorkingDirectory()
    {
        string probed = _env.CreateFile(_folder, "relative.txt", string.Empty).Path;
        var recorder = new EvaluationInputRecorder();
        string? saved = FileUtilities.CurrentThreadWorkingDirectory;
        try
        {
            FileUtilities.CurrentThreadWorkingDirectory = _folder.Path;

            recorder.RecordPropertyFunction(
                typeof(Path),
                "Exists",
                isInstance: false,
                boundMethod: null,
                arguments: ["relative.txt"],
                result: true);

            recorder.Freeze(Evaluate(CreateProject("<Project />")).Key).Files[probed].Kind.ShouldBe(PathKind.File);
        }
        finally
        {
            FileUtilities.CurrentThreadWorkingDirectory = saved;
        }
    }

    [Fact]
    public void ToolLocationHelperInstalledStateIsCacheable()
    {
        string project = CreateProject("""
            <Project>
              <PropertyGroup>
                <Libraries>$([Microsoft.Build.Utilities.ToolLocationHelper]::GetPathToStandardLibraries('.NETFramework', 'v4.7.2', ''))</Libraries>
              </PropertyGroup>
            </Project>
            """);

        Evaluate(project).NonCacheable.ShouldBe(NonCacheableReason.None);
    }

    [Fact]
    public void HostDirectoryCacheIsNotCacheable()
    {
        string project = CreateProject("<Project />");
        ProjectOptions options = CreateOptions();
        options.DirectoryCacheFactory = new PassThroughDirectoryCache();

        Evaluate(project, options).NonCacheable.ShouldBe(NonCacheableReason.HostFileSystem);
    }

    [Fact]
    public void HostFileSystemIsNotCacheable()
    {
        string project = CreateProject("<Project />");
        ProjectOptions options = CreateOptions();
        options.EvaluationContext = EvaluationContext.Create(EvaluationContext.SharingPolicy.Shared, new PassThroughFileSystem());

        Evaluate(project, options).NonCacheable.ShouldBe(NonCacheableReason.HostFileSystem);
    }

    [Theory]
    [InlineData("MsBuildCacheFileExistence")]
    [InlineData("MsBuildCacheFileEnumerations")]
    public void ProcessWideFileCachesAreNotCacheable(string variable)
    {
        _env.SetEnvironmentVariable(variable, "1");
        Traits.UpdateFromEnvironment();
        string project = CreateProject("<Project />");

        Evaluate(project).NonCacheable.ShouldBe(NonCacheableReason.ProcessWideCache);
    }

    [Fact]
    public void WorkingDirectoryIsInTheKeySoRelativeGetFullPathIsCacheable()
    {
        string project = CreateProject("""
            <Project>
              <PropertyGroup>
                <Relative>$([System.IO.Path]::GetFullPath('a.txt'))</Relative>
              </PropertyGroup>
            </Project>
            """);

        EvaluationInputs inputs = Evaluate(project);

        inputs.NonCacheable.ShouldBe(NonCacheableReason.None);
        inputs.Key.WorkingDirectory.ShouldBe(Directory.GetCurrentDirectory());
    }

    [Fact]
    public void ParserConfigurationIsRecordedAndInTheKey()
    {
        // The parser skips what Directory.Parse.config allows, so the files are inputs and their content is in the key.
        string project = CreateProject("<Project />");
        EvaluationInputs without = Evaluate(project);
        string config = _env.CreateFile(_folder, ParserIgnoreConfiguration.ConfigFileName, """<ParseConfig><IgnoreAttributes><Ignore Element="Target" Name="Foo" /></IgnoreAttributes></ParseConfig>""").Path;
        _env.SetEnvironmentVariable(ParserIgnoreConfiguration.EnvironmentVariableName, config);

        EvaluationInputs with = Evaluate(project);

        with.NonCacheable.ShouldBe(NonCacheableReason.None);
        with.Files[config].Kind.ShouldBe(PathKind.File);
        with.Key.ParserConfigurationFingerprint.ShouldNotBe(without.Key.ParserConfigurationFingerprint);
        IsCurrent(with, out _).ShouldBeTrue();

        Touch(config, """<ParseConfig><IgnoreAttributes><Ignore Element="Target" Name="Bar" /></IgnoreAttributes></ParseConfig>""");

        IsCurrent(with, out string? reason).ShouldBeFalse();
        reason.ShouldBe(config);
    }

    [Fact]
    public void KeysOfEqualEvaluationsAreEqual()
    {
        string project = CreateProject("<Project />");
        ProjectOptions options = CreateOptions();
        options.GlobalProperties = new Dictionary<string, string> { ["Configuration"] = "Release", ["Platform"] = "x64" };

        EvaluationInputKey first = Evaluate(project, options).Key;
        EvaluationInputKey second = Evaluate(project, options).Key;

        second.ShouldBe(first);
        second.GetHashCode().ShouldBe(first.GetHashCode());
    }

    [Fact]
    public void KeyDistinguishesToolsetsWithTheSameVersion()
    {
        string project = CreateProject("<Project />");

        EvaluationInputKey first = EvaluateWithToolsetProperty(project, "1").Key;
        EvaluationInputKey same = EvaluateWithToolsetProperty(project, "1").Key;
        EvaluationInputKey other = EvaluateWithToolsetProperty(project, "2").Key;

        same.ShouldBe(first);
        other.ShouldNotBe(first);
    }

    private EvaluationInputs EvaluateWithToolsetProperty(string project, string value)
    {
        ProjectCollection collection = _env.CreateProjectCollection().Collection;
        Toolset current = collection.GetToolset(ObjectModelHelpers.MSBuildDefaultToolsVersion);
        collection.AddToolset(new Toolset("Custom", current.ToolsPath, new Dictionary<string, string> { ["Contoso"] = value }, collection, msbuildOverrideTasksPath: null));
        return Evaluate(project, new ProjectOptions { ProjectCollection = collection, ToolsVersion = "Custom" });
    }

    private string CreateCommonTargetsProject()
    {
        _env.CreateFile(_folder, "Class1.cs", "class C {}");
        _env.CreateFile(_folder, "fixture.items.props", """
            <Project>
              <ItemDefinitionGroup>
                <Compile>
                  <FixtureMetadata>$(FixtureConditional)</FixtureMetadata>
                </Compile>
              </ItemDefinitionGroup>
            </Project>
            """);
        _env.CreateFile(_folder, "fixture.conditional.props", """
            <Project>
              <PropertyGroup>
                <FixtureConditional Condition="'$(FixtureDefault)' == 'DefaultValue'">ConditionalValue</FixtureConditional>
              </PropertyGroup>
              <Import Project="fixture.items.props" />
            </Project>
            """);
        _env.CreateFile(_folder, "fixture.defaults.props", """
            <Project>
              <PropertyGroup>
                <FixtureDefault Condition="'$(FixtureDefault)' == ''">DefaultValue</FixtureDefault>
              </PropertyGroup>
              <Import Project="fixture.conditional.props" />
            </Project>
            """);
        _env.CreateFile(_folder, "fixture.prepare.targets", """
            <Project>
              <Target Name="PrepareFixture" />
            </Project>
            """);
        _env.CreateFile(_folder, "fixture.compile.targets", """
            <Project>
              <Import Project="fixture.prepare.targets" />
              <Target Name="CompileFixture" DependsOnTargets="PrepareFixture" />
            </Project>
            """);
        _env.CreateFile(_folder, "fixture.build.targets", """
            <Project>
              <Import Project="fixture.compile.targets" />
              <Target Name="Build" DependsOnTargets="CompileFixture" />
            </Project>
            """);

        return CreateProject("""
            <Project>
              <Import Project="fixture.defaults.props" />
              <ItemGroup>
                <Compile Include="**/*.cs" />
              </ItemGroup>
              <Import Project="fixture.build.targets" />
            </Project>
            """);
    }

    /// <summary>
    /// Evaluated properties, items with their metadata, item definitions, imports, and targets, without the environment
    /// variable that turns recording on.
    /// </summary>
    private static List<string> Snapshot(ProjectInstance instance)
    {
        List<string> snapshot = [];
        foreach (ProjectPropertyInstance property in instance.Properties)
        {
            if (!string.Equals(property.Name, EnableVariable, StringComparison.OrdinalIgnoreCase))
            {
                snapshot.Add($"{property.Name}={property.EvaluatedValue}");
            }
        }

        foreach (ProjectItemInstance item in instance.Items)
        {
            snapshot.Add($"{item.ItemType}:{item.EvaluatedInclude}");
            foreach (ProjectMetadataInstance metadata in item.Metadata)
            {
                snapshot.Add($"{item.ItemType}:{item.EvaluatedInclude}:{metadata.Name}={metadata.EvaluatedValue}");
            }
        }

        foreach (KeyValuePair<string, ProjectItemDefinitionInstance> definition in instance.ItemDefinitions)
        {
            foreach (ProjectMetadataInstance metadata in definition.Value.Metadata)
            {
                snapshot.Add($"definition {definition.Key}:{metadata.Name}={metadata.EvaluatedValue}");
            }
        }

        foreach (string import in instance.ImportPaths)
        {
            snapshot.Add($"import {import}");
        }

        foreach (string target in instance.Targets.Keys)
        {
            snapshot.Add($"target {target}");
        }

        return snapshot;
    }

    private ProjectInstance EvaluateWithAndWithoutRecording(string project)
    {
        SetRecording(enabled: false);
        ProjectInstance plain = ProjectInstance.FromFile(project, CreateOptions());
        SetRecording(enabled: true);
        ProjectInstance recorded = ProjectInstance.FromFile(project, CreateOptions());

        Snapshot(recorded).ShouldBe(Snapshot(plain), ignoreOrder: true);
        return recorded;
    }

    private void SetRecording(bool enabled)
    {
        _env.SetEnvironmentVariable(EnableVariable, enabled ? "1" : null);
        Traits.UpdateFromEnvironment();
    }

    private string CreateProject(string xml, string name = "test.proj") =>
        _env.CreateFile(_folder, name, xml.Cleanup()).Path;

    private ProjectOptions CreateOptions() =>
        new() { ProjectCollection = _env.CreateProjectCollection().Collection };

    private EvaluationInputs Evaluate(string project, ProjectOptions? options = null)
    {
        ProjectInstance instance = ProjectInstance.FromFile(project, options ?? CreateOptions());
        EvaluationInputs inputs = instance.EvaluationInputs.ShouldNotBeNull();
        _output.WriteLine($"{inputs.Files.Count} files, non-cacheable: {inputs.NonCacheable} {inputs.NonCacheableDetail}");
        return inputs;
    }

    private static bool IsCurrent(EvaluationInputs inputs, out string? reason) =>
        EvaluationInputValidator.IsFileSystemCurrent(inputs, out reason);

    /// <summary>
    /// Rewrites a file and moves its timestamp forward so the change is visible on file systems with coarse timestamps.
    /// </summary>
    private static void Touch(string path, string contents)
    {
        File.WriteAllText(path, contents);
        File.SetLastWriteTimeUtc(path, File.GetLastWriteTimeUtc(path).AddSeconds(2));
    }

    private static void RewriteWithDifferentLengthPreservingTimestamp(string path, string contents, DateTime timestamp, long originalLength)
    {
        File.WriteAllText(path, contents);
        File.SetLastWriteTimeUtc(path, timestamp);
        File.GetLastWriteTimeUtc(path).ShouldBe(timestamp);
        new FileInfo(path).Length.ShouldNotBe(originalLength);
    }

    /// <summary>
    /// Adds a file to a directory, moving its timestamp forward explicitly on file systems with coarse timestamps.
    /// </summary>
    private static void AddFile(string directory, string name)
    {
        DateTime before = Directory.GetLastWriteTimeUtc(directory);
        File.WriteAllText(Path.Combine(directory, name), string.Empty);
        if (Directory.GetLastWriteTimeUtc(directory) == before)
        {
            Directory.SetLastWriteTimeUtc(directory, before.AddSeconds(2));
        }
    }

    [SupportedOSPlatform("windows")]
    private sealed class TestRegistryKey : TransientTestState
    {
        private readonly string _subKey = $@"Software\MSBuild_EvaluationInputs_{Guid.NewGuid():N}";

        internal TestRegistryKey()
        {
            Key = Registry.CurrentUser.CreateSubKey(_subKey);
        }

        internal RegistryKey Key { get; }

        public override void Revert()
        {
            Key.Dispose();
            Registry.CurrentUser.DeleteSubKeyTree(_subKey, throwOnMissingSubKey: false);
        }
    }

    private sealed class PassThroughFileSystem : MSBuildFileSystemBase
    {
    }

    private sealed class PassThroughDirectoryCache : IDirectoryCacheFactory, IDirectoryCache
    {
        public IDirectoryCache GetDirectoryCacheForEvaluation(int evaluationId) => this;

        public bool FileExists(string path) => File.Exists(path);

        public bool DirectoryExists(string path) => Directory.Exists(path);

        public IEnumerable<TResult> EnumerateFiles<TResult>(string path, string pattern, FindPredicate predicate, FindTransform<TResult> transform) =>
            Select(Directory.EnumerateFiles(path, pattern), predicate, transform);

        public IEnumerable<TResult> EnumerateDirectories<TResult>(string path, string pattern, FindPredicate predicate, FindTransform<TResult> transform) =>
            Select(Directory.EnumerateDirectories(path, pattern), predicate, transform);

        private static List<TResult> Select<TResult>(IEnumerable<string> paths, FindPredicate predicate, FindTransform<TResult> transform)
        {
            List<TResult> results = [];
            foreach (string path in paths)
            {
                ReadOnlySpan<char> name = Path.GetFileName(path).AsSpan();
                if (predicate(ref name))
                {
                    results.Add(transform(ref name));
                }
            }

            return results;
        }
    }
}
