using System;
using System.Collections.Generic;
using Shouldly;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.BackEnd.SdkResolution;
using Microsoft.Build.Construction;
using Microsoft.Build.Exceptions;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.UnitTests;
using Xunit;
using Xunit.Abstractions;
using Exception = System.Exception;
using SdkResolverBase = Microsoft.Build.Framework.SdkResolver;
using SdkResolverContextBase = Microsoft.Build.Framework.SdkResolverContext;
using SdkResultBase = Microsoft.Build.Framework.SdkResult;
using SdkResultFactoryBase = Microsoft.Build.Framework.SdkResultFactory;

namespace Microsoft.Build.Engine.UnitTests.BackEnd
{
    public class SdkResolverLoader_Tests
    {
        private readonly ITestOutputHelper _output;
        private readonly MockLogger _logger;
        private readonly LoggingContext _loggingContext;

        public SdkResolverLoader_Tests(ITestOutputHelper output)
        {
            _output = output;
            _logger = new MockLogger(output);
            ILoggingService loggingService = LoggingService.CreateLoggingService(LoggerMode.Synchronous, 1);
            loggingService.RegisterLogger(_logger);

            _loggingContext = new MockLoggingContext(
                loggingService,
                new BuildEventContext(0, 0, BuildEventContext.InvalidProjectContextId, 0, 0));
        }

        [Fact]
        public void AssertDefaultLoaderReturnsDefaultResolvers()
        {
            var loader = new SdkResolverLoader();

            var resolvers = loader.LoadResolvers(_loggingContext, new MockElementLocation("file"));

            resolvers.Select(i => i.GetType().FullName).ShouldBe(new [] { typeof(DefaultSdkResolver).FullName });
            
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

        /// <summary>
        /// Verifies that if an SDK resolver throws while creating an instance that a warning is logged.
        /// </summary>
        [Fact]
        public void VerifyThrowsWhenResolverFailsToLoad()
        {
            SdkResolverLoader sdkResolverLoader = new MockSdkResolverLoader
            {
                LoadResolverAssemblyFunc = (resolverPath, loggingContext, location) => typeof(SdkResolverLoader_Tests).GetTypeInfo().Assembly,
                FindPotentialSdkResolversFunc = (rootFolder, loc) => new List<string>
                {
                    "myresolver.dll"
                },
                GetResolverTypesFunc = assembly => new[] { typeof(MockSdkResolverThatDoesNotLoad) }
            };

            InvalidProjectFileException exception = Should.Throw<InvalidProjectFileException>(() =>
            {
                sdkResolverLoader.LoadResolvers(_loggingContext, ElementLocation.EmptyLocation);
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
                LoadResolverAssemblyFunc = (resolverPath, loggingContext, location) => typeof(SdkResolverLoader_Tests).GetTypeInfo().Assembly,
                FindPotentialSdkResolversFunc = (rootFolder, loc) => new List<string>
                {
                    "myresolver.dll"
                },
                GetResolverTypesFunc = assembly => new[] { typeof(MockSdkResolverNoPublicConstructor) }
            };

            InvalidProjectFileException exception = Should.Throw<InvalidProjectFileException>(() =>
            {
                sdkResolverLoader.LoadResolvers(_loggingContext, ElementLocation.EmptyLocation);
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
                LoadResolverAssemblyFunc = (resolverPath, loggingContext, location) => throw new Exception(expectedMessage),
                FindPotentialSdkResolversFunc = (rootFolder, loc) => new List<string>
                {
                    assemblyPath,
                }
            };

            InvalidProjectFileException exception = Should.Throw<InvalidProjectFileException>(() =>
            {
                sdkResolverLoader.LoadResolvers(_loggingContext, ElementLocation.EmptyLocation);
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

        private class MockSdkResolverThatDoesNotLoad : SdkResolverBase
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

        private class MockSdkResolverNoPublicConstructor : SdkResolverBase
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

        private class MockSdkResolverLoader : SdkResolverLoader
        {
            public Func<string, LoggingContext, ElementLocation, Assembly> LoadResolverAssemblyFunc { get; set; }

            public Func<string, ElementLocation, IList<string>> FindPotentialSdkResolversFunc { get; set; }

            public Func<Assembly, IEnumerable<Type>> GetResolverTypesFunc { get; set; }

            protected override Assembly LoadResolverAssembly(string resolverPath, LoggingContext loggingContext, ElementLocation location)
            {
                if (LoadResolverAssemblyFunc != null)
                {
                    return LoadResolverAssemblyFunc(resolverPath, loggingContext, location);
                }

                return base.LoadResolverAssembly(resolverPath, loggingContext, location);
            }

            protected override IEnumerable<Type> GetResolverTypes(Assembly assembly)
            {
                if (GetResolverTypesFunc != null)
                {
                    return GetResolverTypesFunc(assembly);
                }

                return base.GetResolverTypes(assembly);
            }

            internal override IList<string> FindPotentialSdkResolvers(string rootFolder, ElementLocation location)
            {
                if (FindPotentialSdkResolversFunc != null)
                {
                    return FindPotentialSdkResolversFunc(rootFolder, location);
                }

                return base.FindPotentialSdkResolvers(rootFolder, location);
            }
        }
    }
}
