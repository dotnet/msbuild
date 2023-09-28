// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Reflection;
using Microsoft.DotNet.Cli.Utils;
using RuntimeEnvironment = Microsoft.DotNet.Cli.Utils.RuntimeEnvironment;

namespace Microsoft.DotNet.Cli
{
    [EventSource(Name = "Microsoft-Dotnet-CLI-Performance", Guid = "cbd57d06-3b9f-5374-ed53-cfbcc23cf44f")]
    internal sealed class PerformanceLogEventSource : EventSource
    {
        internal static PerformanceLogEventSource Log = new();

        private PerformanceLogEventSource()
        {
        }

        [NonEvent]
        internal void LogStartUpInformation(PerformanceLogStartupInformation startupInfo)
        {
            if (!IsEnabled())
            {
                return;
            }

            DotnetVersionFile versionFile = DotnetFiles.VersionFileObject;
            string commitSha = versionFile.CommitSha ?? "N/A";

            LogMachineConfiguration();
            OSInfo(RuntimeEnvironment.OperatingSystem, RuntimeEnvironment.OperatingSystemVersion, RuntimeEnvironment.OperatingSystemPlatform.ToString());
            SDKInfo(Product.Version, commitSha, RuntimeInformation.RuntimeIdentifier, versionFile.BuildRid, AppContext.BaseDirectory);
            EnvironmentInfo(Environment.CommandLine);
            LogMemoryConfiguration();
            LogDrives();

            // It's possible that IsEnabled returns true if an out-of-process collector such as ETW is enabled.
            // If the perf log hasn't been enabled, then startupInfo will be null, so protect against nullref here.
            if (startupInfo != null)
            {
                if (startupInfo.TimedAssembly != null)
                {
                    AssemblyLoad(startupInfo.TimedAssembly.GetName().Name, startupInfo.AssemblyLoadTime.TotalMilliseconds);
                }

                Process currentProcess = Process.GetCurrentProcess();
                TimeSpan latency = startupInfo.MainTimeStamp - currentProcess.StartTime;
                HostLatency(latency.TotalMilliseconds);
            }
        }

        [Event(1)]
        internal void OSInfo(string osname, string osversion, string osplatform)
        {
            WriteEvent(1, osname, osversion, osplatform);
        }

        [Event(2)]
        internal void SDKInfo(string version, string commit, string currentRid, string buildRid, string basePath)
        {
            WriteEvent(2, version, commit, currentRid, buildRid, basePath);
        }

        [Event(3)]
        internal void EnvironmentInfo(string commandLine)
        {
            WriteEvent(3, commandLine);
        }

        [Event(4)]
        internal void HostLatency(double timeInMs)
        {
            WriteEvent(4, timeInMs);
        }

        [Event(5)]
        internal void CLIStart()
        {
            WriteEvent(5);
        }

        [Event(6)]
        internal void CLIStop()
        {
            WriteEvent(6);
        }

        [Event(7)]
        internal void FirstTimeConfigurationStart()
        {
            WriteEvent(7);
        }

        [Event(8)]
        internal void FirstTimeConfigurationStop()
        {
            WriteEvent(8);
        }

        [Event(9)]
        internal void TelemetryRegistrationStart()
        {
            WriteEvent(9);
        }

        [Event(10)]
        internal void TelemetryRegistrationStop()
        {
            WriteEvent(10);
        }

        [Event(11)]
        internal void TelemetrySaveIfEnabledStart()
        {
            WriteEvent(11);
        }

        [Event(12)]
        internal void TelemetrySaveIfEnabledStop()
        {
            WriteEvent(12);
        }

        [Event(13)]
        internal void BuiltInCommandStart()
        {
            WriteEvent(13);
        }

        [Event(14)]
        internal void BuiltInCommandStop()
        {
            WriteEvent(14);
        }

        [Event(15)]
        internal void BuiltInCommandParserStart()
        {
            WriteEvent(15);
        }

        [Event(16)]
        internal void BuiltInCommandParserStop()
        {
            WriteEvent(16);
        }

        [Event(17)]
        internal void ExtensibleCommandResolverStart()
        {
            WriteEvent(17);
        }

        [Event(18)]
        internal void ExtensibleCommandResolverStop()
        {
            WriteEvent(18);
        }

        [Event(19)]
        internal void ExtensibleCommandStart()
        {
            WriteEvent(19);
        }

        [Event(20)]
        internal void ExtensibleCommandStop()
        {
            WriteEvent(20);
        }

        [Event(21)]
        internal void TelemetryClientFlushStart()
        {
            WriteEvent(21);
        }

        [Event(22)]
        internal void TelemetryClientFlushStop()
        {
            WriteEvent(22);
        }

        [NonEvent]
        internal void LogMachineConfiguration()
        {
            if (IsEnabled())
            {
                MachineConfiguration(Environment.MachineName, Environment.ProcessorCount);
            }
        }

        [Event(23)]
        internal void MachineConfiguration(string machineName, int processorCount)
        {
            WriteEvent(23, machineName, processorCount);
        }

        [NonEvent]
        internal void LogDrives()
        {
            if (IsEnabled())
            {
                foreach (DriveInfo driveInfo in DriveInfo.GetDrives())
                {
                    try
                    {
                        DriveConfiguration(driveInfo.Name, driveInfo.DriveFormat, driveInfo.DriveType.ToString(),
                            (double)driveInfo.TotalSize / 1024 / 1024, (double)driveInfo.AvailableFreeSpace / 1024 / 1024);
                    }
                    catch
                    {
                        // If we fail to log a drive, skip it and continue.
                    }
                }
            }
        }

        [Event(24)]
        internal void DriveConfiguration(string name, string format, string type, double totalSizeMB, double availableFreeSpaceMB)
        {
            WriteEvent(24, name, format, type, totalSizeMB, availableFreeSpaceMB);
        }

        [Event(25)]
        internal void AssemblyLoad(string assemblyName, double timeInMs)
        {
            WriteEvent(25, assemblyName, timeInMs);
        }

        [NonEvent]
        internal void LogMemoryConfiguration()
        {
            if (IsEnabled())
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    Interop.MEMORYSTATUSEX memoryStatusEx = new();
                    memoryStatusEx.dwLength = (uint)Marshal.SizeOf(memoryStatusEx);

                    if (Interop.GlobalMemoryStatusEx(ref memoryStatusEx))
                    {
                        MemoryConfiguration((int)memoryStatusEx.dwMemoryLoad, (int)(memoryStatusEx.ullAvailPhys / 1024 / 1024),
                            (int)(memoryStatusEx.ullTotalPhys / 1024 / 1024));
                    }
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    ProcMemInfo memInfo = new();
                    if (memInfo.Valid)
                    {
                        MemoryConfiguration(memInfo.MemoryLoad, memInfo.AvailableMemoryMB, memInfo.TotalMemoryMB);
                    }
                }
            }
        }

        [Event(26)]
        internal void MemoryConfiguration(int memoryLoad, int availablePhysicalMB, int totalPhysicalMB)
        {
            WriteEvent(26, memoryLoad, availablePhysicalMB, totalPhysicalMB);
        }

        [NonEvent]
        internal void LogMSBuildStart(string fileName, string arguments)
        {
            if (IsEnabled())
            {
                MSBuildStart($"{fileName} {arguments}");
            }
        }

        [Event(27)]
        internal void MSBuildStart(string cmdline)
        {
            WriteEvent(27, cmdline);
        }

        [Event(28)]
        internal void MSBuildStop(int exitCode)
        {
            WriteEvent(28, exitCode);
        }

        [Event(29)]
        internal void CreateBuildCommandStart()
        {
            WriteEvent(29);
        }

        [Event(30)]
        internal void CreateBuildCommandStop()
        {
            WriteEvent(30);
        }
    }

    internal class PerformanceLogStartupInformation
    {
        public PerformanceLogStartupInformation(DateTime mainTimeStamp)
        {
            // Save the main timestamp.
            MainTimeStamp = mainTimeStamp;

            // Attempt to load an assembly.
            // Ideally, we've picked one that we'll already need, so we're not adding additional overhead.
            MeasureModuleLoad();
        }

        internal DateTime MainTimeStamp { get; private set; }
        internal Assembly TimedAssembly { get; private set; }
        internal TimeSpan AssemblyLoadTime { get; private set; }

        private void MeasureModuleLoad()
        {
            // Make sure the assembly hasn't been loaded yet.
            string assemblyName = "Microsoft.DotNet.Configurer";
            try
            {
                foreach (Assembly loadedAssembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (loadedAssembly.GetName().Name.Equals(assemblyName))
                    {
                        // If the assembly is already loaded, then bail.
                        return;
                    }
                }
            }
            catch
            {
                // If we fail to enumerate, just bail.
                return;
            }

            Stopwatch stopWatch = Stopwatch.StartNew();
            Assembly assembly = null;
            try
            {
                assembly = Assembly.Load(assemblyName);
            }
            catch
            {
                return;
            }
            stopWatch.Stop();
            if (assembly != null)
            {
                // Save the results.
                TimedAssembly = assembly;
                AssemblyLoadTime = stopWatch.Elapsed;
            }
        }
    }

    /// <summary>
    /// Global memory statistics on Windows.
    /// </summary>
    internal static class Interop
    {
        [StructLayout(LayoutKind.Sequential)]
        internal struct MEMORYSTATUSEX
        {
            // The length field must be set to the size of this data structure.
            internal uint dwLength;
            internal uint dwMemoryLoad;
            internal ulong ullTotalPhys;
            internal ulong ullAvailPhys;
            internal ulong ullTotalPageFile;
            internal ulong ullAvailPageFile;
            internal ulong ullTotalVirtual;
            internal ulong ullAvailVirtual;
            internal ulong ullAvailExtendedVirtual;
        }

        [DllImport("kernel32.dll")]
        internal static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);
    }

    /// <summary>
    /// Global memory statistics on Linux.
    /// </summary>
    internal sealed class ProcMemInfo
    {
        private const string MemTotal = "MemTotal:";
        private const string MemAvailable = "MemAvailable:";

        private short _matchingLineCount = 0;

        internal ProcMemInfo()
        {
            Initialize();
        }

        /// <summary>
        /// The data in this class is valid if we parsed the file, found, and properly parsed the two matching lines.
        /// </summary>
        internal bool Valid
        {
            get { return _matchingLineCount == 2; }
        }

        internal int MemoryLoad
        {
            get { return (int)((double)(TotalMemoryMB - AvailableMemoryMB) / TotalMemoryMB * 100); }
        }

        internal int AvailableMemoryMB
        {
            get;
            private set;
        }

        internal int TotalMemoryMB
        {
            get;
            private set;
        }

        private void Initialize()
        {
            try
            {
                using (StreamReader reader = new(File.OpenRead("/proc/meminfo")))
                {
                    string line;
                    while (!Valid && ((line = reader.ReadLine()) != null))
                    {
                        if (line.StartsWith(MemTotal) || line.StartsWith(MemAvailable))
                        {
                            string[] tokens = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                            if (tokens.Length == 3)
                            {
                                if (MemTotal.Equals(tokens[0]))
                                {
                                    TotalMemoryMB = (int)Convert.ToUInt64(tokens[1]) / 1024;
                                    _matchingLineCount++;
                                }
                                else if (MemAvailable.Equals(tokens[0]))
                                {
                                    AvailableMemoryMB = (int)Convert.ToUInt64(tokens[1]) / 1024;
                                    _matchingLineCount++;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex) when (ex is IOException || ex.InnerException is IOException)
            {
                // in some environments (restricted docker container, shared hosting etc.),
                // procfs is not accessible and we get UnauthorizedAccessException while the
                // inner exception is set to IOException. Ignore and continue when that happens.
            }
        }
    }
}
