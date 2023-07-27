// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
                try
                {
                    bool isDocker = File
                        .ReadAllText("/proc/1/cgroup")
                        .Contains("/docker/");

                    return isDocker
                        ? Cli.Telemetry.IsDockerContainer.True
                        : Cli.Telemetry.IsDockerContainer.False;
                }
                catch (Exception ex) when (ex is IOException || ex.InnerException is IOException)
                {
                    // in some environments (restricted docker container, shared hosting etc.),
                    // procfs is not accessible and we get UnauthorizedAccessException while the
                    // inner exception is set to IOException. Ignore and continue when that happens.
                }
            }
            else if (OperatingSystem.IsMacOS())
            {
                return Cli.Telemetry.IsDockerContainer.False;
            }

            return Cli.Telemetry.IsDockerContainer.Unknown;
        }
    }

    internal enum IsDockerContainer
    {
        True,
        False,
        Unknown
    }
}
