// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.BackEnd.SdkResolution;
using Microsoft.Build.Construction;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Unittest;
using Microsoft.Build.UnitTests;
using Shouldly;
using Xunit;
using SdkResolverContextBase = Microsoft.Build.Framework.SdkResolverContext;
using SdkResultBase = Microsoft.Build.Framework.SdkResult;
using SdkResultFactoryBase = Microsoft.Build.Framework.SdkResultFactory;
using SdkResultImpl = Microsoft.Build.BackEnd.SdkResolution.SdkResult;

#nullable disable

namespace Microsoft.Build.Engine.UnitTests.BackEnd
{
    public class SdkResolverService_Tests
    {
        private readonly MockLogger _logger;
        private readonly LoggingContext _loggingContext;

        public SdkResolverService_Tests()
        {
            _logger = new MockLogger();
            ILoggingService loggingService = LoggingService.CreateLoggingService(LoggerMode.Synchronous, 1);
            loggingService.RegisterLogger(_logger);

            _loggingContext = new MockLoggingContext(
                loggingService,
                BuildEventContext.CreateInitial(BuildEventContext.InvalidSubmissionId, 0)
                    .WithProjectInstanceId(0)
                    .WithTargetId(0)
                    .WithTaskId(0));
        }

        [Fact]
        // Scenario: Sdk is not resolved.
        public void AssertAllResolverErrorsLoggedWhenSdkNotResolved()
        {
            var service = new SdkResolverService();
            service.InitializeForTests(new MockLoaderStrategy(includeResolversWithPatterns: true));

            SdkReference sdk = new SdkReference("notfound", "referencedVersion", "minimumVersion");

            var result = service.ResolveSdk(BuildEventContext.InvalidSubmissionId, sdk, _loggingContext, new MockElementLocation("file"), "sln", "projectPath", interactive: false, isRunningInVisualStudio: false, failOnUnresolvedSdk: true);

            result.Success.ShouldBeFalse();
            result.ShouldNotBeNull();
            result.SdkReference.ShouldNotBeNull();
            result.SdkReference.Name.ShouldBe("notfound");
            result.SdkReference.Version.ShouldBe("referencedVersion");
            result.SdkReference.MinimumVersion.ShouldBe("minimumVersion");

            _logger.BuildMessageEvents.Select(i => i.Message).ShouldContain("MockSdkResolver1 running");
            _logger.BuildMessageEvents.Select(i => i.Message).ShouldContain("MockSdkResolver2 running");
            _logger.BuildMessageEvents.Select(i => i.Message).ShouldNotContain("MockSdkResolverWithResolvableSdkPattern1 running");
            _logger.BuildMessageEvents.Select(i => i.Message).ShouldContain("MockSdkResolverWithResolvableSdkPattern2 running");

            // First error is a generic "we failed" message.
            _logger.Errors[0].Message.ShouldBe(ResourceUtilities.FormatResourceStringIgnoreCodeAndKeyword("FailedToResolveSDK", "notfound", string.Join($"{Environment.NewLine}  ", new[] {
                "ERROR4",
                ResourceUtilities.FormatResourceStringStripCodeAndKeyword("SDKResolverReturnedNull", "MockResolverReturnsNull"),
                "ERROR1",
                "ERROR2",
                "notfound"
            })));
            _logger.Warnings.Select(i => i.Message).ShouldBe(new[] { "WARNING4", "WARNING2" });
        }

        [Fact]
        public void AssertSingleResolverErrorLoggedWhenSdkNotResolved()
        {
            var service = new SdkResolverService();

            // Use mock loader that only provides a single resolver
            service.InitializeForTests(new MockLoaderStrategy(includeSingleResolverOnly: true));

            var sdk = new SdkReference("notfound", "referencedVersion", "minimumVersion");

            var result = service.ResolveSdk(BuildEventContext.InvalidSubmissionId, sdk, _loggingContext, new MockElementLocation("file"), "sln", "projectPath", interactive: false, isRunningInVisualStudio: false, failOnUnresolvedSdk: true);

            result.Success.ShouldBeFalse();
            result.ShouldNotBeNull();
            result.SdkReference.ShouldNotBeNull();
            result.SdkReference.Name.ShouldBe("notfound");

            // Check that only the simplified error (no MSBuild wrapper) is logged
            _logger.Errors.Count.ShouldBe(1);
            _logger.Errors[0].Message.ShouldBe(
                ResourceUtilities.FormatResourceStringIgnoreCodeAndKeyword(
                    "SingleResolverFailedToResolveSDK",
                    "notfound",
                    "MockSdkResolver1",
                    "ERROR1"));
        }

        [Fact]
        public void AssertResolutionWarnsIfResolvedVersionIsDifferentFromReferencedVersion()
        {
            var sdk = new SdkReference("foo", "1.0.0", null);

            var service = new SdkResolverService();
            service.InitializeForTests(
                null,
                new List<SdkResolver>
                {
                    new SdkUtilities.ConfigurableMockSdkResolver(
                        new SdkResultImpl(
                            sdk,
                            "path",
                            "2.0.0",
                            Enumerable.Empty<string>()))
                });

            var result = service.ResolveSdk(BuildEventContext.InvalidSubmissionId, sdk, _loggingContext, new MockElementLocation("file"), "sln", "projectPath", interactive: false, isRunningInVisualStudio: false, failOnUnresolvedSdk: true);

            result.Path.ShouldBe("path");

            _logger.WarningCount.ShouldBe(1);
            _logger.Warnings.First().Code.ShouldStartWith("MSB4241");
        }

        [Fact]
        public void AssertResolverThrows()
        {
            var service = new SdkResolverService();
            service.InitializeForTests(new MockLoaderStrategy(includeErrorResolver: true));

            SdkReference sdk = new SdkReference("1sdkName", "version1", "minimumVersion");

            // When an SDK resolver throws, the expander will catch it and stop the build.
            SdkResolverException e = Should.Throw<SdkResolverException>(() => service.ResolveSdk(BuildEventContext.InvalidSubmissionId, sdk, _loggingContext, new MockElementLocation("file"), "sln", "projectPath", interactive: false, isRunningInVisualStudio: false, failOnUnresolvedSdk: true));
            e.Resolver.Name.ShouldBe("MockSdkResolverThrows");
            e.Sdk.Name.ShouldBe("1sdkName");
        }

        [Fact]
        // Scenario: MockSdkResolverWithResolvableSdkPattern2 is a specific resolver (i.e. resolver with pattern)
        // and it successfully resolves sdk.
        public void AssertSecondResolverWithPatternCanResolve()
        {
            var service = new SdkResolverService();
            service.InitializeForTests(new MockLoaderStrategy(includeResolversWithPatterns: true));

            SdkReference sdk = new SdkReference("2sdkName", "referencedVersion", "minimumVersion");

            var result = service.ResolveSdk(BuildEventContext.InvalidSubmissionId, sdk, _loggingContext, new MockElementLocation("file"), "sln", "projectPath", interactive: false, isRunningInVisualStudio: false, failOnUnresolvedSdk: true);

            result.Path.ShouldBe("resolverpathwithresolvablesdkpattern2");
            _logger.BuildMessageEvents.Select(i => i.Message).ShouldContain("MockSdkResolverWithResolvableSdkPattern2 running");
            _logger.BuildMessageEvents.Select(i => i.Message).ShouldNotContain("MockSdkResolver1 running");
            _logger.BuildMessageEvents.Select(i => i.Message).ShouldNotContain("MockSdkResolver2 running");
        }

#if DEBUG
        internal string TryResolveSdk(SdkResolverService service)
        {
            var message = "";
            SdkReference sdk = new SdkReference("2sdkName", "referencedVersion", "minimumVersion");
            try
            {
                service.ResolveSdk(BuildEventContext.InvalidSubmissionId,
                                                        sdk,
                                                        _loggingContext,
                                                        new MockElementLocation("file"),
                                                        "sln",
                                                        "projectPath",
                                                        interactive: false,
                                                        isRunningInVisualStudio: false,
                                                        failOnUnresolvedSdk: true);
            }
            catch (Exception e)
            {
                message = e.ToString();
            }
            return message;
        }


        [Fact]
        // Scenario: we want to test that we solved the contention described here: https://github.com/dotnet/msbuild/issues/7927#issuecomment-1232470838
        public async Task AssertResolverPopulationContentionNotPresent()
        {
            var service = new SdkResolverServiceTextExtension();
            service.InitializeForTests(new MockLoaderStrategy(includeResolversWithPatterns: true));

            SdkReference sdk = new SdkReference("2sdkName", "referencedVersion", "minimumVersion");

            var res1 = Task.Run(() => TryResolveSdk(service));

            Thread.Sleep(200);
            var res2 = Task.Run(() => TryResolveSdk(service));
            string message1 = await res1;
            string message2 = await res2;

            Assert.Equal("", message1);
            Assert.Equal("", message2);
        }
#endif

        [Fact]
        // Scenario: MockSdkResolverWithResolvableSdkPattern1 is a specific resolver, it is loaded but did not resolve sdk.
        // MockSdkResolver1 is a general resolver (i.e. resolver without pattern), it resolves sdk on a fallback.
        public void AssertFirstResolverCanResolve()
        {
            var service = new SdkResolverService();
            service.InitializeForTests(new MockLoaderStrategy());

            SdkReference sdk = new SdkReference("1sdkName", "referencedVersion", "minimumVersion");

            var result = service.ResolveSdk(BuildEventContext.InvalidSubmissionId, sdk, _loggingContext, new MockElementLocation("file"), "sln", "projectPath", interactive: false, isRunningInVisualStudio: false, failOnUnresolvedSdk: true);

            result.Path.ShouldBe("resolverpath1");
            _logger.BuildMessageEvents.Select(i => i.Message).ShouldContain("MockSdkResolver1 running");
            _logger.BuildMessageEvents.Select(i => i.Message).ShouldNotContain("MockSdkResolverWithResolvableSdkPattern1 running");
        }

        [Fact]
        // Scenario: MockSdkResolver1 has higher priority than MockSdkResolverWithResolvableSdkPattern1 but MockSdkResolverWithResolvableSdkPattern1 resolves sdk,
        // becuase MockSdkResolver1 is general and MockSdkResolverWithResolvableSdkPattern1 is specific.
        public void AssertFirstResolverWithPatternCanResolve()
        {
            var service = new SdkResolverService();
            service.InitializeForTests(new MockLoaderStrategy(includeResolversWithPatterns: true));

            SdkReference sdk = new SdkReference("11sdkName", "referencedVersion", "minimumVersion");

            var result = service.ResolveSdk(BuildEventContext.InvalidSubmissionId, sdk, _loggingContext, new MockElementLocation("file"), "sln", "projectPath", interactive: false, isRunningInVisualStudio: false, failOnUnresolvedSdk: true);

            result.Path.ShouldBe("resolverpathwithresolvablesdkpattern1");
            _logger.BuildMessageEvents.Select(i => i.Message).ShouldContain("MockSdkResolverWithResolvableSdkPattern1 running");
            _logger.BuildMessageEvents.Select(i => i.Message).ShouldNotContain("MockSdkResolver1 running");
        }

        [Fact]
        public void AssertSdkResolutionMessagesAreLogged()
        {
            var service = new SdkResolverService();
            service.InitializeForTests(new MockLoaderStrategy());
            SdkReference sdk = new SdkReference("1sdkName", "referencedVersion", "minimumVersion");

            var result = service.ResolveSdk(BuildEventContext.InvalidSubmissionId, sdk, _loggingContext, new MockElementLocation("file"), "sln", "projectPath", interactive: false, isRunningInVisualStudio: false, failOnUnresolvedSdk: true);

            // First resolver attempted to resolve, but failed.
            _logger.BuildMessageEvents.Select(i => i.Message).ShouldContain(ResourceUtilities.FormatResourceStringStripCodeAndKeyword("SDKResolverAttempt", nameof(MockResolverReturnsNull), sdk.ToString(), "null",
                ResourceUtilities.FormatResourceStringStripCodeAndKeyword("SDKResolverReturnedNull", nameof(MockResolverReturnsNull))));
            // Second resolver succeeded.
            _logger.BuildMessageEvents.Select(i => i.Message).ShouldContain(ResourceUtilities.FormatResourceStringStripCodeAndKeyword("SucceededToResolveSDK", sdk.ToString(), nameof(MockSdkResolver1), result.Path, result.Version));
        }

        [Fact]
        public void AssertSdkResolutionMessagesAreLoggedInEventSource()
        {
            using var eventSourceTestListener = new EventSourceTestHelper();
            var service = new SdkResolverService();
            service.InitializeForTests(new MockLoaderStrategy(false, false, true));
            var sdkName = Guid.NewGuid().ToString();
            SdkReference sdk = new SdkReference(sdkName, "referencedVersion", "minimumVersion");

            service.ResolveSdk(BuildEventContext.InvalidSubmissionId, sdk, _loggingContext, new MockElementLocation("file"), "sln", "projectPath", interactive: false, isRunningInVisualStudio: false, failOnUnresolvedSdk: true);
            var eventsLogged = eventSourceTestListener.GetEvents();
            eventsLogged.ShouldContain(x => x.EventId == 64); // Start of the sdk resolve
            eventsLogged.ShouldContain(x => x.EventId == 65 && x.Payload[1].ToString() == sdkName);
        }

        [Fact]
        public void AssertFirstResolverErrorsSupressedWhenResolved()
        {
            var service = new SdkResolverService();
            service.InitializeForTests(new MockLoaderStrategy());

            // 2sdkName will cause MockSdkResolver1 to fail with an error reason. The error will not
            // be logged because MockSdkResolver2 will succeed.
            SdkReference sdk = new SdkReference("2sdkName", "version2", "minimumVersion");

            var result = service.ResolveSdk(BuildEventContext.InvalidSubmissionId, sdk, _loggingContext, new MockElementLocation("file"), "sln", "projectPath", interactive: false, isRunningInVisualStudio: false, failOnUnresolvedSdk: true);

            result.Path.ShouldBe("resolverpath2");

            // Both resolvers should run, and no ERROR string.
            _logger.BuildMessageEvents.Select(i => i.Message).ShouldContain("MockSdkResolver1 running");
            _logger.BuildMessageEvents.Select(i => i.Message).ShouldContain("MockSdkResolver2 running");

            // Resolver2 gives a warning on success or failure.
            _logger.Warnings.Select(i => i.Message).ShouldBe(new[] { "WARNING2" });
            _logger.ErrorCount.ShouldBe(0);
        }

        [Fact]
        public void AssertResolverHasStatePreserved()
        {
            const int submissionId = 5;

            var service = new SdkResolverService();
            service.InitializeForTests(new MockLoaderStrategy());

            SdkReference sdk = new SdkReference("othersdk", "1.0", "minimumVersion");

            // First call should not know state
            service.ResolveSdk(submissionId, sdk, _loggingContext, new MockElementLocation("file"), "sln", "projectPath", interactive: false, isRunningInVisualStudio: false, failOnUnresolvedSdk: true).Path.ShouldBe("resolverpath");

            // Second call should have received state
            service.ResolveSdk(submissionId, sdk, _loggingContext, new MockElementLocation("file"), "sln", "projectPath", interactive: false, isRunningInVisualStudio: false, failOnUnresolvedSdk: true).Path.ShouldBe(MockSdkResolverWithState.Expected);
        }

        [Fact]
        public void AssertResolverStateNotPreserved()
        {
            const int submissionId = BuildEventContext.InvalidSubmissionId;

            var service = new SdkResolverService();
            service.InitializeForTests(new MockLoaderStrategy());

            SdkReference sdk = new SdkReference("othersdk", "1.0", "minimumVersion");

            // First call should not know state
            service.ResolveSdk(submissionId, sdk, _loggingContext, new MockElementLocation("file"), "sln", "projectPath", interactive: false, isRunningInVisualStudio: false, failOnUnresolvedSdk: true).Path.ShouldBe("resolverpath");

            // Second call should have received state
            service.ResolveSdk(submissionId, sdk, _loggingContext, new MockElementLocation("file"), "sln", "projectPath", interactive: false, isRunningInVisualStudio: false, failOnUnresolvedSdk: true).Path.ShouldBe("resolverpath");
        }

        [Fact]
        public void AssertResolversLoadedIfDefaultResolverSucceeds()
        {
            const int submissionId = BuildEventContext.InvalidSubmissionId;

            MockLoaderStrategy mockLoaderStrategy = new MockLoaderStrategy(includeDefaultResolver: true);
            var service = new SdkResolverService();
            service.InitializeForTests(mockLoaderStrategy);

            SdkReference sdk = new SdkReference("notfound", "1.0", "minimumVersion");

            service.ResolveSdk(submissionId, sdk, _loggingContext, new MockElementLocation("file"), "sln", "projectPath", interactive: false, isRunningInVisualStudio: false, failOnUnresolvedSdk: true).Path.ShouldBe("defaultpath");

#if NETCOREAPP
            // On Core, we check the default resolver *first*, so regular resolvers are not loaded.
            mockLoaderStrategy.ResolversHaveBeenLoaded.ShouldBeFalse();
            mockLoaderStrategy.ManifestsHaveBeenLoaded.ShouldBeFalse();
#else
            // On Framework, the default resolver is a fallback, so regular resolvers will have been loaded.
            mockLoaderStrategy.ResolversHaveBeenLoaded.ShouldBeTrue();
            mockLoaderStrategy.ManifestsHaveBeenLoaded.ShouldBeTrue();
#endif
        }

        [Theory]
        [InlineData(null, "1.0", true)]
        [InlineData("1.0", "1.0", true)]
        [InlineData("1.0-preview", "1.0-PrEvIeW", true)]
        [InlineData("1.0", "1.0.0", false)]
        [InlineData("1.0", "1.0.0.0", false)]
        [InlineData("1.0.0", "1.0.0.0", false)]
        [InlineData("1.2.0.0", "1.0.0.0", false)]
        [InlineData("1.2.3.0", "1.2.0.0", false)]
        [InlineData("1.2.3.4", "1.2.3.0", false)]
        public void IsReferenceSameVersionTests(string version1, string version2, bool expected)
        {
            SdkReference sdk = new SdkReference("Microsoft.NET.Sdk", version1, null);

            SdkResolverService.IsReferenceSameVersion(sdk, version2).ShouldBe(expected);
        }

        [Fact]
        public void CachingWrapperShouldWarnWhenMultipleVersionsAreReferenced()
        {
            var sdk = new SdkReference("foo", "1.0.0", null);

            var resolver = new SdkUtilities.ConfigurableMockSdkResolver(
                new SdkResultImpl(
                    sdk,
                    "path",
                    "1.0.0",
                    Enumerable.Empty<string>()));

            var service = new CachingSdkResolverService();
            service.InitializeForTests(
                null,
                new List<SdkResolver>
                {
                    resolver
                });

            var result = service.ResolveSdk(BuildEventContext.InvalidSubmissionId, sdk, _loggingContext, new MockElementLocation("file"), "sln", "projectPath", interactive: false, isRunningInVisualStudio: false, failOnUnresolvedSdk: true);
            resolver.ResolvedCalls.Count.ShouldBe(1);
            result.Path.ShouldBe("path");
            result.Version.ShouldBe("1.0.0");
            _logger.WarningCount.ShouldBe(0);

            result = service.ResolveSdk(BuildEventContext.InvalidSubmissionId, new SdkReference("foo", "2.0.0", null), _loggingContext, new MockElementLocation("file"), "sln", "projectPath", interactive: false, isRunningInVisualStudio: false, failOnUnresolvedSdk: true);
            resolver.ResolvedCalls.Count.ShouldBe(1);
            result.Path.ShouldBe("path");
            result.Version.ShouldBe("1.0.0");
            _logger.WarningCount.ShouldBe(1);
            _logger.Warnings.First().Code.ShouldBe("MSB4240");

            resolver.ResolvedCalls.First().Key.ShouldBe("foo");
            resolver.ResolvedCalls.Count.ShouldBe(1);
        }

        private void CreateMockSdkResultPropertiesAndItems(out Dictionary<string, string> propertiesToAdd, out Dictionary<string, SdkResultItem> itemsToAdd)
        {
            propertiesToAdd = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    {"PropertyFromSdkResolver", "ValueFromSdkResolver" }
                };

            itemsToAdd = new Dictionary<string, SdkResultItem>(StringComparer.OrdinalIgnoreCase)
                {
                    { "ItemNameFromSdkResolver", new SdkResultItem( "ItemValueFromSdkResolver",
                        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                        {
                            { "MetadataName", "MetadataValue" }
                        })
                    }
                };
        }

        private void ValidateExpectedPropertiesAndItems(bool includePropertiesAndItems, SdkResultBase result)
        {
            if (includePropertiesAndItems)
            {
                result.PropertiesToAdd.Count.ShouldBe(1);
                result.PropertiesToAdd["PropertyFromSdkResolver"].ShouldBe("ValueFromSdkResolver");

                result.ItemsToAdd.Count.ShouldBe(1);
                result.ItemsToAdd.Keys.Single().ShouldBe("ItemNameFromSdkResolver");
                result.ItemsToAdd.Values.Single().ItemSpec.ShouldBe("ItemValueFromSdkResolver");
                var metadata = result.ItemsToAdd.Values.Single().Metadata;
                metadata.ShouldBeSameIgnoringOrder(new[] { new KeyValuePair<string, string>("MetadataName", "MetadataValue") });
            }
            else
            {
                result.PropertiesToAdd.ShouldBeNull();
                result.ItemsToAdd.ShouldBeNull();
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void SdkResolverCanReturnNoPaths(bool includePropertiesAndItems)
        {
            var sdk = new SdkReference("foo", null, null);

            Dictionary<string, string> propertiesToAdd = null;
            Dictionary<string, SdkResultItem> itemsToAdd = null;

            if (includePropertiesAndItems)
            {
                CreateMockSdkResultPropertiesAndItems(out propertiesToAdd, out itemsToAdd);
            }

            var resolver = new SdkUtilities.ConfigurableMockSdkResolver(
                new SdkResultImpl(
                    sdk,
                    Enumerable.Empty<string>(),
                    version: null,
                    propertiesToAdd,
                    itemsToAdd,
                    warnings: null));

            var service = new SdkResolverService();
            service.InitializeForTests(null, new List<SdkResolver>() { resolver });

            var result = service.ResolveSdk(BuildEventContext.InvalidSubmissionId, sdk, _loggingContext, new MockElementLocation("file"), "sln", "projectPath", interactive: false, isRunningInVisualStudio: false, failOnUnresolvedSdk: true);

            result.Success.ShouldBeTrue();
            result.Path.ShouldBeNull();
            result.Version.ShouldBeNull();

            result.AdditionalPaths.ShouldBeNull();

            ValidateExpectedPropertiesAndItems(includePropertiesAndItems, result);

            _logger.WarningCount.ShouldBe(0);
        }

        [Fact]
        public void SdkResultCanReturnPropertiesAndItems()
        {
            string expectedPath = "Path/To/Return/From/Resolver";

            var sdk = new SdkReference("foo", null, null);

            Dictionary<string, string> propertiesToAdd;
            Dictionary<string, SdkResultItem> itemsToAdd;

            CreateMockSdkResultPropertiesAndItems(out propertiesToAdd, out itemsToAdd);

            var resolver = new SdkUtilities.ConfigurableMockSdkResolver(
                new SdkResultImpl(
                    sdk,
                    new[] { expectedPath },
                    version: "1.0",
                    propertiesToAdd,
                    itemsToAdd,
                    warnings: null));

            var service = new SdkResolverService();
            service.InitializeForTests(null, new List<SdkResolver>() { resolver });

            var result = service.ResolveSdk(BuildEventContext.InvalidSubmissionId, sdk, _loggingContext, new MockElementLocation("file"), "sln", "projectPath", interactive: false, isRunningInVisualStudio: false, failOnUnresolvedSdk: true);

            result.Success.ShouldBeTrue();
            result.Path.ShouldBe(expectedPath);
            result.Version.ShouldBe("1.0");

            result.AdditionalPaths.ShouldBeNull();

            ValidateExpectedPropertiesAndItems(true, result);

            _logger.WarningCount.ShouldBe(0);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void SdkResultCanReturnMultiplePaths(bool includePropertiesAndItems)
        {
            string expectedPath1 = "First/Path/To/Return/From/Resolver";
            string expectedPath2 = "Second/Path/To/Return/From/Resolver";

            var sdk = new SdkReference("foo", "1.0", null);

            Dictionary<string, string> propertiesToAdd = null;
            Dictionary<string, SdkResultItem> itemsToAdd = null;

            if (includePropertiesAndItems)
            {
                CreateMockSdkResultPropertiesAndItems(out propertiesToAdd, out itemsToAdd);
            }

            var resolver = new SdkUtilities.ConfigurableMockSdkResolver(
                new SdkResultImpl(
                    sdk,
                    new[]
                    {
                        expectedPath1,
                        expectedPath2
                    },
                    version: "1.0",
                    propertiesToAdd,
                    itemsToAdd,
                    warnings: null));

            var service = new SdkResolverService();
            service.InitializeForTests(null, new List<SdkResolver>() { resolver });

            var result = service.ResolveSdk(BuildEventContext.InvalidSubmissionId, sdk, _loggingContext, new MockElementLocation("file"), "sln", "projectPath", interactive: false, isRunningInVisualStudio: false, failOnUnresolvedSdk: true);

            result.Success.ShouldBeTrue();

            var resultPaths = new List<string>();
            resultPaths.Add(result.Path);
            resultPaths.AddRange(result.AdditionalPaths);

            resultPaths.ShouldBeSameIgnoringOrder(new[]
            {
                expectedPath1,
                expectedPath2
            });

            ValidateExpectedPropertiesAndItems(includePropertiesAndItems, result);

            _logger.WarningCount.ShouldBe(0);
        }

        [Fact]
        public void AssertResolutionWarnsIfResolvedVersionIsDifferentFromReferencedVersionWithMultipleReturnPaths()
        {
            var expectedPath1 = "First/Path/To/Return/From/Resolver";
            var expectedPath2 = "Second/Path/To/Return/From/Resolver";

            var sdk = new SdkReference("foo", "1.0", null);
            Dictionary<string, string> propertiesToAdd;
            Dictionary<string, SdkResultItem> itemsToAdd;
            CreateMockSdkResultPropertiesAndItems(out propertiesToAdd, out itemsToAdd);

            var resolver = new SdkUtilities.ConfigurableMockSdkResolver(
                new SdkResultImpl(
                    sdk,
                    new[]
                    {
                        expectedPath1,
                        expectedPath2
                    },
                    version: "1.1",
                    propertiesToAdd,
                    itemsToAdd,
                    warnings: null));

            var service = new SdkResolverService();
            service.InitializeForTests(null, new List<SdkResolver>() { resolver });

            var result = service.ResolveSdk(BuildEventContext.InvalidSubmissionId, sdk, _loggingContext, new MockElementLocation("file"), "sln", "projectPath", interactive: false, isRunningInVisualStudio: false, failOnUnresolvedSdk: true);

            result.Success.ShouldBeTrue();

            var resultPaths = new List<string>();
            resultPaths.Add(result.Path);
            resultPaths.AddRange(result.AdditionalPaths);

            resultPaths.ShouldBeSameIgnoringOrder(new[]
            {
                expectedPath1,
                expectedPath2
            });

            ValidateExpectedPropertiesAndItems(true, result);

            _logger.WarningCount.ShouldBe(1);
            _logger.Warnings.First().Code.ShouldStartWith("MSB4241");
        }

        /// <summary>
        /// Verifies that an SDK resolver is only called once per build per SDK.
        /// </summary>
        [Fact]
        public void CachingWrapperShouldOnlyResolveOnce()
        {
            var sdk = new SdkReference("foo", "1.0.0", null);

            var resolver = new SdkUtilities.ConfigurableMockSdkResolver(
                new SdkResultImpl(
                    sdk,
                    "path",
                    "1.0.0",
                    Enumerable.Empty<string>()));

            var service = new CachingSdkResolverService();
            service.InitializeForTests(
                null,
                new List<SdkResolver>
                {
                    resolver
                });

            // Resolve the SDK 10 times in parallel
            Parallel.For(
                0,
                10,
                _ => service.ResolveSdk(BuildEventContext.InvalidSubmissionId, sdk, _loggingContext, new MockElementLocation("file"), "sln", "projectPath", interactive: false, isRunningInVisualStudio: false, failOnUnresolvedSdk: true));

            var result = resolver.ResolvedCalls.ShouldHaveSingleItem();

            result.Key.ShouldBe(sdk.Name);

            // The SDK resolver keeps track of the number of times it was called which should be 1
            result.Value.ShouldBe(1, $"The SDK resolver should have only been called once but was called {result.Value} times");
        }

        [Fact]
        public void InteractiveIsSetForResolverContext()
        {
            // Start with interactive false
            bool interactive = false;

            var service = new CachingSdkResolverService();

            service.InitializeForTests(
                resolvers: new List<SdkResolver>
                {
                    new SdkUtilities.ConfigurableMockSdkResolver((sdkRference, resolverContext, factory) =>
                    {
                        interactive = resolverContext.Interactive;

                        return null;
                    })
                });

            service.ResolveSdk(
                BuildEventContext.InvalidSubmissionId,
                new SdkReference("foo", "1.0.0", null),
                _loggingContext,
                new MockElementLocation("file"),
                "sln",
                "projectPath",
                // Pass along interactive and expect it to be received in the SdkResolverContext
                interactive: true,
                isRunningInVisualStudio: false,
                failOnUnresolvedSdk: true);

            interactive.ShouldBeTrue();
        }

        [Fact]
        public void IsRunningInVisualStudioIsSetForResolverContext()
        {
            bool isRunningInVisualStudio = false;

            var service = new CachingSdkResolverService();
            service.InitializeForTests(
                resolvers: new List<SdkResolver>
                {
                    new SdkUtilities.ConfigurableMockSdkResolver((sdkRference, resolverContext, factory) =>
                    {
                        isRunningInVisualStudio = resolverContext.IsRunningInVisualStudio;
                        return null;
                    })
                });

            var result = service.ResolveSdk(
                BuildEventContext.InvalidSubmissionId,
                new SdkReference("foo", "1.0.0", null),
                _loggingContext,
                new MockElementLocation("file"),
                "sln",
                "projectPath",
                false,
                // Pass along isRunningInVisualStudio and expect it to be received in the SdkResolverContext
                isRunningInVisualStudio: true,
                failOnUnresolvedSdk: true);

            isRunningInVisualStudio.ShouldBeTrue();
        }

        [Fact]
        public void AssertResolversCanReturnEnvVariables()
        {
            var service = new CachingSdkResolverService();

            service.InitializeForTests(
                resolvers: new List<SdkResolver>
                {
                    new SdkUtilities.ConfigurableMockSdkResolver((sdkReference, resolverContext, factory) =>
                    {
                        // Simulate returning environment variables
                        return factory.IndicateSuccess("envresolver", "1.0.0", null, null, environmentVariablesToAdd: new Dictionary<string, string>
                        {
                            { "EnvVar1", "Value1" }
                        });
                    })
                });
            var result = service.ResolveSdk(
                BuildEventContext.InvalidSubmissionId,
                new SdkReference("envresolver", "1.0.0", null),
                _loggingContext,
                new MockElementLocation("file"),
                "sln",
                "projectPath",
                interactive: false,
                isRunningInVisualStudio: false,
                failOnUnresolvedSdk: true);

            result.EnvironmentVariablesToAdd.Count.ShouldBe(1);
        }

        internal sealed class SdkResolverServiceTextExtension : SdkResolverService
        {

            internal bool _fake_initialization = false;
            internal IReadOnlyList<SdkResolverManifest> _fakeManifestRegistry;

            internal override void WaitIfTestRequires()
            {
                if (_fake_initialization)
                {
                    Thread.Sleep(10);
                }
            }
            internal override IReadOnlyList<SdkResolverManifest> GetResolverManifests(ElementLocation location)
            {
                return _fakeManifestRegistry;
            }

            internal override void InitializeForTests(SdkResolverLoader resolverLoader = null, IReadOnlyList<SdkResolver> resolvers = null)
            {
                if (resolverLoader != null)
                {
                    _sdkResolverLoader = resolverLoader;
                    _fake_initialization = true;
                    List<SdkResolverManifest> manifests = new List<SdkResolverManifest>();
                    for (int i = 1; i != 20; i++)
                    {
                        var man = new SdkResolverManifest(DisplayName: "TestResolversManifest", Path: null, ResolvableSdkRegex: new Regex("abc"));
                        manifests.Add(man);
                        man = new SdkResolverManifest(DisplayName: "TestResolversManifest", Path: null, null);
                        manifests.Add(man);
                    }
                    _fakeManifestRegistry = manifests.AsReadOnly();
                    return;
                }
            }
        }

        private sealed class MockLoaderStrategy : SdkResolverLoader
        {
            private List<SdkResolver> _resolvers;
            private List<SdkResolver> _defaultResolvers;
            private List<(string ResolvableSdkPattern, SdkResolver Resolver)> _resolversWithPatterns;

            public bool ResolversHaveBeenLoaded { get; private set; } = false;
            public bool ManifestsHaveBeenLoaded { get; private set; } = false;

            public MockLoaderStrategy(bool includeErrorResolver = false, bool includeResolversWithPatterns = false, bool includeDefaultResolver = false, bool includeSingleResolverOnly = false) : this()
            {
                if (includeSingleResolverOnly)
                {
                    _resolvers = new List<SdkResolver> { new MockSdkResolver1() };
                    return; // Exit early so other ones aren't added
                }

                if (includeErrorResolver)
                {
                    _resolvers.Add(new MockSdkResolverThrows());
                }

                if (includeResolversWithPatterns)
                {
                    _resolversWithPatterns.Add(("1.*", new MockSdkResolverWithResolvableSdkPattern1()));
                    _resolversWithPatterns.Add((".*", new MockSdkResolverWithResolvableSdkPattern2()));
                }

                if (includeDefaultResolver)
                {
                    _defaultResolvers.Add(new MockSdkResolverDefault());
                }
            }

            private MockLoaderStrategy()
            {
                _resolvers = new List<SdkResolver>
                {
                    new MockSdkResolver1(),
                    new MockSdkResolver2(),
                    new MockResolverReturnsNull(),
                    new MockSdkResolverWithState()
                };

                _defaultResolvers = new List<SdkResolver>();

                _resolversWithPatterns = new List<(string ResolvableSdkPattern, SdkResolver Resolver)>();
            }

            internal override IReadOnlyList<SdkResolver> LoadAllResolvers(ElementLocation location)
            {
                ResolversHaveBeenLoaded = true;

                return _resolvers.OrderBy(i => i.Priority).ToList();
            }

            internal override IReadOnlyList<SdkResolverManifest> GetResolversManifests(ElementLocation location)
            {
                ManifestsHaveBeenLoaded = true;

                var manifests = new List<SdkResolverManifest>();
                foreach (SdkResolver resolver in _resolvers)
                {
                    SdkResolverManifest sdkResolverManifest = new SdkResolverManifest(resolver.Name, null, null);
                    manifests.Add(sdkResolverManifest);
                }
                foreach ((string ResolvableSdkPattern, SdkResolver Resolver) pair in _resolversWithPatterns)
                {
                    SdkResolverManifest sdkResolverManifest = new SdkResolverManifest(
                        pair.Resolver.Name,
                        null,
                        new Regex(pair.ResolvableSdkPattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(500)));
                    manifests.Add(sdkResolverManifest);
                }
                return manifests;
            }

            protected internal override IReadOnlyList<SdkResolver> LoadResolversFromManifest(SdkResolverManifest manifest, ElementLocation location)
            {
                ResolversHaveBeenLoaded = true;

                var resolvers = new List<SdkResolver>();
                foreach (var resolver in _resolvers)
                {
                    if (resolver.Name == manifest.DisplayName)
                    {
                        resolvers.Add(resolver);
                    }
                }
                foreach (var pair in _resolversWithPatterns)
                {
                    if (pair.Resolver.Name == manifest.DisplayName)
                    {
                        resolvers.Add(pair.Resolver);
                    }
                }
                return resolvers.OrderBy(t => t.Priority).ToList();
            }

            internal override IReadOnlyList<SdkResolver> GetDefaultResolvers()
            {
                return _defaultResolvers;
            }
        }

        private sealed class MockResolverReturnsNull : SdkResolver
        {
            public override string Name => nameof(MockResolverReturnsNull);

            public override int Priority => -1;

            public override SdkResultBase Resolve(SdkReference sdkReference, SdkResolverContextBase resolverContext, SdkResultFactoryBase factory) => null;
        }

        private sealed class MockSdkResolver1 : SdkResolver
        {
            public override string Name => nameof(MockSdkResolver1);

            public override int Priority => 1;

            public override SdkResultBase Resolve(SdkReference sdk, SdkResolverContextBase resolverContext, SdkResultFactoryBase factory)
            {
                resolverContext.Logger.LogMessage("MockSdkResolver1 running", MessageImportance.Normal);

                if (sdk.Name.StartsWith("1"))
                {
                    return factory.IndicateSuccess("resolverpath1", "version1");
                }

                return factory.IndicateFailure(new[] { "ERROR1" });
            }
        }

        private sealed class MockSdkResolver2 : SdkResolver
        {
            public override string Name => nameof(MockSdkResolver2);

            public override int Priority => 2;

            public override SdkResultBase Resolve(SdkReference sdk, SdkResolverContextBase resolverContext, SdkResultFactoryBase factory)
            {
                resolverContext.Logger.LogMessage("MockSdkResolver2 running", MessageImportance.Normal);

                if (sdk.Name.StartsWith("2"))
                {
                    return factory.IndicateSuccess("resolverpath2", "version2", new[] { "WARNING2" });
                }

                return factory.IndicateFailure(new[] { "ERROR2" }, new[] { "WARNING2" });
            }
        }

        private sealed class MockSdkResolverWithResolvableSdkPattern1 : SdkResolver
        {
            public override string Name => nameof(MockSdkResolverWithResolvableSdkPattern1);

            public override int Priority => 2;

            public override SdkResultBase Resolve(SdkReference sdk, SdkResolverContextBase resolverContext, SdkResultFactoryBase factory)
            {
                resolverContext.Logger.LogMessage("MockSdkResolverWithResolvableSdkPattern1 running", MessageImportance.Normal);

                if (sdk.Name.StartsWith("11"))
                {
                    return factory.IndicateSuccess("resolverpathwithresolvablesdkpattern1", "version3");
                }

                return factory.IndicateFailure(new[] { "ERROR3" });
            }
        }

        private sealed class MockSdkResolverWithResolvableSdkPattern2 : SdkResolver
        {
            public override string Name => nameof(MockSdkResolverWithResolvableSdkPattern2);

            public override int Priority => 0;

            public override SdkResultBase Resolve(SdkReference sdk, SdkResolverContextBase resolverContext, SdkResultFactoryBase factory)
            {
                resolverContext.Logger.LogMessage("MockSdkResolverWithResolvableSdkPattern2 running", MessageImportance.Normal);

                if (sdk.Name.StartsWith("2"))
                {
                    return factory.IndicateSuccess("resolverpathwithresolvablesdkpattern2", "version4", new[] { "WARNING4" });
                }

                return factory.IndicateFailure(new[] { "ERROR4" }, new[] { "WARNING4" });
            }
        }

        private sealed class MockSdkResolverWithState : SdkResolver
        {
            public const string Expected = "01713226A202458F97D9074168DF2618";

            public override string Name => nameof(MockSdkResolverWithState);

            public override int Priority => 3;

            public override SdkResultBase Resolve(SdkReference sdkReference, SdkResolverContextBase resolverContext, SdkResultFactoryBase factory)
            {
                if (sdkReference.Name.Equals("notfound"))
                {
                    return factory.IndicateFailure(new string[] { "notfound" });
                }
                if (resolverContext.State != null)
                {
                    return factory.IndicateSuccess((string)resolverContext.State, "1.0");
                }

                resolverContext.State = Expected;

                return factory.IndicateSuccess("resolverpath", "1.0");
            }
        }

        private sealed class MockSdkResolverThrows : SdkResolver
        {
            public override string Name => nameof(MockSdkResolverThrows);
            public override int Priority => 0;

            public override SdkResultBase Resolve(SdkReference sdk, SdkResolverContextBase resolverContext, SdkResultFactoryBase factory)
            {
                resolverContext.Logger.LogMessage("MockSdkResolverThrows running", MessageImportance.Normal);

                throw new ArithmeticException("EXMESSAGE");
            }
        }

        private sealed class MockSdkResolverDefault : SdkResolver
        {
            public override string Name => nameof(MockSdkResolverDefault);
            public override int Priority => 9999;

            public override SdkResultBase Resolve(SdkReference sdk, SdkResolverContextBase resolverContext, SdkResultFactoryBase factory)
            {
                resolverContext.Logger.LogMessage("MockSdkResolverDefault running", MessageImportance.Normal);

                return factory.IndicateSuccess("defaultpath", string.Empty);
            }
        }
    }
}
