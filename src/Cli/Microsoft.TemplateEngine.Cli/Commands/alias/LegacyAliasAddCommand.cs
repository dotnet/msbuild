// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.CommandLine;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal class LegacyAliasAddCommand : BaseAliasAddCommand
    {
        internal LegacyAliasAddCommand(
            Func<ParseResult, ITemplateEngineHost> hostBuilder)
            : base(hostBuilder, "--alias")
        {
            AddAlias("-a");
            IsHidden = true;
        }
    }
}
