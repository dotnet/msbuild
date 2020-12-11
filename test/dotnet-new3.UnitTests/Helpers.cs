using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace dotnet_new3.UnitTests
{
    static class Helpers
    {
        public static string CreateTemporaryFolder([CallerMemberName] string name = "")
        {
            string workingDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), name);
            Directory.CreateDirectory(workingDir);
            return workingDir;
        }

        public static string HomeEnvironmentVariableName { get; } = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "USERPROFILE" : "HOME";
    }
}
