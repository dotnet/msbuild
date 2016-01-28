using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.IO;

using Microsoft.Dnx.Runtime.Common.CommandLine;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.Common;

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
