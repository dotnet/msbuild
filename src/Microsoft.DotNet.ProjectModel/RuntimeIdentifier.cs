// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Microsoft.DotNet.ProjectModel
{
    public static class RuntimeIdentifier
    {
        public static string Current { get; } = DetermineRID();

        private static string DetermineRID()
        {
            // TODO: Not this, obviously. Do proper RID detection
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return "win7-x64";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                if(IsCentOS())
                {
                    return "centos.7-x64";
                }
                else if(IsUbuntu())
                {
                    return "ubuntu.14.04-x64";
                }
                else
                {
                    // unknown distro. Lets fail fast
                    throw new InvalidOperationException("Current linux distro is not supported.");
                }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return "osx.10.10-x64";
            }

            throw new InvalidOperationException("Current operating system is not supported.");
        }

        private static bool IsCentOS()
        {
            return IsLinuxDistro("centos");
        }

        private static bool IsUbuntu()
        {
            return IsLinuxDistro("ubuntu");
        }

        private static bool IsLinuxDistro(string distro)
        {
            // HACK - A file which can be found in most linux distros
            // Did not test in non-en distros
            const string OSIDFILE = "/etc/os-release";

            if(!File.Exists(OSIDFILE))
            {
                return false;
            }

            return File.ReadAllText(OSIDFILE).ToLower().Contains(distro);
        }
    }
}
