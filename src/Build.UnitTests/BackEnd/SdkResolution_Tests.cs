using System;
using Microsoft.Build.BackEnd;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Construction;
using Microsoft.Build.Framework;
using Microsoft.Build.UnitTests;
using Microsoft.Build.UnitTests.BackEnd;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace Microsoft.Build.Engine.UnitTests.BackEnd
{
    public class SdkResolution_Tests
    {
        [Fact]
        public void AssertFirstResolverCanResolve()
        {
            var log = new StringBuilder();
            var sdk = new SdkReference("1sdkName", "referencedVersion", "minimumVersion");
            var logger = new MockLoggingService(message => log.AppendLine(message));
            var bec = new BuildEventContext(0, 0, 0, 0, 0);
            
            SdkResolution resolution = new SdkResolution(new MockLoaderStrategy());
            var result = resolution.GetSdkPath(sdk, logger, bec, new MockElementLocation("file"), "sln");

            Assert.Equal("resolverpath1", result);
            Assert.Equal("MockSdkResolver1 running", log.ToString().Trim());
        }

        [Fact]
        public void AssertFirstResolverErrorsSupressedWhenResolved()
        {
            // 2sdkName will cause MockSdkResolver1 to fail with an error reason. The error will not 
            // be logged because MockSdkResolver2 will succeed.
            var log = new StringBuilder();
            var sdk = new SdkReference("2sdkName", "referencedVersion", "minimumVersion");
            var logger = new MockLoggingService(message => log.AppendLine(message));
            var bec = new BuildEventContext(0, 0, 0, 0, 0);

            SdkResolution resolution = new SdkResolution(new MockLoaderStrategy());
            var result = resolution.GetSdkPath(sdk, logger, bec, new MockElementLocation("file"), "sln");

            var logResult = log.ToString();
            Assert.Equal("resolverpath2", result);

            // Both resolvers should run, and no ERROR string.
            Assert.Contains("MockSdkResolver1 running", logResult);
            Assert.Contains("MockSdkResolver2 running", logResult);

            // Resolver2 gives a warning on success or failure.
            Assert.Contains("WARNING2", logResult);
            Assert.DoesNotContain("ERROR", logResult);
        }

        [Fact]
        public void AssertAllResolverErrorsLoggedWhenSdkNotResolved()
        {
            var log = new StringBuilder();
            var sdk = new SdkReference("notfound", "referencedVersion", "minimumVersion");
            var logger = new MockLoggingService(message => log.AppendLine(message));
            var bec = new BuildEventContext(0, 0, 0, 0, 0);

            SdkResolution resolution = new SdkResolution(new MockLoaderStrategy());
            var result = resolution.GetSdkPath(sdk, logger, bec, new MockElementLocation("file"), "sln");

            var logResult = log.ToString();
            Assert.Null(result);
            Assert.Contains("MockSdkResolver1 running", logResult);
            Assert.Contains("MockSdkResolver2 running", logResult);
            Assert.Contains("ERROR1", logResult);
            Assert.Contains("ERROR2", logResult);
            Assert.Contains("WARNING2", logResult);
        }

        [Fact]
        public void AssertErrorLoggedWhenResolverThrows()
        {
            var log = new StringBuilder();
            var sdk = new SdkReference("1sdkName", "referencedVersion", "minimumVersion");
            var logger = new MockLoggingService(message => log.AppendLine(message));
            var bec = new BuildEventContext(0, 0, 0, 0, 0);

            SdkResolution resolution = new SdkResolution(new MockLoaderStrategy(true));
            var result = resolution.GetSdkPath(sdk, logger, bec, new MockElementLocation("file"), "sln");

            Assert.Equal("resolverpath1", result);
            Assert.Contains("EXMESSAGE", log.ToString());
        }

        private class MockLoaderStrategy : SdkResolverLoader
        {
            private readonly bool _includeErrorResolver;

            public MockLoaderStrategy(bool includeErrorResolver = false)
            {
                _includeErrorResolver = includeErrorResolver;
            }

            internal override IList<SdkResolver> LoadResolvers(ILoggingService logger, BuildEventContext bec, ElementLocation location)
            {
                return _includeErrorResolver
                    ? new List<SdkResolver> {new MockSdkResolverThrows(),new MockSdkResolver1(),new MockSdkResolver2()}
                    : new List<SdkResolver> {new MockSdkResolver1(), new MockSdkResolver2()};
            }
        }

        private class MockSdkResolver1 : SdkResolver
        {
            public override string Name => "MockSdkResolver1";
            public override int Priority => 1;

            public override SdkResult Resolve(SdkReference sdk, SdkResolverContext resolverContext, SdkResultFactory factory)
            {
                resolverContext.Logger.LogMessage("MockSdkResolver1 running", MessageImportance.Normal);

                if (sdk.Name.StartsWith("1"))
                    return factory.IndicateSuccess("resolverpath1", "version1");

                return factory.IndicateFailure(new[] {"ERROR1"});
            }
        }

        private class MockSdkResolver2 : SdkResolver
        {
            public override string Name => "MockSdkResolver2";
            public override int Priority => 2;

            public override SdkResult Resolve(SdkReference sdk, SdkResolverContext resolverContext, SdkResultFactory factory)
            {
                resolverContext.Logger.LogMessage("MockSdkResolver2 running", MessageImportance.Normal);

                if (sdk.Name.StartsWith("2"))
                    return factory.IndicateSuccess("resolverpath2", "version2", new[] {"WARNING2"});

                return factory.IndicateFailure(new[] { "ERROR2" }, new[] { "WARNING2" });
            }
        }

        private class MockSdkResolverThrows : SdkResolver
        {
            public override string Name => "MockSdkResolverThrows";
            public override int Priority => 0;

            public override SdkResult Resolve(SdkReference sdk, SdkResolverContext resolverContext, SdkResultFactory factory)
            {
                resolverContext.Logger.LogMessage("MockSdkResolverThrows running", MessageImportance.Normal);

                throw new ArithmeticException("EXMESSAGE");
            }
        }
    }
}
