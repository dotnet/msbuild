// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.Build.BackEnd.SdkResolution;
using Microsoft.Build.Construction;
using Microsoft.Build.Definition;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Evaluation.Context;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared.FileSystem;
using Microsoft.Build.Unittest;
using Shouldly;
using Xunit;
using SdkResult = Microsoft.Build.BackEnd.SdkResolution.SdkResult;

#nullable disable

namespace Microsoft.Build.UnitTests.Definition
{
    /// <summary>
    ///     Tests some manipulations of Project and ProjectCollection that require dealing with internal data.
    /// </summary>
    public class ProjectEvaluationContext_Tests : IDisposable
    {
        public ProjectEvaluationContext_Tests()
        {
            _env = TestEnvironment.Create();

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

        private readonly SdkUtilities.ConfigurableMockSdkResolver _resolver;
        private readonly TestEnvironment _env;

        private static void SetResolverForContext(EvaluationContext context, SdkResolver resolver)
        {
            var sdkService = (SdkResolverService)context.SdkResolverService;

            sdkService.InitializeForTests(null, new List<SdkResolver> { resolver });
        }

        [Theory]
        [InlineData(EvaluationContext.SharingPolicy.Shared)]
        [InlineData(EvaluationContext.SharingPolicy.SharedSDKCache)]
        [InlineData(EvaluationContext.SharingPolicy.Isolated)]
        public void SharedContextShouldGetReusedWhereasIsolatedContextShouldNot(EvaluationContext.SharingPolicy policy)
        {
            var previousContext = EvaluationContext.Create(policy);

            for (var i = 0; i < 10; i++)
            {
                var currentContext = previousContext.ContextForNewProject();

                if (i == 0)
                {
                    currentContext.ShouldBeSameAs(previousContext, "first usage context was not the same as the initial context");
                }
                else
                {
                    switch (policy)
                    {
                        case EvaluationContext.SharingPolicy.Shared:
                            currentContext.ShouldBeSameAs(previousContext, $"Shared policy: usage {i} was not the same as usage {i - 1}");
                            break;
                        case EvaluationContext.SharingPolicy.SharedSDKCache:
                            currentContext.ShouldNotBeSameAs(previousContext, $"SharedSDKCache policy: usage {i} was the same as usage {i - 1}");
                            break;
                        case EvaluationContext.SharingPolicy.Isolated:
                            currentContext.ShouldNotBeSameAs(previousContext, $"Isolated policy: usage {i} was the same as usage {i - 1}");
                            break;
                        default:
                            throw new ArgumentOutOfRangeException(nameof(policy), policy, null);
                    }
                }

                previousContext = currentContext;
            }
        }

        [Fact]
        public void PassedInFileSystemShouldBeReusedInSharedContext()
        {
            var projectFiles = new[]
            {
                _env.CreateFile("1.proj", @"<Project> <PropertyGroup Condition=`Exists('1.file')`></PropertyGroup> </Project>".Cleanup()).Path,
                _env.CreateFile("2.proj", @"<Project> <PropertyGroup Condition=`Exists('2.file')`></PropertyGroup> </Project>".Cleanup()).Path
            };

            var projectCollection = _env.CreateProjectCollection().Collection;
            var fileSystem = new Helpers.LoggingFileSystem();
            var evaluationContext = EvaluationContext.Create(EvaluationContext.SharingPolicy.Shared, fileSystem);

            foreach (var projectFile in projectFiles)
            {
                Project.FromFile(
                    projectFile,
                    new ProjectOptions
                    {
                        ProjectCollection = projectCollection,
                        EvaluationContext = evaluationContext
                    });
            }

            fileSystem.ExistenceChecks.OrderBy(kvp => kvp.Key)
                .ShouldBe(
                    new Dictionary<string, int>
                    {
                        {Path.Combine(_env.DefaultTestDirectory.Path, "1.file"), 1},
                        {Path.Combine(_env.DefaultTestDirectory.Path, "2.file"), 1}
                    }.OrderBy(kvp => kvp.Key));

            fileSystem.FileOrDirectoryExistsCalls.ShouldBe(2);
        }

        [Theory]
        [InlineData(EvaluationContext.SharingPolicy.SharedSDKCache)]
        [InlineData(EvaluationContext.SharingPolicy.Isolated)]
        public void NonSharedContextShouldNotSupportBeingPassedAFileSystem(EvaluationContext.SharingPolicy policy)
        {
            var fileSystem = new Helpers.LoggingFileSystem();
            Should.Throw<ArgumentException>(() => EvaluationContext.Create(policy, fileSystem));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void EvaluationShouldUseDirectoryCache(bool useProjectInstance)
        {
            var projectFile = _env.CreateFile("1.proj", @"<Project> <Import Project='1.file' Condition=`Exists('1.file')`/> <ItemGroup><Compile Include='*.cs'/></ItemGroup> </Project>".Cleanup()).Path;

            var projectCollection = _env.CreateProjectCollection().Collection;
            var directoryCacheFactory = new Helpers.LoggingDirectoryCacheFactory();

            int expectedEvaluationId;
            if (useProjectInstance)
            {
                var projectInstance = ProjectInstance.FromFile(
                    projectFile,
                    new ProjectOptions
                    {
                        ProjectCollection = projectCollection,
                        DirectoryCacheFactory = directoryCacheFactory,
                    });
                expectedEvaluationId = projectInstance.EvaluationId;
            }
            else
            {
                var project = Project.FromFile(
                    projectFile,
                    new ProjectOptions
                    {
                        ProjectCollection = projectCollection,
                        DirectoryCacheFactory = directoryCacheFactory,
                    });
                expectedEvaluationId = project.LastEvaluationId;
            }

            directoryCacheFactory.DirectoryCaches.Count.ShouldBe(1);
            var directoryCache = directoryCacheFactory.DirectoryCaches[0];

            directoryCache.EvaluationId.ShouldBe(expectedEvaluationId);

            directoryCache.ExistenceChecks.OrderBy(kvp => kvp.Key).ShouldBe(
                new Dictionary<string, int>
                {
                    { _env.DefaultTestDirectory.Path, 1},
                    { Path.Combine(_env.DefaultTestDirectory.Path, "1.file"), 2 }
                }.OrderBy(kvp => kvp.Key));
            directoryCache.Enumerations.ShouldBe(
                new Dictionary<string, int>
                {
                    { _env.DefaultTestDirectory.Path, 1 }
                });
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
            _env.CreateFile("state.marker", string.Empty);
            _env.CreateFile("State.cs", string.Empty);
            string projectFile = _env.CreateFile(
                "state.proj",
                """
                <Project>
                  <PropertyGroup Condition="Exists('state.marker')">
                    <Observed>true</Observed>
                  </PropertyGroup>
                  <ItemGroup>
                    <Compile Include="*.cs" />
                  </ItemGroup>
                </Project>
                """.Cleanup()).Path;

            Project baseline;
            using (EvaluationObservationSession.TestOnlyConfigure(enabled: false))
            {
                baseline = Project.FromFile(projectFile, new ProjectOptions
                {
                    ProjectCollection = _env.CreateProjectCollection().Collection,
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
                });
            }

            report.ShouldNotBeNull();
            observed.GetPropertyValue("Observed").ShouldBe(baseline.GetPropertyValue("Observed"));
            observed.GetItems("Compile").Select(item => item.EvaluatedInclude)
                .ShouldBe(baseline.GetItems("Compile").Select(item => item.EvaluatedInclude));
        }

        [Fact]
        public void EvaluationObservationRecordsProbesAndEnumerations()
        {
            EvaluationObservationReport report = null;
            using IDisposable scope = EvaluationObservationSession.TestOnlyConfigure(
                enabled: true,
                createdReport => report = createdReport);

            _env.CreateFile("Observed.cs", string.Empty);
            string projectFile = _env.CreateFile(
                "observed.proj",
                """
                <Project>
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
            report.ReadyForCacheHits.ShouldBeFalse();
            report.EvaluationSucceeded.ShouldBeTrue();
            (report.Reasons & EvaluationObservationReason.PrototypeCategoriesIncomplete)
                .ShouldBe(EvaluationObservationReason.PrototypeCategoriesIncomplete);
            (report.Reasons & EvaluationObservationReason.ProjectXmlContentNotObserved)
                .ShouldBe(EvaluationObservationReason.ProjectXmlContentNotObserved);
            (report.Reasons & EvaluationObservationReason.UnversionedProjectRootElementCache)
                .ShouldBe(EvaluationObservationReason.UnversionedProjectRootElementCache);
            (report.Reasons & EvaluationObservationReason.AmbiguousNegativeProbe)
                .ShouldBe(EvaluationObservationReason.AmbiguousNegativeProbe);

            string missingPath = Path.Combine(_env.DefaultTestDirectory.Path, "missing.props");
            report.PathProbes.ShouldContain(observation =>
                FileUtilities.PathsEqual(observation.Path, missingPath) &&
                observation.Kind == EvaluationPathKind.FileOrDirectory &&
                !observation.Exists);

            string observedFile = Path.Combine(_env.DefaultTestDirectory.Path, "Observed.cs");
            report.DirectoryEnumerations.ShouldContain(observation =>
                FileUtilities.PathsEqual(observation.Path, _env.DefaultTestDirectory.Path) &&
                observation.Completion == EvaluationEnumerationCompletion.Complete &&
                observation.Entries.Any(entry => FileUtilities.PathsEqual(entry, observedFile)));

            project.GetItems("Compile").ShouldContain(item =>
                string.Equals(Path.GetFileName(item.EvaluatedInclude), "Observed.cs", StringComparison.OrdinalIgnoreCase));
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
        public void EvaluationObservationMarksSharedSdkResolverCacheAsUnversioned()
        {
            EvaluationObservationReport report = null;
            using IDisposable scope = EvaluationObservationSession.TestOnlyConfigure(
                enabled: true,
                createdReport => report = createdReport);

            string projectFile = _env.CreateFile("shared-sdk-cache.proj", "<Project />").Path;

            Project.FromFile(projectFile, new ProjectOptions
            {
                ProjectCollection = _env.CreateProjectCollection().Collection,
                EvaluationContext = EvaluationContext.Create(EvaluationContext.SharingPolicy.SharedSDKCache),
            });

            report.ShouldNotBeNull();
            (report.Reasons & EvaluationObservationReason.UnversionedSdkResolverCache)
                .ShouldBe(EvaluationObservationReason.UnversionedSdkResolverCache);
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
            report.DirectoryEnumerations[0].Entries.ShouldBe(["first.cs"]);
            (report.Reasons & EvaluationObservationReason.PartialEnumeration)
                .ShouldBe(EvaluationObservationReason.PartialEnumeration);
        }

        [Fact]
        public void RecordingFileSystemDoesNotRetainEnumerationAfterCompletion()
        {
            EvaluationObservationSession session = EvaluationObservationSession.CreateForTests();
            var recordingFileSystem = new RecordingFileSystem(new PartialEnumerationFileSystem(), session);
            IEnumerator<string> enumerator = recordingFileSystem.EnumerateFiles("root").GetEnumerator();

            enumerator.MoveNext().ShouldBeTrue();
            EvaluationObservationReport report = session.Complete(evaluationSucceeded: true);
            enumerator.Dispose();

            report.DirectoryEnumerations.ShouldBeEmpty();
            session.TestOnlyRetainedObservationCount.ShouldBe(0);
        }

        [Fact]
        public void RecordingFileSystemMarksConflictingProbeResults()
        {
            EvaluationObservationSession session = EvaluationObservationSession.CreateForTests();
            var recordingFileSystem = new RecordingFileSystem(new AlternatingProbeFileSystem(), session);

            recordingFileSystem.FileExists("probe").ShouldBeFalse();
            recordingFileSystem.FileExists("probe").ShouldBeTrue();

            EvaluationObservationReport report = session.Complete(evaluationSucceeded: true);

            (report.Reasons & EvaluationObservationReason.AmbiguousNegativeProbe)
                .ShouldBe(EvaluationObservationReason.AmbiguousNegativeProbe);
            (report.Reasons & EvaluationObservationReason.ConflictingObservation)
                .ShouldBe(EvaluationObservationReason.ConflictingObservation);
        }

        [Fact]
        public void RecordingFileSystemRecordsMetadataAndFileReads()
        {
            EvaluationObservationSession session = EvaluationObservationSession.CreateForTests();
            var recordingFileSystem = new RecordingFileSystem(new ReadAndMetadataFileSystem(), session);
            string textPath = Path.Combine(_env.DefaultTestDirectory.Path, "text.txt");
            string readerPath = Path.Combine(_env.DefaultTestDirectory.Path, "reader.txt");

            recordingFileSystem.ReadFileAllText(textPath).ShouldBe("content");
            recordingFileSystem.ReadFile(readerPath).ReadToEnd().ShouldBe("reader");
            recordingFileSystem.GetAttributes(textPath).ShouldBe(FileAttributes.ReadOnly);
            recordingFileSystem.GetLastWriteTimeUtc(textPath).ShouldBe(new DateTime(1234, DateTimeKind.Utc));

            EvaluationObservationReport report = session.Complete(evaluationSucceeded: true);

            EvaluationFileReadObservation textRead = report.FileReads.Single(
                observation => FileUtilities.PathsEqual(observation.Path, textPath));
            textRead.IsVerifiable.ShouldBeTrue();
            textRead.ContentHash.ShouldNotBeNull();

            EvaluationFileReadObservation readerRead = report.FileReads.Single(
                observation => FileUtilities.PathsEqual(observation.Path, readerPath));
            readerRead.IsVerifiable.ShouldBeFalse();
            readerRead.ContentHash.ShouldBeNull();
            report.MetadataReads.Count(observation => FileUtilities.PathsEqual(observation.Path, textPath))
                .ShouldBe(2);
            (report.Reasons & EvaluationObservationReason.UnverifiableFileRead)
                .ShouldBe(EvaluationObservationReason.UnverifiableFileRead);
        }

        [Fact]
        public void RecordingFileSystemMarksReadAndMetadataFailures()
        {
            EvaluationObservationSession session = EvaluationObservationSession.CreateForTests();
            var recordingFileSystem = new RecordingFileSystem(new ThrowingFileSystem(), session);

            Should.Throw<IOException>(() => recordingFileSystem.ReadFileAllText("read"));
            Should.Throw<IOException>(() => recordingFileSystem.GetAttributes("metadata"));

            EvaluationObservationReport report = session.Complete(evaluationSucceeded: false);

            report.EvaluationSucceeded.ShouldBeFalse();
            (report.Reasons & EvaluationObservationReason.ExternalOperationFailure)
                .ShouldBe(EvaluationObservationReason.ExternalOperationFailure);
        }

        [Theory]
        [InlineData(EvaluationContext.SharingPolicy.Shared)]
        [InlineData(EvaluationContext.SharingPolicy.SharedSDKCache)]
        [InlineData(EvaluationContext.SharingPolicy.Isolated)]
        public void ReevaluationShouldNotReuseInitialContext(EvaluationContext.SharingPolicy policy)
        {
            try
            {
                EvaluationContext.TestOnlyHookOnCreate = c => SetResolverForContext(c, _resolver);

                var collection = _env.CreateProjectCollection().Collection;

                var context = EvaluationContext.Create(policy);

                using var xmlReader = XmlReader.Create(new StringReader("<Project Sdk=\"foo\"></Project>"));
                var project = Project.FromXmlReader(
                    xmlReader,
                    new ProjectOptions
                    {
                        ProjectCollection = collection,
                        EvaluationContext = context,
                        LoadSettings = ProjectLoadSettings.IgnoreMissingImports
                    });

                _resolver.ResolvedCalls["foo"].ShouldBe(1);

                project.AddItem("a", "b");

                project.ReevaluateIfNecessary();

                _resolver.ResolvedCalls["foo"].ShouldBe(2);
            }
            finally
            {
                EvaluationContext.TestOnlyHookOnCreate = null;
            }
        }

        [Theory]
        [InlineData(EvaluationContext.SharingPolicy.Shared)]
        [InlineData(EvaluationContext.SharingPolicy.SharedSDKCache)]
        [InlineData(EvaluationContext.SharingPolicy.Isolated)]
        public void ProjectInstanceShouldRespectSharingPolicy(EvaluationContext.SharingPolicy policy)
        {
            try
            {
                var seenContexts = new HashSet<EvaluationContext>();

                EvaluationContext.TestOnlyHookOnCreate = c => seenContexts.Add(c);

                var collection = _env.CreateProjectCollection().Collection;

                var context = EvaluationContext.Create(policy);

                const int numIterations = 10;
                for (int i = 0; i < numIterations; i++)
                {
                    ProjectInstance.FromProjectRootElement(
                        ProjectRootElement.Create(),
                        new ProjectOptions
                        {
                            ProjectCollection = collection,
                            EvaluationContext = context,
                            LoadSettings = ProjectLoadSettings.IgnoreMissingImports
                        });
                }

                int expectedNumContexts = policy == EvaluationContext.SharingPolicy.Shared ? 1 : numIterations;

                seenContexts.Count.ShouldBe(expectedNumContexts);
                seenContexts.ShouldAllBe(c => c.Policy == policy);
            }
            finally
            {
                EvaluationContext.TestOnlyHookOnCreate = null;
            }
        }

        private static string[] _sdkResolutionProjects =
        {
            "<Project Sdk=\"foo\"></Project>",
            "<Project Sdk=\"bar\"></Project>",
            "<Project Sdk=\"foo\"></Project>",
            "<Project Sdk=\"bar\"></Project>"
        };

        [Theory]
        [InlineData(EvaluationContext.SharingPolicy.Shared, 1, 1)]
        [InlineData(EvaluationContext.SharingPolicy.SharedSDKCache, 1, 1)]
        [InlineData(EvaluationContext.SharingPolicy.Isolated, 4, 4)]
        public void ContextPinsSdkResolverCache(EvaluationContext.SharingPolicy policy, int sdkLookupsForFoo, int sdkLookupsForBar)
        {
            try
            {
                EvaluationContext.TestOnlyHookOnCreate = c => SetResolverForContext(c, _resolver);

                var context = EvaluationContext.Create(policy);
                EvaluateProjects(_sdkResolutionProjects, context, null);

                _resolver.ResolvedCalls.Count.ShouldBe(2);
                _resolver.ResolvedCalls["foo"].ShouldBe(sdkLookupsForFoo);
                _resolver.ResolvedCalls["bar"].ShouldBe(sdkLookupsForBar);
            }
            finally
            {
                EvaluationContext.TestOnlyHookOnCreate = null;
            }
        }

        [Fact]
        public void DefaultContextIsIsolatedContext()
        {
            try
            {
                var seenContexts = new HashSet<EvaluationContext>();

                EvaluationContext.TestOnlyHookOnCreate = c => seenContexts.Add(c);

                EvaluateProjects(_sdkResolutionProjects, null, null);

                seenContexts.Count.ShouldBe(8); // 4 evaluations and 4 reevaluations
                seenContexts.ShouldAllBe(c => c.Policy == EvaluationContext.SharingPolicy.Isolated);
            }
            finally
            {
                EvaluationContext.TestOnlyHookOnCreate = null;
            }
        }

        public static IEnumerable<object[]> ContextPinsGlobExpansionCacheData
        {
            get
            {
                yield return new object[]
                {
                    EvaluationContext.SharingPolicy.Shared,
                    new[]
                    {
                        new[] {"0.cs"},
                        new[] {"0.cs"},
                        new[] {"0.cs"},
                        new[] {"0.cs"}
                    }
                };

                foreach (var policy in new[] { EvaluationContext.SharingPolicy.SharedSDKCache, EvaluationContext.SharingPolicy.Isolated })
                {
                    yield return new object[]
                    {
                        policy,
                        new[]
                        {
                            new[] {"0.cs"},
                            new[] {"0.cs", "1.cs"},
                            new[] {"0.cs", "1.cs", "2.cs"},
                            new[] {"0.cs", "1.cs", "2.cs", "3.cs"},
                        }
                    };
                }
            }
        }

        private static string[] _projectsWithGlobs =
        {
            @"<Project>
                <ItemGroup>
                    <i Include=`**/*.cs` />
                </ItemGroup>
            </Project>",

            @"<Project>
                <ItemGroup>
                    <i Include=`**/*.cs` />
                </ItemGroup>
            </Project>",
        };

        [Theory]
        [MemberData(nameof(ContextPinsGlobExpansionCacheData))]
        public void ContextCachesItemElementGlobExpansions(EvaluationContext.SharingPolicy policy, string[][] expectedGlobExpansions)
        {
            var projectDirectory = _env.DefaultTestDirectory.Path;

            var context = EvaluationContext.Create(policy);

            var evaluationCount = 0;

            File.WriteAllText(Path.Combine(projectDirectory, $"{evaluationCount}.cs"), "");

            EvaluateProjects(
                _projectsWithGlobs,
                context,
                project =>
                {
                    var expectedGlobExpansion = expectedGlobExpansions[evaluationCount];
                    evaluationCount++;

                    File.WriteAllText(Path.Combine(projectDirectory, $"{evaluationCount}.cs"), "");

                    ObjectModelHelpers.AssertItems(expectedGlobExpansion, project.GetItems("i"));
                });
        }

        public static IEnumerable<object[]> ContextDisambiguatesRelativeGlobsData
        {
            get
            {
                yield return new object[]
                {
                    EvaluationContext.SharingPolicy.Shared,
                    new[]
                    {
                        new[] {"0.cs"}, // first project
                        new[] {"0.cs", "1.cs"}, // second project
                        new[] {"0.cs"}, // first project reevaluation
                        new[] {"0.cs", "1.cs"}, // second project reevaluation
                    }
                };

                foreach (var policy in new[] { EvaluationContext.SharingPolicy.SharedSDKCache, EvaluationContext.SharingPolicy.Isolated })
                {
                    yield return new object[]
                    {
                        policy,
                        new[]
                        {
                            new[] {"0.cs"},
                            new[] {"0.cs", "1.cs"},
                            new[] {"0.cs", "1.cs", "2.cs"},
                            new[] {"0.cs", "1.cs", "2.cs", "3.cs"},
                        }
                    };
                }
            }
        }

        [Theory]
        [MemberData(nameof(ContextDisambiguatesRelativeGlobsData))]
        public void ContextDisambiguatesSameRelativeGlobsPointingInsideDifferentProjectCones(EvaluationContext.SharingPolicy policy, string[][] expectedGlobExpansions)
        {
            var projectDirectory1 = _env.DefaultTestDirectory.CreateDirectory("1").Path;
            var projectDirectory2 = _env.DefaultTestDirectory.CreateDirectory("2").Path;

            var context = EvaluationContext.Create(policy);

            var evaluationCount = 0;

            File.WriteAllText(Path.Combine(projectDirectory1, $"1.{evaluationCount}.cs"), "");
            File.WriteAllText(Path.Combine(projectDirectory2, $"2.{evaluationCount}.cs"), "");

            EvaluateProjects(
                new[]
                {
                    new ProjectSpecification(
                        Path.Combine(projectDirectory1, "1"),
                        $@"<Project>
                            <ItemGroup>
                                <i Include=`{Path.Combine("**", "*.cs")}` />
                            </ItemGroup>
                        </Project>"),
                    new ProjectSpecification(
                        Path.Combine(projectDirectory2, "2"),
                        $@"<Project>
                            <ItemGroup>
                                <i Include=`{Path.Combine("**", "*.cs")}` />
                            </ItemGroup>
                        </Project>"),
                },
                context,
                project =>
                {
                    var projectName = Path.GetFileNameWithoutExtension(project.FullPath);

                    var expectedGlobExpansion = expectedGlobExpansions[evaluationCount]
                        .Select(i => $"{projectName}.{i}")
                        .ToArray();

                    ObjectModelHelpers.AssertItems(expectedGlobExpansion, project.GetItems("i"));

                    evaluationCount++;

                    File.WriteAllText(Path.Combine(projectDirectory1, $"1.{evaluationCount}.cs"), "");
                    File.WriteAllText(Path.Combine(projectDirectory2, $"2.{evaluationCount}.cs"), "");
                });
        }

        [Theory]
        [MemberData(nameof(ContextDisambiguatesRelativeGlobsData))]
        public void ContextDisambiguatesSameRelativeGlobsPointingOutsideDifferentProjectCones(EvaluationContext.SharingPolicy policy, string[][] expectedGlobExpansions)
        {
            var project1Root = _env.DefaultTestDirectory.CreateDirectory("Project1");
            var project1Directory = project1Root.CreateDirectory("1").Path;
            var project1GlobDirectory = project1Root.CreateDirectory("Glob").CreateDirectory("1").Path;

            var project2Root = _env.DefaultTestDirectory.CreateDirectory("Project2");
            var project2Directory = project2Root.CreateDirectory("2").Path;
            var project2GlobDirectory = project2Root.CreateDirectory("Glob").CreateDirectory("2").Path;

            var context = EvaluationContext.Create(policy);

            var evaluationCount = 0;

            File.WriteAllText(Path.Combine(project1GlobDirectory, $"1.{evaluationCount}.cs"), "");
            File.WriteAllText(Path.Combine(project2GlobDirectory, $"2.{evaluationCount}.cs"), "");

            EvaluateProjects(
                new[]
                {
                    new ProjectSpecification(
                        Path.Combine(project1Directory, "1"),
                        $@"<Project>
                            <ItemGroup>
                                <i Include=`{Path.Combine("..", "Glob", "**", "*.cs")}`/>
                            </ItemGroup>
                        </Project>"),
                    new ProjectSpecification(
                        Path.Combine(project2Directory, "2"),
                        $@"<Project>
                            <ItemGroup>
                                <i Include=`{Path.Combine("..", "Glob", "**", "*.cs")}`/>
                            </ItemGroup>
                        </Project>")
                },
                context,
                project =>
                {
                    var projectName = Path.GetFileNameWithoutExtension(project.FullPath);

                    // globs have the fixed directory part prepended, so add it to the expected results
                    var expectedGlobExpansion = expectedGlobExpansions[evaluationCount]
                        .Select(i => Path.Combine("..", "Glob", projectName, $"{projectName}.{i}"))
                        .ToArray();

                    var actualGlobExpansion = project.GetItems("i");
                    ObjectModelHelpers.AssertItems(expectedGlobExpansion, actualGlobExpansion);

                    evaluationCount++;

                    File.WriteAllText(Path.Combine(project1GlobDirectory, $"1.{evaluationCount}.cs"), "");
                    File.WriteAllText(Path.Combine(project2GlobDirectory, $"2.{evaluationCount}.cs"), "");
                });
        }

        [Theory]
        [MemberData(nameof(ContextDisambiguatesRelativeGlobsData))]
        public void ContextDisambiguatesAFullyQualifiedGlobPointingInAnotherRelativeGlobsCone(EvaluationContext.SharingPolicy policy, string[][] expectedGlobExpansions)
        {
            if (policy == EvaluationContext.SharingPolicy.Shared)
            {
                // This test case has a dependency on our glob expansion caching policy. If the evaluation context is reused
                // between evaluations and files are added to the filesystem between evaluations, the cache may be returning
                // stale results. Run only the Isolated variant.
                return;
            }

            var project1Directory = _env.DefaultTestDirectory.CreateDirectory("Project1");
            var project1GlobDirectory = project1Directory.CreateDirectory("Glob").CreateDirectory("1").Path;

            var project2Directory = _env.DefaultTestDirectory.CreateDirectory("Project2");

            var context = EvaluationContext.Create(policy);

            var evaluationCount = 0;

            File.WriteAllText(Path.Combine(project1GlobDirectory, $"{evaluationCount}.cs"), "");

            EvaluateProjects(
                new[]
                {
                    // first project uses a relative path
                    new ProjectSpecification(
                        Path.Combine(project1Directory.Path, "1"),
                        $@"<Project>
                            <ItemGroup>
                                <i Include=`{Path.Combine("Glob", "**", "*.cs")}` />
                            </ItemGroup>
                        </Project>"),
                    // second project reaches out into first project's cone via a fully qualified path
                    new ProjectSpecification(
                        Path.Combine(project2Directory.Path, "2"),
                        $@"<Project>
                            <ItemGroup>
                                <i Include=`{Path.Combine(project1Directory.Path, "Glob", "**", "*.cs")}` />
                            </ItemGroup>
                        </Project>")
                },
                context,
                project =>
                {
                    var projectName = Path.GetFileNameWithoutExtension(project.FullPath);

                    // globs have the fixed directory part prepended, so add it to the expected results
                    var expectedGlobExpansion = expectedGlobExpansions[evaluationCount]
                        .Select(i => Path.Combine("Glob", "1", i))
                        .ToArray();

                    // project 2 has fully qualified directory parts, so make the results for 2 fully qualified
                    if (projectName.Equals("2"))
                    {
                        expectedGlobExpansion = expectedGlobExpansion
                            .Select(i => Path.Combine(project1Directory.Path, i))
                            .ToArray();
                    }

                    var actualGlobExpansion = project.GetItems("i");
                    ObjectModelHelpers.AssertItems(expectedGlobExpansion, actualGlobExpansion);

                    evaluationCount++;

                    File.WriteAllText(Path.Combine(project1GlobDirectory, $"{evaluationCount}.cs"), "");
                });
        }

        [Theory]
        [MemberData(nameof(ContextDisambiguatesRelativeGlobsData))]
        public void ContextDisambiguatesDistinctRelativeGlobsPointingOutsideOfSameProjectCone(EvaluationContext.SharingPolicy policy, string[][] expectedGlobExpansions)
        {
            var globDirectory = _env.DefaultTestDirectory.CreateDirectory("glob");

            var projectRoot = _env.DefaultTestDirectory.CreateDirectory("proj");

            var project1Directory = projectRoot.CreateDirectory("Project1");

            var project2SubDir = projectRoot.CreateDirectory("subdirectory");

            var project2Directory = project2SubDir.CreateDirectory("Project2");

            var context = EvaluationContext.Create(policy);

            var evaluationCount = 0;

            File.WriteAllText(Path.Combine(globDirectory.Path, $"{evaluationCount}.cs"), "");

            EvaluateProjects(
                new[]
                {
                    new ProjectSpecification(
                        Path.Combine(project1Directory.Path, "1"),
                        @"<Project>
                            <ItemGroup>
                                <i Include=`../../glob/*.cs` />
                            </ItemGroup>
                        </Project>"),
                    new ProjectSpecification(
                        Path.Combine(project2Directory.Path, "2"),
                        @"<Project>
                            <ItemGroup>
                                <i Include=`../../../glob/*.cs` />
                            </ItemGroup>
                        </Project>")
                },
                context,
                project =>
                {
                    var projectName = Path.GetFileNameWithoutExtension(project.FullPath);
                    var globFixedDirectoryPart = projectName.EndsWith("1", StringComparison.Ordinal)
                        ? Path.Combine("..", "..", "glob")
                        : Path.Combine("..", "..", "..", "glob");

                    // globs have the fixed directory part prepended, so add it to the expected results
                    var expectedGlobExpansion = expectedGlobExpansions[evaluationCount]
                        .Select(i => Path.Combine(globFixedDirectoryPart, i))
                        .ToArray();

                    var actualGlobExpansion = project.GetItems("i");
                    ObjectModelHelpers.AssertItems(expectedGlobExpansion, actualGlobExpansion);

                    evaluationCount++;

                    File.WriteAllText(Path.Combine(globDirectory.Path, $"{evaluationCount}.cs"), "");
                });
        }

        [Theory]
        [MemberData(nameof(ContextPinsGlobExpansionCacheData))]
        // projects should cache glob expansions when the __fully qualified__ glob is shared between projects and points outside of project cone
        public void ContextCachesCommonOutOfProjectConeFullyQualifiedGlob(EvaluationContext.SharingPolicy policy, string[][] expectedGlobExpansions)
        {
            ContextCachesCommonOutOfProjectCone(itemSpecPathIsRelative: false, policy: policy, expectedGlobExpansions: expectedGlobExpansions);
        }

        [Theory(Skip = "https://github.com/dotnet/msbuild/issues/3889")]
        [MemberData(nameof(ContextPinsGlobExpansionCacheData))]
        // projects should cache glob expansions when the __relative__ glob is shared between projects and points outside of project cone
        public void ContextCachesCommonOutOfProjectConeRelativeGlob(EvaluationContext.SharingPolicy policy, string[][] expectedGlobExpansions)
        {
            ContextCachesCommonOutOfProjectCone(itemSpecPathIsRelative: true, policy: policy, expectedGlobExpansions: expectedGlobExpansions);
        }

        private void ContextCachesCommonOutOfProjectCone(bool itemSpecPathIsRelative, EvaluationContext.SharingPolicy policy, string[][] expectedGlobExpansions)
        {
            var testDirectory = _env.DefaultTestDirectory;
            var globDirectory = testDirectory.CreateDirectory("GlobDirectory");

            var itemSpecDirectoryPart = itemSpecPathIsRelative
                ? Path.Combine("..", "GlobDirectory")
                : globDirectory.Path;

            Directory.CreateDirectory(globDirectory.Path);

            // Globs with a directory part will produce items prepended with that directory part.
            // Make a deep copy of the argument to avoid writing to global variables.
            string[][] prependedExpectedGlobExpansions = new string[expectedGlobExpansions.Length][];
            for (int expIndex = 0; expIndex < expectedGlobExpansions.Length; expIndex++)
            {
                string[] globExpansion = expectedGlobExpansions[expIndex];
                string[] prependedGlobExpansion = new string[globExpansion.Length];

                prependedExpectedGlobExpansions[expIndex] = prependedGlobExpansion;
                for (var i = 0; i < globExpansion.Length; i++)
                {
                    prependedGlobExpansion[i] = Path.Combine(itemSpecDirectoryPart, globExpansion[i]);
                }
            }

            var projectSpecs = new[]
            {
                $@"<Project>
                <ItemGroup>
                    <i Include=`{Path.Combine("{0}", "**", "*.cs")}`/>
                </ItemGroup>
            </Project>",
                $@"<Project>
                <ItemGroup>
                    <i Include=`{Path.Combine("{0}", "**", "*.cs")}`/>
                </ItemGroup>
            </Project>"
            }
                .Select(p => string.Format(p, itemSpecDirectoryPart))
                .Select((p, i) => new ProjectSpecification(Path.Combine(testDirectory.Path, $"ProjectDirectory{i}", $"Project{i}.proj"), p));

            var context = EvaluationContext.Create(policy);

            var evaluationCount = 0;

            File.WriteAllText(Path.Combine(globDirectory.Path, $"{evaluationCount}.cs"), "");

            EvaluateProjects(
                projectSpecs,
                context,
                project =>
                {
                    var expectedGlobExpansion = prependedExpectedGlobExpansions[evaluationCount];
                    evaluationCount++;

                    File.WriteAllText(Path.Combine(globDirectory.Path, $"{evaluationCount}.cs"), "");

                    ObjectModelHelpers.AssertItems(expectedGlobExpansion, project.GetItems("i"));
                });
        }

        private static string[] _projectsWithGlobImports =
        {
            @"<Project>
                <Import Project=`*.props` />
            </Project>",

            @"<Project>
                <Import Project=`*.props` />
            </Project>",
        };

        [Theory]
        [MemberData(nameof(ContextPinsGlobExpansionCacheData))]
        public void ContextCachesImportGlobExpansions(EvaluationContext.SharingPolicy policy, string[][] expectedGlobExpansions)
        {
            var projectDirectory = _env.DefaultTestDirectory.Path;

            var context = EvaluationContext.Create(policy);

            var evaluationCount = 0;

            File.WriteAllText(Path.Combine(projectDirectory, $"{evaluationCount}.props"), $"<Project><ItemGroup><i Include=`{evaluationCount}.cs`/></ItemGroup></Project>".Cleanup());

            EvaluateProjects(
                _projectsWithGlobImports,
                context,
                project =>
                {
                    var expectedGlobExpansion = expectedGlobExpansions[evaluationCount];
                    evaluationCount++;

                    File.WriteAllText(Path.Combine(projectDirectory, $"{evaluationCount}.props"), $"<Project><ItemGroup><i Include=`{evaluationCount}.cs`/></ItemGroup></Project>".Cleanup());

                    ObjectModelHelpers.AssertItems(expectedGlobExpansion, project.GetItems("i"));
                });
        }

        private static string[] _projectsWithConditions =
        {
            @"<Project>
                <PropertyGroup Condition=`Exists('0.cs')`>
                    <p>val</p>
                </PropertyGroup>
            </Project>",

            @"<Project>
                <PropertyGroup Condition=`Exists('0.cs')`>
                    <p>val</p>
                </PropertyGroup>
            </Project>",
        };

        [Theory]
        [InlineData(EvaluationContext.SharingPolicy.Isolated)]
        [InlineData(EvaluationContext.SharingPolicy.SharedSDKCache)]
        [InlineData(EvaluationContext.SharingPolicy.Shared)]
        public void ContextCachesExistenceChecksInConditions(EvaluationContext.SharingPolicy policy)
        {
            var projectDirectory = _env.DefaultTestDirectory.Path;

            var context = EvaluationContext.Create(policy);

            var theFile = Path.Combine(projectDirectory, "0.cs");
            File.WriteAllText(theFile, "");

            var evaluationCount = 0;

            EvaluateProjects(
                _projectsWithConditions,
                context,
                project =>
                {
                    evaluationCount++;

                    if (File.Exists(theFile))
                    {
                        File.Delete(theFile);
                    }

                    if (evaluationCount == 1)
                    {
                        project.GetPropertyValue("p").ShouldBe("val");
                    }
                    else
                    {
                        switch (policy)
                        {
                            case EvaluationContext.SharingPolicy.Shared:
                                project.GetPropertyValue("p").ShouldBe("val");
                                break;
                            case EvaluationContext.SharingPolicy.SharedSDKCache:
                            case EvaluationContext.SharingPolicy.Isolated:
                                project.GetPropertyValue("p").ShouldBeEmpty();
                                break;
                            default:
                                throw new ArgumentOutOfRangeException(nameof(policy), policy, null);
                        }
                    }
                });
        }

        [Theory]
        [InlineData(EvaluationContext.SharingPolicy.Isolated)]
        [InlineData(EvaluationContext.SharingPolicy.SharedSDKCache)]
        [InlineData(EvaluationContext.SharingPolicy.Shared)]
        public void ContextCachesExistenceChecksInGetDirectoryNameOfFileAbove(EvaluationContext.SharingPolicy policy)
        {
            var context = EvaluationContext.Create(policy);

            var subdirectory = _env.DefaultTestDirectory.CreateDirectory("subDirectory");
            var subdirectoryFile = subdirectory.CreateFile("a");
            _env.DefaultTestDirectory.CreateFile("a");

            int evaluationCount = 0;

            EvaluateProjects(
                new[]
                {
                    $@"<Project>
                      <PropertyGroup>
                        <SearchedPath>$([MSBuild]::GetDirectoryNameOfFileAbove('{subdirectory.Path}', 'a'))</SearchedPath>
                      </PropertyGroup>
                    </Project>"
                },
                context,
                project =>
                {
                    evaluationCount++;

                    var searchedPath = project.GetProperty("SearchedPath");

                    switch (policy)
                    {
                        case EvaluationContext.SharingPolicy.Shared:
                            searchedPath.EvaluatedValue.ShouldBe(subdirectory.Path);
                            break;
                        case EvaluationContext.SharingPolicy.SharedSDKCache:
                        case EvaluationContext.SharingPolicy.Isolated:
                            searchedPath.EvaluatedValue.ShouldBe(
                                evaluationCount == 1
                                    ? subdirectory.Path
                                    : _env.DefaultTestDirectory.Path);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException(nameof(policy), policy, null);
                    }

                    if (evaluationCount == 1)
                    {
                        // this will cause the upper file to get picked up in the Isolated policy
                        subdirectoryFile.Delete();
                    }
                });

            evaluationCount.ShouldBe(2);
        }

        [Fact]
        public void CachingFileSystemWrapperDistinguishesFileAndDirectoryExistence()
        {
            var directory = _env.DefaultTestDirectory.CreateDirectory("subDirectory");
            var file = _env.DefaultTestDirectory.CreateFile("file");
            var fileSystem = new CachingFileSystemWrapper(FileSystems.Default);

            fileSystem.FileExists(directory.Path).ShouldBeFalse();
            fileSystem.DirectoryExists(directory.Path).ShouldBeTrue();
            fileSystem.FileOrDirectoryExists(directory.Path).ShouldBeTrue();

            fileSystem.DirectoryExists(file.Path).ShouldBeFalse();
            fileSystem.FileExists(file.Path).ShouldBeTrue();
            fileSystem.FileOrDirectoryExists(file.Path).ShouldBeTrue();
        }

        [Theory]
        [InlineData(EvaluationContext.SharingPolicy.Isolated)]
        [InlineData(EvaluationContext.SharingPolicy.SharedSDKCache)]
        [InlineData(EvaluationContext.SharingPolicy.Shared)]
        public void GetDirectoryNameOfFileAboveWithEmptyFileNameDoesNotPoisonProjectLevelWildcards(EvaluationContext.SharingPolicy policy)
        {
            var context = EvaluationContext.Create(policy);
            _env.DefaultTestDirectory.CreateFile("Source.cs");

            EvaluateProjects(
                new[]
                {
                    """
                    <Project>
                      <PropertyGroup>
                        <SearchedPath>$([MSBuild]::GetDirectoryNameOfFileAbove('$(MSBuildProjectDirectory)', ''))</SearchedPath>
                      </PropertyGroup>
                      <ItemGroup>
                        <i Include="*.cs" />
                      </ItemGroup>
                    </Project>
                    """
                },
                context,
                project =>
                {
                    project.GetPropertyValue("SearchedPath").ShouldBeEmpty();
                    ObjectModelHelpers.AssertItems(["Source.cs"], project.GetItems("i"));
                });
        }

        [Theory]
        [InlineData(EvaluationContext.SharingPolicy.Isolated)]
        [InlineData(EvaluationContext.SharingPolicy.SharedSDKCache)]
        [InlineData(EvaluationContext.SharingPolicy.Shared)]
        public void ContextCachesExistenceChecksInGetPathOfFileAbove(EvaluationContext.SharingPolicy policy)
        {
            var context = EvaluationContext.Create(policy);

            var subdirectory = _env.DefaultTestDirectory.CreateDirectory("subDirectory");
            var subdirectoryFile = subdirectory.CreateFile("a");
            var rootFile = _env.DefaultTestDirectory.CreateFile("a");

            int evaluationCount = 0;

            EvaluateProjects(
                new[]
                {
                    $@"<Project>
                      <PropertyGroup>
                        <SearchedPath>$([MSBuild]::GetPathOfFileAbove('a', '{subdirectory.Path}'))</SearchedPath>
                      </PropertyGroup>
                    </Project>"
                },
                context,
                project =>
                {
                    evaluationCount++;

                    var searchedPath = project.GetProperty("SearchedPath");

                    switch (policy)
                    {
                        case EvaluationContext.SharingPolicy.Shared:
                            searchedPath.EvaluatedValue.ShouldBe(subdirectoryFile.Path);
                            break;
                        case EvaluationContext.SharingPolicy.SharedSDKCache:
                        case EvaluationContext.SharingPolicy.Isolated:
                            searchedPath.EvaluatedValue.ShouldBe(
                                evaluationCount == 1
                                    ? subdirectoryFile.Path
                                    : rootFile.Path);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException(nameof(policy), policy, null);
                    }

                    if (evaluationCount == 1)
                    {
                        // this will cause the upper file to get picked up in the Isolated policy
                        subdirectoryFile.Delete();
                    }
                });

            evaluationCount.ShouldBe(2);
        }

        private abstract class TestFileSystemBase : IFileSystem
        {
            public virtual TextReader ReadFile(string path) => throw new NotSupportedException();
            public virtual Stream GetFileStream(string path, FileMode mode, FileAccess access, FileShare share) => throw new NotSupportedException();
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

        private sealed class AlternatingProbeFileSystem : TestFileSystemBase
        {
            private bool _exists;

            public override bool FileExists(string path)
            {
                bool result = _exists;
                _exists = true;
                return result;
            }
        }

        private sealed class ReadAndMetadataFileSystem : TestFileSystemBase
        {
            public override TextReader ReadFile(string path) => new StringReader("reader");
            public override string ReadFileAllText(string path) => "content";
            public override FileAttributes GetAttributes(string path) => FileAttributes.ReadOnly;
            public override DateTime GetLastWriteTimeUtc(string path) => new(1234, DateTimeKind.Utc);
        }

        private sealed class ThrowingFileSystem : TestFileSystemBase
        {
            public override string ReadFileAllText(string path) => throw new IOException("Read failed.");
            public override FileAttributes GetAttributes(string path) => throw new IOException("Metadata failed.");
        }

        private void EvaluateProjects(IEnumerable<string> projectContents, EvaluationContext context, Action<Project> afterEvaluationAction)
        {
            EvaluateProjects(
                projectContents.Select((p, i) => new ProjectSpecification(Path.Combine(_env.DefaultTestDirectory.Path, $"Project{i}.proj"), p)),
                context,
                afterEvaluationAction);
        }

        private struct ProjectSpecification
        {
            public string ProjectFilePath { get; }
            public string ProjectContents { get; }

            public ProjectSpecification(string projectFilePath, string projectContents)
            {
                ProjectFilePath = projectFilePath;
                ProjectContents = projectContents;
            }

            public void Deconstruct(out string projectPath, out string projectContents)
            {
                projectPath = this.ProjectFilePath;
                projectContents = this.ProjectContents;
            }
        }

        /// <summary>
        /// Should be at least two test projects to test cache visibility between projects
        /// </summary>
        private void EvaluateProjects(IEnumerable<ProjectSpecification> projectSpecs, EvaluationContext context, Action<Project> afterEvaluationAction)
        {
            var collection = _env.CreateProjectCollection().Collection;

            var projects = new List<Project>();

            foreach (var (projectFilePath, projectContents) in projectSpecs)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(projectFilePath));
                File.WriteAllText(projectFilePath, projectContents.Cleanup());

                var project = Project.FromFile(
                    projectFilePath,
                    new ProjectOptions
                    {
                        ProjectCollection = collection,
                        EvaluationContext = context,
                        LoadSettings = ProjectLoadSettings.IgnoreMissingImports
                    });

                afterEvaluationAction?.Invoke(project);

                projects.Add(project);
            }

            foreach (var project in projects)
            {
                project.AddItem("a", "b");
                project.ReevaluateIfNecessary(context);

                afterEvaluationAction?.Invoke(project);
            }
        }
    }
}
