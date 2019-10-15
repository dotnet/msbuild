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
using Microsoft.DotNet.Tools.Tool.Common;
using Microsoft.Extensions.EnvironmentAbstractions;

namespace Microsoft.DotNet.Tools.Tool.List
{
    internal class ToolListCommand : CommandBase
    {
        private readonly AppliedOption _options;
        private readonly ParseResult _result;
        private readonly ToolListGlobalOrToolPathCommand _toolListGlobalOrToolPathCommand;
        private readonly ToolListLocalCommand _toolListLocalCommand;

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
        }

        public override int Execute()
        {
            ToolAppliedOption.EnsureNoConflictGlobalLocalToolPathOption(
                _options,
                LocalizableStrings.ListToolCommandInvalidGlobalAndLocalAndToolPath);

            if (_options.ValueOrDefault<bool>(ToolAppliedOption.GlobalOption)
                || !string.IsNullOrWhiteSpace(_options.SingleArgumentOrDefault(ToolAppliedOption.ToolPathOption)))
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
