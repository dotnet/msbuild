// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
