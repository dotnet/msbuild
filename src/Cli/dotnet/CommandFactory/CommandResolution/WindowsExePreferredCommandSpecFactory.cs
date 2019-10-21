// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.CommandFactory
{
    public class WindowsExePreferredCommandSpecFactory : IPlatformCommandSpecFactory
    {
        public CommandSpec CreateCommandSpec(
           string commandName,
           IEnumerable<string> args,
           string commandPath,
           IEnvironmentProvider environment)
        {
            var useCmdWrapper = false;

            if (Path.GetExtension(commandPath).Equals(".cmd", StringComparison.OrdinalIgnoreCase))
            {
                var preferredCommandPath = environment.GetCommandPath(commandName, ".exe");

                if (preferredCommandPath == null)
                {
                    useCmdWrapper = true;
                }
                else
                {
                    commandPath = preferredCommandPath;
                }
            }

            return useCmdWrapper
                ? CreateCommandSpecWrappedWithCmd(commandPath, args)
                : CreateCommandSpecFromExecutable(commandPath, args);
        }

        private CommandSpec CreateCommandSpecFromExecutable(
            string command,
            IEnumerable<string> args)
        {
            var escapedArgs = ArgumentEscaper.EscapeAndConcatenateArgArrayForProcessStart(args);
            return new CommandSpec(command, escapedArgs);
        }

        private CommandSpec CreateCommandSpecWrappedWithCmd(
            string command,
            IEnumerable<string> args)
        {
            var comSpec = Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe";

            // Handle the case where ComSpec is already the command
            if (command.Equals(comSpec, StringComparison.OrdinalIgnoreCase))
            {
                command = args.FirstOrDefault();
                args = args.Skip(1);
            }

            var cmdEscapedArgs = ArgumentEscaper.EscapeAndConcatenateArgArrayForCmdProcessStart(args);

            if (!ArgumentEscaper.IsSurroundedWithQuotes(command) // Don't quote already quoted strings
                && ArgumentEscaper.ShouldSurroundWithQuotes(command))
            {
                command = $"\"{command}\"";
            }

            var escapedArgString = $"/s /c \"{command} {cmdEscapedArgs}\"";

            return new CommandSpec(comSpec, escapedArgString);
        }
    }
}
