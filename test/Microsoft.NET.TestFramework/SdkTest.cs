using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.NET.TestFramework
{
    public class SdkTest : IDisposable
    {
        protected TestAssetsManager _testAssetsManager = new TestAssetsManager();

        public SdkTest()
        {
            Environment.SetEnvironmentVariable("DOTNET_SKIP_FIRST_TIME_EXPERIENCE", "1");
        }

        public void Dispose()
        {
            _testAssetsManager.ValidateDestinationDirectories();
        }
    }
}
