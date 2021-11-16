// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Win32.Msi
{
    public static class Error
    {
        /// <summary>
        /// The operation was successful.
        /// </summary>
        public const int S_OK = 0;

        /// <summary>
        /// The operation completed successfully.
        /// </summary>
        public const uint SUCCESS = 0;

        /// <summary>
        /// Incorrect function.
        /// </summary>
        public const uint INVALID_FUNCTION = 1;

        /// <summary>
        /// The system cannot find the file specified.
        /// </summary>
        public const uint FILE_NOT_FOUND = 2;

        /// <summary>
        ///  Access is denied.
        /// </summary>
        public const uint ACCESS_DENIED = 5;

        /// <summary>
        /// The data is invalid.
        /// </summary>
        public const uint INVALID_DATA = 13;

        /// <summary>
        /// The parameter is not valid.
        /// </summary>
        public const uint INVALID_PARAMETER = 87;

        /// <summary>
        /// More data is available.
        /// </summary>
        public const uint MORE_DATA = 234;

        /// <summary>
        /// No more data is available.
        /// </summary>
        public const uint NO_MORE_ITEMS = 259;

        /// <summary>
        /// The Windows Installer Service could not be accessed.
        /// </summary>
        public const uint INSTALL_SERVICE_FAILURE = 1601;

        /// <summary>
        /// The user cancelled the installation.
        /// </summary>
        public const uint INSTALL_USER_EXIT = 1602;

        /// <summary>
        /// Fatal error during installation.
        /// </summary>
        public const uint INSTALL_FAILURE = 1603;

        /// <summary>
        /// The product is unadvertised or uninstalled.
        /// </summary>
        public const uint UNKNOWN_PRODUCT = 1605;

        /// <summary>
        /// The feature ID is not registered.
        /// </summary>
        public const uint UNKNOWN_FEATURE = 1606;

        /// <summary>
        /// The component ID is not registered.
        /// </summary>
        public const uint UNKNOWN_COMPONENT = 1607;

        /// <summary>
        /// The property is unrecognized. The product is advertised, but not installed.
        /// </summary>
        public const uint UNKNOWN_PROPERTY = 1608;

        /// <summary>
        /// Install suspended, incomplete. Triggered when the ForceReboot action is executed by the installer.
        /// </summary>
        public const uint INSTALL_SUSPEND = 1604;

        /// <summary>
        /// The configuration data for this product is corrupt.
        /// </summary>
        public const uint BAD_CONFIGURATION = 1610;

        /// <summary>
        /// Another installation is already in progress. Complete that installation before proceeding with this install.
        /// </summary>
        public const uint INSTALL_ALREADY_RUNNING = 1618;

        /// <summary>
        /// This installation package could not be opened. Verify that the package exists and that you can access it, or contact the application vendor to verify that this is a valid Windows Installer package.
        /// </summary>
        public const uint INSTALL_PACKAGE_OPEN_FAILED = 1619;

        /// <summary>
        /// This installation package could not be opened. Contact the application vendor to verify that this is a valid Windows Installer package.
        /// </summary>
        public const uint INSTALL_PACKAGE_INVALID = 1620;

        /// <summary>
        /// The function failed during execution.
        /// </summary>
        public const uint FUNCTION_FAILED = 1627;

        /// <summary>
        /// The installer has initiated a restart. This message is indicative of a success.
        /// </summary>
        public const uint SUCCESS_REBOOT_INITIATED = 1641;

        /// <summary>
        /// A restart is required to complete the install. This message is indicative of a success. This does not include installs 
        /// where the ForceReboot action is run.
        /// </summary>
        public const uint SUCCESS_REBOOT_REQUIRED = 3010;

        /// <summary>
        /// The facility code for Win32 errors to use in an HRESULT. 
        /// </summary>
        /// <remarks>
        /// See https://docs.microsoft.com/en-us/openspecs/windows_protocols/ms-erref/0642cb2f-2075-4469-918c-4441e69c548a
        /// </remarks>
        private const uint FacilityWin32 = 7;

        /// <summary>
        /// Converts an error code into an HRESULT.
        /// </summary>
        /// <param name="errorCode">The error code to convert.</param>
        /// <returns>An HRESULT for the error code.</returns>
        public static int HResultFromWin32(uint errorCode)
        {
            // See winerror.h
            // #define __HRESULT_FROM_WIN32(x) ((HRESULT)(x) <= 0 ? ((HRESULT)(x)) : ((HRESULT) (((x) & 0x0000FFFF) | (FACILITY_WIN32 << 16) | 0x80000000)))

            return (int)errorCode <= 0 ? (int)errorCode : (int)((errorCode & 0x0000ffff) | (FacilityWin32 << 16) | 0x8000000);
        }

        /// <summary>
        /// Determines if the specified error indicates a success result.
        /// </summary>
        /// <param name="error">The error code to check.</param>
        /// <returns><see langword="true"/> if the error is <see cref="SUCCESS"/>, <see cref="SUCCESS_REBOOT_INITIATED"/>, or
        /// <see cref="SUCCESS_REBOOT_REQUIRED"/>; <see langword="false"/> otherwise.</returns>
        public static bool Success(uint error)
        {
            return error == SUCCESS || error == SUCCESS_REBOOT_INITIATED || error == SUCCESS_REBOOT_REQUIRED;
        }
    }
}
