// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;

namespace Dotnet_new3
{
    internal static class ParserFactory
    {
        internal static Parser CreateParser(Command command, bool disableHelp = false)
        {
            var builder = new CommandLineBuilder(command)
            //.UseExceptionHandler(ExceptionHandler)
            //.UseLocalizationResources(new CommandLineValidationMessages())
            .UseParseDirective()
            .UseSuggestDirective()
            .UseParseErrorReporting()//TODO: discuss with SDK if it is possible to use it.
            //TODO: implement when help is done
            //.UseHelpBuilder((context) =>
            //{
            //    var hb = new HelpBuilder(LocalizationResources.Instance);
            //    hb.Customize(command, (result) => )
            //    return hb;
            // })
            .DisablePosixBundling();

            if (!disableHelp)
            {
                builder = builder.UseHelp();
                //.UseHelpBuilder(context => DotnetHelpBuilder.Instance.Value)
            }
            return builder.Build();
        }

        private static CommandLineBuilder DisablePosixBundling(this CommandLineBuilder builder)
        {
            builder.EnablePosixBundling = false;
            return builder;
        }
    }
}
