// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Transactions;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Configurer;
using Microsoft.DotNet.ShellShim;
using Microsoft.DotNet.ToolPackage;
using Microsoft.DotNet.Tools.Tool.Common;
using Microsoft.Extensions.EnvironmentAbstractions;
using NuGet.Versioning;

namespace Microsoft.DotNet.Tools.Tool.Install
{
    internal class ToolInstallCommand : CommandBase
    {
        private readonly AppliedOption _appliedCommand;
        private readonly ParseResult _parseResult;
        private readonly ToolInstallLocalCommand _toolInstallLocalCommand;
        private readonly ToolInstallGlobalOrToolPathCommand _toolInstallGlobalOrToolPathCommand;
        private readonly bool _global;
        private readonly string _toolPath;
        private readonly bool _local;
        private readonly string _framework;

        public ToolInstallCommand(
            AppliedOption appliedCommand,
            ParseResult parseResult,
            ToolInstallGlobalOrToolPathCommand toolInstallGlobalOrToolPathCommand = null,
            ToolInstallLocalCommand toolInstallLocalCommand = null)
            : base(parseResult)
        {
            _appliedCommand = appliedCommand ?? throw new ArgumentNullException(nameof(appliedCommand));
            _parseResult = parseResult ?? throw new ArgumentNullException(nameof(parseResult));
            _toolInstallLocalCommand =
                toolInstallLocalCommand
                ?? new ToolInstallLocalCommand(_appliedCommand, _parseResult);

            _toolInstallGlobalOrToolPathCommand =
                toolInstallGlobalOrToolPathCommand
                ?? new ToolInstallGlobalOrToolPathCommand(_appliedCommand, _parseResult);

            _global = appliedCommand.ValueOrDefault<bool>(ToolAppliedOption.GlobalOption);
            _local = appliedCommand.ValueOrDefault<bool>(ToolAppliedOption.LocalOption);
            _toolPath = appliedCommand.SingleArgumentOrDefault(ToolAppliedOption.ToolPathOption);
            _framework = appliedCommand.ValueOrDefault<string>("framework");
        }

        public override int Execute()
        {
            ToolAppliedOption.EnsureNoConflictGlobalLocalToolPathOption(
                _appliedCommand,
                LocalizableStrings.InstallToolCommandInvalidGlobalAndLocalAndToolPath);

            ToolAppliedOption.EnsureToolManifestAndOnlyLocalFlagCombination(
                _appliedCommand);

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
