// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.CommandLine;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal class UpdateCommandArgs : GlobalArgs
    {
        public UpdateCommandArgs(BaseUpdateCommand command, ParseResult parseResult) : base(command, parseResult)
        {
            if (command is UpdateCommand updateCommand)
            {
                CheckOnly = parseResult.GetValueForOption(UpdateCommand.CheckOnlyOption);
            }
            else if (command is LegacyUpdateCheckCommand)
            {
                CheckOnly = true;
            }
            else if (command is LegacyUpdateApplyCommand)
            {
                CheckOnly = false;
            }
            else
            {
                throw new ArgumentException($"Unsupported type {command.GetType().FullName}", nameof(command));
            }

            Interactive = parseResult.GetValueForOption(command.InteractiveOption);
            AdditionalSources = parseResult.GetValueForOption(command.AddSourceOption);
        }

        public bool CheckOnly { get; }

        public bool Interactive { get; }

        public IReadOnlyList<string>? AdditionalSources { get; }
    }
}
