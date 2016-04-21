using System;

namespace Microsoft.DotNet.Tools.Compiler.Native
{
    static class RuntimeExtensions
    {
        internal static ArchitectureMode GetCurrentArchitecture()
        {
#if NET451 
            return Environment.Is64BitProcess ? ArchitectureMode.x64 : ArchitectureMode.x86; 
#else 
            return IntPtr.Size == 8 ? ArchitectureMode.x64 : ArchitectureMode.x86; 
#endif
        }
    }
}
