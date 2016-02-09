using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Dotnet.Cli.Msi.Tests
{
    class Utils
    {
        internal static bool ExistsOnPath(string fileName)
        {
            var paths = GetCurrentPathEnvironmentVariable();
            return paths
                .Split(';')
                .Any(path => File.Exists(Path.Combine(path, fileName)));
        }

        internal static string GetCurrentPathEnvironmentVariable()
        {
            var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32);           
            var regKey = hklm.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager\Environment", false);

            return (string)regKey.GetValue("Path");
        }
    }
}
