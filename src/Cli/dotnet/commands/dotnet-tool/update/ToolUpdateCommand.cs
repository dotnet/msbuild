// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine;
using System.CommandLine.Parsing;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.Tool.Common;

namespace Microsoft.DotNet.Tools.Tool.Update
{
    internal class ToolUpdateCommand : CommandBase
    {
        private readonly ToolUpdateLocalCommand _toolUpdateLocalCommand;
        private readonly ToolUpdateGlobalOrToolPathCommand _toolUpdateGlobalOrToolPathCommand;
        private readonly bool _global;
        private readonly string _toolPath;

        public ToolUpdateCommand(
            ParseResult result,
            IReporter reporter = null,
            ToolUpdateGlobalOrToolPathCommand toolUpdateGlobalOrToolPathCommand = null,
            ToolUpdateLocalCommand toolUpdateLocalCommand = null)
            : base(result)
        {
            _toolUpdateLocalCommand
                = toolUpdateLocalCommand ??
                  new ToolUpdateLocalCommand(result);

            _toolUpdateGlobalOrToolPathCommand =
                toolUpdateGlobalOrToolPathCommand
                ?? new ToolUpdateGlobalOrToolPathCommand(result);

            _global = result.GetValueForOption(ToolUpdateCommandParser.GlobalOption);
            _toolPath = result.GetValueForOption(ToolUpdateCommandParser.ToolPathOption);
        }

        public override int Execute()
        {
            ToolAppliedOption.EnsureNoConflictGlobalLocalToolPathOption(
                _parseResult,
                LocalizableStrings.UpdateToolCommandInvalidGlobalAndLocalAndToolPath);

            ToolAppliedOption.EnsureToolManifestAndOnlyLocalFlagCombination(_parseResult);

            if (_global || !string.IsNullOrWhiteSpace(_toolPath))
            {
                return _toolUpdateGlobalOrToolPathCommand.Execute();
            }
            else
            {
                return _toolUpdateLocalCommand.Execute();
            }
        }
    }
}
