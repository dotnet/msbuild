// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Xunit;
using System.Runtime.InteropServices;

namespace Microsoft.NET.TestFramework
{
    public class UnixOnlyTheoryAttribute : TheoryAttribute
    {
        public UnixOnlyTheoryAttribute()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                this.Skip = "This test requires Unix to run";
            }
        }
    }
}
