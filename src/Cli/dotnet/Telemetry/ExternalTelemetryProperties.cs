// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Globalization;
using System.Security;
using Microsoft.Win32;

namespace Microsoft.DotNet.Cli.Telemetry
{
    // Some properties we need for telemetry, that don't yet have suitable
    // public API
    internal static class ExternalTelemetryProperties
    {
        /// <summary>
        /// For Windows, returns the OS installation type, eg. "Nano Server", "Server Core", "Server", or "Client".
        /// For Unix, or on error, currently returns empty string.
        /// </summary>
        internal static string GetInstallationType()
        {
            if (!OperatingSystem.IsWindows())
            {
                return "";
            }

            const string Key = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion";
            const string ValueName = @"InstallationType";

            try
            {
                return (string)Registry.GetValue(Key, ValueName, defaultValue: "");
            }
            // Catch everything: this is for telemetry only.
            catch (Exception e)
            {
                Debug.Assert(e is ArgumentException | e is SecurityException | e is InvalidCastException);
                return "";
            }
        }

        [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = false)]
        private static extern bool GetProductInfo(uint dwOSMajorVersion, uint dwOSMinorVersion, uint dwSpMajorVersion, uint dwSpMinorVersion, out uint pdwReturnedProductType);

        /// <summary>
        /// For Windows, returns the product type, loosely the SKU, as encoded by GetProductInfo().
        /// For example, Enterprise is "4" (0x4) and Professional is "48" (0x30)
        /// See https://msdn.microsoft.com/en-us/library/windows/desktop/ms724358(v=vs.85).aspx for the full list.
        /// We're not attempting to decode the value on the client side as new Windows releases may add new values.
        /// For Unix, or on error, returns an empty string.
        /// </summary>
        internal static string GetProductType()
        {
            if (!OperatingSystem.IsWindows())
            {
                return "";
            }

            try
            {
                if (GetProductInfo((uint)Environment.OSVersion.Version.Major, (uint)Environment.OSVersion.Version.Minor, 0, 0, out uint productType))
                {
                    return productType.ToString("D", CultureInfo.InvariantCulture);
                }
            }
            // Catch everything: this is for telemetry only
            catch (Exception e)
            {
                Debug.Assert(false, $"Unexpected exception from GetProductInfo: ${e.GetType().Name}: ${e.Message}");
            }

            return "";
        }

        [DllImport("libc", ExactSpelling = true, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr gnu_get_libc_release();

        [DllImport("libc", ExactSpelling = true, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr gnu_get_libc_version();

        /// <summary>
        /// If gnulibc is available, returns the release, such as "stable".
        /// If the libc is musl, currently returns empty string.
        /// Otherwise returns empty string.
        /// </summary>
        internal static string GetLibcRelease()
        {
            if (OperatingSystem.IsWindows())
            {
                return "";
            }

            try
            {
                return Marshal.PtrToStringUTF8(gnu_get_libc_release());
            }
            // Catch everything: this is for telemetry only
            catch (Exception e)
            {
                Debug.Assert(e is DllNotFoundException || e is EntryPointNotFoundException);
                return "";
            }
        }

        /// <summary>
        /// If gnulibc is available, returns the version, such as "2.22".
        /// If the libc is musl, currently returns empty string. (In future could run "ldd -version".)
        /// Otherwise returns empty string.
        /// </summary>
        internal static string GetLibcVersion()
        {
            if (OperatingSystem.IsWindows())
            {
                return "";
            }

            try
            {
                return Marshal.PtrToStringUTF8(gnu_get_libc_version());
            }
            // Catch everything: this is for telemetry only
            catch (Exception e)
            {
                Debug.Assert(e is DllNotFoundException || e is EntryPointNotFoundException);
                return "";
            }
        }
    }
}
