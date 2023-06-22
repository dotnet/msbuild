// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Threading;
using Xunit.Abstractions;

namespace Microsoft.NET.TestFramework
{
    public abstract class SdkTest
    {
        protected TestAssetsManager _testAssetsManager;

        protected bool UsingFullFrameworkMSBuild => TestContext.Current.ToolsetUnderTest.ShouldUseFullFrameworkMSBuild;

        protected ITestOutputHelper Log { get; }

        protected SdkTest(ITestOutputHelper log)
        {
            Log = log;
            _testAssetsManager = new TestAssetsManager(log);
        }

        protected static void WaitForUtcNowToAdvance()
        {
            var start = DateTime.UtcNow;

            while (DateTime.UtcNow <= start)
            {
                Thread.Sleep(millisecondsTimeout: 1);
            }
        }
    }
}
