// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Reflection;
using System.Runtime.Versioning;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Workloads.Workload;
using Microsoft.Win32.Msi;

namespace Microsoft.DotNet.Installer.Windows
{
    /// <summary>
    /// Base class for Windows installer components.
    /// </summary>
    [SupportedOSPlatform("windows")]
    internal abstract class InstallerBase
    {
        /// <summary>
        /// The current process.
        /// </summary>
        public static readonly Process CurrentProcess;

        /// <summary>
        /// A lower invariant string representation of the processor architecture of the running .NET host, e.g.,
        /// "x86" or "arm64".
        /// </summary>
        public static readonly string HostArchitecture = RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant();

        /// <summary>
        /// The dispatcher used for processing install commands using IPC.
        /// </summary>
        protected InstallMessageDispatcher Dispatcher => ElevationContext.Dispatcher;

        /// <summary>
        /// The elevation context associated with the process.
        /// </summary>
        protected InstallElevationContextBase ElevationContext
        {
            get;
        }

        /// <summary>
        /// Returns true if the current process is 64-bit.
        /// </summary>
        protected readonly bool Is64BitProcess = Environment.Is64BitProcess;

        /// <summary>
        /// <see langword="true"/> if this is an install client; <see langword="false"/> otherwise.
        /// </summary>
        protected bool IsClient => ElevationContext.IsClient;

        /// <summary>
        /// <see langword="true"/> if the <see cref="System.Security.Principal"/> associated
        /// with this process belongs to the administrators group.
        /// </summary>
        protected bool IsElevated => ElevationContext.IsElevated;

        /// <summary>
        /// Provides access to the underlying setup log.
        /// </summary>
        protected readonly ISetupLogger Log;

        /// <summary>
        /// The parent process of this process.
        /// </summary>
        public static readonly Process ParentProcess;

        /// <summary>
        /// Gets the processor architecture.
        /// </summary>
        protected static readonly string ProcessorArchitecture;

        /// <summary>
        /// Queries WUA to determine if the system has a pending reboot.
        /// </summary>
        protected readonly bool RebootPending = WindowsUtils.RebootRequired();

        /// <summary>
        /// <see langword="true"/> if an operation returned <see cref="Error.SUCCESS_REBOOT_INITIATED"/> or <see cref="Error.SUCCESS_REBOOT_REQUIRED"/>.
        /// </summary>
        public bool Restart
        {
            get;
            private set;
        }

        /// <summary>
        /// The name of the SDK directory, e.g. 6.0.100.
        /// </summary>
        protected string SdkDirectory => Path.GetFileName(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));

        /// <summary>
        /// Gets whether signatures for workload packages and installers should be verified.
        /// </summary>
        protected bool VerifySignatures
        {
            get;
        }

        /// <summary>
        /// Creates a new <see cref="InstallerBase"/> instance using the specified elevation context and logger.
        /// </summary>
        /// <param name="elevationContext"></param>
        /// <param name="logger"></param>
        /// <param name="verifySignatures"></param>
        protected InstallerBase(InstallElevationContextBase elevationContext, ISetupLogger logger, bool verifySignatures)
        {
            ElevationContext = elevationContext;
            Log = logger;
            VerifySignatures = verifySignatures;
        }

        /// <summary>
        /// Starts an elevated process to perform privileged operations.
        /// </summary>
        protected void Elevate()
        {
            ElevationContext.Elevate();
        }

        /// <summary>
        /// Checks the specified error code to determine whether it indicates a success result. If not, additional extended information
        /// is retrieved before throwing a <see cref="WorkloadException"/>.
        /// 
        /// The<see cref="Restart"/> property will be set to <see langword="true" /> if the error is either <see cref="Error.SUCCESS_REBOOT_INITIATED"/>
        /// or <see cref="Error.SUCCESS_REBOOT_REQUIRED"/>.
        /// </summary>
        /// <param name="error">The error code to check.</param>
        /// <param name="message">The message to include the exception.</param>
        /// <exception cref="WorkloadException" />
        protected void ExitOnError(uint error, string message)
        {
            if (!Error.Success(error))
            {
                StringBuilder sb = new StringBuilder(2048);
                NativeMethods.FormatMessage((uint)(FormatMessage.FromSystem | FormatMessage.IgnoreInserts),
                    IntPtr.Zero, error, 0, sb, (uint)sb.Capacity, IntPtr.Zero);
                string errorDetail = sb.ToString().TrimEnd(Environment.NewLine.ToCharArray());
                string errorMessage = $"{message} Error: 0x{error:x8}.";

                if (!string.IsNullOrWhiteSpace(errorDetail))
                {
                    errorMessage += $" {errorDetail}";
                }

                throw new WorkloadException(error, errorMessage);
            }

            // Once set to true, we retain restart information for the duration of the underlying command.
            Restart |= (error == Error.SUCCESS_REBOOT_INITIATED) || (error == Error.SUCCESS_REBOOT_REQUIRED);
        }

        /// <summary>
        /// Reports the specified error if the provided condition evaluates to <see langword="true"/>.
        /// </summary>
        /// <param name="condition">The condition to evaluate before reporting the error.</param>
        /// <param name="error">The error code to report if the condition is met.</param>
        /// <param name="message">The message associated with the error.</param>
        protected void ExitOnError(bool condition, uint error, string message)
        {
            if (condition)
            {
                ExitOnError(error, message);
            }
        }

        /// <summary>
        /// Throws an exception if the HRESULT of the response message indicates
        /// a failure.
        /// </summary>
        /// <param name="response">The response message to examine.</param>
        /// <exception cref="WorkloadException"/>
        protected void ExitOnFailure(InstallResponseMessage response, string message)
        {
            if (response.HResult < 0)
            {
                throw new WorkloadException(string.IsNullOrWhiteSpace(message) ? response.Message : $"{message} {response.Message}", response.HResult);
            }
        }

        /// <summary>
        /// Logs a message if the specified error code does not indicate a success result. The <see cref="Restart"/>
        /// property will be set to <see langword="true" /> if the error is either <see cref="Error.SUCCESS_REBOOT_INITIATED"/>
        /// or <see cref="Error.SUCCESS_REBOOT_REQUIRED"/>.
        /// 
        /// No exception is thrown by this method. See <see cref="ExitOnError(uint, string)"/> for more detail.
        /// </summary>
        /// <param name="error">The error code to log.</param>
        /// <param name="message">The message to log.</param>
        protected void LogError(uint error, string message)
        {
            if (!Error.Success(error))
            {
                Log?.LogMessage($"{message} Error: 0x{error:x8}.");
            }

            // Once set to true, we retain restart information for the duration of the underlying command.
            Restart |= (error == Error.SUCCESS_REBOOT_INITIATED) || (error == Error.SUCCESS_REBOOT_REQUIRED);
        }

        /// <summary>
        /// Writes the specified exception to the log.
        /// </summary>
        /// <param name="exception">The exception to log.</param>
        protected void LogException(Exception exception)
        {
            Log?.LogMessage($"Exception: {exception.Message}, HResult: 0x{exception.HResult:x8}.");
            Log?.LogMessage($"{exception.StackTrace}");
        }

        static InstallerBase()
        {
            CurrentProcess = Process.GetCurrentProcess();
            ParentProcess = CurrentProcess.GetParentProcess();
            ProcessorArchitecture = Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE").ToLowerInvariant();
        }
    }
}
