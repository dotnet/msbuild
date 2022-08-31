// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.CommandLine;
using System.CommandLine.Parsing;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal static class ParserFactory
    {
        internal static Parser CreateParser(Command command, bool disableHelp = false)
        {
            var builder = new CommandLineBuilder(command)
                .UseParseErrorReporting()
                //TODO: decide if it's needed to implement it; and implement if needed
                //.UseParseDirective()
                //.UseSuggestDirective()
                .EnablePosixBundling(false);

            if (!disableHelp)
            {
                builder = builder.UseHelp();
            }
            return builder.Build();
        }

    }
}
