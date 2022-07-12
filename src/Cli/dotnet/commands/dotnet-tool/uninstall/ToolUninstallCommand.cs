// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine;
using System.CommandLine.Parsing;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.Tool.Common;

namespace Microsoft.DotNet.Tools.Tool.Uninstall
{
    internal class ToolUninstallCommand : CommandBase
    {
        private readonly ToolUninstallLocalCommand _toolUninstallLocalCommand;
        private readonly ToolUninstallGlobalOrToolPathCommand _toolUninstallGlobalOrToolPathCommand;
        private readonly bool _global;
        private readonly string _toolPath;

        public ToolUninstallCommand(
            ParseResult result,
            IReporter reporter = null,
            ToolUninstallGlobalOrToolPathCommand toolUninstallGlobalOrToolPathCommand = null,
            ToolUninstallLocalCommand toolUninstallLocalCommand = null)
            : base(result)
        {
            _toolUninstallLocalCommand
                = toolUninstallLocalCommand ??
                  new ToolUninstallLocalCommand(result);

            _toolUninstallGlobalOrToolPathCommand =
                toolUninstallGlobalOrToolPathCommand
                ?? new ToolUninstallGlobalOrToolPathCommand(result);

            _global = result.GetValueForOption(ToolUninstallCommandParser.GlobalOption);
            _toolPath = result.GetValueForOption(ToolUninstallCommandParser.ToolPathOption);
        }

        public override int Execute()
        {
            ToolAppliedOption.EnsureNoConflictGlobalLocalToolPathOption(
                _parseResult,
                LocalizableStrings.UninstallToolCommandInvalidGlobalAndLocalAndToolPath);

            ToolAppliedOption.EnsureToolManifestAndOnlyLocalFlagCombination(_parseResult);

            if (_global || !string.IsNullOrWhiteSpace(_toolPath))
            {
                return _toolUninstallGlobalOrToolPathCommand.Execute();
            }
            else
            {
                return _toolUninstallLocalCommand.Execute();
            }
        }
    }
}
