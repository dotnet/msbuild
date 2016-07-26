using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

using Microsoft.DotNet.Cli.Build.Framework;

namespace Microsoft.DotNet.Cli.Build
{
    public static class Utils
    {
        public static string GetVersionFileContent(string commitHash, string version)
        {
            return $@"{commitHash}{Environment.NewLine}{version}{Environment.NewLine}";
        }
    }
}
