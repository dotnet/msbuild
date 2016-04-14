// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.PlatformAbstractions;
using Xunit;

namespace Microsoft.DotNet.Tools.Test.Utilities
{
    public class WindowsOnlyFactAttribute : FactAttribute
    {
        public WindowsOnlyFactAttribute()
        {
            if (PlatformServices.Default.Runtime.OperatingSystemPlatform != Platform.Windows)
            {
                this.Skip = "This test requires windows to run";
            }
        }
    }

    public class WindowsOnlyTheoryAttribute : TheoryAttribute
    {
        public WindowsOnlyTheoryAttribute()
        {
            if (PlatformServices.Default.Runtime.OperatingSystemPlatform != Platform.Windows)
            {
                this.Skip = "This test requires windows to run";
            }
        }
    }
}