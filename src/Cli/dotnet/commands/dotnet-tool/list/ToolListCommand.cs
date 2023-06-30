// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
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

            if (_parseResult.GetValue(ToolListCommandParser.GlobalOption)
                || _parseResult.GetResult(ToolListCommandParser.ToolPathOption) is not null)
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
