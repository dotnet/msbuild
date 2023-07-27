// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Win32.Msi
{
    /// <summary>
    /// Provides managed wrappers for native MSI APIs.
    /// </summary>
    public static class WindowsInstaller
    {
        /// <summary>
        /// Install only the default authored features.
        /// </summary>
        public const int INSTALLLEVEL_DEFAULT = 0;

        /// <summary>
        /// Only install required features.
        /// </summary>
        public const int INSTALLLEVEL_MINIMUM = 1;

        /// <summary>
        /// Selects all features during an install.
        /// </summary>
        public const int INSTALLLEVEL_MAXIMUIM = 0xffff;

        /// <summary>
        /// Install or configure a product.
        /// </summary>
        /// <param name="productCode">Specifies the product code for the product to be configured.</param>
        /// <param name="installLevel">Specifies how much of the product should be installed when installing the product to its default state.</param>
        /// <param name="installState">Specifies the installation state for the product.</param>
        /// <param name="commandLine">Specifies the command-line property settings.</param>
        /// <returns></returns>
        public static uint ConfigureProduct(string productCode, int installLevel, InstallState installState, string commandLine)
        {
            return NativeMethods.MsiConfigureProductEx(productCode, installLevel, installState, commandLine);
        }

        /// <summary>
        /// Installs or uninstalls a product.
        /// </summary>
        /// <param name="packagePath">Specifies the path and file name of the package to install. The value may be a URL 
        /// (e.g. https://packagelocation/package/package.msi), network path (e.g. \\packagelocation\package.msi), file path (e.g. 
        /// file://packagelocation/package.msi, or
        /// local path (e.g. C:\packageLocation\package.msi).</param>
        /// <param name="commandLine">One or more commandline property, formatted as Property=Setting. To uninstall a product, 
        /// set REMOVE=ALL. To perform an administrative installation, set ACTION=ADMIN.
        /// </param>
        /// <returns><see cref="Error.SUCCESS"/>, <see cref="Error.SUCCESS_REBOOT_INITIATED"/>, or <see cref="Error.SUCCESS_REBOOT_REQUIRED"/> if 
        /// the operation completed successfully.</returns>
        public static uint InstallProduct(string packagePath, string commandLine)
        {
            return NativeMethods.MsiInstallProduct(packagePath, commandLine);
        }

        /// <summary>
        /// Retrieve product information for a per-machine installed or advertised product.
        /// </summary>
        /// <param name="productCode">The ProductCode (GUID) of the product instance being queried.</param>
        /// <param name="property">The property to query. Only 
        /// <seealso href="https://docs.microsoft.com/en-us/windows/desktop/Msi/required-properties">required properties</seealso> are 
        /// guaranteed to be available.</param>
        /// <param name="value">If successful, contains the value of the queried property.</param>
        /// <returns>
        ///   <see cref="Error.SUCCESS"/> if the function completed successfully.
        ///   <see cref="Error.ACCESS_DENIED"/> if the current user has insufficient privileges to get information for a product installed for another user.
        ///   <see cref="Error.BAD_CONFIGURATION"/> if the configuration data for the product is corrupt.
        ///   <see cref="Error.INVALID_PARAMETER"/> if an invalid parameter is passed to the function.
        ///   <see cref="Error.UNKNOWN_PRODUCT"/> if the product is neither advertised nor installed.
        ///   <see cref="Error.UNKNOWN_PROPERTY"/> if the property is unrecognized.
        ///   <see cref="Error.FUNCTION_FAILED"/> if an unexpected, internal failure occurred.
        /// </returns>
        public static uint GetProductInfo(string productCode, string property, out string value)
        {
            return GetProductInfo(productCode, null, MsiInstallContext.MACHINE, property, out value);
        }

        /// <summary>
        /// Retrieve product information for an installed or advertised product. Products installed under a different user account may be queried 
        /// if the calling process has administrative privileges. Advertised products under a per-user-unmanaged context can only be queried by
        /// the current user.
        /// </summary>
        /// <param name="productCode">The ProductCode (GUID) of the product instance being queried.</param>
        /// <param name="userSid">The security identifier for a specific user or <see langword="null"/> for the currently logged-on user. 
        /// The special SID, "S-1-5-18", cannot be used. To access per-machine installed products, set the <paramref name="userSid"/> 
        /// to <see langword="null"/> and <paramref name="context"/> to <see cref="MsiInstallContext.MACHINE"/>. </param>
        /// <param name="context">The installation context of the product instance to query.</param>
        /// <param name="property">The property to query. Only 
        /// <seealso href="https://docs.microsoft.com/en-us/windows/desktop/Msi/required-properties">required properties</seealso> are 
        /// guaranteed to be available.</param>
        /// <param name="value">If successful, contains the value of the queried property.</param>
        /// <returns>
        ///   <see cref="Error.SUCCESS"/> if the function completed successfully. 
        ///   <see cref="Error.ACCESS_DENIED"/> if the current user has insufficient privileges to get information for a product installed for another user.
        ///   <see cref="Error.BAD_CONFIGURATION"/> if the configuration data for the product is corrupt.
        ///   <see cref="Error.INVALID_PARAMETER"/> if an invalid parameter is passed to the function.
        ///   <see cref="Error.UNKNOWN_PRODUCT"/> if the product is neither advertised nor installed.
        ///   <see cref="Error.UNKNOWN_PROPERTY"/> if the property is unrecognized.
        ///   <see cref="Error.FUNCTION_FAILED"/> if an unexpected, internal failure occurred.
        /// </returns>
        public static uint GetProductInfo(string productCode, string? userSid, MsiInstallContext context, string property, out string value)
        {
            StringBuilder buffer = new(32);
            uint bufsize = (uint)buffer.Capacity;
            uint error = NativeMethods.MsiGetProductInfoEx(productCode, userSid, context,
                property, buffer, ref bufsize);

            if (error == Error.MORE_DATA)
            {
                // pccValue returns the number of TCHARs required, excluding the null terminating character 
                // so we have to allocate space for it.
                buffer.Capacity = (int)++bufsize;
                error = NativeMethods.MsiGetProductInfoEx(productCode, userSid, context,
                    property, buffer, ref bufsize);
            }

            value = buffer.ToString();
            return error;
        }

        /// <summary>
        /// Retrieve a list of advertised and installed product codes that list the specified UpgradeCode in their property table.
        /// </summary>
        /// <param name="upgradeCode">The UpgradeCode to query.</param>
        /// <returns>An enumerable whose elements represent related product codes.</returns>
        public static IEnumerable<string> FindRelatedProducts(string upgradeCode)
        {
            // We only need room for a GUID: {807215B4-F42F-4E5F-BFEE-9817D7F2CEA5}
            StringBuilder productBuffer = new(39);

            // Keep looping until we have no more items
            for (uint productIndex = 0; true; productIndex++)
            {
                uint error = NativeMethods.MsiEnumRelatedProducts(upgradeCode, 0, productIndex, productBuffer);

                if (error == Error.NO_MORE_ITEMS)
                {
                    yield break;
                }
                if (error != Error.SUCCESS)
                {
                    throw new WindowsInstallerException(unchecked((int)error));
                }

                yield return productBuffer.ToString();
            }
        }

        /// <summary>
        /// Sets the log mode for all subsequent installations that are initiated in the calling process.
        /// </summary>
        /// <param name="logMode">Specifies the log mode.</param>
        /// <param name="logFile">Specifies the full path to the log file. When set to <see langword="null"/>, logging is disabled and <paramref name="logMode"/> is ignored".
        /// If a path is specified, <paramref name="logMode"/> must be set.
        /// </param>
        /// <param name="logAttributes">Specifies how frequently the log buffer is to be flushed.</param>
        /// <returns><see cref="Error.SUCCESS"/> if successful or <see cref="Error.INVALID_PARAMETER"/> if an invalid log mode was specified.</returns>
        public static uint EnableLog(InstallLogMode logMode, string logFile, InstallLogAttributes logAttributes)
        {
            return NativeMethods.MsiEnableLog((uint)logMode, logFile, (uint)logAttributes);
        }

        /// <summary>
        /// Query the installation state of a product.
        /// </summary>
        /// <param name="productCode">The product code to query.</param>
        /// <returns>
        /// <see cref="InstallState.ABSENT"/> if the product is installed for a different user.
        /// <see cref="InstallState.ADVERTISED"/> if the product is advertised, but not installed.
        /// <see cref="InstallState.DEFAULT"/> if the product is installed for the current user.
        /// <see cref="InstallState.INVALIDARG"/> an invalid product code was passed to the function.
        /// <see cref="InstallState.UNKNOWN"/> the product is neither advertised nor installed.
        /// </returns>
        public static InstallState QueryProduct(string productCode)
        {
            return (InstallState)NativeMethods.MsiQueryProductState(productCode);
        }

        /// <summary>
        /// Reinstalls (repair) a product.
        /// </summary>
        /// <param name="productCode">The product code of the product to be reinstalled.</param>
        /// <param name="reinstallMode">Specifies what to reinstall.</param>
        /// <returns>
        /// <see cref="Error.INSTALL_SUCCESS"/> if successful; otheriwse returns
        /// <see cref="Error.INSTALL_FAILURE"/>, <see cref="Error.INVALID_PARAMETER"/>,
        /// <see cref="Error.INSTALL_SERVICE_FAILURE"/>, <see cref="Error.INSTALL_SUSPEND"/>, 
        /// <see cref="Error.INSTALL_USER_EXIT"/>, or <see cref="Error.UNKNOWN_PRODUCT"/>.
        /// </returns>
        public static uint ReinstallProduct(string productCode, ReinstallMode reinstallMode)
        {
            return NativeMethods.MsiReinstallProduct(productCode, (uint)reinstallMode);
        }

        /// <summary>
        /// Enable the installer's internal user interface. This interface is used for all subsequent calls to user-interface-generating installer
        /// functions in this process.
        /// </summary>
        /// <param name="uiLevel">Specifies the level of complexity of the user interface.</param>
        /// <param name="windowHandle">Handle to a window that becomes the owner of any user interface created. A pointer to the previous owner
        /// of the user interface is returned. If set to <see langword="null"/>, the owner does not change.</param>
        /// <returns>The previous user interface level. If an invalid UI level is passed, then <see cref="InstallUILevel.NoChange"/> is returned.</returns>
        public static InstallUILevel SetInternalUI(InstallUILevel uiLevel, ref IntPtr windowHandle)
        {
            return (InstallUILevel)NativeMethods.MsiSetInternalUI((uint)uiLevel, ref windowHandle);
        }

        /// <summary>
        /// Enable the installer's internal user interface. This interface is used for all subsequent calls to user-interface-generating installer
        /// functions in this process.
        /// </summary>
        /// <param name="uiLevel">Specifies the level of complexity of the user interface.</param>
        /// <returns>The previous user interface level. If an invalid UI level is passed, then <see cref="InstallUILevel.NoChange"/> is returned.</returns>
        public static InstallUILevel SetInternalUI(InstallUILevel uiLevel)
        {
            return (InstallUILevel)NativeMethods.MsiSetInternalUI((uint)uiLevel, IntPtr.Zero);
        }

        /// <summary>
        /// Verifies that the given file is an installation package.
        /// </summary>
        /// <param name="packagePath">Specifies the path and file name of the package.</param>
        /// <returns>
        /// <see cref="Error.SUCCESS"/> if the file is a valid installation package, otherwise returns
        /// <see cref="Error.INSTALL_PACKAGE_INVALID"/>, <see cref="Error.INSTALL_PACKAGE_OPEN_FAILED"/>, or <see cref="Error.INVALID_PARAMETER"/>.
        /// </returns>
        public static uint VerifyPackage(string packagePath)
        {
            return NativeMethods.MsiVerifyPackage(packagePath);
        }
    }
}
