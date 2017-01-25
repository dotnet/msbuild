using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.NET.TestFramework
{
    public class SdkTest : IDisposable
    {
        protected TestAssetsManager _testAssetsManager = new TestAssetsManager();
        bool _shouldValidateDirectories;

        public SdkTest()
        {
            Environment.SetEnvironmentVariable("DOTNET_SKIP_FIRST_TIME_EXPERIENCE", "1");

            string msbuildPath = Environment.GetEnvironmentVariable("DOTNET_SDK_TEST_MSBUILD_PATH");

            //  Skip path length validation if running on full framework MSBuild.  We do the path length validation
            //  to avoid getting path to long errors when copying the test drop in our build infrastructure.  However,
            //  those builds are only built with .NET Core MSBuild.
            _shouldValidateDirectories = string.IsNullOrEmpty(msbuildPath);
        }

        public void Dispose()
        {
            if (_shouldValidateDirectories)
            {
                _testAssetsManager.ValidateDestinationDirectories();
            }
        }
    }
}
