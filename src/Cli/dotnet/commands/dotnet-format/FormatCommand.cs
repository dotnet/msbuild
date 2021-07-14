// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Collections.Generic;
using System.CommandLine.Parsing;

using Microsoft.DotNet.Cli.Format;

namespace Microsoft.DotNet.Cli
{
    public class FormatCommand : AbstractFormatCommand
    {
        protected override string ParseFrom => "dotnet format";

        protected override List<string> AddArgs(ParseResult parseResult)
        {
            var dotnetFormatArgs = new List<string>();
            dotnetFormatArgs.AddProjectOrSolutionDotnetFormatArgs(parseResult);
            dotnetFormatArgs.AddCommonDotnetFormatArgs(parseResult);
            dotnetFormatArgs.AddFormattingDotnetFormatArgs(parseResult);
            dotnetFormatArgs.AddStyleDotnetFormatArgs(parseResult);
            dotnetFormatArgs.AddAnalyzerDotnetFormatArgs(parseResult);
            return dotnetFormatArgs;
        }

        public static int Run(string[] args) => new FormatCommand().RunCommand(args);
    }
}
