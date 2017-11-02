using Microsoft.NET.TestFramework.Commands;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Xunit.Abstractions;

namespace Microsoft.NET.TestFramework
{
    public abstract class SdkTest : IDisposable
    {
        protected TestAssetsManager _testAssetsManager = new TestAssetsManager();

        protected bool UsingFullFrameworkMSBuild => TestContext.Current.ToolsetUnderTest.ShouldUseFullFrameworkMSBuild;

        protected ITestOutputHelper Log { get; }

        protected SdkTest(ITestOutputHelper log)
        {
            Log = log;
        }
        public void Dispose()
        {
            //  Skip path length validation if running on full framework MSBuild.  We do the path length validation
            //  to avoid getting path to long errors when copying the test drop in our build infrastructure.  However,
            //  those builds are only built with .NET Core MSBuild running on Windows
            if (!UsingFullFrameworkMSBuild && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                _testAssetsManager.ValidateDestinationDirectories();
            }
        }
    }
}
