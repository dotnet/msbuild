using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.DotNet.ProjectModel
{
    public class ResourceFile
    {
        public string Path { get; }
        public string Locale { get; }

        public ResourceFile(string path, string locale)
        {
            Path = path;
            Locale = locale;
        }
    }
}
