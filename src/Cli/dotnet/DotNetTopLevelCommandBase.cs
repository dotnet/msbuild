// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools;

namespace Microsoft.DotNet.Cli
{
    public abstract class DotNetTopLevelCommandBase
    {
        protected abstract string CommandName { get; }
        protected abstract string FullCommandNameLocalized { get; }
        protected abstract string ArgumentName { get; }
        protected abstract string ArgumentDescriptionLocalized { get; }
        protected ParseResult ParseResult { get; private set; }

        internal abstract Dictionary<string, Func<AppliedOption, CommandBase>> SubCommands { get; }

        public int RunCommand(string[] args)
        {
            DebugHelper.HandleDebugSwitch(ref args);

            var parser = Parser.Instance;

            ParseResult = parser.ParseFrom($"dotnet {CommandName}", args);

            ShowHelpIfRequested();

            var subcommandName = ParseResult.Command().Name;

            try
            {
                var create = SubCommands[subcommandName];

                var command = create(ParseResult["dotnet"][CommandName]);

                return command.Execute();
            }
            catch (KeyNotFoundException)
            {
                Reporter.Error.WriteLine(CommonLocalizableStrings.RequiredCommandNotPassed.Red());
                ParseResult.ShowHelp();
                return 1;
            }
            catch (GracefulException e)
            {
                if (Reporter.IsVerbose)
                {
                    Reporter.Error.WriteLine(e.VerboseMessage.Red());
                }
                
                Reporter.Error.WriteLine(e.Message.Red());
                
                if (e.IsUserError)
                {
                    ParseResult.ShowHelp();
                }

                return 1;
            }
        }

        private void ShowHelpIfRequested()
        {
            // This checks for the help option only on the top-level command
            if (ParseResult["dotnet"][CommandName].IsHelpRequested())
            {
                throw new HelpException(ParseResult.Command().HelpView().TrimEnd());
            }
        }
    }
}
