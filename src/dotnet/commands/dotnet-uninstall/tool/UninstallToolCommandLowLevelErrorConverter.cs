// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.DotNet.ShellShim;
using Microsoft.DotNet.ToolPackage;

namespace Microsoft.DotNet.Tools.Uninstall.Tool
{
    internal static class UninstallToolCommandLowLevelErrorConverter
    {
        public static IEnumerable<string> GetUserFacingMessages(Exception ex, PackageId packageId)
        {
            string[] userFacingMessages = null;
            if (ex is ToolPackageException)
            {
                userFacingMessages = new[]
                {
                    ex.Message
                };
            }
            else if (ex is ToolConfigurationException || ex is ShellShimException)
            {
                userFacingMessages = new[]
                {
                    String.Format(
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
