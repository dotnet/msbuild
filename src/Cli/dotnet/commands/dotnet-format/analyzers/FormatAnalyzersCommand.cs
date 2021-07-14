// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Collections.Generic;
using System.CommandLine.Parsing;

using Microsoft.DotNet.Cli.Format;

namespace Microsoft.DotNet.Cli
{
    internal class FormatAnalyzersCommand : AbstractFormatCommand
    {
        protected override string ParseFrom => "dotnet format analyzers";

        protected override List<string> AddArgs(ParseResult parseResult)
        {
            var dotnetFormatArgs = new List<string>();
            dotnetFormatArgs.AddProjectOrSolutionDotnetFormatArgs(parseResult);
            dotnetFormatArgs.AddCommonDotnetFormatArgs(parseResult);
            dotnetFormatArgs.AddAnalyzerDotnetFormatArgs(parseResult);
            return dotnetFormatArgs;
        }

        public static int Run(string[] args) => new FormatAnalyzersCommand().RunCommand(args);
    }
}
