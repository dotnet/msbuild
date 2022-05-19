// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Reflection;
using Microsoft.DotNet.Installer.Windows.Security;
using Microsoft.Win32;

namespace Microsoft.DotNet.Workloads.Workload
{
    internal static class SignCheck
    {
        private static readonly string s_dotnet = Assembly.GetExecutingAssembly().Location;

        /// <summary>
        /// Determines whether dotnet is signed.
        /// </summary>
        /// <returns><see langword="true"/> if dotnet is signed; <see langword="false"/> otherwise.</returns>
        public static bool IsDotNetSigned() => IsSigned(s_dotnet);

        /// <summary>
        /// Determines whether the specified file is signed by a trusted organization.
        /// </summary>
        /// <returns><see langword="true"/> if file is signed; <see langword="false"/> otherwise.</returns>
        internal static bool IsSigned(string path)
        {
            if (OperatingSystem.IsWindows())
            {
                return AuthentiCode.IsSigned(path) &&
                    AuthentiCode.IsSignedByTrustedOrganization(path, AuthentiCode.TrustedOrganizations);
            }

            return false;
        }

        /// <summary>
        /// Determines whether the global policy to enforce signature checks for workloads is set.
        /// </summary>
        /// <returns><see langword="true"/> if the policy is set; <see langword="false"/> otherwise.</returns>
        public static bool IsWorkloadSignVerificationPolicySet()
        {
            if (OperatingSystem.IsWindows())
            {
                using RegistryKey policyKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Policies\Microsoft\dotnet\Workloads");
                
                return ((int?)policyKey?.GetValue("VerifySignatures") ?? 0) != 0;
            }

            return false;
        }
    }
}
