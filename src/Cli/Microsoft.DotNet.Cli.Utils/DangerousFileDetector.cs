// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.DotNet.PlatformAbstractions;
using RuntimeEnvironment = Microsoft.DotNet.PlatformAbstractions.RuntimeEnvironment;

namespace Microsoft.DotNet.Cli.Utils
{
    internal class DangerousFileDetector : IDangerousFileDetector
    {
        public bool IsDangerous(string filePath)
        {
            if (RuntimeEnvironment.OperatingSystemPlatform != Platform.Windows)
            {
                return false;
            }

            return InternetSecurity.IsDangerous(filePath);
        }

        private static class InternetSecurity
        {
            private const string CLSID_InternetSecurityManager = "7b8a2d94-0ac9-11d1-896c-00c04fb6bfc4";
            private const uint ZoneLocalMachine = 0;
            private const uint ZoneIntranet = 1;
            private const uint ZoneTrusted = 2;
            private const uint ZoneInternet = 3;
            private const uint ZoneUntrusted = 4;
            private const int REGDB_E_CLASSNOTREG = unchecked((int)0x80040154);
            private static IInternetSecurityManager internetSecurityManager = null;
            public static bool IsDangerous(string filename)
            {
                try
                {
                    // First check the zone, if they are not an untrusted zone, they aren't dangerous
                    if (internetSecurityManager == null)
                    {
                        Type iismType = Type.GetTypeFromCLSID(new Guid(CLSID_InternetSecurityManager));
                        internetSecurityManager = (IInternetSecurityManager)Activator.CreateInstance(iismType);
                    }
                    int zone = 0;
                    internetSecurityManager.MapUrlToZone(Path.GetFullPath(filename), out zone, 0);
                    if (zone < ZoneInternet)
                    {
                        return false;
                    }
                    // By default all file types that get here are considered dangerous
                    return true;
                }
                catch (COMException ex) when (ex.ErrorCode == REGDB_E_CLASSNOTREG)
                {
                    // When the COM is missing(Class not registered error), it is in a locked down
                    // version like Nano Server
                    return false;
                }
            }
        }
    }
}
