// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.BackEnd.SdkResolution;
using Microsoft.Build.Construction;
using Microsoft.Build.Exceptions;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.UnitTests;
using Shouldly;
using Xunit;
using Xunit.Abstractions;
using Exception = System.Exception;
using SdkResolverBase = Microsoft.Build.Framework.SdkResolver;
using SdkResolverContextBase = Microsoft.Build.Framework.SdkResolverContext;
using SdkResultBase = Microsoft.Build.Framework.SdkResult;
using SdkResultFactoryBase = Microsoft.Build.Framework.SdkResultFactory;

#nullable disable

namespace Microsoft.Build.Engine.UnitTests.BackEnd
{
    public class SdkResolverLoader_Tests
    {
        private readonly ITestOutputHelper _output;
        private readonly MockLogger _logger;

        public SdkResolverLoader_Tests(ITestOutputHelper output)
        {
            _output = output;
            _logger = new MockLogger(output);
            ILoggingService loggingService = LoggingService.CreateLoggingService(LoggerMode.Synchronous, 1);
            loggingService.RegisterLogger(_logger);
        }

        [Fact]
        public void AssertDefaultLoaderReturnsDefaultResolvers()
        {
            var loader = new SdkResolverLoader();

            var resolvers = loader.LoadAllResolvers(new MockElementLocation("file"));

            resolvers.Select(i => i.GetType().FullName).ShouldBe(new[] { typeof(DefaultSdkResolver).FullName });

            _logger.ErrorCount.ShouldBe(0);
            _logger.WarningCount.ShouldBe(0);
        }

        [Fact]
        public void VerifySdkResolverLoaderFileDiscoveryPattern()
        {
            var root = FileUtilities.GetTemporaryDirectory();
            try
            {
                // Valid pattern is root\(Name)\(Name).dll. No other files should be considered.
                var d1 = Directory.CreateDirectory(Path.Combine(root, "Resolver1"));

                // Valid.
                var f1 = Path.Combine(d1.FullName, "Resolver1.dll");

                // Invalid, won't be considered.
                var f2 = Path.Combine(d1.FullName, "Dependency.dll");
                var f3 = Path.Combine(d1.FullName, "InvalidName.dll");
                var f4 = Path.Combine(d1.FullName, "NoResolver.txt");

                File.WriteAllText(f1, string.Empty);
                File.WriteAllText(f2, string.Empty);
                File.WriteAllText(f3, string.Empty);
                File.WriteAllText(f4, string.Empty);

                var strategy = new SdkResolverLoader();
                var files = strategy.FindPotentialSdkResolvers(root, new MockElementLocation("file"));

                files.Count.ShouldBe(1);
                files[0].ShouldBe(f1);
            }
            finally
            {
                FileUtilities.DeleteDirectoryNoThrow(root, true);
            }
        }

        [Fact]
        public void SdkResolverLoaderPrefersManifestFile()
        {
            var root = FileUtilities.GetTemporaryDirectory();
            try
            {
                var testFolder = Directory.CreateDirectory(Path.Combine(root, "MyTestResolver"));

                var wrongResolverDll = Path.Combine(testFolder.FullName, "MyTestResolver.dll");
                var resolverManifest = Path.Combine(testFolder.FullName, "MyTestResolver.xml");
                var assemblyToLoad = Path.Combine(root, "SomeOtherResolver.dll");

                File.WriteAllText(wrongResolverDll, string.Empty);
                File.WriteAllText(assemblyToLoad, string.Empty);

                File.WriteAllText(resolverManifest, $@"
                    <SdkResolver>
                      <Path>{assemblyToLoad}</Path>
                    </SdkResolver>");

                SdkResolverLoader loader = new SdkResolverLoader();
                var resolversFound = loader.FindPotentialSdkResolvers(root, new MockElementLocation("file"));

                resolversFound.Count.ShouldBe(1);
                resolversFound.First().ShouldBe(assemblyToLoad);
            }
            finally
            {
                FileUtilities.DeleteDirectoryNoThrow(root, true);
            }
        }

        /// <summary>
        /// Verifies that if an SDK resolver throws while creating an instance that a warning is logged.
        /// </summary>
        [Fact]
        public void VerifyThrowsWhenResolverFailsToLoad()
        {
            SdkResolverLoader sdkResolverLoader = new MockSdkResolverLoader
            {
                LoadResolverAssemblyFunc = (resolverPath) => typeof(SdkResolverLoader_Tests).GetTypeInfo().Assembly,
                FindPotentialSdkResolversFunc = (rootFolder, loc) => new List<string>
                {
                    "myresolver.dll"
                },
                GetResolverTypesFunc = assembly => new[] { typeof(MockSdkResolverThatDoesNotLoad) }
            };

            InvalidProjectFileException exception = Should.Throw<InvalidProjectFileException>(() =>
            {
                sdkResolverLoader.LoadAllResolvers(ElementLocation.EmptyLocation);
            });

            exception.Message.ShouldBe($"The SDK resolver type \"{nameof(MockSdkResolverThatDoesNotLoad)}\" failed to load. A8BB8B3131D3475D881ACD3AF8D75BD6");

            Exception innerException = exception.InnerException.ShouldBeOfType<Exception>();

            innerException.Message.ShouldBe(MockSdkResolverThatDoesNotLoad.ExpectedMessage);

            _logger.WarningCount.ShouldBe(0);
            _logger.ErrorCount.ShouldBe(0);
        }

        /// <summary>
        /// Verifies that when we attempt to create an instance of a resolver with no public constructor that a warning
        /// is logged with the appropriate message.
        /// </summary>
        [Fact]
        public void VerifyThrowsWhenResolverHasNoPublicConstructor()
        {
            SdkResolverLoader sdkResolverLoader = new MockSdkResolverLoader
            {
                LoadResolverAssemblyFunc = (resolverPath) => typeof(SdkResolverLoader_Tests).GetTypeInfo().Assembly,
                FindPotentialSdkResolversFunc = (rootFolder, loc) => new List<string>
                {
                    "myresolver.dll"
                },
                GetResolverTypesFunc = assembly => new[] { typeof(MockSdkResolverNoPublicConstructor) }
            };

            InvalidProjectFileException exception = Should.Throw<InvalidProjectFileException>(() =>
            {
                sdkResolverLoader.LoadAllResolvers(ElementLocation.EmptyLocation);
            });

            exception.Message.ShouldStartWith($"The SDK resolver type \"{nameof(MockSdkResolverNoPublicConstructor)}\" failed to load.");

            exception.InnerException.ShouldBeOfType<MissingMethodException>();

            _logger.WarningCount.ShouldBe(0);
            _logger.ErrorCount.ShouldBe(0);
        }

        /// <summary>
        /// Verifies that when a resolver assembly cannot be loaded, that a warning is logged and other resolvers are still loaded.
        /// </summary>
        [Fact]
        public void VerifyWarningLoggedWhenResolverAssemblyCannotBeLoaded()
        {
            const string assemblyPath = @"C:\foo\bar\myresolver.dll";
            const string expectedMessage = "91BF077D4E9646819DE7AB2CBA2637B6";

            SdkResolverLoader sdkResolverLoader = new MockSdkResolverLoader
            {
                LoadResolverAssemblyFunc = (resolverPath) => throw new Exception(expectedMessage),
                FindPotentialSdkResolversFunc = (rootFolder, loc) => new List<string>
                {
                    assemblyPath,
                }
            };

            InvalidProjectFileException exception = Should.Throw<InvalidProjectFileException>(() =>
            {
                sdkResolverLoader.LoadAllResolvers(ElementLocation.EmptyLocation);
            });

            exception.Message.ShouldBe($"The SDK resolver assembly \"{assemblyPath}\" could not be loaded. {expectedMessage}");

            Exception innerException = exception.InnerException.ShouldBeOfType<Exception>();

            innerException.Message.ShouldBe(expectedMessage);

            _logger.WarningCount.ShouldBe(0);
            _logger.ErrorCount.ShouldBe(0);
        }

        [Fact]
        public void SdkResolverLoaderReadsManifestFile()
        {
            using (var env = TestEnvironment.Create(_output))
            {
                var root = env.CreateFolder().Path;
                var resolverPath = Path.Combine(root, "MyTestResolver");
                var resolverManifest = Path.Combine(resolverPath, "MyTestResolver.xml");

                var assemblyToLoad = env.CreateFile(".dll").Path;

                Directory.CreateDirectory(resolverPath);
                File.WriteAllText(resolverManifest, $@"
                    <SdkResolver>
                      <Path>{assemblyToLoad}</Path>
                    </SdkResolver>");

                SdkResolverLoader loader = new SdkResolverLoader();
                var resolversFound = loader.FindPotentialSdkResolvers(root, new MockElementLocation("file"));

                resolversFound.Count.ShouldBe(1);
                resolversFound.First().ShouldBe(assemblyToLoad);
            }
        }

        [Fact]
        public void SdkResolverLoaderReadsManifestFileWithResolvableSdkPattern()
        {
            using (var env = TestEnvironment.Create(_output))
            {
                var root = env.CreateFolder().Path;
                var resolverPath = Path.Combine(root, "MyTestResolver");
                var resolverManifest = Path.Combine(resolverPath, "MyTestResolver.xml");

                var assemblyToLoad = env.CreateFile(".dll").Path;

                Directory.CreateDirectory(resolverPath);
                File.WriteAllText(resolverManifest, $@"
                    <SdkResolver>
                      <ResolvableSdkPattern>1&lt;.*</ResolvableSdkPattern>
                      <Path>{assemblyToLoad}</Path>
                    </SdkResolver>");

                SdkResolverLoader loader = new SdkResolverLoader();
                var resolversManifestsFound = loader.FindPotentialSdkResolversManifests(root, new MockElementLocation("file"));

                resolversManifestsFound.Count.ShouldBe(1);
                resolversManifestsFound.First().Path.ShouldBe(assemblyToLoad);
                resolversManifestsFound.First().ResolvableSdkRegex.ToString().ShouldBe("1<.*");
            }
        }

        [Fact]
        public void SdkResolverLoaderErrorsWithInvalidManifestFile()
        {
            using (var env = TestEnvironment.Create(_output))
            {
                var root = env.CreateFolder().Path;
                var resolverPath = Path.Combine(root, "MyTestResolver");
                var resolverManifest = Path.Combine(resolverPath, "MyTestResolver.xml");

                var assemblyToLoad = env.CreateFile(".dll").Path;

                Directory.CreateDirectory(resolverPath);
                File.WriteAllText(resolverManifest, $@"
                    <SdkResolver2>
                      <Path>{assemblyToLoad}</Path>
                    </SdkResolver2>");

                SdkResolverLoader loader = new SdkResolverLoader();

                var ex = Should.Throw<InvalidProjectFileException>(() => loader.FindPotentialSdkResolvers(root, new MockElementLocation("file")));
                ex.ErrorCode.ShouldBe("MSB4245");
            }
        }

        [Fact]
        public void SdkResolverLoaderErrorsWhenNoDllOrAssemblyFound()
        {
            using (var env = TestEnvironment.Create(_output))
            {
                var root = env.CreateFolder().Path;
                var resolverPath = Path.Combine(root, "MyTestResolver");

                Directory.CreateDirectory(resolverPath);
                SdkResolverLoader loader = new SdkResolverLoader();

                var ex = Should.Throw<InvalidProjectFileException>(() => loader.FindPotentialSdkResolvers(root, new MockElementLocation("file")));
                ex.ErrorCode.ShouldBe("MSB4246");
            }
        }

        [Fact]
        public void SdkResolverLoaderErrorsWhenManifestTargetMissing()
        {
            using (var env = TestEnvironment.Create(_output))
            {
                var root = env.CreateFolder().Path;
                var resolverPath = Path.Combine(root, "MyTestResolver");
                var resolverManifest = Path.Combine(resolverPath, "MyTestResolver.xml");

                // Note this does NOT create the file just gets a valid name
                var assemblyToLoad = env.GetTempFile(".dll").Path;

                Directory.CreateDirectory(resolverPath);
                File.WriteAllText(resolverManifest, $@"
                    <SdkResolver>
                      <Path>{assemblyToLoad}</Path>
                    </SdkResolver>");

                SdkResolverLoader loader = new SdkResolverLoader();
                var ex = Should.Throw<InvalidProjectFileException>(() => loader.FindPotentialSdkResolvers(root, new MockElementLocation("file")));
                ex.ErrorCode.ShouldBe("MSB4247");
            }
        }

        [Fact]
        public void SdkResolverLoaderHonorsIncludeDefaultEnvVar()
        {
            using (var env = TestEnvironment.Create(_output))
            {
                var origIncludeDefault = Environment.GetEnvironmentVariable("MSBUILDINCLUDEDEFAULTSDKRESOLVER");
                try
                {
                    var testRoot = env.CreateFolder().Path;
                    Environment.SetEnvironmentVariable("MSBUILDINCLUDEDEFAULTSDKRESOLVER", "false");
                    SdkResolverLoader loader = new MockSdkResolverLoader()
                    {
                        LoadResolversAction = (resolverPath, location, resolvers) =>
                        {
                            resolvers.Add(new MockSdkResolverWithAssemblyPath(resolverPath));
                        }
                    };
                    IReadOnlyList<SdkResolverBase> resolvers = loader.LoadAllResolvers(new MockElementLocation("file"));

                    resolvers.Count.ShouldBe(0);
                }
                finally
                {
                    Environment.SetEnvironmentVariable("MSBUILDINCLUDEDEFAULTSDKRESOLVER", origIncludeDefault);
                }
            }
        }

        [Fact]
        public void SdkResolverLoaderHonorsAdditionalResolversFolder()
        {
            using (var env = TestEnvironment.Create(_output))
            {
                var origResolversFolder = Environment.GetEnvironmentVariable("MSBUILDADDITIONALSDKRESOLVERSFOLDER");
                try
                {
                    var testRoot = env.CreateFolder().Path;
                    var additionalRoot = env.CreateFolder().Path;

                    var resolver1 = "Resolver1";
                    var resolver1Path = Path.Combine(additionalRoot, resolver1, $"{resolver1}.dll");
                    Directory.CreateDirectory(Path.Combine(testRoot, resolver1));
                    File.WriteAllText(Path.Combine(testRoot, resolver1, $"{resolver1}.dll"), string.Empty);
                    Directory.CreateDirectory(Path.Combine(additionalRoot, resolver1));
                    File.WriteAllText(resolver1Path, string.Empty);
                    var resolver2 = "Resolver2";
                    var resolver2Path = Path.Combine(testRoot, resolver2, $"{resolver2}.dll");
                    Directory.CreateDirectory(Path.Combine(testRoot, resolver2));
                    File.WriteAllText(resolver2Path, string.Empty);
                    var resolver3 = "Resolver3";
                    var resolver3Path = Path.Combine(additionalRoot, resolver3, $"{resolver3}.dll");
                    Directory.CreateDirectory(Path.Combine(additionalRoot, resolver3));
                    File.WriteAllText(resolver3Path, string.Empty);

                    Environment.SetEnvironmentVariable("MSBUILDADDITIONALSDKRESOLVERSFOLDER", additionalRoot);

                    SdkResolverLoader loader = new SdkResolverLoader();
                    IReadOnlyList<string> resolvers = loader.FindPotentialSdkResolvers(testRoot, new MockElementLocation("file"));

                    resolvers.ShouldBeSameIgnoringOrder(new[] { resolver1Path, resolver2Path, resolver3Path });
                }
                finally
                {
                    Environment.SetEnvironmentVariable("MSBUILDADDITIONALSDKRESOLVERSFOLDER", origResolversFolder);
                }
            }
        }

        /// <summary>
        /// Test that LoadResolverAssembly handles fallback behavior correctly based on BuildEnvironment flags.
        /// This test calls the actual LoadResolverAssembly method to ensure it fails when the fix is reverted.
        /// </summary>
        [Theory]
        [InlineData(false, false)]   // needsFallback = false (VS/MSBuild.exe), no fallback, should fail when Assembly.Load fails
        [InlineData(true, true)]     // needsFallback = true (API/dotnet CLI), has fallback, should succeed with LoadFrom
        public void LoadResolverAssembly_MSBuildSdkResolver_WithAndWithoutFallback(bool needsFallback, bool shouldSucceed)
        {
            using (var env = TestEnvironment.Create(_output))
            {
                // Save current BuildEnvironment to restore later
                var currentBuildEnvironment = BuildEnvironmentHelper.Instance;

                try
                {
                    // Setup BuildEnvironment based on test scenario
                    // needsFallback = true: Mode = Standalone && RunningInMSBuildExe = false (API/dotnet CLI)
                    // needsFallback = false: Mode = Standalone && RunningInMSBuildExe = true (MSBuild.exe direct usage)
                    // Note: We use Standalone mode for both cases to avoid VisualStudio mode requiring VisualStudioInstallRootDirectory
                    BuildEnvironmentMode mode = BuildEnvironmentMode.Standalone;
                    bool runningInMSBuildExe = !needsFallback;

                    // Use current MSBuild path or fallback to a valid path if null
                    // This ensures MSBuildToolsDirectory32 and MSBuildToolsDirectoryRoot are set correctly
                    string msBuildExePath = currentBuildEnvironment.CurrentMSBuildExePath;
                    if (string.IsNullOrEmpty(msBuildExePath))
                    {
                        // Use the executing assembly path as fallback
                        msBuildExePath = FileUtilities.ExecutingAssemblyPath;
                        // If that's also null/empty, use test assembly location
                        if (string.IsNullOrEmpty(msBuildExePath))
                        {
                            msBuildExePath = typeof(BuildEnvironmentHelper).Assembly.Location;
                        }
                    }

                    BuildEnvironmentHelper.ResetInstance_ForUnitTestsOnly(
                        new BuildEnvironment(
                            mode,
                            msBuildExePath,
                            currentBuildEnvironment.RunningTests,
                            runningInMSBuildExe,
                            currentBuildEnvironment.RunningInVisualStudio,
                            currentBuildEnvironment.VisualStudioInstallRootDirectory));

                    // Create resolver folder structure with the specific name that triggers special logic
                    var testRoot = env.CreateFolder().Path;
                    var resolverFolder = Path.Combine(testRoot, "Microsoft.DotNet.MSBuildSdkResolver");
                    Directory.CreateDirectory(resolverFolder);

                    var assemblyFile = Path.Combine(resolverFolder, "Microsoft.DotNet.MSBuildSdkResolver.dll");

                    // Create file based on test scenario
                    if (shouldSucceed)
                    {
                        // For fallback test: create a valid assembly file using the test assembly
                        // This avoids side effects from loading Microsoft.Build.dll copy
                        var sourceAssembly = typeof(MockSdkResolverWithAssemblyPath).Assembly;
                        string sourceLocation = sourceAssembly.Location;
                        if (string.IsNullOrEmpty(sourceLocation))
                        {
                            throw new InvalidOperationException("Source assembly location is null or empty");
                        }
                        File.Copy(sourceLocation, assemblyFile, true);
                    }
                    else
                    {
                        // For no-fallback test: create invalid assembly content to force Assembly.Load to fail
                        File.WriteAllText(assemblyFile, "invalid assembly content");
                    }

                    // Use MockSdkResolverLoader but don't mock LoadResolverAssemblyFunc
                    // This ensures we test the actual logic in SdkResolverLoader.cs
                    var loader = new MockSdkResolverLoader
                    {
                        FindPotentialSdkResolversFunc = (_, __) => new List<string> { assemblyFile },
                        GetResolverTypesFunc = assembly => new[] { typeof(MockSdkResolverWithAssemblyPath) }
                        // LoadResolverAssemblyFunc is not set, so it will call the real method
                    };

                    if (shouldSucceed)
                    {
                        // Test that loading succeeds with fallback logic
                        var resolvers = loader.LoadAllResolvers(new MockElementLocation("file"));
                        resolvers.ShouldNotBeNull();
                        resolvers.Count.ShouldBeGreaterThan(0);
                    }
                    else
                    {
                        // Should throw InvalidProjectFileException because:
                        // 1. needsFallback = false → no fallback, uses Assembly.Load directly
                        // 2. Assembly.Load fails on invalid assembly
                        // 3. No fallback → exception propagates
                        var exception = Should.Throw<InvalidProjectFileException>(() =>
                            loader.LoadAllResolvers(new MockElementLocation("file")));

                        exception.Message.ShouldContain("could not be loaded");
                    }
                }
                finally
                {
                    // Restore original BuildEnvironment to avoid test pollution
                    BuildEnvironmentHelper.ResetInstance_ForUnitTestsOnly(currentBuildEnvironment);
                }
            }
        }

        private sealed class MockSdkResolverThatDoesNotLoad : SdkResolverBase
        {
            public const string ExpectedMessage = "A8BB8B3131D3475D881ACD3AF8D75BD6";

            public MockSdkResolverThatDoesNotLoad()
            {
                throw new Exception(ExpectedMessage);
            }

            public override string Name => nameof(MockSdkResolverThatDoesNotLoad);

            public override int Priority => 0;

            public override SdkResultBase Resolve(SdkReference sdkReference, SdkResolverContextBase resolverContext, SdkResultFactoryBase factory)
            {
                throw new NotImplementedException();
            }
        }

        private sealed class MockSdkResolverNoPublicConstructor : SdkResolverBase
        {
            private MockSdkResolverNoPublicConstructor()
            {
            }

            public override string Name => nameof(MockSdkResolverNoPublicConstructor);

            public override int Priority => 0;

            public override SdkResultBase Resolve(SdkReference sdkReference, SdkResolverContextBase resolverContext, SdkResultFactoryBase factory)
            {
                throw new NotImplementedException();
            }
        }

        private sealed class MockSdkResolverWithAssemblyPath : SdkResolverBase
        {
            public string AssemblyPath;

            // Parameterless constructor for reflection-based instantiation
            public MockSdkResolverWithAssemblyPath()
                : this("")
            {
            }

            public MockSdkResolverWithAssemblyPath(string assemblyPath)
            {
                AssemblyPath = assemblyPath;
            }

            public override string Name => nameof(MockSdkResolverWithAssemblyPath);

            public override int Priority => 0;

            public override SdkResultBase Resolve(SdkReference sdkReference, SdkResolverContextBase resolverContext, SdkResultFactoryBase factory)
            {
                throw new NotImplementedException();
            }
        }

        private sealed class MockSdkResolverLoader : SdkResolverLoader
        {
            public Func<string, Assembly> LoadResolverAssemblyFunc { get; set; }

            public Func<string, ElementLocation, IReadOnlyList<string>> FindPotentialSdkResolversFunc { get; set; }

            public Func<Assembly, IEnumerable<Type>> GetResolverTypesFunc { get; set; }

            public Action<string, ElementLocation, List<SdkResolver>> LoadResolversAction { get; set; }

            protected override Assembly LoadResolverAssembly(string resolverPath)
            {
                if (LoadResolverAssemblyFunc != null)
                {
                    return LoadResolverAssemblyFunc(resolverPath);
                }

                return base.LoadResolverAssembly(resolverPath);
            }

            protected override IEnumerable<Type> GetResolverTypes(Assembly assembly)
            {
                if (GetResolverTypesFunc != null)
                {
                    return GetResolverTypesFunc(assembly);
                }

                return base.GetResolverTypes(assembly);
            }

            internal override IReadOnlyList<string> FindPotentialSdkResolvers(string rootFolder, ElementLocation location)
            {
                if (FindPotentialSdkResolversFunc != null)
                {
                    return FindPotentialSdkResolversFunc(rootFolder, location);
                }

                return base.FindPotentialSdkResolvers(rootFolder, location);
            }

            protected override void LoadResolvers(string resolverPath, ElementLocation location, List<SdkResolver> resolvers)
            {
                if (LoadResolversAction != null)
                {
                    LoadResolversAction(resolverPath, location, resolvers);
                    return;
                }
                base.LoadResolvers(resolverPath, location, resolvers);
            }
        }
    }
}
