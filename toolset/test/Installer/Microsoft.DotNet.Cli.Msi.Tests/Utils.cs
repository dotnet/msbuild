// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using System.Linq;
using Microsoft.Win32;

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
