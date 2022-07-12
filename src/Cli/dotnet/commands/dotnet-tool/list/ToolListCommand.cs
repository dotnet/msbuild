// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine;
using System.CommandLine.Parsing;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Tools.Tool.Common;

namespace Microsoft.DotNet.Tools.Tool.List
{
    internal class ToolListCommand : CommandBase
    {
        private readonly ToolListGlobalOrToolPathCommand _toolListGlobalOrToolPathCommand;
        private readonly ToolListLocalCommand _toolListLocalCommand;

        public ToolListCommand(
            ParseResult result,
            ToolListGlobalOrToolPathCommand toolListGlobalOrToolPathCommand = null,
            ToolListLocalCommand toolListLocalCommand = null
        )
            : base(result)
        {
            _toolListGlobalOrToolPathCommand
                = toolListGlobalOrToolPathCommand ?? new ToolListGlobalOrToolPathCommand(result);
            _toolListLocalCommand
                = toolListLocalCommand ?? new ToolListLocalCommand(result);
        }

        public override int Execute()
        {
            ToolAppliedOption.EnsureNoConflictGlobalLocalToolPathOption(
                _parseResult,
                LocalizableStrings.ListToolCommandInvalidGlobalAndLocalAndToolPath);

            if (_parseResult.GetValueForOption(ToolListCommandParser.GlobalOption)
                || _parseResult.HasOption(ToolListCommandParser.ToolPathOption))
            {
                return _toolListGlobalOrToolPathCommand.Execute();
            }
            else
            {
                return _toolListLocalCommand.Execute();
            }
        }
    }
}
