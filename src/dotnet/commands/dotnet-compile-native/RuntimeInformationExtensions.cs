using System;
using System.Runtime.InteropServices;

namespace Microsoft.DotNet.Tools.Compiler.Native
{
    static class RuntimeInformationExtensions
    {
        internal static OSMode GetCurrentOS()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return OSMode.Windows;
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return OSMode.Mac;
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return OSMode.Linux;
            }
            else
            {
                throw new Exception("Unrecognized OS. dotnet-compile-native is compatible with Windows, OSX, and Linux");
            }
        }

    }
}
