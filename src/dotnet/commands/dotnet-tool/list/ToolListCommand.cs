// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Configurer;
using Microsoft.DotNet.ToolPackage;
using Microsoft.Extensions.EnvironmentAbstractions;

namespace Microsoft.DotNet.Tools.Tool.List
{
    internal class ToolListCommand : CommandBase
    {
        private readonly AppliedOption _options;
        private readonly ParseResult _result;
        private readonly ToolListGlobalOrToolPathCommand _toolListGlobalOrToolPathCommand;
        private readonly ToolListLocalCommand _toolListLocalCommand;
        private readonly bool _global;
        private readonly bool _local;
        private readonly string _toolPath;
        private const string GlobalOption = "global";
        private const string LocalOption = "local";
        private const string ToolPathOption = "tool-path";

        public ToolListCommand(
            AppliedOption options,
            ParseResult result,
            ToolListGlobalOrToolPathCommand toolListGlobalOrToolPathCommand = null,
            ToolListLocalCommand toolListLocalCommand = null
        )
            : base(result)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _result = result ?? throw new ArgumentNullException(nameof(result));
            _toolListGlobalOrToolPathCommand
                = toolListGlobalOrToolPathCommand ?? new ToolListGlobalOrToolPathCommand(_options, _result);
            _toolListLocalCommand
                = toolListLocalCommand ?? new ToolListLocalCommand(_options, _result);
            _global = options.ValueOrDefault<bool>(GlobalOption);
            _local = options.ValueOrDefault<bool>(LocalOption);
            _toolPath = options.SingleArgumentOrDefault(ToolPathOption);
        }

        public override int Execute()
        {
            EnsureNoConflictGlobalLocalToolPathOption();

            if (_global || !string.IsNullOrWhiteSpace(_toolPath))
            {
                return _toolListGlobalOrToolPathCommand.Execute();
            }
            else
            {
                return _toolListLocalCommand.Execute();
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
                        LocalizableStrings.ListToolCommandInvalidGlobalAndLocalAndToolPath,
                        string.Join(" ", options)));
            }
        }
    }
}
