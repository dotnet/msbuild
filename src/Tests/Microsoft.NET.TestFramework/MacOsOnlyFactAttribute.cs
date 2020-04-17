// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;
using Xunit;

namespace Microsoft.NET.TestFramework
{
    public class MacOsOnlyFactAttribute : FactAttribute
    {
        public MacOsOnlyFactAttribute()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                this.Skip = "This test requires macos to run";
            }
        }
    }
}
