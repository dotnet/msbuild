// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.MSBuild;

namespace Microsoft.DotNet.Tools
{
    public static class NuGetSignatureVerificationEnabler
    {
        private static readonly EnvironmentProvider s_environmentProvider = new();

        internal static readonly string DotNetNuGetSignatureVerification = "DOTNET_NUGET_SIGNATURE_VERIFICATION";

        public static void ConditionallyEnable(ForwardingApp forwardingApp, IEnvironmentProvider? environmentProvider = null)
        {
            ArgumentNullException.ThrowIfNull(forwardingApp, nameof(forwardingApp));

            if (!IsLinux())
            {
                return;
            }

            string value = GetSignatureVerificationEnablementValue(environmentProvider);

            forwardingApp.WithEnvironmentVariable(DotNetNuGetSignatureVerification, value);
        }

        public static void ConditionallyEnable(MSBuildForwardingApp forwardingApp, IEnvironmentProvider? environmentProvider = null)
        {
            ArgumentNullException.ThrowIfNull(forwardingApp, nameof(forwardingApp));

            if (!IsLinux())
            {
                return;
            }

            string value = GetSignatureVerificationEnablementValue(environmentProvider);

            forwardingApp.EnvironmentVariable(DotNetNuGetSignatureVerification, value);
        }

        private static string GetSignatureVerificationEnablementValue(IEnvironmentProvider? environmentProvider)
        {
            string? value = (environmentProvider ?? s_environmentProvider).GetEnvironmentVariable(DotNetNuGetSignatureVerification);

            return string.Equals(bool.FalseString, value, StringComparison.OrdinalIgnoreCase)
                ? bool.FalseString : bool.TrueString;
        }

        private static bool IsLinux()
        {
            return RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
        }
    }
}
