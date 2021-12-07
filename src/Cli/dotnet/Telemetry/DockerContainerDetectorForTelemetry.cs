// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
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
            if (OperatingSystem.IsWindows())
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
            else if (OperatingSystem.IsLinux())
            {
                return ReadProcToDetectDockerInLinux()
                    ? Cli.Telemetry.IsDockerContainer.True
                    : Cli.Telemetry.IsDockerContainer.False;
            }
            else if (OperatingSystem.IsMacOS())
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
            try
            {
                return File
                    .ReadAllText("/proc/1/cgroup")
                    .Contains("/docker/");
            }
            catch (Exception ex) when (ex is IOException || ex.InnerException is IOException)
            {
                // in some environments (restricted docker container, shared hosting etc.),
                // procfs is not accessible and we get UnauthorizedAccessException while the
                // inner exception is set to IOException. Ignore and continue when that happens.
            }

            return false;
        }
    }

    internal enum IsDockerContainer
    {
        True,
        False,
        Unknown
    }
}
