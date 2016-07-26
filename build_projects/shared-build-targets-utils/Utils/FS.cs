using System.IO;
using System.Runtime.InteropServices;
using System;

using Microsoft.DotNet.Cli.Build.Framework;

namespace Microsoft.DotNet.Cli.Build
{
    public static class FS
    {
        public static void Mkdirp(string dir)
        {
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
        }
    }
}
