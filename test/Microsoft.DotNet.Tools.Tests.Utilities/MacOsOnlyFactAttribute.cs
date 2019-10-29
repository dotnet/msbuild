// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.PlatformAbstractions;
using Xunit;

namespace Microsoft.DotNet.Tools.Test.Utilities
{
    public class MacOsOnlyFactAttribute : FactAttribute
    {
        public MacOsOnlyFactAttribute()
        {
            if (RuntimeEnvironment.OperatingSystemPlatform != Platform.Darwin)
            {
                this.Skip = "This test requires macos to run";
            }
        }
    }
}
