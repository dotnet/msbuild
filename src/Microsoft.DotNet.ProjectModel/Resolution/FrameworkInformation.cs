using System;
using System.Collections.Generic;
using System.IO;

namespace Microsoft.Extensions.ProjectModel.Resolution
{
    internal class FrameworkInformation
    {
        private bool? _exists;

        public FrameworkInformation()
        {
            Assemblies = new Dictionary<string, AssemblyEntry>();
        }

        public bool Exists
        {
            get
            {
                if (_exists == null)
                {
                    _exists = Directory.Exists(Path);
                }

                return _exists.Value;
            }
            set
            {
                _exists = true;
            }
        }

        public string Path { get; set; }

        public IEnumerable<string> SearchPaths { get; set; }

        public string RedistListPath { get; set; }

        public IDictionary<string, AssemblyEntry> Assemblies { get; private set; }

        public string Name { get; set; }
    }

    internal class AssemblyEntry
    {
        public string Path { get; set; }
        public Version Version { get; set; }
    }
}