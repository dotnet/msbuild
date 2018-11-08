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
using Microsoft.Extensions.EnvironmentAbstractions;
using NuGet.Versioning;

namespace Microsoft.DotNet.Tools.Tool.Install
{
    internal class ToolInstallCommand : CommandBase
    {
        private readonly AppliedOption _appliedCommand;
        private readonly ParseResult _parseResult;
        private readonly ToolInstallGlobalOrToolPathCommand _toolInstallGlobalOrToolPathCommand;
        private readonly bool _global;
        private readonly string _toolPath;

        public ToolInstallCommand(
            AppliedOption appliedCommand,
            ParseResult parseResult,
            ToolInstallGlobalOrToolPathCommand toolInstallGlobalOrToolPathCommand = null)
            : base(parseResult)
        {
            _appliedCommand = appliedCommand ?? throw new ArgumentNullException(nameof(appliedCommand));
            _parseResult = parseResult ?? throw new ArgumentNullException(nameof(parseResult));
            _toolInstallGlobalOrToolPathCommand =
                toolInstallGlobalOrToolPathCommand
                ?? new ToolInstallGlobalOrToolPathCommand(_appliedCommand, _parseResult);

            _global = appliedCommand.ValueOrDefault<bool>("global");
            _toolPath = appliedCommand.SingleArgumentOrDefault("tool-path");
        }

        public override int Execute()
        {
            if (string.IsNullOrWhiteSpace(_toolPath) && !_global)
            {
                throw new GracefulException(LocalizableStrings.InstallToolCommandNeedGlobalOrToolPath);
            }

            if (!string.IsNullOrWhiteSpace(_toolPath) && _global)
            {
                throw new GracefulException(LocalizableStrings.InstallToolCommandInvalidGlobalAndToolPath);
            }

            return _toolInstallGlobalOrToolPathCommand.Execute();
        }
    }
}
