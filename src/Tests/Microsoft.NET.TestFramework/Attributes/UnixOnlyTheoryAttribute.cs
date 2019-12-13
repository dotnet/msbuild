using System;
using System.Collections.Generic;
using System.Text;
using Xunit;
using Microsoft.DotNet.PlatformAbstractions;

namespace Microsoft.NET.TestFramework
{
    public class UnixOnlyTheoryAttribute : TheoryAttribute
    {
        public UnixOnlyTheoryAttribute()
        {
            if (RuntimeEnvironment.OperatingSystemPlatform == Platform.Windows)
            {
                this.Skip = "This test requires Unix to run";
            }
        }
    }
}
