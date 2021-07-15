// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Collections.Generic;
using System.CommandLine.Parsing;

using Microsoft.DotNet.Cli.Format;

namespace Microsoft.DotNet.Cli
{
    internal class FormatStyleCommand : AbstractFormatCommand
    {
        protected override string ParseFrom => "dotnet format style";

        protected override List<string> AddArgs(ParseResult parseResult)
        {
            var dotnetFormatArgs = new List<string>();
            dotnetFormatArgs.AddProjectOrSolutionDotnetFormatArgs(parseResult);
            dotnetFormatArgs.AddCommonDotnetFormatArgs(parseResult);
            dotnetFormatArgs.AddStyleDotnetFormatArgs(parseResult);
            return dotnetFormatArgs;
        }

        public static int Run(string[] args) => new FormatStyleCommand().RunCommand(args);
    }
}
