using Microsoft.DotNet.PlatformAbstractions;
using Xunit;

namespace Microsoft.DotNet.Tools.Test.Utilities
{
    public class WindowsOnlyTheoryAttribute : TheoryAttribute
    {
        public WindowsOnlyTheoryAttribute()
        {
            if (RuntimeEnvironment.OperatingSystemPlatform != Platform.Windows)
            {
                this.Skip = "This test requires windows to run";
            }
        }
    }
}
