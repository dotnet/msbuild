// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Tools.Test
{
    public static class CommandTestRunnerExtensions
    {
        public static ProcessStartInfo ToProcessStartInfo(this ICommand command)
        {
            return new ProcessStartInfo(command.CommandName, command.CommandArgs);
        }
    }
}
