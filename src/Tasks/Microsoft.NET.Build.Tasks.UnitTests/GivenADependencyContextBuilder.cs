// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using FluentAssertions.Json;
using Microsoft.Build.Framework;
using Microsoft.Extensions.DependencyModel;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Frameworks;
using NuGet.Packaging.Core;
using NuGet.ProjectModel;
using NuGet.Versioning;
using NuGet.RuntimeModel;
using Xunit;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    public class GivenADependencyContextBuilder
    {
        /// <summary>
        /// Tests that DependencyContextBuilder generates DependencyContexts correctly.
        /// </summary>
        [Theory]
        [MemberData(nameof(ProjectData))]
        public void ItBuildsDependencyContextsFromProjectLockFiles(
            string mainProjectName,
            string mainProjectVersion,
            CompilationOptions compilationOptions,
            string baselineFileName,
            string runtime,
            ITaskItem[] assemblySatelliteAssemblies,
            ITaskItem[] referencePaths,
            ITaskItem[] referenceSatellitePaths,
            object[] resolvedNuGetFiles)
        {
            LockFile lockFile = TestLockFiles.GetLockFile(mainProjectName);
            LockFileLookup lockFileLookup = new LockFileLookup(lockFile);

            SingleProjectInfo mainProject = SingleProjectInfo.Create(
                "/usr/Path",
                mainProjectName,
                ".dll",
                mainProjectVersion,
                assemblySatelliteAssemblies ?? new ITaskItem[] { });

            IEnumerable<ReferenceInfo> directReferences =
                ReferenceInfo.CreateDirectReferenceInfos(
                    referencePaths ?? new ITaskItem[] { },
                    referenceSatellitePaths ?? new ITaskItem[] { },
                    lockFileLookup: lockFileLookup,
                    i => true,
                    true);

            ProjectContext projectContext = lockFile.CreateProjectContext(
                FrameworkConstants.CommonFrameworks.NetCoreApp10.GetShortFolderName(),
                runtime,
                Constants.DefaultPlatformLibrary,
                runtimeFrameworks: null,
                isSelfContained: !string.IsNullOrEmpty(runtime));

            if (resolvedNuGetFiles == null)
            {
                resolvedNuGetFiles = Array.Empty<ResolvedFile>();
            }

            DependencyContext dependencyContext = new DependencyContextBuilder(mainProject, includeRuntimeFileVersions: false, runtimeGraph: null, projectContext: projectContext, libraryLookup: lockFileLookup)
                .WithDirectReferences(directReferences)
                .WithCompilationOptions(compilationOptions)
                .WithResolvedNuGetFiles((ResolvedFile[]) resolvedNuGetFiles)
                .Build();

            JObject result = Save(dependencyContext);
            JObject baseline = ReadJson($"{baselineFileName}.deps.json");

            try
            {
                baseline
                    .Should()
                    .BeEquivalentTo(result);
            }
            catch
            {
                // write the result file out on failure for easy comparison

                using (JsonTextWriter writer = new JsonTextWriter(File.CreateText($"result-{baselineFileName}.deps.json")))
                {
                    JsonSerializer serializer = new JsonSerializer();
                    serializer.Formatting = Formatting.Indented;
                    serializer.Serialize(writer, result);
                }

                throw;
            }
        }

        public static IEnumerable<object[]> ProjectData
        {
            get
            {
                ITaskItem[] dotnetNewSatelliteAssemblies = new ITaskItem[]
                {
                    new MockTaskItem(
                        @"de\dotnet.new.resources.dll",
                        new Dictionary<string, string>
                        {
                            { "Culture", "de" },
                            { "TargetPath", @"de\dotnet.new.resources.dll" },
                        }),
                    new MockTaskItem(
                        @"fr\dotnet.new.resources.dll",
                        new Dictionary<string, string>
                        {
                            { "Culture", "fr" },
                            { "TargetPath", @"fr\dotnet.new.resources.dll" },
                        }),
                };

                var resolvedNuGetFiles = new[]
                {
                    new ResolvedFile("Newtonsoft.Json.dll", "",
                        new PackageIdentity("Newtonsoft.Json", new NuGetVersion("9.0.1")),
                        AssetType.Runtime,
                        "lib/netstandard1.0/Newtonsoft.Json.dll"),

                    new ResolvedFile("System.Collections.NonGeneric.dll", "",
                        new PackageIdentity("System.Collections.NonGeneric", new NuGetVersion("4.0.1")),
                        AssetType.Runtime,
                        "lib/netstandard1.3/System.Collections.NonGeneric.dll"),

                    new ResolvedFile("System.Runtime.Serialization.Primitives.dll", "",
                        new PackageIdentity("System.Runtime.Serialization.Primitives", new NuGetVersion("4.1.1")),
                        AssetType.Runtime,
                        "lib/netstandard1.3/System.Runtime.Serialization.Primitives.dll")
                };

                return new[]
                {
                    new object[] { "dotnet.new", "1.0.0", null, "dotnet.new", null, null, null, null, null},
                    new object[] { "dotnet.new", "1.0.0", null, "dotnet.new.resources", null, dotnetNewSatelliteAssemblies, null, null, null },
                    new object[] { "simple.dependencies", "1.0.0", null, "simple.dependencies", null, null, null, null, resolvedNuGetFiles },
                };



            }
        }

        private static JObject ReadJson(string path)
        {
            using (JsonTextReader jsonReader = new JsonTextReader(File.OpenText(path)))
            {
                return JObject.Load(jsonReader);
            }
        }

        private JObject Save(DependencyContext dependencyContext)
        {
            using (var memoryStream = new MemoryStream())
            {
                new DependencyContextWriter().Write(dependencyContext, memoryStream);
                using (var readStream = new MemoryStream(memoryStream.ToArray()))
                {
                    using (var textReader = new StreamReader(readStream))
                    {
                        using (var reader = new JsonTextReader(textReader))
                        {
                            return JObject.Load(reader);
                        }
                    }
                }
            }
        }

        [Fact]
        public void ItDoesntCreateReferenceAssembliesWhenNoCompilationOptions()
        {
            DependencyContext dependencyContext = BuildDependencyContextWithReferenceAssemblies(useCompilationOptions: false);

            dependencyContext.CompileLibraries.Should().BeEmpty();
            dependencyContext
                .RuntimeLibraries
                .Should()
                .NotContain(l => l.Type == "referenceassembly");
            dependencyContext
                .RuntimeLibraries
                .SelectMany(l => l.Dependencies)
                .Should()
                .NotBeEmpty()
                .And
                .NotContain(d => d.Name == "System.NotConflicting")
                .And
                .NotContain(d => d.Name == "System.Collections.NonGeneric.Reference");
        }

        [Fact]
        public void ItHandlesReferenceAndPackageReferenceNameCollisions()
        {
            DependencyContext dependencyContext = BuildDependencyContextWithReferenceAssemblies(useCompilationOptions: true);

            dependencyContext.CompileLibraries.Should()
                .Contain(c => c.Name == "System.NotConflicting" && c.Type == "referenceassembly");

            // Note: System.Collections.NonGeneric is referenced in the lockfile, so DependencyContextBuilder
            // appends ".Reference" to make it unique
            dependencyContext.CompileLibraries.Should()
                .Contain(c => c.Name == "System.Collections.NonGeneric.Reference" && c.Type == "referenceassembly");
            dependencyContext.CompileLibraries.Should()
                .Contain(c => c.Name == "System.Collections.NonGeneric.Reference.Reference" && c.Type == "referenceassembly");
        }

        private DependencyContext BuildDependencyContextWithReferenceAssemblies(bool useCompilationOptions)
        {
            string mainProjectName = "simple.dependencies";
            LockFile lockFile = TestLockFiles.GetLockFile(mainProjectName);

            SingleProjectInfo mainProject = SingleProjectInfo.Create(
                "/usr/Path",
                mainProjectName,
                ".dll",
                "1.0.0",
                new ITaskItem[] { });

            ITaskItem[] referencePaths = new ITaskItem[]
            {
                new MockTaskItem(
                    "/usr/Path/System.NotConflicting.dll",
                    new Dictionary<string, string>
                    {
                        { "CopyLocal", "false" },
                        { "FusionName", "System.NotConflicting, Version=4.0.0.0, Culture=neutral, PublicKeyToken=null" },
                        { "Version", "" },
                    }),
                new MockTaskItem(
                    "/usr/Path/System.Collections.NonGeneric.dll",
                    new Dictionary<string, string>
                    {
                        { "CopyLocal", "false" },
                        { "FusionName", "System.Collections.NonGeneric, Version=4.0.0.0, Culture=neutral, PublicKeyToken=null" },
                        { "Version", "" },
                    }),
                new MockTaskItem(
                    "/usr/Path/System.Collections.NonGeneric.Reference.dll",
                    new Dictionary<string, string>
                    {
                        { "CopyLocal", "false" },
                        { "FusionName", "System.Collections.NonGeneric.Reference, Version=4.0.0.0, Culture=neutral, PublicKeyToken=null" },
                        { "Version", "" },
                    }),
            };

            ProjectContext projectContext = lockFile.CreateProjectContext(
                FrameworkConstants.CommonFrameworks.NetCoreApp10.GetShortFolderName(),
                runtime: null,
                platformLibraryName: Constants.DefaultPlatformLibrary,
                runtimeFrameworks: null,
                isSelfContained: false);

            CompilationOptions compilationOptions = 
                useCompilationOptions ? CreateCompilationOptions() :
                null;

            DependencyContext dependencyContext = new DependencyContextBuilder(mainProject, includeRuntimeFileVersions: false, runtimeGraph: null, projectContext: projectContext, libraryLookup: new LockFileLookup(lockFile))
                .WithReferenceAssemblies(ReferenceInfo.CreateReferenceInfos(referencePaths))
                .WithCompilationOptions(compilationOptions)
                .Build();

            // ensure the DependencyContext can be written out successfully - it has no duplicate dependency names
            Save(dependencyContext);

            return dependencyContext;
        }

        private static CompilationOptions CreateCompilationOptions()
        {
            return new CompilationOptions(
                    defines: new[] { "DEBUG", "TRACE" },
                    languageVersion: "6",
                    platform: "x64",
                    allowUnsafe: true,
                    warningsAsErrors: false,
                    optimize: null,
                    keyFile: "../keyfile.snk",
                    delaySign: null,
                    publicSign: null,
                    debugType: "portable",
                    emitEntryPoint: true,
                    generateXmlDocumentation: true);
        }

        [Fact]
        public void ItCanGenerateTheRuntimeFallbackGraph()
        {
            string mainProjectName = "simple.dependencies";
            LockFile lockFile = TestLockFiles.GetLockFile(mainProjectName);

            SingleProjectInfo mainProject = SingleProjectInfo.Create(
                "/usr/Path",
                mainProjectName,
                ".dll",
                "1.0.0",
                new ITaskItem[] { });

            ProjectContext projectContext = lockFile.CreateProjectContext(
                FrameworkConstants.CommonFrameworks.NetCoreApp10.GetShortFolderName(),
                runtime: null,
                platformLibraryName: Constants.DefaultPlatformLibrary,
                runtimeFrameworks: null,
                isSelfContained: true);

            var runtimeGraph = new RuntimeGraph(
                new RuntimeDescription []
                {
                    new RuntimeDescription("os-arch", new string [] { "os", "base" }),
                    new RuntimeDescription("new_os-arch", new string [] { "os-arch", "os", "base" }),
                    new RuntimeDescription("os-new_arch", new string [] { "os-arch", "os", "base" }),
                    new RuntimeDescription("new_os-new_arch", new string [] { "new_os-arch", "os-new_arch", "os-arch", "os", "base" }),
                    new RuntimeDescription("os-another_arch", new string [] { "os", "base" })
                });

            void CheckRuntimeFallbacks(string runtimeIdentifier, int fallbackCount)
            {
                projectContext.LockFileTarget.RuntimeIdentifier = runtimeIdentifier;
                var dependencyContextBuilder = new DependencyContextBuilder(mainProject, includeRuntimeFileVersions: false, runtimeGraph, projectContext, libraryLookup: new LockFileLookup(lockFile));
                var runtimeFallbacks = dependencyContextBuilder.Build().RuntimeGraph;

                runtimeFallbacks
                    .Count()
                    .Should()
                    .Be(fallbackCount);

                runtimeFallbacks
                    .Any(runtimeFallback => !runtimeFallback.Runtime.Equals(runtimeIdentifier) && !runtimeFallback.Fallbacks.Contains(runtimeIdentifier))
                    .Should()
                    .BeFalse();
            }

            CheckRuntimeFallbacks("os-arch", 4);
            CheckRuntimeFallbacks("new_os-arch", 2);
            CheckRuntimeFallbacks("os-new_arch", 2);
            CheckRuntimeFallbacks("new_os-new_arch", 1);
            CheckRuntimeFallbacks("unrelated_os-unknown_arch", 0);
        }
    }
}
