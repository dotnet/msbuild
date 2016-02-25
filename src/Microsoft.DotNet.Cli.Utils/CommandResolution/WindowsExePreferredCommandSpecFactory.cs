using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.DotNet.ProjectModel;
using Microsoft.DotNet.ProjectModel.Graph;
using Microsoft.Extensions.PlatformAbstractions;
using NuGet.Frameworks;
using NuGet.Packaging;

namespace Microsoft.DotNet.Cli.Utils
{
    public class WindowsExePreferredCommandSpecFactory : IPlatformCommandSpecFactory
    {
        public CommandSpec CreateCommandSpec(
           string commandName,
           IEnumerable<string> args,
           string commandPath,
           CommandResolutionStrategy resolutionStrategy,
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
                ? CreateCommandSpecWrappedWithCmd(commandPath, args, resolutionStrategy)
                : CreateCommandSpecFromExecutable(commandPath, args, resolutionStrategy);
        }

        private CommandSpec CreateCommandSpecFromExecutable(
            string command,
            IEnumerable<string> args,
            CommandResolutionStrategy resolutionStrategy)
        {
            var escapedArgs = ArgumentEscaper.EscapeAndConcatenateArgArrayForProcessStart(args);
            return new CommandSpec(command, escapedArgs, resolutionStrategy);
        }

        private CommandSpec CreateCommandSpecWrappedWithCmd(
            string command,
            IEnumerable<string> args,
            CommandResolutionStrategy resolutionStrategy)
        {
            var comSpec = Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe";

            // Handle the case where ComSpec is already the command
            if (command.Equals(comSpec, StringComparison.OrdinalIgnoreCase))
            {
                command = args.FirstOrDefault();
                args = args.Skip(1);
            }

            var cmdEscapedArgs = ArgumentEscaper.EscapeAndConcatenateArgArrayForCmdProcessStart(args);

            if (ArgumentEscaper.ShouldSurroundWithQuotes(command))
            {
                command = $"\"{command}\"";
            }

            var escapedArgString = $"/s /c \"{command} {cmdEscapedArgs}\"";

            return new CommandSpec(comSpec, escapedArgString, resolutionStrategy);
        }
    }
}
