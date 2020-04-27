// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using Microsoft.Win32;

namespace Microsoft.DotNet.Cli.Telemetry
{
    internal class DockerContainerDetectorForTelemetry : IDockerContainerDetector
    {
        public IsDockerContainer IsDockerContainer()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                try
                {
                    using (RegistryKey subkey
                        = Registry.LocalMachine.OpenSubKey("System\\CurrentControlSet\\Control"))
                    {
                        return subkey?.GetValue("ContainerType") != null
                            ? Cli.Telemetry.IsDockerContainer.True
                            : Cli.Telemetry.IsDockerContainer.False;
                    }
                }
                catch (SecurityException)
                {
                    return Cli.Telemetry.IsDockerContainer.Unknown;
                }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return ReadProcToDetectDockerInLinux()
                    ? Cli.Telemetry.IsDockerContainer.True
                    : Cli.Telemetry.IsDockerContainer.False;
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return Cli.Telemetry.IsDockerContainer.False;
            }
            else
            {
                return Cli.Telemetry.IsDockerContainer.Unknown;
            }
        }

        private static bool ReadProcToDetectDockerInLinux()
        {
            return File
                .ReadAllText("/proc/1/cgroup")
                .Contains("/docker/");
        }
    }

    internal enum IsDockerContainer
    {
        True,
        False,
        Unknown
    }
}
