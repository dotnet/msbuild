// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.CommandLine;
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
            AppliedOption options,
            ParseResult result,
            LocalToolsCommandResolver localToolsCommandResolver = null)
            : base(result)
        {
            _toolCommandName = options.Arguments.Single();
            _forwardArgument = result.UnmatchedTokens.Concat(result.UnparsedTokens);
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
