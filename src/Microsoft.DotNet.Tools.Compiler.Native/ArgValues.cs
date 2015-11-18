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
    internal class ArgValues
    {
        public string LogPath { get; set; }
        public string InputManagedAssemblyPath { get; set; }
        public string OutputDirectory { get; set; }
        public string IntermediateDirectory { get; set; }
        public string BuildConfiguration { get; set; }
        public string Architecture { get; set; }
        public string NativeMode { get; set; }
        public List<string> ReferencePaths { get; set; }
        public string IlcArgs { get; set; }
        public List<string> LinkLibPaths { get; set; }
        public string AppDepSDKPath { get; set; }
        public string IlcPath { get; set; }
    }

}
