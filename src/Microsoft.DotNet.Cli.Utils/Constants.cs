using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Cli.Utils
{
    internal static class Constants
    {
        // TODO: On Unix, exe suffix is empty string...
        public static readonly string ExeSuffix = ".exe";
        public static readonly string CoreConsoleName = "coreconsole" + ExeSuffix;
    }
}
