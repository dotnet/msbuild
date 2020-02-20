// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq;
using Microsoft.DotNet.Cli.CommandLine;

namespace Microsoft.DotNet.Cli
{
    internal static class InternalReportinstallsuccessCommandParser
    {
        public static Command InternalReportinstallsuccess() =>
            Create.Command(
                "internal-reportinstallsuccess",
                "",
                Accept.ExactlyOneArgument());
    }
}