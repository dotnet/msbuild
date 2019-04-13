// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Linq;
using System.Transactions;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.Tool.Common;

namespace Microsoft.DotNet.Tools.Tool.Uninstall
{
    internal class ToolUninstallCommand : CommandBase
    {
        private readonly AppliedOption _options;
        private readonly ToolUninstallLocalCommand _toolUninstallLocalCommand;
        private readonly ToolUninstallGlobalOrToolPathCommand _toolUninstallGlobalOrToolPathCommand;
        private readonly bool _global;
        private readonly string _toolPath;

        public ToolUninstallCommand(
            AppliedOption options,
            ParseResult result,
            IReporter reporter = null,
            ToolUninstallGlobalOrToolPathCommand toolUninstallGlobalOrToolPathCommand = null,
            ToolUninstallLocalCommand toolUninstallLocalCommand = null)
            : base(result)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _toolUninstallLocalCommand
                = toolUninstallLocalCommand ??
                  new ToolUninstallLocalCommand(options, result);

            _toolUninstallGlobalOrToolPathCommand =
                toolUninstallGlobalOrToolPathCommand
                ?? new ToolUninstallGlobalOrToolPathCommand(options, result);

            _global = options.ValueOrDefault<bool>(ToolAppliedOption.GlobalOption);
            _toolPath = options.SingleArgumentOrDefault(ToolAppliedOption.ToolPathOption);
        }

        public override int Execute()
        {
            ToolAppliedOption.EnsureNoConflictGlobalLocalToolPathOption(
                _options,
                LocalizableStrings.UninstallToolCommandInvalidGlobalAndLocalAndToolPath);

            ToolAppliedOption.EnsureToolManifestAndOnlyLocalFlagCombination(_options);

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
