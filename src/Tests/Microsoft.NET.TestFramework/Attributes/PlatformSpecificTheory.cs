// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq;
using Microsoft.DotNet.PlatformAbstractions;
using Xunit;

namespace Microsoft.NET.TestFramework
{
    public class PlatformSpecificTheory : TheoryAttribute
    {
        public PlatformSpecificTheory(params Platform[] platforms)
        {
            if (!platforms.Contains(RuntimeEnvironment.OperatingSystemPlatform))
            {
                this.Skip = "This test is not supported on this platform.";
            }
        }
    }
}
