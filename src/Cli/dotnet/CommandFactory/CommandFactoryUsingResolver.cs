using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.DotNet.Cli.Utils;
using NuGet.Frameworks;

namespace Microsoft.DotNet.CommandFactory
{
    public static class CommandFactoryUsingResolver
    {
        private static string[] _knownCommandsAvailableAsDotNetTool = new[] { "dotnet-dev-certs", "dotnet-sql-cache", "dotnet-user-secrets", "dotnet-watch", "dotnet-user-jwts" };

        public static Command CreateDotNet(
            string commandName,
            IEnumerable<string> args,
            NuGetFramework framework = null,
            string configuration = Constants.DefaultConfiguration)
        {
            return Create("dotnet",
                new[] { commandName }.Concat(args),
                framework,
                configuration: configuration);
        }

        /// <summary>
        /// Create a command with the specified arg array. Args will be 
        /// escaped properly to ensure that exactly the strings in this
        /// array will be present in the corresponding argument array
        /// in the command's process.
        /// </summary>
        public static Command Create(
            string commandName,
            IEnumerable<string> args,
            NuGetFramework framework = null,
            string configuration = Constants.DefaultConfiguration,
            string outputPath = null,
            string applicationName = null)
        {
            return Create(
                new DefaultCommandResolverPolicy(),
                commandName,
                args,
                framework,
                configuration,
                outputPath,
                applicationName);
        }

        public static Command Create(
            ICommandResolverPolicy commandResolverPolicy,
            string commandName,
            IEnumerable<string> args,
            NuGetFramework framework = null,
            string configuration = Constants.DefaultConfiguration,
            string outputPath = null,
            string applicationName = null)
        {
            var commandSpec = CommandResolver.TryResolveCommandSpec(
                commandResolverPolicy,
                commandName,
                args,
                framework,
                configuration: configuration,
                outputPath: outputPath,
                applicationName: applicationName);

            if (commandSpec == null)
            {
                if (_knownCommandsAvailableAsDotNetTool.Contains(commandName, StringComparer.OrdinalIgnoreCase))
                {
                    throw new CommandAvailableAsDotNetToolException(commandName);
                }
                else
                {
                    throw new CommandUnknownException(commandName);
                }
            }

            var command = Create(commandSpec);

            return command;
        }

        public static Command Create(CommandSpec commandSpec)
        {
            var psi = new ProcessStartInfo
            {
                FileName = commandSpec.Path,
                Arguments = commandSpec.Args,
                UseShellExecute = false
            };

            foreach (var environmentVariable in commandSpec.EnvironmentVariables)
            {
                if (!psi.Environment.ContainsKey(environmentVariable.Key))
                {
                    psi.Environment.Add(environmentVariable.Key, environmentVariable.Value);
                }
            }

            var _process = new Process
            {
                StartInfo = psi
            };

            return new Command(_process);
        }
    }
}
