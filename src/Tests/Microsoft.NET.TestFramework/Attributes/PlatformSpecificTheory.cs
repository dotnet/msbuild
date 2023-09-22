// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.TestFramework
{
    public class PlatformSpecificTheory : TheoryAttribute
    {
        public PlatformSpecificTheory(TestPlatforms platforms)
        {
            if (PlatformSpecificFact.ShouldSkip(platforms))
            {
                Skip = "This test is not supported on this platform.";
            }
        }
    }
}
