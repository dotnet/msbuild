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
        internal abstract Dictionary<string, Func<AppliedOption, CommandBase>> SubCommands { get; }

        public int RunCommand(string[] args)
        {
            DebugHelper.HandleDebugSwitch(ref args);

            var parser = Parser.Instance;

            var result = parser.ParseFrom($"dotnet {CommandName}", args);

            result.ShowHelpOrErrorIfAppropriate();

            var subcommandName = result.Command().Name;

            try
            {
                var create = SubCommands[subcommandName];

                var command = create(result["dotnet"][CommandName]);

                return command.Execute();
            }
            catch (KeyNotFoundException e)
            {
                throw new GracefulException(CommonLocalizableStrings.RequiredCommandNotPassed);
            }
            catch (GracefulException e)
            {
                Reporter.Error.WriteLine(e.Message.Red());
                result.ShowHelp();
                return 1;
            }
            catch (CommandParsingException e)
            {
                Reporter.Error.WriteLine(e.Message.Red());
                return 1;
            }
        }
    }
}