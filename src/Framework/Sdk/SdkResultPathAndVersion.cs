using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Build.Framework
{
    public sealed class SdkResultPathAndVersion
    {
        public string Path { get; }

        public string Version { get; }

        public SdkResultPathAndVersion(string path)
        {
            Path = path;
            Version = null;
        }

        public SdkResultPathAndVersion(string path, string version)
        {
            Path = path;
            Version = version;
        }
    }
}
