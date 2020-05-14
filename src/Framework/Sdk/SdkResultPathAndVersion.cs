using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// Encapsulates both the path to an SDK returned by an SDK resolver, and its version
    /// </summary>
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

        public override bool Equals(object obj)
        {
            return obj is SdkResultPathAndVersion version &&
                   Path == version.Path &&
                   Version == version.Version;
        }

        public override int GetHashCode()
        {
            int hashCode = 2040984829;
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Path);
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Version);
            return hashCode;
        }
    }
}
