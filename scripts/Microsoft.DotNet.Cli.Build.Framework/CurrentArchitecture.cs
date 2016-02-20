using System;
using Microsoft.Extensions.PlatformAbstractions;

namespace Microsoft.DotNet.Cli.Build.Framework
{
    public static class CurrentArchitecture
    {
        public static BuildArchitecture Current
        {
            get
            {
                return DetermineCurrentArchitecture();
            }
        }

        public static bool Isx86
        {
            get
            {
                var archName = PlatformServices.Default.Runtime.RuntimeArchitecture;
                return string.Equals(archName, "x86", StringComparison.OrdinalIgnoreCase);
            }
        }

        public static bool Isx64
        {
            get
            {
                var archName = PlatformServices.Default.Runtime.RuntimeArchitecture;
                return string.Equals(archName, "x64", StringComparison.OrdinalIgnoreCase);
            }
        }

        private static BuildArchitecture DetermineCurrentArchitecture()
        {
            if (Isx86)
            {
                return BuildArchitecture.x86;
            }
            else if (Isx64)
            {
                return BuildArchitecture.x64;
            }
            else
            {
                return default(BuildArchitecture);
            }
        }
    }
}