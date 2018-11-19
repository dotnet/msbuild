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

namespace Microsoft.DotNet.Tools.Tool.Uninstall
{

    internal class ToolUninstallCommand : CommandBase
    {
        private readonly ToolUninstallLocalCommand _toolUninstallLocalCommand;
        private readonly ToolUninstallGlobalOrToolPathCommand _toolUninstallGlobalOrToolPathCommand;
        private readonly bool _global;
        private readonly bool _local;
        private readonly string _toolPath;
        private readonly string _toolManifestOption;
        private const string GlobalOption = "global";
        private const string LocalOption = "local";
        private const string ToolPathOption = "tool-path";

        public ToolUninstallCommand(
            AppliedOption options,
            ParseResult result,
            IReporter reporter = null,
            ToolUninstallGlobalOrToolPathCommand toolUninstallGlobalOrToolPathCommand = null,
            ToolUninstallLocalCommand toolUninstallLocalCommand = null)
            : base(result)
        {
            _toolUninstallLocalCommand
                = toolUninstallLocalCommand ??
                  new ToolUninstallLocalCommand(options, result);

            _toolUninstallGlobalOrToolPathCommand =
                toolUninstallGlobalOrToolPathCommand
                ?? new ToolUninstallGlobalOrToolPathCommand(options, result);

            _global = options.ValueOrDefault<bool>(GlobalOption);
            _local = options.ValueOrDefault<bool>(LocalOption);
            _toolPath = options.SingleArgumentOrDefault(ToolPathOption);
            _toolManifestOption = options.ValueOrDefault<string>("tool-manifest");
        }

        public override int Execute()
        {
            EnsureNoConflictGlobalLocalToolPathOption();

            if (_global || !string.IsNullOrWhiteSpace(_toolPath))
            {
                if (!string.IsNullOrWhiteSpace(_toolManifestOption))
                {
                    throw new GracefulException(
                        string.Format(
                            LocalizableStrings.OnlyLocalOptionSupportManifestFileOption));
                }

                return _toolUninstallGlobalOrToolPathCommand.Execute();
            }
            else
            {
                return _toolUninstallLocalCommand.Execute();
            }
        }

        private void EnsureNoConflictGlobalLocalToolPathOption()
        {
            List<string> options = new List<string>();
            if (_global)
            {
                options.Add(GlobalOption);
            }

            if (_local)
            {
                options.Add(LocalOption);
            }

            if (!string.IsNullOrWhiteSpace(_toolPath))
            {
                options.Add(ToolPathOption);
            }

            if (options.Count > 1)
            {
                throw new GracefulException(
                    string.Format(
                        LocalizableStrings.UninstallToolCommandInvalidGlobalAndLocalAndToolPath,
                        string.Join(" ", options)));
            }
        }
    }
}
