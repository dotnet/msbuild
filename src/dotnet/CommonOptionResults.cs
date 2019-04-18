using System;
using System.IO;
using System.Linq;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Tools.Common;
using Microsoft.DotNet.Tools;

namespace Microsoft.DotNet.Cli
{
    internal static class CommonOptionResult
    {
        public static bool GetInteractive(AppliedOption appliedOption)
        {
            return appliedOption.HasOption("interactive");
        }
    }
}
