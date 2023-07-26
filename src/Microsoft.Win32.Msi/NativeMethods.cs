// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


//  Work around https://github.com/dotnet/roslyn-analyzers/issues/6094
#pragma warning disable CA1420

namespace Microsoft.Win32.Msi
{
    /// <summary>
    /// Message handler for callbacks from the installer.
    /// </summary>
    /// <param name="pvContext">Pointer to the application context.</param>
    /// <param name="iMessageType">A combination of a message box style, icon type, one default button and an installation
    /// message type.</param>
    /// <param name="message">The message text.</param>
    /// <returns>-1 if an internal error occurred or 0 if the message was not handled, otherwise a result corresponding
    /// to the button type in the message can be returned.
    /// </returns>
    /// <remarks>
    /// See https://docs.microsoft.com/en-us/windows/win32/api/msi/nc-msi-installui_handlerw
    /// </remarks>
    internal delegate DialogResult InstallUIHandler(IntPtr pvContext, uint iMessageType, [MarshalAs(UnmanagedType.LPWStr)] string message);

    internal static class NativeMethods
    {
        [DllImport("msi.dll", CharSet = CharSet.Unicode)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        public static extern uint MsiConfigureProductEx(string szProduct, int iInstallLevel, InstallState eInstallState, string szCommandLine);

        [DllImport("msi.dll", CharSet = CharSet.Unicode)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        public static extern uint MsiEnableLog(uint dwLogMode, string szLogFile, uint dwLogAttributes);

        [DllImport("msi.dll", CharSet = CharSet.Unicode)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        public static extern uint MsiEnumRelatedProducts(string lpUpgradeCode, uint dwReserved, uint iProductIndex, StringBuilder lpProductBuf);

        [DllImport("msi.dll", CharSet = CharSet.Unicode)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        public static extern uint MsiGetProductInfoEx(string szProductCode, string? szUserSid, MsiInstallContext dwContext,
            string szProperty, StringBuilder szValue, ref uint pcchValue);

        [DllImport("msi.dll", CharSet = CharSet.Unicode)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        public static extern uint MsiInstallProduct(string szPackagePath, string szCommandLine);

        [DllImport("msi.dll", CharSet = CharSet.Unicode)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        public static extern int MsiQueryProductState(string szProduct);

        [DllImport("msi.dll", CharSet = CharSet.Unicode)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        public static extern uint MsiReinstallProduct(string szProduct, uint szReinstallMode);

        [DllImport("msi.dll", CharSet = CharSet.Unicode)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        public static extern InstallUIHandler MsiSetExternalUI([MarshalAs(UnmanagedType.FunctionPtr)] InstallUIHandler puiHandler, uint dwMessageFilter, IntPtr pvContext);

        [DllImport("msi.dll", CharSet = CharSet.Unicode)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        public static extern uint MsiSetInternalUI(uint dwUILevel, ref IntPtr phWnd);

        [DllImport("msi.dll", CharSet = CharSet.Unicode)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        public static extern uint MsiSetInternalUI(uint dwUILevel, IntPtr phWnd);

        [DllImport("msi.dll", CharSet = CharSet.Unicode)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        public static extern uint MsiVerifyPackage(string szPackagePath);
    }
}
