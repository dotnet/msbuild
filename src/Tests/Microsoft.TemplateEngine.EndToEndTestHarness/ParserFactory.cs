// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

#nullable enable

using System.CommandLine;
using System.CommandLine.Parsing;

namespace Microsoft.TemplateEngine.EndToEndTestHarness
{
    internal static class ParserFactory
    {
        internal static Parser CreateParser(Command command, bool disableHelp = false)
        {
            var builder = new CommandLineBuilder(command)
            .UseParseDirective()
            .UseSuggestDirective()
            .EnablePosixBundling(false);

            if (!disableHelp)
            {
                builder = builder.UseHelp();
            }
            return builder.Build();
        }
    }
}
