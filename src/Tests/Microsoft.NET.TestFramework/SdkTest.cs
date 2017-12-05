using Microsoft.NET.TestFramework.Commands;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Xunit.Abstractions;

namespace Microsoft.NET.TestFramework
{
    public abstract class SdkTest
    {
        protected TestAssetsManager _testAssetsManager = new TestAssetsManager();

        protected bool UsingFullFrameworkMSBuild => TestContext.Current.ToolsetUnderTest.ShouldUseFullFrameworkMSBuild;

        protected ITestOutputHelper Log { get; }

        protected SdkTest(ITestOutputHelper log)
        {
            Log = log;
        }
    }
}
