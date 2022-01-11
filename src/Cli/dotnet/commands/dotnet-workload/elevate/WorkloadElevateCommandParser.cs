// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine;
using LocalizableStrings = Microsoft.DotNet.Workloads.Workload.Elevate.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class WorkloadElevateCommandParser
    {
        public static Command GetCommand()
        {
            Command command = new Command("elevate", LocalizableStrings.CommandDescription)
            {
                IsHidden = true
            };

            return command;
        }
    }
}
