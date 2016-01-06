using System.Collections.Generic;

namespace Microsoft.DotNet.Tools.Restore
{
    public struct RestoreTask
    {
        public string ProjectPath { get; set; }

        public IEnumerable<string> Arguments { get; set; } 
    }
}