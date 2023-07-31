// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
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

            _global = result.GetValue(ToolUninstallCommandParser.GlobalOption);
            _toolPath = result.GetValue(ToolUninstallCommandParser.ToolPathOption);
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
