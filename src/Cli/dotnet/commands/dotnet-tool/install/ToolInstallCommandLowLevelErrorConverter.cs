// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.ShellShim;
using Microsoft.DotNet.ToolPackage;

namespace Microsoft.DotNet.Tools.Tool.Install
{
    internal static class InstallToolCommandLowLevelErrorConverter
    {
        public static IEnumerable<string> GetUserFacingMessages(Exception ex, PackageId packageId)
        {
            string[] userFacingMessages = null;
            if (ex is ToolPackageException)
            {
                userFacingMessages = new[]
                {
                    ex.Message,
                    string.Format(LocalizableStrings.ToolInstallationFailedWithRestoreGuidance, packageId),
                };
            }
            else if (ex is ToolConfigurationException)
            {
                userFacingMessages = new[]
                {
                    string.Format(
                        LocalizableStrings.InvalidToolConfiguration,
                        ex.Message),
                    string.Format(LocalizableStrings.ToolInstallationFailedContactAuthor, packageId)
                };
            }
            else if (ex is ShellShimException)
            {
                userFacingMessages = new[]
                {
                    string.Format(
                        LocalizableStrings.FailedToCreateToolShim,
                        packageId,
                        ex.Message),
                    string.Format(LocalizableStrings.ToolInstallationFailed, packageId)
                };
            }

            return userFacingMessages;
        }

        public static bool ShouldConvertToUserFacingError(Exception ex)
        {
            return ex is ToolPackageException
                   || ex is ToolConfigurationException
                   || ex is ShellShimException;
        }
    }
}
