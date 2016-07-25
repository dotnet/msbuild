using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.MSBuild.LockFile.Tasks
{
    /// <summary>
    /// Values for File Group Metadata
    /// </summary>
    internal enum FileGroup
    {
        CompileTimeAssembly,
        RuntimeAssembly,
        ContentFile,
        NativeLibrary,
        ResourceAssembly,
        RuntimeTarget
    }
}
