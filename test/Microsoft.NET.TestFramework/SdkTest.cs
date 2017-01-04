using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.NET.TestFramework
{
    public class SdkTest : IDisposable
    {
        protected TestAssetsManager _testAssetsManager = new TestAssetsManager();

        public void Dispose()
        {
            _testAssetsManager.ValidateDestinationDirectories();
        }
    }
}
