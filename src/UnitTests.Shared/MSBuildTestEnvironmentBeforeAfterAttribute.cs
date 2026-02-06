// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Reflection;
using System.Threading;
using Xunit;
using Xunit.v3;

#nullable disable

namespace Microsoft.Build.UnitTests
{
    /// <summary>
    /// A BeforeAfterTestAttribute that creates and disposes a TestEnvironment
    /// around each test method. This replaces the custom per-class/per-method
    /// AssemblyFixture scope from the xUnit v2 custom runner.
    /// </summary>
    [AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class MSBuildTestEnvironmentBeforeAfterAttribute : BeforeAfterTestAttribute
    {
        private static readonly AsyncLocal<TestEnvironment> s_testEnvironment = new();

        public override void Before(MethodInfo methodUnderTest, IXunitTest test)
        {
            s_testEnvironment.Value = TestEnvironment.Create();
        }

        public override void After(MethodInfo methodUnderTest, IXunitTest test)
        {
            s_testEnvironment.Value?.Dispose();
            s_testEnvironment.Value = null;
        }
    }
}
