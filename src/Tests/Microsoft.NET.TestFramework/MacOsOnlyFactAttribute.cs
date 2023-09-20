// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.TestFramework
{
    public class MacOsOnlyFactAttribute : FactAttribute
    {
        public MacOsOnlyFactAttribute()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Skip = "This test requires macos to run";
            }
        }
    }
}
