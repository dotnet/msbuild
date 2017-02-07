using Microsoft.DotNet.PlatformAbstractions;
using Xunit;

namespace Microsoft.DotNet.Tools.Test.Utilities
{
    public class NonWindowsOnlyFactAttribute : FactAttribute
    {
        public NonWindowsOnlyFactAttribute()
        {
            if (RuntimeEnvironment.OperatingSystemPlatform == Platform.Windows)
            {
                this.Skip = "This test requires windows to run";
            }
        }
    }
}
