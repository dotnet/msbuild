// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using System.Security;
using Microsoft.Win32;
using Microsoft.DotNet.PlatformAbstractions;

namespace Microsoft.DotNet.Cli.Telemetry
{
    internal class DockerContainerDetectorForTelemetry : IDockerContainerDetector
    {
        public IsDockerContainer IsDockerContainer()
        {
            switch (RuntimeEnvironment.OperatingSystemPlatform)
            {
                case Platform.Windows:
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
                case Platform.Linux:
                    return ReadProcToDetectDockerInLinux()
                        ? Cli.Telemetry.IsDockerContainer.True
                        : Cli.Telemetry.IsDockerContainer.False;
                case Platform.Unknown:
                    return Cli.Telemetry.IsDockerContainer.Unknown;
                case Platform.Darwin:
                default:
                    return Cli.Telemetry.IsDockerContainer.False;
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
