// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System;
using System.Reflection;
using Xunit.v3;

namespace Xunit
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
    public class SkipOnPlatformAttribute : BeforeAfterTestAttribute
    {
        private readonly string _skipMessage;

        public SkipOnPlatformAttribute(TestPlatforms platforms, string reason = null)
        {
            if (DiscovererHelpers.TestPlatformApplies(platforms))
            {
                _skipMessage = reason ?? "Test is not supported on this platform.";
            }
        }

        public override void Before(MethodInfo methodUnderTest, IXunitTest test)
        {
            if (_skipMessage is not null)
            {
                throw new Exception(DynamicSkipToken.Value + _skipMessage);
            }
        }
    }
}
