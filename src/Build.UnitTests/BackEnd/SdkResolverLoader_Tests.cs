using Shouldly;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Build.BackEnd.SdkResolution;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.UnitTests;
using Microsoft.Build.UnitTests.BackEnd;
using Xunit;

namespace Microsoft.Build.Engine.UnitTests.BackEnd
{
    public class SdkResolverLoader_Tests
    {
        private readonly StringBuilder _log;
        private readonly MockLoggingContext _loggingContext;

        public SdkResolverLoader_Tests()
        {
            _log = new StringBuilder();

            var logger = new MockLoggingService(message => _log.AppendLine(message));
            var bec = new BuildEventContext(0, 0, 0, 0, 0);

            _loggingContext = new MockLoggingContext(logger, bec);
        }

        [Fact]
        public void AssertDefaultLoaderReturnsDefaultResolvers()
        {
            var loader = new SdkResolverLoader();

            var resolvers = loader.LoadResolvers(_loggingContext, new MockElementLocation("file"));

            resolvers.Select(i => i.GetType()).ShouldBe(new [] { typeof(DefaultSdkResolver) });
        }

        [Fact]
        public void VerifySdkResolverLoaderFileDiscoveryPattern()
        {
            var root = FileUtilities.GetTemporaryDirectory();
            try
            {
                // Valid pattern is root\(Name)\(Name).dll. No other files should be considered.
                var d1 = Directory.CreateDirectory(Path.Combine(root, "Resolver1"));
                var d2 = Directory.CreateDirectory(Path.Combine(root, "NoResolver"));

                // Valid.
                var f1 = Path.Combine(d1.FullName, "Resolver1.dll");

                // Invalid, won't be considered.
                var f2 = Path.Combine(d1.FullName, "Dependency.dll");
                var f3 = Path.Combine(d2.FullName, "InvalidName.dll");
                var f4 = Path.Combine(d2.FullName, "NoResolver.txt");

                File.WriteAllText(f1, string.Empty);
                File.WriteAllText(f2, string.Empty);
                File.WriteAllText(f3, string.Empty);
                File.WriteAllText(f4, string.Empty);

                var strategy = new SdkResolverLoader();
                var files = strategy.FindPotentialSdkResolvers(root);

                files.Count.ShouldBe(1);
                files[0].ShouldBe(f1);
            }
            finally
            {
                FileUtilities.DeleteDirectoryNoThrow(root, true);
            }
        }
    }
}
