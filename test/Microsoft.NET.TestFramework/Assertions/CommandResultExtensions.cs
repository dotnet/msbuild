// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Cli.Utils;
using Xunit.Abstractions;

namespace Microsoft.NET.TestFramework.Assertions
{
    public static class CommandResultExtensions
    {
        public static CommandResultAssertions Should(this CommandResult commandResult)
        {
            return new CommandResultAssertions(commandResult);
        }

        public static CommandResult AndLog(this CommandResult commandResult, ITestOutputHelper log)
        {
            log.WriteLine($"> {commandResult.StartInfo.FileName} {commandResult.StartInfo.Arguments}");
            log.WriteLine(commandResult.StdOut);

            if (!string.IsNullOrEmpty(commandResult.StdErr))
            {
                log.WriteLine("");
                log.WriteLine("StdErr:");
                log.WriteLine(commandResult.StdErr);
            }

            if (commandResult.ExitCode != 0)
            {
                log.WriteLine($"Exit Code: {commandResult.ExitCode}");
            }

            return commandResult;
        }
    }
}
