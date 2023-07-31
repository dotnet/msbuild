// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Installer.Windows.Security
{
    /// <summary>
    /// Structure used to pass information to trust providers when calling WinVerifyTrust.
    /// See <see href="https://docs.microsoft.com/en-us/windows/win32/api/wintrust/ns-wintrust-wintrust_data">this</see> for further details.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct WinTrustData
    {
        /// <summary>
        /// The size, in bytes, of the structure.
        /// </summary>
        public uint cbStruct;

        /// <summary>
        /// A pointer to a buffer used to pass policy-specific data to a policy provider. This field may be <see langword="null"/>.
        /// </summary>
        public IntPtr pPolicyCallbackData;

        /// <summary>
        /// Pointer to a buffer used to pass subject interface package data to a SIP provider. This field may be <see langword="null"/>.
        /// </summary>
        public IntPtr pSIPClientData;

        /// <summary>
        /// Specifies the type of interface to use.
        /// </summary>
        public UIChoice dwUIChoice;

        /// <summary>
        /// Specifies certificate revocation check options that the selected policy provider can perform.
        /// </summary>
        public RevocationChecks fdwRevocationChecks;

        /// <summary>
        /// Specifies the union member to be used. This determines the type of object for which trust will be verified. <see cref="pWinTrustInfo"/>
        /// acts as the union member.
        /// </summary>
        public UnionChoice dwUnionChoice;

        /// <summary>
        /// Pointer to the object for which trust will be verified. This is a union member. See <see cref="dwUIChoice"/>.
        /// </summary>
        public IntPtr pWinTrustInfo; 

        /// <summary>
        /// Specifies the action to be taken.
        /// </summary>
        public StateAction dwStateAction;

        /// <summary>
        /// A handle to the state data. The contents depends on the value of <see cref="dwStateAction"/>.
        /// </summary>
        public IntPtr hWVTStateData;

        /// <summary>
        /// Reserved. This field should be <see langword="null" />.
        /// </summary>
        public IntPtr pwszURLReference;

        /// <summary>
        /// DWORD value that specifies trust provider settings.
        /// </summary>
        public ProviderSettings dwProvFlags;

        /// <summary>
        /// A DWORD value that specifies the user interface context for the WinVerifyTrust function.
        /// This causes the text in the Authenticode dialog box to match the action taken on the file.
        /// </summary>
        public uint dwUIContext;
    }
}
