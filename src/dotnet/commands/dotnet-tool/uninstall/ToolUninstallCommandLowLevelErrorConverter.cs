// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.DotNet.ShellShim;
using Microsoft.DotNet.ToolPackage;

namespace Microsoft.DotNet.Tools.Tool.Uninstall
{
    internal static class ToolUninstallCommandLowLevelErrorConverter
    {
        public static IEnumerable<string> GetUserFacingMessages(Exception ex, PackageId packageId)
        {
            string[] userFacingMessages = null;
            if (ex is ToolPackageException)
            {
                userFacingMessages = new[]
                {
                    String.Format(
                        CommonLocalizableStrings.FailedToUninstallToolPackage,
                        packageId,
                        ex.Message),
                };
            }
            else if (ex is ToolConfigurationException || ex is ShellShimException)
            {
                userFacingMessages = new[]
                {
                    string.Format(
                        LocalizableStrings.FailedToUninstallTool,
                        packageId,
                        ex.Message)
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
