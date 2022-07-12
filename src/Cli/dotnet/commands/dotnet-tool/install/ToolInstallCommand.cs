// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine;
using System.CommandLine.Parsing;
using System.Linq;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.Tool.Common;

namespace Microsoft.DotNet.Tools.Tool.Install
{
    internal class ToolInstallCommand : CommandBase
    {
        private readonly ToolInstallLocalCommand _toolInstallLocalCommand;
        private readonly ToolInstallGlobalOrToolPathCommand _toolInstallGlobalOrToolPathCommand;
        private readonly bool _global;
        private readonly string _toolPath;
        private readonly bool _local;
        private readonly string _framework;

        public ToolInstallCommand(
            ParseResult parseResult,
            ToolInstallGlobalOrToolPathCommand toolInstallGlobalOrToolPathCommand = null,
            ToolInstallLocalCommand toolInstallLocalCommand = null)
            : base(parseResult)
        {
            _toolInstallLocalCommand =
                toolInstallLocalCommand
                ?? new ToolInstallLocalCommand(_parseResult);

            _toolInstallGlobalOrToolPathCommand =
                toolInstallGlobalOrToolPathCommand
                ?? new ToolInstallGlobalOrToolPathCommand(_parseResult);

            _global = parseResult.GetValueForOption(ToolAppliedOption.GlobalOption);
            _local = parseResult.GetValueForOption(ToolAppliedOption.LocalOption);
            _toolPath = parseResult.GetValueForOption(ToolAppliedOption.ToolPathOption);
            _framework = parseResult.GetValueForOption(ToolInstallCommandParser.FrameworkOption);
        }

        public override int Execute()
        {
            ToolAppliedOption.EnsureNoConflictGlobalLocalToolPathOption(
                _parseResult,
                LocalizableStrings.InstallToolCommandInvalidGlobalAndLocalAndToolPath);

            ToolAppliedOption.EnsureToolManifestAndOnlyLocalFlagCombination(
                _parseResult);

            if (_global || !string.IsNullOrWhiteSpace(_toolPath))
            {
                return _toolInstallGlobalOrToolPathCommand.Execute();
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(_framework))
                {
                    throw new GracefulException(
                        string.Format(
                            LocalizableStrings.LocalOptionDoesNotSupportFrameworkOption));
                }

                return _toolInstallLocalCommand.Execute();
            }
        }
    }
}
