// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.Build.BackEnd.SdkResolution;
using Microsoft.Build.Collections;
using Microsoft.Build.Construction;
using Microsoft.Build.Definition;
using Microsoft.Build.Engine.UnitTests.InstanceFromRemote;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Evaluation.Context;
using Microsoft.Build.Exceptions;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Shared.FileSystem;
using Microsoft.Build.Unittest;
using Shouldly;
using Xunit;
using SdkResult = Microsoft.Build.BackEnd.SdkResolution.SdkResult;

#nullable disable

namespace Microsoft.Build.UnitTests.Definition
{
    public class EvaluationObservation_Tests : IDisposable
    {
        private readonly SdkUtilities.ConfigurableMockSdkResolver _resolver;
        private readonly TestEnvironment _env;
        private readonly ITestOutputHelper _output;

        public EvaluationObservation_Tests(ITestOutputHelper output)
        {
            _output = output;
            _env = TestEnvironment.Create(_output);
            _resolver = new SdkUtilities.ConfigurableMockSdkResolver(
                new Dictionary<string, SdkResult>
                {
                    {"foo", new SdkResult(new SdkReference("foo", "1.0.0", null), "path", "1.0.0", null) },
                    {"bar", new SdkResult(new SdkReference("bar", "1.0.0", null), "path", "1.0.0", null) }
                });
        }

        public void Dispose()
        {
            _env.Dispose();
        }

        private static void SetResolverForContext(EvaluationContext context, SdkResolver resolver)
        {
            var sdkService = (SdkResolverService)context.SdkResolverService;
            sdkService.InitializeForTests(null, new List<SdkResolver> { resolver });
        }

        [Fact]
        public void EvaluationObservationCompletionFreezesOneReport()
        {
            EvaluationObservationSession session = EvaluationObservationSession.CreateForTests();
            string path = Path.Combine(_env.DefaultTestDirectory.Path, "before.marker");
            session.RecordRequest(new EvaluationRequestObservation { ProjectPath = "before" });
            session.RecordProbe(path, EvaluationPathKind.File, exists: true);

            EvaluationObservationReport report = session.Complete(evaluationSucceeded: true);

            session.RecordRequest(new EvaluationRequestObservation { ProjectPath = "after" });
            session.RecordProbe(
                Path.Combine(_env.DefaultTestDirectory.Path, "after.marker"),
                EvaluationPathKind.File,
                exists: false);

            report.Request.ProjectPath.ShouldBe("before");
            report.PathProbes.ShouldHaveSingleItem().Path.ShouldBe(path);
            session.Complete(evaluationSucceeded: true).ShouldBeNull();
            session.TestOnlyRetainedObservationCount.ShouldBe(0);
            session.TestOnlyObservationCollectionsDetached.ShouldBeTrue();
        }

        [Fact]
        public void EvaluationObservationMarksConflictingProbeResults()
        {
            EvaluationObservationSession session = EvaluationObservationSession.CreateForTests();
            string path = Path.Combine(_env.DefaultTestDirectory.Path, "probe.marker");

            session.RecordProbe(path, EvaluationPathKind.File, exists: false);
            session.RecordProbe(path, EvaluationPathKind.File, exists: true);

            EvaluationObservationReport report = session.Complete(evaluationSucceeded: true);

            (report.Reasons & EvaluationObservationReason.ConflictingObservation)
                .ShouldBe(EvaluationObservationReason.ConflictingObservation);
        }

        [Fact]
        public void RecordingFileSystemMarksReadAndMetadataFailures()
        {
            EvaluationObservationSession session = EvaluationObservationSession.CreateForTests();
            var fileSystem = new RecordingFileSystem(new ThrowingReadAndMetadataFileSystem(), session);
            string readPath = Path.Combine(_env.DefaultTestDirectory.Path, "read.txt");
            string metadataPath = Path.Combine(_env.DefaultTestDirectory.Path, "metadata.txt");

            Should.Throw<IOException>(() => fileSystem.ReadFileAllText(readPath));
            Should.Throw<IOException>(() => fileSystem.GetAttributes(metadataPath));

            EvaluationObservationReport report = session.Complete(evaluationSucceeded: false);

            report.OperationFailures.ShouldContain(failure =>
                failure.Category == EvaluationObservationCategory.FileContent &&
                failure.Operation == nameof(IFileSystem.ReadFileAllText) &&
                failure.Path == readPath);
            report.OperationFailures.ShouldContain(failure =>
                failure.Category == EvaluationObservationCategory.FileMetadata &&
                failure.Operation == nameof(IFileSystem.GetAttributes) &&
                failure.Path == metadataPath);
            report.Categories.ShouldContain(observation =>
                observation.Category == EvaluationObservationCategory.FileContent &&
                observation.State == EvaluationObservationCategoryState.Incomplete);
            report.Categories.ShouldContain(observation =>
                observation.Category == EvaluationObservationCategory.FileMetadata &&
                observation.State == EvaluationObservationCategoryState.Incomplete);
        }

        [Fact]
        public void EvaluationObservationCanBeDisabled()
        {
            int reportsCreated = 0;
            using IDisposable scope = EvaluationObservationSession.TestOnlyConfigure(
                enabled: false,
                _ => reportsCreated++);

            string projectFile = _env.CreateFile(
                "disabled.proj",
                """
                <Project>
                  <PropertyGroup Condition="Exists('disabled.marker')">
                    <Observed>true</Observed>
                  </PropertyGroup>
                </Project>
                """.Cleanup()).Path;

            Project.FromFile(projectFile, new ProjectOptions
            {
                ProjectCollection = _env.CreateProjectCollection().Collection,
            });

            reportsCreated.ShouldBe(0);
        }

        [Fact]
        public void EvaluationObservationDoesNotChangeEvaluatedState()
        {
            _env.SetEnvironmentVariable("MsBuildCacheFileExistence", null);
            _env.SetEnvironmentVariable("MsBuildCacheFileEnumerations", null);
            _env.SetEnvironmentVariable("OBSERVATION_EQUIVALENCE_ENV", "equivalent");
            _env.CreateFile("state.marker", string.Empty);
            _env.CreateFile("State.cs", string.Empty);
            _env.CreateFile("state.txt", "state-content");
            string importedProject = _env.CreateFile(
                "state.props",
                """
                <Project>
                  <PropertyGroup>
                    <ImportedState>imported</ImportedState>
                  </PropertyGroup>
                </Project>
                """.Cleanup()).Path;
            string projectFile = _env.CreateFile(
                "state.proj",
                """
                <Project>
                  <Import Project="state.props" />
                  <Import Project="state.props" />
                  <PropertyGroup Condition="Exists('state.marker')">
                    <Observed>true</Observed>
                    <Environment>$(OBSERVATION_EQUIVALENCE_ENV)</Environment>
                    <Content>$([System.IO.File]::ReadAllText('$(MSBuildThisFileDirectory)state.txt'))</Content>
                    <EscapedProperty>property%3Bvalue</EscapedProperty>
                  </PropertyGroup>
                  <ItemDefinitionGroup>
                    <Ordered>
                      <Inherited>definition</Inherited>
                      <Override>default</Override>
                    </Ordered>
                  </ItemDefinitionGroup>
                  <ItemGroup>
                    <Compile Include="*.cs" />
                    <Input Include="state.txt" />
                    <MetadataValue Include="@(Input->'%(ModifiedTime)')" />
                    <Ordered Include="first"><Position>1</Position></Ordered>
                    <Ordered Include="second"><Position>2</Position><Override>item</Override></Ordered>
                    <Ordered Include="first"><Position>3</Position></Ordered>
                    <Escaped Include="semi%3Bcolon"><EscapedMetadata>metadata%3Bvalue</EscapedMetadata></Escaped>
                  </ItemGroup>
                </Project>
                """.Cleanup()).Path;

            Project baseline;
            using (EvaluationObservationSession.TestOnlyConfigure(enabled: false))
            {
                baseline = Project.FromFile(projectFile, new ProjectOptions
                {
                    ProjectCollection = _env.CreateProjectCollection().Collection,
                    LoadSettings = ProjectLoadSettings.RecordDuplicateButNotCircularImports,
                });
            }

            EvaluationObservationReport report = null;
            Project observed;
            using (EvaluationObservationSession.TestOnlyConfigure(
                enabled: true,
                createdReport => report = createdReport))
            {
                observed = Project.FromFile(projectFile, new ProjectOptions
                {
                    ProjectCollection = _env.CreateProjectCollection().Collection,
                    LoadSettings = ProjectLoadSettings.RecordDuplicateButNotCircularImports,
                });
            }

            report.ShouldNotBeNull();
            report.ProjectSources.ShouldNotBeEmpty();
            baseline.ImportsIncludingDuplicates
                .Select(static import => import.ImportedProject.FullPath)
                .ShouldBe([importedProject, importedProject]);
            baseline.GetItems("Ordered")
                .Select(static item => item.EvaluatedInclude)
                .ShouldBe(["first", "second", "first"]);
            baseline.GetItems("Ordered")
                .Select(static item => item.GetMetadataValue("Position"))
                .ShouldBe(["1", "2", "3"]);
            baseline.GetItems("Ordered")
                .Select(static item => item.GetMetadataValue("Inherited"))
                .ShouldBe(["definition", "definition", "definition"]);
            baseline.GetItems("Ordered")
                .Select(static item => item.GetMetadataValue("Override"))
                .ShouldBe(["default", "item", "default"]);
            ((IProperty)baseline.GetProperty("EscapedProperty"))
                .EvaluatedValueEscaped.ShouldBe("property%3Bvalue");
            ProjectItem escapedItem = baseline.GetItems("Escaped").ShouldHaveSingleItem();
            ((IItem)escapedItem).EvaluatedIncludeEscaped.ShouldBe("semi%3Bcolon");
            ((IItem)escapedItem).GetMetadataValueEscaped("EscapedMetadata").ShouldBe("metadata%3Bvalue");

            AssertEquivalentEvaluatedState(baseline, observed);
        }

        [Fact]
        public void EvaluationObservationRecordsProbesAndGlobs()
        {
            EvaluationObservationReport report = null;
            using IDisposable scope = EvaluationObservationSession.TestOnlyConfigure(
                enabled: true,
                createdReport => report = createdReport);

            _env.CreateFile("Observed.cs", string.Empty);
            string importedProject = _env.CreateFile(
                "observed.props",
                """
                <Project>
                  <PropertyGroup>
                    <ImportedValue>true</ImportedValue>
                  </PropertyGroup>
                </Project>
                """.Cleanup()).Path;
            string projectFile = _env.CreateFile(
                "observed.proj",
                """
                <Project>
                  <Import Project="observed.props" />
                  <PropertyGroup Condition="Exists('missing.props')">
                    <Imported>true</Imported>
                  </PropertyGroup>
                  <ItemGroup>
                    <Compile Include="*.cs" />
                  </ItemGroup>
                </Project>
                """.Cleanup()).Path;

            Project project = Project.FromFile(projectFile, new ProjectOptions
            {
                ProjectCollection = _env.CreateProjectCollection().Collection,
            });

            report.ShouldNotBeNull();
            report.ProjectPath.ShouldBe(projectFile);
            report.HasBlockingObservations.ShouldBeTrue();
            report.EvaluationSucceeded.ShouldBeTrue();
            (report.Reasons & EvaluationObservationReason.ParsedProjectSourceOnly)
                .ShouldBe(EvaluationObservationReason.None);
            report.Request.ProjectPath.ShouldBe(projectFile);
            report.Request.EngineVersion.ShouldNotBe(report.Request.EngineAssemblyVersion);
            Assembly engineAssembly = typeof(Project).Assembly;
            report.Request.EngineVersion.ShouldBe(
                engineAssembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ??
                System.Diagnostics.FileVersionInfo.GetVersionInfo(engineAssembly.Location).FileVersion);
            report.Request.EngineAssemblyVersion.ShouldBe(engineAssembly.GetName().Version?.ToString());
            report.Request.Runtime.ShouldBe(
                System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription);
            report.Request.OperatingSystem.ShouldBe(
                System.Runtime.InteropServices.RuntimeInformation.OSDescription);
            report.Request.ProcessArchitecture.ShouldBe(
                System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString());
            report.Request.PathComparison.ShouldBe(FileUtilities.PathComparison.ToString());
            report.ProjectSources.ShouldContain(observation =>
                observation.Role == EvaluationProjectSourceRole.Root &&
                observation.Outcome == EvaluationProjectSourceOutcome.Parsed &&
                FileUtilities.PathsEqual(observation.Path, projectFile) &&
                observation.HashKind == EvaluationContentHashKind.RawBytes &&
                observation.ContentHash == EvaluationObservationSession.ComputeBytesHash(File.ReadAllBytes(projectFile)));
            report.ProjectSources.ShouldContain(observation =>
                observation.Role == EvaluationProjectSourceRole.Import &&
                observation.Outcome == EvaluationProjectSourceOutcome.Parsed &&
                FileUtilities.PathsEqual(observation.Path, importedProject) &&
                observation.ContentHash == EvaluationObservationSession.ComputeBytesHash(File.ReadAllBytes(importedProject)));

            string missingPath = Path.Combine(_env.DefaultTestDirectory.Path, "missing.props");
            report.PathProbes.ShouldContain(observation =>
                FileUtilities.PathsEqual(observation.Path, missingPath) &&
                observation.Kind == EvaluationPathKind.FileOrDirectory &&
                !observation.Exists);

            string observedFile = Path.Combine(_env.DefaultTestDirectory.Path, "Observed.cs");
            report.Globs.ShouldContain(observation =>
                observation.Include == "*.cs" &&
                observation.Results.Any(entry => entry.EndsWith("Observed.cs", StringComparison.OrdinalIgnoreCase)));
            report.DirectoryEnumerations.ShouldBeEmpty(
                string.Join(
                    Environment.NewLine,
                    report.DirectoryEnumerations.Select(observation =>
                        string.Concat(observation.Kind, "|", observation.Path, "|", observation.SearchPattern))));

            project.GetItems("Compile").ShouldContain(item =>
                string.Equals(Path.GetFileName(item.EvaluatedInclude), "Observed.cs", StringComparison.OrdinalIgnoreCase));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void EvaluationObservationRecordsMalformedImportBytes(bool ignoreInvalidImport)
        {
            string malformedImport = Path.Combine(
                _env.DefaultTestDirectory.Path,
                "malformed.props");
            byte[] malformedBytes = Encoding.UTF8.GetBytes(
                "<?xml version=\"1.0\" encoding=\"windows-1252\"?>" +
                "<Project><PropertyGroup><Value>before</Value></Project>" +
                new string('x', 128 * 1024));
            File.WriteAllBytes(malformedImport, malformedBytes);
            string expectedHash = EvaluationObservationSession.ComputeBytesHash(malformedBytes);
            string projectFile = _env.CreateFile(
                "malformed-import.proj",
                """
                <Project>
                  <Import Project="malformed.props" />
                </Project>
                """.Cleanup()).Path;
            EvaluationObservationReport report = null;
            using IDisposable scope = EvaluationObservationSession.TestOnlyConfigure(
                enabled: true,
                createdReport => report = createdReport);

            Action evaluate = () => Project.FromFile(projectFile, new ProjectOptions
            {
                ProjectCollection = _env.CreateProjectCollection().Collection,
                LoadSettings = ignoreInvalidImport
                    ? ProjectLoadSettings.IgnoreInvalidImports
                    : ProjectLoadSettings.Default,
            });
            if (ignoreInvalidImport)
            {
                Should.NotThrow(evaluate);
            }
            else
            {
                Should.Throw<InvalidProjectFileException>(evaluate);
            }

            report.ShouldNotBeNull();
            report.EvaluationSucceeded.ShouldBe(ignoreInvalidImport);
            EvaluationProjectSourceObservation source = report.ProjectSources.Single(
                observation => FileUtilities.PathsEqual(observation.Path, malformedImport));
            source.Role.ShouldBe(EvaluationProjectSourceRole.Import);
            source.Outcome.ShouldBe(EvaluationProjectSourceOutcome.ParseFailure);
            source.Version.ShouldBe(0);
            source.ContentHash.ShouldBe(expectedHash);
            source.HashKind.ShouldBe(EvaluationContentHashKind.RawBytes);
            source.Encoding.ShouldBe(
                "windows-1252",
                StringCompareShould.IgnoreCase);
            source.Provider.ShouldBe("Disk");
            source.HasLastWriteTimeUtc.ShouldBeTrue();
            source.TimestampWasStableDuringRead.ShouldBeTrue();
            report.FileReads.ShouldContain(observation =>
                FileUtilities.PathsEqual(observation.Path, malformedImport) &&
                observation.ContentHash == expectedHash &&
                observation.HashKind == EvaluationContentHashKind.RawBytes &&
                observation.IsVerifiable);
            EvaluationOperationFailureObservation failure =
                report.OperationFailures.Single(
                    observation => FileUtilities.PathsEqual(observation.Path, malformedImport));
            failure.Category.ShouldBe(EvaluationObservationCategory.ProjectSource);
            failure.Operation.ShouldBe("ProjectSource.Parse");
            failure.Provider.ShouldBe("Disk");
            failure.ExceptionType.ShouldBe(typeof(XmlException).FullName);
            (report.Reasons & EvaluationObservationReason.ExternalOperationFailure)
                .ShouldBe(EvaluationObservationReason.ExternalOperationFailure);
            (report.Reasons & EvaluationObservationReason.ProjectXmlContentNotObserved)
                .ShouldBe(EvaluationObservationReason.None);
            report.Categories.Single(observation =>
                observation.Category == EvaluationObservationCategory.ProjectSource)
                .State.ShouldBe(EvaluationObservationCategoryState.Incomplete);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void EvaluationObservationRecordsMalformedRootBytes(bool useProjectInstance)
        {
            string malformedRoot = Path.Combine(
                _env.DefaultTestDirectory.Path,
                "malformed-root.proj");
            byte[] malformedBytes = Encoding.UTF8.GetBytes(
                "<Project><PropertyGroup><Value>before</Value></Project>" +
                new string('x', 128 * 1024));
            File.WriteAllBytes(malformedRoot, malformedBytes);
            string expectedHash = EvaluationObservationSession.ComputeBytesHash(malformedBytes);
            var reports = new List<EvaluationObservationReport>();
            using IDisposable scope = EvaluationObservationSession.TestOnlyConfigure(
                enabled: true,
                reports.Add);
            var options = new ProjectOptions
            {
                ProjectCollection = _env.CreateProjectCollection().Collection,
            };

            Action load = useProjectInstance
                ? () => ProjectInstance.FromFile(malformedRoot, options)
                : () => Project.FromFile(malformedRoot, options);

            Should.Throw<InvalidProjectFileException>(load);

            EvaluationObservationReport report = reports.ShouldHaveSingleItem();
            report.ProjectPath.ShouldBe(malformedRoot);
            report.EvaluationSucceeded.ShouldBeFalse();
            report.Request.ShouldBeNull();
            EvaluationProjectSourceObservation source =
                report.ProjectSources.ShouldHaveSingleItem();
            source.Role.ShouldBe(EvaluationProjectSourceRole.Root);
            source.Outcome.ShouldBe(EvaluationProjectSourceOutcome.ParseFailure);
            source.ContentHash.ShouldBe(expectedHash);
            source.HashKind.ShouldBe(EvaluationContentHashKind.RawBytes);
            source.Encoding.ShouldBe(Encoding.UTF8.WebName);
            source.Provider.ShouldBe("Disk");
            report.FileReads.ShouldContain(observation =>
                FileUtilities.PathsEqual(observation.Path, malformedRoot) &&
                observation.ContentHash == expectedHash &&
                observation.HashKind == EvaluationContentHashKind.RawBytes &&
                observation.IsVerifiable);
            EvaluationOperationFailureObservation failure =
                report.OperationFailures.ShouldHaveSingleItem();
            failure.Category.ShouldBe(EvaluationObservationCategory.ProjectSource);
            failure.Operation.ShouldBe("ProjectSource.Parse");
            failure.Path.ShouldBe(malformedRoot);
            failure.ExceptionType.ShouldBe(typeof(XmlException).FullName);
            report.Categories.Single(observation =>
                observation.Category == EvaluationObservationCategory.Request)
                .State.ShouldBe(EvaluationObservationCategoryState.Incomplete);
            report.Categories.Single(observation =>
                observation.Category == EvaluationObservationCategory.ProjectSource)
                .State.ShouldBe(EvaluationObservationCategoryState.Incomplete);
        }

        [Fact]
        public void ProjectInstanceGlobDoesNotRetainSupportingEnumerations()
        {
            EvaluationObservationReport report = null;
            using IDisposable scope = EvaluationObservationSession.TestOnlyConfigure(
                enabled: true,
                createdReport => report = createdReport);

            TransientTestFolder sourceFolder = _env.CreateFolder(
                Path.Combine(_env.DefaultTestDirectory.Path, "project-instance-src"));
            _env.CreateFile(sourceFolder, "ProjectInstance.cs", string.Empty);
            string projectFile = _env.CreateFile(
                "project-instance-glob.proj",
                """
                <Project>
                  <ItemGroup>
                    <Compile Include="project-instance-src/**/*.cs" />
                  </ItemGroup>
                </Project>
                """.Cleanup()).Path;

            ProjectInstance.FromFile(projectFile, new ProjectOptions
            {
                ProjectCollection = _env.CreateProjectCollection().Collection,
            });

            report.ShouldNotBeNull();
            report.Globs.ShouldHaveSingleItem();
            report.DirectoryEnumerations.ShouldBeEmpty(
                string.Join(
                    Environment.NewLine,
                    report.DirectoryEnumerations.Select(observation =>
                        string.Concat(observation.Kind, "|", observation.Path, "|", observation.SearchPattern))));
        }

        [Fact]
        public void EvaluationObservationSummaryModeRetainsFingerprintsWithoutMemberArrays()
        {
            EvaluationObservationReport report = null;
            using IDisposable scope = EvaluationObservationSession.TestOnlyConfigure(
                enabled: true,
                createdReport => report = createdReport,
                retainDetails: false);

            TransientTestFolder sourceFolder = _env.CreateFolder(
                Path.Combine(_env.DefaultTestDirectory.Path, "summary-src"));
            _env.CreateFile(sourceFolder, "Summary.cs", string.Empty);
            _env.CreateFile("summary.marker", string.Empty);
            string projectFile = _env.CreateFile(
                "summary.proj",
                """
                <Project>
                  <PropertyGroup>
                    <Above>$([MSBuild]::GetPathOfFileAbove('summary.marker', '$(MSBuildThisFileDirectory)'))</Above>
                  </PropertyGroup>
                  <ItemGroup>
                    <Compile Include="summary-src/**/*.cs" />
                  </ItemGroup>
                </Project>
                """.Cleanup()).Path;

            Project.FromFile(projectFile, new ProjectOptions
            {
                ProjectCollection = _env.CreateProjectCollection().Collection,
            });

            report.ShouldNotBeNull();
            EvaluationGlobObservation glob = report.Globs.ShouldHaveSingleItem();
            glob.Results.ShouldBeEmpty();
            glob.ResultCount.ShouldBe(1);
            glob.ResultsFingerprint.ShouldNotBeNullOrEmpty();
            EvaluationSearchObservation search = report.Searches.Single(
                observation => observation.Kind == "GetPathOfFileAbove");
            search.Candidates.ShouldBeEmpty();
            search.CandidateCount.ShouldBeGreaterThan(0);
            search.CandidatesFingerprint.ShouldNotBeNullOrEmpty();
        }

        [Fact]
        public void EvaluationObservationSeparatesPathCalculationsFromFileMetadata()
        {
            string missingChild = Path.Combine(_env.DefaultTestDirectory.Path, "missing", "child");
            EvaluationObservationReport report = null;
            using IDisposable scope = EvaluationObservationSession.TestOnlyConfigure(
                enabled: true,
                createdReport => report = createdReport);
            string projectFile = _env.CreateFile(
                "path-calculations.proj",
                $"""
                <Project>
                  <PropertyGroup>
                    <Parent>$([System.IO.Directory]::GetParent('{missingChild}'))</Parent>
                    <ParentFullName>$([System.IO.Directory]::GetParent('{missingChild}').FullName)</ParentFullName>
                    <ParentName>$([System.IO.Directory]::GetParent('{missingChild}').Name)</ParentName>
                    <GrandParent>$([System.IO.Directory]::GetParent('{missingChild}').Parent.FullName)</GrandParent>
                  </PropertyGroup>
                  <ItemGroup>
                    <Ghost Include="ghost.txt" />
                    <GhostFullPath Include="@(Ghost->'%(FullPath)')" />
                    <GhostRootDirectory Include="@(Ghost->'%(RootDir)')" />
                    <GhostRelativeDirectory Include="@(Ghost->'%(RelativeDir)')" />
                    <GhostDirectory Include="@(Ghost->'%(Directory)')" />
                  </ItemGroup>
                </Project>
                """.Cleanup()).Path;

            Project project = Project.FromFile(projectFile, new ProjectOptions
            {
                ProjectCollection = _env.CreateProjectCollection().Collection,
            });

            project.GetPropertyValue("Parent").ShouldBe(Path.GetDirectoryName(missingChild));
            report.ShouldNotBeNull();
            report.MetadataReads.ShouldBeEmpty();
            report.PropertyFunctions.ShouldContain(observation =>
                observation.ReceiverType == typeof(Directory).FullName &&
                observation.Member == nameof(Directory.GetParent) &&
                observation.Effects == EvaluationPropertyFunctionEffect.Ambient);
            report.PropertyFunctions.ShouldContain(observation =>
                observation.ReceiverType == typeof(DirectoryInfo).FullName &&
                observation.Member == nameof(DirectoryInfo.FullName) &&
                observation.Effects == EvaluationPropertyFunctionEffect.Ambient);
            report.PropertyFunctions.ShouldContain(observation =>
                observation.ReceiverType == typeof(DirectoryInfo).FullName &&
                observation.Member == nameof(DirectoryInfo.Parent) &&
                observation.Effects == EvaluationPropertyFunctionEffect.Ambient);
            report.ExternalInputs.ShouldContain(observation =>
                observation.Kind == EvaluationExternalInputKind.Ambient &&
                observation.Operation == $"{typeof(Directory).FullName}::{nameof(Directory.GetParent)}");
            report.ExternalInputs.ShouldContain(observation =>
                observation.Kind == EvaluationExternalInputKind.Ambient &&
                observation.Operation == $"{typeof(DirectoryInfo).FullName}::{nameof(DirectoryInfo.FullName)}");
            foreach (string modifier in new[] { "FullPath", "RootDir", "RelativeDir", "Directory" })
            {
                report.ExternalInputs.ShouldContain(observation =>
                    observation.Kind == EvaluationExternalInputKind.Ambient &&
                    observation.Operation == $"ItemMetadata::{modifier}" &&
                    observation.Request.IndexOf("ItemSpec=ghost.txt", StringComparison.Ordinal) >= 0);
            }

            report.Categories.Single(observation =>
                observation.Category == EvaluationObservationCategory.FileMetadata)
                .State.ShouldBe(EvaluationObservationCategoryState.NotExercised);
        }

        [Fact]
        public void EvaluationObservationCanonicalizesRelativePropertyFunctionPaths()
        {
            string root = _env.CreateFolder().Path;
            string inputPath = _env.CreateFile(Path.Combine(root, "relative.txt"), "content").Path;
            string enumerationRoot = _env.CreateFolder(Path.Combine(root, "enum")).Path;
            string topFile = _env.CreateFile(Path.Combine(enumerationRoot, "top.txt"), string.Empty).Path;
            string nestedDirectory = _env.CreateFolder(Path.Combine(enumerationRoot, "nested")).Path;
            string nestedFile = _env.CreateFile(Path.Combine(nestedDirectory, "nested.txt"), string.Empty).Path;
            _env.SetCurrentDirectory(root);
            EvaluationObservationReport report = null;
            using IDisposable scope = EvaluationObservationSession.TestOnlyConfigure(
                enabled: true,
                createdReport => report = createdReport);
            string projectFile = _env.CreateFile(
                Path.Combine(root, "relative-paths.proj"),
                """
                <Project>
                  <PropertyGroup>
                    <Read>$([System.IO.File]::ReadAllText('relative.txt'))</Read>
                    <Exists>$([System.IO.File]::Exists('relative.txt'))</Exists>
                    <WriteTime>$([System.IO.File]::GetLastWriteTimeUtc('relative.txt'))</WriteTime>
                  </PropertyGroup>
                  <ItemGroup>
                    <Files Include="$([System.IO.Directory]::GetFiles('enum', '*.txt', 'System.IO.SearchOption.AllDirectories'))" />
                    <Input Include="relative.txt" />
                    <Modified Include="@(Input->'%(ModifiedTime)')" />
                  </ItemGroup>
                </Project>
                """.Cleanup()).Path;

            Project project = Project.FromFile(projectFile, new ProjectOptions
            {
                ProjectCollection = _env.CreateProjectCollection().Collection,
            });

            project.GetPropertyValue("Read").ShouldBe("content");
            report.ShouldNotBeNull();
            report.FileReads.ShouldContain(observation =>
                FileUtilities.PathsEqual(observation.Path, inputPath) &&
                observation.HashKind == EvaluationContentHashKind.DecodedText);
            report.FileReads.ShouldNotContain(observation => observation.Path == "relative.txt");
            report.PathProbes.ShouldContain(observation =>
                FileUtilities.PathsEqual(observation.Path, inputPath) &&
                observation.Kind == EvaluationPathKind.File);
            report.MetadataReads.Count(observation =>
                FileUtilities.PathsEqual(observation.Path, inputPath)).ShouldBe(2);
            EvaluationDirectoryEnumerationObservation enumeration =
                report.DirectoryEnumerations.ShouldHaveSingleItem();
            FileUtilities.PathsEqual(enumeration.Path, enumerationRoot).ShouldBeTrue();
            enumeration.Entries.ShouldBe(
                [topFile, nestedFile],
                ignoreOrder: true);
            report.PropertyFunctions.ShouldContain(observation =>
                observation.ReceiverType == typeof(File).FullName &&
                observation.Member == nameof(File.ReadAllText) &&
                observation.Arguments.ShouldHaveSingleItem() == "relative.txt");
            report.PropertyFunctions.ShouldContain(observation =>
                observation.ReceiverType == typeof(Directory).FullName &&
                observation.Member == nameof(Directory.GetFiles) &&
                observation.Arguments[0] == "enum");
            (report.Reasons & EvaluationObservationReason.UnrootedPath)
                .ShouldBe(EvaluationObservationReason.None);
        }

        [WindowsOnlyFact]
        public void EvaluationObservationUnifiesExtendedDrivePathIdentity()
        {
            string root = _env.CreateFolder().Path;
            string inputPath = _env.CreateFile(Path.Combine(root, "input.txt"), "content").Path;
            string extendedPath = $@"\\?\{inputPath}";
            EvaluationObservationReport report = null;
            using IDisposable scope = EvaluationObservationSession.TestOnlyConfigure(
                enabled: true,
                createdReport => report = createdReport);
            string projectFile = _env.CreateFile(
                Path.Combine(root, "extended-path.proj"),
                $"""
                <Project>
                  <PropertyGroup>
                    <Normal>$([System.IO.File]::ReadAllText('{inputPath}'))</Normal>
                    <Extended>$([System.IO.File]::ReadAllText('{extendedPath}'))</Extended>
                  </PropertyGroup>
                </Project>
                """.Cleanup()).Path;

            Project project = Project.FromFile(projectFile, new ProjectOptions
            {
                ProjectCollection = _env.CreateProjectCollection().Collection,
            });

            project.GetPropertyValue("Normal").ShouldBe("content");
            project.GetPropertyValue("Extended").ShouldBe("content");
            report.ShouldNotBeNull();
            report.FileReads.Count(observation =>
                observation.HashKind == EvaluationContentHashKind.DecodedText &&
                FileUtilities.PathsEqual(observation.Path, inputPath)).ShouldBe(1);
            report.FileReads.ShouldNotContain(observation =>
                observation.Path.StartsWith(@"\\?\", StringComparison.Ordinal));
            report.PropertyFunctions.Count(observation =>
                observation.ReceiverType == typeof(File).FullName &&
                observation.Member == nameof(File.ReadAllText)).ShouldBe(2);
            (report.Reasons & EvaluationObservationReason.ConflictingObservation)
                .ShouldBe(EvaluationObservationReason.None);
        }

        [WindowsOnlyFact]
        public void EvaluationObservationNormalizesOnlyEquivalentExtendedNamespaces()
        {
            FileUtilities.NormalizePathForObservation(@"\\?\C:\root\file.txt")
                .ShouldBe(@"C:\root\file.txt");
            FileUtilities.NormalizePathForObservation(@"\\?\UNC\server\share\file.txt")
                .ShouldBe(@"\\server\share\file.txt");
            FileUtilities.NormalizePathForObservation(@"\\?\Volume{00000000-0000-0000-0000-000000000000}\file.txt")
                .ShouldBe(@"\\?\Volume{00000000-0000-0000-0000-000000000000}\file.txt");
            FileUtilities.NormalizePathForObservation(@"\\.\pipe\name")
                .ShouldBe(@"\\.\pipe\name");
        }

        [Fact]
        public void EvaluationObservationRecordsEnvironmentAndPropertyFunctions()
        {
            _env.SetEnvironmentVariable("OBSERVED_ENVIRONMENT_INPUT", "environment-value");
            string projectSettingsPath = _env.CreateFile("settings.txt", "settings-value").Path;
            string currentDirectory = _env.CreateFolder(
                Path.Combine(
                    _env.DefaultTestDirectory.Path,
                    "ambient-current-directory")).Path;
            string currentDirectorySettingsPath = _env.CreateFile(
                Path.Combine(currentDirectory, "settings.txt"),
                "current-directory-settings").Path;
            File.SetLastWriteTime(
                currentDirectorySettingsPath,
                DateTime.Now.AddHours(-2));
            string expectedModifiedTime = File.GetLastWriteTime(currentDirectorySettingsPath)
                .ToString(FileUtilities.FileTimeFormat);
            _env.SetCurrentDirectory(currentDirectory);

            EvaluationObservationReport report = null;
            using IDisposable scope = EvaluationObservationSession.TestOnlyConfigure(
                enabled: true,
                createdReport => report = createdReport);

            string projectFile = _env.CreateFile(
                "ambient.proj",
                """
                <Project>
                  <PropertyGroup>
                    <Imported>$(OBSERVED_ENVIRONMENT_INPUT)</Imported>
                    <Missing>$(OBSERVED_MISSING_ENVIRONMENT_INPUT)</Missing>
                    <Live>$([System.Environment]::GetEnvironmentVariable('OBSERVED_ENVIRONMENT_INPUT'))</Live>
                    <Settings>$([System.IO.File]::ReadAllText('$(MSBuildThisFileDirectory)settings.txt'))</Settings>
                    <Above>$([MSBuild]::GetPathOfFileAbove('settings.txt', '$(MSBuildThisFileDirectory)'))</Above>
                    <Formatted>$([System.String]::Format('{0}', 'formatted'))</Formatted>
                    <Volatile>$([System.DateTime]::utcnow)</Volatile>
                  </PropertyGroup>
                  <ItemGroup>
                    <Input Include="settings.txt" />
                    <MetadataValue Include="@(Input->'%(ModifiedTime)')" />
                    <Missing Include="missing.txt" />
                    <MissingMetadataValue Include="@(Missing->'%(ModifiedTime)')" />
                  </ItemGroup>
                </Project>
                """.Cleanup()).Path;

            Project project = Project.FromFile(projectFile, new ProjectOptions
            {
                ProjectCollection = _env.CreateProjectCollection().Collection,
            });

            project.GetPropertyValue("Imported").ShouldBe("environment-value");
            project.GetPropertyValue("Live").ShouldBe("environment-value");
            project.GetPropertyValue("Settings").ShouldBe("settings-value");
            report.ShouldNotBeNull();
            report.Environment.ShouldContain(observation =>
                observation.Name == "OBSERVED_ENVIRONMENT_INPUT" &&
                observation.Source == EvaluationEnvironmentSource.Imported &&
                observation.Value == "environment-value");
            report.Environment.ShouldContain(observation =>
                observation.Name == "OBSERVED_ENVIRONMENT_INPUT" &&
                observation.Source == EvaluationEnvironmentSource.LiveProcess &&
                observation.Value == "environment-value");
            report.Environment.ShouldContain(observation =>
                observation.Name == "OBSERVED_MISSING_ENVIRONMENT_INPUT" &&
                observation.Source == EvaluationEnvironmentSource.MissingImported &&
                !observation.Present);
            report.PropertyFunctions.ShouldContain(observation =>
                observation.ReceiverType == typeof(Environment).FullName &&
                observation.Member == nameof(Environment.GetEnvironmentVariable) &&
                (observation.Effects & EvaluationPropertyFunctionEffect.Environment) != 0);
            report.PropertyFunctions.ShouldContain(observation =>
                observation.ReceiverType == typeof(DateTime).FullName &&
                string.Equals(observation.Member, nameof(DateTime.UtcNow), StringComparison.OrdinalIgnoreCase) &&
                (observation.Effects & EvaluationPropertyFunctionEffect.Volatile) != 0);
            report.PropertyFunctions.ShouldContain(observation =>
                observation.ReceiverType == typeof(string).FullName &&
                observation.Member == nameof(string.Format) &&
                (observation.Effects & EvaluationPropertyFunctionEffect.Ambient) != 0);
            report.FileReads.ShouldContain(observation =>
                observation.Path.EndsWith("settings.txt", StringComparison.OrdinalIgnoreCase) &&
                observation.IsVerifiable);
            report.Searches.ShouldContain(observation =>
                observation.Kind == "GetPathOfFileAbove" &&
                observation.Candidates.Any(candidate =>
                    candidate.EndsWith("settings.txt", StringComparison.OrdinalIgnoreCase)) &&
                observation.SelectedPaths.Length == 1 &&
                observation.SelectedPaths[0].EndsWith("settings.txt", StringComparison.OrdinalIgnoreCase));
            report.MetadataReads.ShouldContain(observation =>
                observation.Kind == EvaluationMetadataKind.ItemModifiedTime &&
                FileUtilities.PathsEqual(
                    observation.Path,
                    currentDirectorySettingsPath) &&
                FileUtilities.PathsEqual(observation.BaseDirectory, currentDirectory) &&
                observation.TextValue == expectedModifiedTime);
            report.MetadataReads.ShouldNotContain(observation =>
                observation.Kind == EvaluationMetadataKind.ItemModifiedTime &&
                FileUtilities.PathsEqual(observation.Path, projectSettingsPath));
            report.MetadataReads.ShouldContain(observation =>
                observation.Kind == EvaluationMetadataKind.ItemModifiedTime &&
                FileUtilities.PathsEqual(
                    observation.Path,
                    Path.Combine(currentDirectory, "missing.txt")) &&
                FileUtilities.PathsEqual(observation.BaseDirectory, currentDirectory) &&
                observation.TextValue == string.Empty);
            (report.Reasons & EvaluationObservationReason.UnsupportedVolatileInput)
                .ShouldBe(EvaluationObservationReason.UnsupportedVolatileInput);
            (report.Reasons & EvaluationObservationReason.UnrootedPath)
                .ShouldBe(EvaluationObservationReason.None);
            report.Categories.ShouldContain(observation =>
                observation.Category == EvaluationObservationCategory.PropertyFunction &&
                observation.State == EvaluationObservationCategoryState.Observed);
            report.SchemaVersion.ShouldBe(17);
            report.PropertyFunctionClassificationVersion.ShouldBeGreaterThan(0);
            report.Request.PathComparison.ShouldBe(FileUtilities.PathComparison.ToString());
        }

        [Fact]
        public void EvaluationObservationMarksSourceTimestampChangeDuringReadIncomplete()
        {
            EvaluationObservationReport report = null;
            using IDisposable scope = EvaluationObservationSession.TestOnlyConfigure(
                enabled: true,
                createdReport => report = createdReport);
            string projectFile = _env.CreateFile("timestamp-race.proj", "<Project />").Path;
            DateTime initialTime = DateTime.UtcNow.AddMinutes(-10);
            File.SetLastWriteTimeUtc(projectFile, initialTime);
            ProjectRootElement.TestOnlyHookAfterSourceRead =
                path => File.SetLastWriteTimeUtc(path, initialTime.AddMinutes(1));

            try
            {
                Project.FromFile(projectFile, new ProjectOptions
                {
                    ProjectCollection = _env.CreateProjectCollection().Collection,
                });
            }
            finally
            {
                ProjectRootElement.TestOnlyHookAfterSourceRead = null;
            }

            EvaluationProjectSourceObservation root = report.ProjectSources.Single(
                observation => observation.Role == EvaluationProjectSourceRole.Root);
            root.TimestampWasStableDuringRead.ShouldBeFalse();
            (report.Reasons & EvaluationObservationReason.ProjectSourceChangedDuringRead)
                .ShouldBe(EvaluationObservationReason.ProjectSourceChangedDuringRead);
            report.Categories.ShouldContain(observation =>
                observation.Category == EvaluationObservationCategory.ProjectSource &&
                observation.State == EvaluationObservationCategoryState.Incomplete);
        }

        [Fact]
        public void EvaluationObservationInvalidatesDiskSourceHashAfterInMemoryMutation()
        {
            EvaluationObservationReport report = null;
            using IDisposable scope = EvaluationObservationSession.TestOnlyConfigure(
                enabled: true,
                createdReport => report = createdReport);

            string projectFile = _env.CreateFile("mutated-source.proj", "<Project />").Path;
            ProjectRootElement root = ProjectRootElement.Open(projectFile);
            root.AddProperty("Mutated", "true");

            Project.FromProjectRootElement(root, new ProjectOptions
            {
                ProjectCollection = _env.CreateProjectCollection().Collection,
            });

            report.ShouldNotBeNull();
            EvaluationProjectSourceObservation source = report.ProjectSources.Single(
                observation => observation.Role == EvaluationProjectSourceRole.Root);
            source.ContentHash.ShouldBe(EvaluationObservationSession.ComputeTextHash(root.RawXml));
            report.FileReads.ShouldContain(observation =>
                FileUtilities.PathsEqual(observation.Path, projectFile) &&
                observation.HashKind == EvaluationContentHashKind.ParsedXml &&
                !observation.IsVerifiable);
            (report.Reasons & EvaluationObservationReason.UnversionedProjectRootElementCache)
                .ShouldBe(EvaluationObservationReason.UnversionedProjectRootElementCache);
        }

        [Fact]
        public void EvaluationObservationUsesLinkedProjectVersionAsAuthoritativeIdentity()
        {
            EvaluationObservationSession session = EvaluationObservationSession.CreateForTests();
            string projectFile = Path.Combine(_env.DefaultTestDirectory.Path, "linked.proj");
            var root = new ProjectRootElement(new FakeProjectRootElementLink(projectFile));

            session.RecordProjectSource(root, EvaluationProjectSourceRole.Root);
            EvaluationObservationReport report = session.Complete(evaluationSucceeded: true);

            EvaluationProjectSourceObservation source = report.ProjectSources.ShouldHaveSingleItem();
            source.Version.ShouldBe(7);
            source.ContentHash.ShouldBeNull();
            source.Provider.ShouldContain(nameof(FakeProjectRootElementLink));
            source.HasLastWriteTimeUtc.ShouldBeFalse();
            source.TimestampWasStableDuringRead.ShouldBeTrue();
            (report.Reasons & EvaluationObservationReason.ParsedProjectSourceOnly)
                .ShouldBe(EvaluationObservationReason.None);
            (report.Reasons & EvaluationObservationReason.UnversionedProjectRootElementCache)
                .ShouldBe(EvaluationObservationReason.None);
        }

        [Fact]
        public void EvaluationObservationMarksXmlReaderSourceWithoutHostIdentityIncomplete()
        {
            EvaluationObservationSession session = EvaluationObservationSession.CreateForTests();
            using var reader = XmlReader.Create(new StringReader("<Project />"));
            ProjectRootElement root = ProjectRootElement.Create(
                reader,
                _env.CreateProjectCollection().Collection);

            session.RecordProjectSource(root, EvaluationProjectSourceRole.Root);
            EvaluationObservationReport report = session.Complete(evaluationSucceeded: true);

            report.ProjectSources.ShouldHaveSingleItem().Provider.ShouldBe("XmlReader");
            report.ProjectSources.ShouldHaveSingleItem().HasLastWriteTimeUtc.ShouldBeFalse();
            (report.Reasons & EvaluationObservationReason.UnversionedSourceProvider)
                .ShouldBe(EvaluationObservationReason.UnversionedSourceProvider);
        }

        [Fact]
        public void EvaluationObservationMarksUnrestrictedFileSystemSideEffectsUnsupported()
        {
            EvaluationObservationReport report = null;
            using IDisposable scope = EvaluationObservationSession.TestOnlyConfigure(
                enabled: true,
                createdReport => report = createdReport);

            TransientTestFolder projectDirectory = _env.CreateFolder(
                Path.Combine(_env.DefaultTestDirectory.Path, "side-effect-project"));
            string projectFile = _env.CreateFile(
                projectDirectory,
                "side-effect.proj",
                """
                <Project>
                  <PropertyGroup>
                    <Created>$([System.IO.Directory]::GetParent('$(MSBuildThisFileDirectory)').CreateSubdirectory('side-effect-created'))</Created>
                  </PropertyGroup>
                </Project>
                """.Cleanup()).Path;

            Project project = Project.FromFile(projectFile, new ProjectOptions
            {
                ProjectCollection = _env.CreateProjectCollection().Collection,
            });

            Directory.Exists(project.GetPropertyValue("Created")).ShouldBeTrue();
            report.ShouldNotBeNull();
            report.PropertyFunctions.ShouldContain(observation =>
                observation.Member == "CreateSubdirectory" &&
                (observation.Effects & EvaluationPropertyFunctionEffect.SideEffect) != 0 &&
                (observation.Effects & EvaluationPropertyFunctionEffect.OpaqueUnsupported) != 0);
            (report.Reasons & EvaluationObservationReason.EvaluationSideEffect)
                .ShouldBe(EvaluationObservationReason.EvaluationSideEffect);
            report.Categories.ShouldContain(observation =>
                observation.Category == EvaluationObservationCategory.VolatileOrSideEffect &&
                observation.State == EvaluationObservationCategoryState.Unsupported);
        }

        [Fact]
        public void EvaluationObservationMarksEnableAllPropertyFunctionsUnsupported()
        {
            _env.WithTransientTestState(
                new TransientAppContextSwitch("Microsoft.Build.EnableAllPropertyFunctions", value: true));

            EvaluationObservationReport report = null;
            using IDisposable scope = EvaluationObservationSession.TestOnlyConfigure(
                enabled: true,
                createdReport => report = createdReport);

            string projectFile = _env.CreateFile("enable-all.proj", "<Project />").Path;
            Project.FromFile(projectFile, new ProjectOptions
            {
                ProjectCollection = _env.CreateProjectCollection().Collection,
            });

            report.ShouldNotBeNull();
            (report.Reasons & EvaluationObservationReason.AllPropertyFunctionsEnabled)
                .ShouldBe(EvaluationObservationReason.AllPropertyFunctionsEnabled);
            report.Categories.ShouldContain(observation =>
                observation.Category == EvaluationObservationCategory.PropertyFunction &&
                observation.State == EvaluationObservationCategoryState.Unsupported);
        }

#if NET
        [Fact]
        public void EvaluationObservationFailsClosedForUnclassifiedKnownTypeMember()
        {
            EvaluationObservationReport report = null;
            using IDisposable scope = EvaluationObservationSession.TestOnlyConfigure(
                enabled: true,
                createdReport => report = createdReport);

            string projectFile = _env.CreateFile(
                "unclassified-property-function.proj",
                """
                <Project>
                  <PropertyGroup>
                    <Relative>$([System.IO.Path]::GetRelativePath('a', 'b'))</Relative>
                  </PropertyGroup>
                </Project>
                """.Cleanup()).Path;

            Project.FromFile(projectFile, new ProjectOptions
            {
                ProjectCollection = _env.CreateProjectCollection().Collection,
            });

            report.ShouldNotBeNull();
            report.PropertyFunctions.ShouldContain(observation =>
                observation.ReceiverType == typeof(Path).FullName &&
                observation.Member == nameof(Path.GetRelativePath) &&
                (observation.Effects & EvaluationPropertyFunctionEffect.OpaqueUnsupported) != 0);
            (report.Reasons & EvaluationObservationReason.UnclassifiedPropertyFunction)
                .ShouldBe(EvaluationObservationReason.UnclassifiedPropertyFunction);
        }
#endif

        [Fact]
        public void EvaluationObservationRecordsTypedPropertyFunctionFailure()
        {
            string root = _env.CreateFolder().Path;
            string missingPath = Path.Combine(root, "missing.txt");
            _env.SetCurrentDirectory(root);
            EvaluationObservationReport report = null;
            using IDisposable scope = EvaluationObservationSession.TestOnlyConfigure(
                enabled: true,
                createdReport => report = createdReport);
            string projectFile = _env.CreateFile(
                Path.Combine(root, "failed-read.proj"),
                """
                <Project>
                  <PropertyGroup>
                    <Missing>$([System.IO.File]::ReadAllText('missing.txt'))</Missing>
                  </PropertyGroup>
                </Project>
                """.Cleanup()).Path;

            Should.Throw<InvalidProjectFileException>(() =>
                Project.FromFile(projectFile, new ProjectOptions
                {
                    ProjectCollection = _env.CreateProjectCollection().Collection,
                }));

            report.ShouldNotBeNull();
            EvaluationOperationFailureObservation failure =
                report.OperationFailures.ShouldHaveSingleItem();
            failure.Category.ShouldBe(EvaluationObservationCategory.FileContent);
            failure.Operation.ShouldBe($"{typeof(File).FullName}::{nameof(File.ReadAllText)}");
            failure.Path.ShouldBe(missingPath);
            failure.Provider.ShouldBe(FileSystems.Default.GetType().AssemblyQualifiedName);
            failure.ExceptionType.ShouldBe(typeof(FileNotFoundException).FullName);
            failure.HResult.ShouldNotBe(0);
            failure.Message.ShouldNotBeNullOrEmpty();
            report.FileReads.ShouldNotContain(observation =>
                FileUtilities.PathsEqual(observation.Path, missingPath));
            report.Categories.Single(observation =>
                observation.Category == EvaluationObservationCategory.FileContent)
                .State.ShouldBe(EvaluationObservationCategoryState.Incomplete);
        }

        [Fact]
        public void EvaluationObservationFailureRecordingCannotReplaceEvaluationFailure()
        {
            EvaluationObservationSession session = EvaluationObservationSession.CreateForTests();

            Should.NotThrow(() =>
                session.RecordPropertyFunctionFailure(
                    typeof(File),
                    nameof(File.ReadAllText),
                    instance: null,
                    [new ThrowingStringValue()],
                    _env.DefaultTestDirectory.Path,
                    new IOException("Original evaluation failure.")));

            EvaluationObservationReport report = session.Complete(evaluationSucceeded: false);

            report.OperationFailures.ShouldBeEmpty();
            (report.Reasons & EvaluationObservationReason.ObservationIncomplete)
                .ShouldBe(EvaluationObservationReason.ObservationIncomplete);
            (report.Reasons & EvaluationObservationReason.ExternalOperationFailure)
                .ShouldBe(EvaluationObservationReason.ExternalOperationFailure);
        }

        [Fact]
        public void EvaluationObservationRecordsParserConfigurationInputs()
        {
            string parserConfig = _env.CreateFile(
                "Directory.Parse.config",
                """
                <ParseConfig />
                """).Path;
            _env.SetEnvironmentVariable(ParserIgnoreConfiguration.EnvironmentVariableName, parserConfig);

            EvaluationObservationReport report = null;
            using IDisposable scope = EvaluationObservationSession.TestOnlyConfigure(
                enabled: true,
                createdReport => report = createdReport);

            string projectFile = _env.CreateFile("parser.proj", "<Project />").Path;
            Project.FromFile(projectFile, new ProjectOptions
            {
                ProjectCollection = _env.CreateProjectCollection().Collection,
            });

            report.ShouldNotBeNull();
            report.Environment.ShouldContain(observation =>
                observation.Name == ParserIgnoreConfiguration.EnvironmentVariableName &&
                observation.Value == parserConfig);
            report.FileReads.ShouldContain(observation =>
                FileUtilities.PathsEqual(observation.Path, parserConfig) &&
                observation.IsVerifiable &&
                observation.HashKind == EvaluationContentHashKind.RawBytes &&
                observation.ContentHash == EvaluationObservationSession.ComputeBytesHash(
                    File.ReadAllBytes(parserConfig)));
            report.PathProbes.ShouldContain(observation =>
                FileUtilities.PathsEqual(observation.Path, parserConfig) &&
                observation.Kind == EvaluationPathKind.File &&
                observation.Exists);
            report.ExternalInputs.ShouldContain(observation =>
                observation.Kind == EvaluationExternalInputKind.ParserConfiguration &&
                observation.Operation == "ParseOutcome" &&
                FileUtilities.PathsEqual(observation.Request, parserConfig) &&
                observation.Result == "ParsedParseConfig");
        }

        [WindowsOnlyFact]
        public void EvaluationObservationRecordsRegistryFunctions()
        {
            EvaluationObservationReport report = null;
            using IDisposable scope = EvaluationObservationSession.TestOnlyConfigure(
                enabled: true,
                createdReport => report = createdReport);

            string projectFile = _env.CreateFile(
                "registry.proj",
                """
                <Project>
                  <PropertyGroup>
                    <RegistryValue>$([MSBuild]::GetRegistryValue('HKEY_CURRENT_USER\Software\MSBuildObservationMissing', 'Value', 'fallback'))</RegistryValue>
                  </PropertyGroup>
                </Project>
                """.Cleanup()).Path;

            Project project = Project.FromFile(projectFile, new ProjectOptions
            {
                ProjectCollection = _env.CreateProjectCollection().Collection,
            });

            project.GetPropertyValue("RegistryValue").ShouldBeEmpty();
            report.ShouldNotBeNull();
            report.ExternalInputs.ShouldContain(observation =>
                observation.Kind == EvaluationExternalInputKind.Registry &&
                observation.Operation == "GetRegistryValue" &&
                string.IsNullOrEmpty(observation.Result));
        }

        [Fact]
        public void EvaluationObservationRecordsSdkResolverAndUsingTask()
        {
            EvaluationObservationReport report = null;
            using IDisposable scope = EvaluationObservationSession.TestOnlyConfigure(
                enabled: true,
                createdReport => report = createdReport);

            string projectFile = _env.CreateFile(
                "sdk-and-task.proj",
                """
                <Project Sdk="foo">
                  <UsingTask TaskName="ObservedTask" AssemblyFile="observed-task.dll" />
                </Project>
                """.Cleanup()).Path;

            EvaluationContext context = EvaluationContext.Create(EvaluationContext.SharingPolicy.Isolated);
            SetResolverForContext(context, _resolver);
            Project.FromFile(projectFile, new ProjectOptions
            {
                ProjectCollection = _env.CreateProjectCollection().Collection,
                EvaluationContext = context,
                LoadSettings = ProjectLoadSettings.IgnoreMissingImports,
            });

            report.ShouldNotBeNull();
            report.SdkResolutions.Count(observation =>
                observation.SdkName == "foo" &&
                !observation.FromCache).ShouldBe(1);
            report.SdkResolutions.ShouldAllBe(observation => observation.Success);
            report.TaskRegistrations.ShouldContain(observation =>
                observation.TaskName == "ObservedTask" &&
                observation.AssemblyFile.EndsWith("observed-task.dll", StringComparison.OrdinalIgnoreCase));
            report.Categories.ShouldContain(observation =>
                observation.Category == EvaluationObservationCategory.SdkResolution &&
                observation.State == EvaluationObservationCategoryState.Observed);
        }

        [Fact]
        public void EvaluationObservationMarksHostDirectoryCacheAsUnversioned()
        {
            EvaluationObservationReport report = null;
            using IDisposable scope = EvaluationObservationSession.TestOnlyConfigure(
                enabled: true,
                createdReport => report = createdReport);

            string projectFile = _env.CreateFile(
                "directory-cache.proj",
                """
                <Project>
                  <PropertyGroup Condition="Exists('directory-cache.marker')">
                    <Observed>true</Observed>
                  </PropertyGroup>
                </Project>
                """.Cleanup()).Path;

            Project.FromFile(projectFile, new ProjectOptions
            {
                ProjectCollection = _env.CreateProjectCollection().Collection,
                DirectoryCacheFactory = new Helpers.LoggingDirectoryCacheFactory(),
            });

            report.ShouldNotBeNull();
            (report.Reasons & EvaluationObservationReason.UnversionedDirectoryCache)
                .ShouldBe(EvaluationObservationReason.UnversionedDirectoryCache);
        }

        [Fact]
        public void EvaluationObservationRecordsCustomFileSystemProvider()
        {
            EvaluationObservationReport report = null;
            using IDisposable scope = EvaluationObservationSession.TestOnlyConfigure(
                enabled: true,
                createdReport => report = createdReport);

            string projectFile = _env.CreateFile(
                "custom-filesystem.proj",
                """
                <Project>
                  <PropertyGroup Condition="Exists('custom.marker')" />
                </Project>
                """.Cleanup()).Path;
            var fileSystem = new Helpers.LoggingFileSystem();

            Project.FromFile(projectFile, new ProjectOptions
            {
                ProjectCollection = _env.CreateProjectCollection().Collection,
                EvaluationContext = EvaluationContext.Create(EvaluationContext.SharingPolicy.Shared, fileSystem),
            });

            report.ShouldNotBeNull();
            report.Request.FileSystemProvider
                .IndexOf(nameof(Helpers.LoggingFileSystem), StringComparison.Ordinal)
                .ShouldBeGreaterThanOrEqualTo(0);
            report.PathProbes.ShouldContain(observation =>
                observation.Provider.IndexOf(nameof(Helpers.LoggingFileSystem), StringComparison.Ordinal) >= 0);
            (report.Reasons & EvaluationObservationReason.UnversionedCustomProvider)
                .ShouldBe(EvaluationObservationReason.UnversionedCustomProvider);
        }

        [Fact]
        public void EvaluationObservationMarksPartialEvaluationAsIncomplete()
        {
            EvaluationObservationReport report = null;
            using IDisposable scope = EvaluationObservationSession.TestOnlyConfigure(
                enabled: true,
                createdReport => report = createdReport);

            string projectFile = _env.CreateFile("partial.proj", "<Project />").Path;

            ProjectInstance.FromProjectRootElement(
                ProjectRootElement.Open(projectFile),
                new ProjectOptions
                {
                    ProjectCollection = _env.CreateProjectCollection().Collection,
                    EvaluationStage = ProjectEvaluationStage.Properties,
                });

            report.ShouldNotBeNull();
            (report.Reasons & EvaluationObservationReason.IncompleteEvaluationStage)
                .ShouldBe(EvaluationObservationReason.IncompleteEvaluationStage);
        }

        [Fact]
        public void EvaluationObservationCallbackFailureIsReportedAfterEvaluation()
        {
            string projectFile = _env.CreateFile(
                "callback.proj",
                """
                <Project>
                  <PropertyGroup>
                    <Observed>true</Observed>
                  </PropertyGroup>
                </Project>
                """.Cleanup()).Path;
            Project project = null;

            InvalidOperationException exception = Should.Throw<InvalidOperationException>(() =>
            {
                using IDisposable scope = EvaluationObservationSession.TestOnlyConfigure(
                    enabled: true,
                    _ => throw new ApplicationException("Callback failed."));

                project = Project.FromFile(projectFile, new ProjectOptions
                {
                    ProjectCollection = _env.CreateProjectCollection().Collection,
                });
            });

            exception.InnerException.ShouldBeOfType<ApplicationException>();
            project.ShouldNotBeNull();
            project.GetPropertyValue("Observed").ShouldBe("true");
        }

        [Fact]
        public async Task SharedEvaluationContextProducesDisjointObservationReports()
        {
            var reports = new ConcurrentBag<EvaluationObservationReport>();
            using IDisposable scope = EvaluationObservationSession.TestOnlyConfigure(
                enabled: true,
                reports.Add);

            string firstProject = _env.CreateFile(
                "first.proj",
                """
                <Project>
                  <PropertyGroup Condition="Exists('first.marker')" />
                </Project>
                """.Cleanup()).Path;
            string secondProject = _env.CreateFile(
                "second.proj",
                """
                <Project>
                  <PropertyGroup Condition="Exists('second.marker')" />
                </Project>
                """.Cleanup()).Path;

            EvaluationContext context = EvaluationContext.Create(EvaluationContext.SharingPolicy.Shared);
            ProjectCollection firstCollection = _env.CreateProjectCollection().Collection;
            ProjectCollection secondCollection = _env.CreateProjectCollection().Collection;

            await Task.WhenAll(
                Task.Run(() => Project.FromFile(firstProject, new ProjectOptions
                {
                    ProjectCollection = firstCollection,
                    EvaluationContext = context,
                })),
                Task.Run(() => Project.FromFile(secondProject, new ProjectOptions
                {
                    ProjectCollection = secondCollection,
                    EvaluationContext = context,
                })));

            reports.Count.ShouldBe(2);

            string firstMarker = Path.Combine(_env.DefaultTestDirectory.Path, "first.marker");
            string secondMarker = Path.Combine(_env.DefaultTestDirectory.Path, "second.marker");

            reports.Count(report =>
                FileUtilities.PathsEqual(report.ProjectPath, firstProject) &&
                report.PathProbes.Any(observation => FileUtilities.PathsEqual(observation.Path, firstMarker)) &&
                !report.PathProbes.Any(observation => FileUtilities.PathsEqual(observation.Path, secondMarker)))
                .ShouldBe(1);
            reports.Count(report =>
                FileUtilities.PathsEqual(report.ProjectPath, secondProject) &&
                report.PathProbes.Any(observation => FileUtilities.PathsEqual(observation.Path, secondMarker)) &&
                !report.PathProbes.Any(observation => FileUtilities.PathsEqual(observation.Path, firstMarker)))
                .ShouldBe(1);

            reports.ShouldAllBe(report =>
                (report.Reasons & EvaluationObservationReason.UnversionedSharedCache) != 0);
        }

        [Fact]
        public void RecordingFileSystemPreservesPartialEnumeration()
        {
            EvaluationObservationSession session = EvaluationObservationSession.CreateForTests();
            var innerFileSystem = new PartialEnumerationFileSystem();
            var recordingFileSystem = new RecordingFileSystem(innerFileSystem, session);

            using (IEnumerator<string> enumerator = recordingFileSystem.EnumerateFiles("root").GetEnumerator())
            {
                enumerator.MoveNext().ShouldBeTrue();
                enumerator.Current.ShouldBe("first.cs");
            }

            EvaluationObservationReport report = session.Complete(evaluationSucceeded: true);

            innerFileSystem.EntriesProduced.ShouldBe(1);
            report.DirectoryEnumerations.ShouldHaveSingleItem()
                .Completion.ShouldBe(EvaluationEnumerationCompletion.Partial);
            report.DirectoryEnumerations.Single().Entries.ShouldBe(
                [Path.Combine(Directory.GetCurrentDirectory(), "first.cs")]);
            (report.Reasons & EvaluationObservationReason.PartialEnumeration)
                .ShouldBe(EvaluationObservationReason.PartialEnumeration);
        }

        [Fact]
        public void RecordingFileSystemMarksWritableStreamsUnsupported()
        {
            EvaluationObservationSession session = EvaluationObservationSession.CreateForTests();
            var recordingFileSystem = new RecordingFileSystem(new ReadAndMetadataFileSystem(), session);
            string path = Path.Combine(_env.DefaultTestDirectory.Path, "writable-stream.txt");

            using Stream stream = recordingFileSystem.GetFileStream(
                path,
                FileMode.OpenOrCreate,
                System.IO.FileAccess.ReadWrite,
                FileShare.None);

            EvaluationObservationReport report = session.Complete(evaluationSucceeded: true);

            report.FileReads.ShouldContain(observation =>
                observation.Path == path &&
                !observation.IsVerifiable);
            EvaluationSideEffectObservation sideEffect = report.SideEffects.ShouldHaveSingleItem();
            sideEffect.Kind.ShouldBe("WritableFileStream");
            sideEffect.Identity.ShouldBe(path);
            (report.Reasons & EvaluationObservationReason.EvaluationSideEffect)
                .ShouldBe(EvaluationObservationReason.EvaluationSideEffect);
            report.Categories.Single(observation =>
                observation.Category == EvaluationObservationCategory.VolatileOrSideEffect)
                .State.ShouldBe(EvaluationObservationCategoryState.Unsupported);
        }

        [Fact]
        public void EvaluationObservationMarksNoThrowProbeFailuresAmbiguous()
        {
            EvaluationObservationSession session = EvaluationObservationSession.CreateForTests();
            string path = Path.Combine(_env.DefaultTestDirectory.Path, "ambiguous.marker");

            using (session.Enter())
            {
                FileUtilities.FileExistsNoThrow(path, new ThrowingProbeFileSystem()).ShouldBeFalse();
            }

            EvaluationObservationReport report = session.Complete(evaluationSucceeded: true);

            (report.Reasons & EvaluationObservationReason.AmbiguousNegativeProbe)
                .ShouldBe(EvaluationObservationReason.AmbiguousNegativeProbe);
            report.Categories.ShouldContain(observation =>
                observation.Category == EvaluationObservationCategory.PathProbe &&
                observation.State == EvaluationObservationCategoryState.Incomplete);
        }

        private static void AssertEquivalentEvaluatedState(Project reference, Project observed)
        {
            observed.ImportsIncludingDuplicates
                .Select(static import => import.ImportedProject.FullPath)
                .ShouldBe(reference.ImportsIncludingDuplicates.Select(static import => import.ImportedProject.FullPath));

            ProjectProperty[] referenceProperties = reference.Properties
                .OrderBy(static property => property.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static property => property.Name, StringComparer.Ordinal)
                .ToArray();
            ProjectProperty[] observedProperties = observed.Properties
                .OrderBy(static property => property.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static property => property.Name, StringComparer.Ordinal)
                .ToArray();
            observedProperties.Length.ShouldBe(referenceProperties.Length);
            for (int propertyIndex = 0; propertyIndex < referenceProperties.Length; propertyIndex++)
            {
                ProjectProperty referenceProperty = referenceProperties[propertyIndex];
                ProjectProperty observedProperty = observedProperties[propertyIndex];
                MSBuildNameIgnoreCaseComparer.Default
                    .Equals(observedProperty.Name, referenceProperty.Name)
                    .ShouldBeTrue();
                ((IProperty)observedProperty).EvaluatedValueEscaped
                    .ShouldBe(((IProperty)referenceProperty).EvaluatedValueEscaped);
            }

            string[] referenceItemTypes = reference.ItemTypes
                .OrderBy(static itemType => itemType, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static itemType => itemType, StringComparer.Ordinal)
                .ToArray();
            string[] observedItemTypes = observed.ItemTypes
                .OrderBy(static itemType => itemType, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static itemType => itemType, StringComparer.Ordinal)
                .ToArray();
            observedItemTypes.Length.ShouldBe(referenceItemTypes.Length);
            for (int itemTypeIndex = 0; itemTypeIndex < referenceItemTypes.Length; itemTypeIndex++)
            {
                string referenceItemType = referenceItemTypes[itemTypeIndex];
                string observedItemType = observedItemTypes[itemTypeIndex];
                MSBuildNameIgnoreCaseComparer.Default
                    .Equals(observedItemType, referenceItemType)
                    .ShouldBeTrue();

                ProjectItem[] referenceItems = reference.GetItems(referenceItemType).ToArray();
                ProjectItem[] observedItems = observed.GetItems(observedItemType).ToArray();
                observedItems.Length.ShouldBe(referenceItems.Length);
                for (int itemIndex = 0; itemIndex < referenceItems.Length; itemIndex++)
                {
                    ProjectItem referenceItem = referenceItems[itemIndex];
                    ProjectItem observedItem = observedItems[itemIndex];
                    ((IItem)observedItem).EvaluatedIncludeEscaped
                        .ShouldBe(((IItem)referenceItem).EvaluatedIncludeEscaped);

                    ProjectMetadata[] referenceMetadata = referenceItem.Metadata
                        .OrderBy(static metadata => metadata.Name, StringComparer.OrdinalIgnoreCase)
                        .ThenBy(static metadata => metadata.Name, StringComparer.Ordinal)
                        .ToArray();
                    ProjectMetadata[] observedMetadata = observedItem.Metadata
                        .OrderBy(static metadata => metadata.Name, StringComparer.OrdinalIgnoreCase)
                        .ThenBy(static metadata => metadata.Name, StringComparer.Ordinal)
                        .ToArray();
                    observedMetadata.Length.ShouldBe(referenceMetadata.Length);
                    for (int metadataIndex = 0; metadataIndex < referenceMetadata.Length; metadataIndex++)
                    {
                        ProjectMetadata referenceMetadatum = referenceMetadata[metadataIndex];
                        ProjectMetadata observedMetadatum = observedMetadata[metadataIndex];
                        MSBuildNameIgnoreCaseComparer.Default
                            .Equals(observedMetadatum.Name, referenceMetadatum.Name)
                            .ShouldBeTrue();
                        observedMetadatum.EvaluatedValueEscaped
                            .ShouldBe(referenceMetadatum.EvaluatedValueEscaped);
                    }
                }
            }
        }

        private sealed class TransientAppContextSwitch : TransientTestState
        {
            private readonly bool _originalValue;
            private readonly string _switchName;
            private readonly bool _switchWasSet;

            internal TransientAppContextSwitch(string switchName, bool value)
            {
                _switchName = switchName;
                _switchWasSet = AppContext.TryGetSwitch(switchName, out _originalValue);
                AppContext.SetSwitch(switchName, value);
            }

            public override void Revert()
            {
                if (_switchWasSet)
                {
                    AppContext.SetSwitch(_switchName, _originalValue);
                    return;
                }

                foreach (FieldInfo field in typeof(AppContext).GetFields(BindingFlags.NonPublic | BindingFlags.Static))
                {
                    if (field.GetValue(null) is System.Collections.IDictionary switches)
                    {
                        lock (switches)
                        {
                            if (switches.Contains(_switchName))
                            {
                                switches.Remove(_switchName);
                                return;
                            }
                        }
                    }
                }

                throw new InvalidOperationException($"Could not restore unset AppContext switch '{_switchName}'.");
            }
        }

        private abstract class TestFileSystemBase : IFileSystem
        {
            public virtual TextReader ReadFile(string path) => throw new NotSupportedException();
            public virtual Stream GetFileStream(string path, FileMode mode, System.IO.FileAccess access, FileShare share) => throw new NotSupportedException();
            public virtual string ReadFileAllText(string path) => throw new NotSupportedException();
            public virtual byte[] ReadFileAllBytes(string path) => throw new NotSupportedException();
            public virtual IEnumerable<string> EnumerateFiles(string path, string searchPattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly) => throw new NotSupportedException();
            public virtual IEnumerable<string> EnumerateDirectories(string path, string searchPattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly) => throw new NotSupportedException();
            public virtual IEnumerable<string> EnumerateFileSystemEntries(string path, string searchPattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly) => throw new NotSupportedException();
            public virtual FileAttributes GetAttributes(string path) => throw new NotSupportedException();
            public virtual DateTime GetLastWriteTimeUtc(string path) => throw new NotSupportedException();
            public virtual bool DirectoryExists(string path) => throw new NotSupportedException();
            public virtual bool FileExists(string path) => throw new NotSupportedException();
            public virtual bool FileOrDirectoryExists(string path) => throw new NotSupportedException();
        }

        private sealed class PartialEnumerationFileSystem : TestFileSystemBase
        {
            internal int EntriesProduced { get; private set; }

            public override IEnumerable<string> EnumerateFiles(
                string path,
                string searchPattern = "*",
                SearchOption searchOption = SearchOption.TopDirectoryOnly)
            {
                EntriesProduced++;
                yield return "first.cs";
                EntriesProduced++;
                yield return "second.cs";
            }
        }

        private sealed class ReadAndMetadataFileSystem : TestFileSystemBase
        {
            public override TextReader ReadFile(string path) => new StringReader("reader");
            public override Stream GetFileStream(
                string path,
                FileMode mode,
                System.IO.FileAccess access,
                FileShare share) => new MemoryStream();
            public override string ReadFileAllText(string path) => "content";
            public override byte[] ReadFileAllBytes(string path) => Encoding.UTF8.GetBytes("content");
            public override FileAttributes GetAttributes(string path) => FileAttributes.ReadOnly;
            public override DateTime GetLastWriteTimeUtc(string path) => new(1234, DateTimeKind.Utc);
        }

        private sealed class ThrowingProbeFileSystem : TestFileSystemBase
        {
            public override bool FileExists(string path) => throw new IOException("Probe failed.");
        }

        private sealed class ThrowingStringValue
        {
            public override string ToString() => throw new InvalidOperationException("Observation serialization failed.");
        }

        private sealed class ThrowingReadAndMetadataFileSystem : TestFileSystemBase
        {
            public override string ReadFileAllText(string path) =>
                throw new IOException("Read failed.");

            public override FileAttributes GetAttributes(string path) =>
                throw new IOException("Metadata failed.");
        }
    }
}
