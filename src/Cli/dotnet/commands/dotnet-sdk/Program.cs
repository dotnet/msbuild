// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.CommandLine.Parsing;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.Sdk.Check;

namespace Microsoft.DotNet.Tools.Sdk
{
    public class SdkCommand : DotNetTopLevelCommandBase
    {
        protected override string CommandName => "sdk";
        protected override string FullCommandNameLocalized => LocalizableStrings.AppFullName;
        protected override string ArgumentName => "";
        protected override string ArgumentDescriptionLocalized => "";

        internal override Dictionary<string, Func<ParseResult, CommandBase>> SubCommands =>
            new Dictionary<string, Func<ParseResult, CommandBase>>
            {
                ["check"] =
                sln => new SdkCheckCommand(
                    ParseResult),
            };

        public static int Run(string[] args)
        {
            var command = new SdkCommand();
            return command.RunCommand(args);
        }
    }
}
