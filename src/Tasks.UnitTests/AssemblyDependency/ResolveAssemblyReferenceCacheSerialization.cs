using System;
using System.IO;
using Microsoft.Build.Tasks;
using Microsoft.Build.Utilities;
using Xunit;

namespace Microsoft.Build.UnitTests.ResolveAssemblyReference_Tests
{
    public class ResolveAssemblyReferenceCacheSerialization
    {
        [Fact]
        public void RoundTripEmptyState()
        {
            string rarCacheFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".UnitTest.RarCache");
            var taskLoggingHelper = new TaskLoggingHelper(new MockEngine(), "TaskA");

            SystemState systemState = new();

            systemState.SerializeCacheByTranslator(rarCacheFile, taskLoggingHelper);

            var deserialized = SystemState.DeserializeCacheByTranslator(rarCacheFile, taskLoggingHelper);

            Assert.NotNull(deserialized);
        }

        [Fact]
        public void RoundTripFullFileState()
        {
            // read old file
            // white as TR
            // read as TR
            // write as BF
            // compare old and new BF

            string rarCacheFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".UnitTest.RarCache");
            var taskLoggingHelper = new TaskLoggingHelper(new MockEngine(), "TaskA");
        }
    }
}
