// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.CommandFactory;

namespace Microsoft.DotNet.Tools.Tool.Run
{
    internal class ToolRunCommand : CommandBase
    {
        private readonly string _toolCommandName;
        private readonly LocalToolsCommandResolver _localToolsCommandResolver;
        private readonly IEnumerable<string> _forwardArgument;

        public ToolRunCommand(
            ParseResult result,
            LocalToolsCommandResolver localToolsCommandResolver = null)
            : base(result)
        {
            _toolCommandName = result.GetValue(ToolRunCommandParser.CommandNameArgument);
            _forwardArgument = result.GetValue(ToolRunCommandParser.CommandArgument);
            _localToolsCommandResolver = localToolsCommandResolver ?? new LocalToolsCommandResolver();
        }

        public override int Execute()
        {
            CommandSpec commandspec = _localToolsCommandResolver.ResolveStrict(new CommandResolverArguments()
            {
                // since LocalToolsCommandResolver is a resolver, and all resolver input have dotnet-
                CommandName = $"dotnet-{_toolCommandName}",
                CommandArguments = _forwardArgument
            });

            if (commandspec == null)
            {
                throw new GracefulException(
                    new string[] { string.Format(LocalizableStrings.CannotFindCommandName, _toolCommandName) },
                    isUserError: false);
            }

            var result = CommandFactoryUsingResolver.Create(commandspec).Execute();
            return result.ExitCode;
        }
    }
}
