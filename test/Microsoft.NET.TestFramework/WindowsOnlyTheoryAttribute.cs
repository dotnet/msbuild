// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.PlatformAbstractions;
using Xunit;

namespace Microsoft.NET.TestFramework
{
    public class WindowsOnlyTheoryAttribute : TheoryAttribute
    {
        public WindowsOnlyTheoryAttribute()
        {
            if (RuntimeEnvironment.OperatingSystemPlatform != Platform.Windows)
            {
                this.Skip = "This test requires Windows to run";
            }
        }
    }
}
