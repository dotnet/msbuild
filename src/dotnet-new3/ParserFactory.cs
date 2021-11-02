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
        internal static Parser CreateParser(Command command)
        {
            return new CommandLineBuilder(command)
            //.UseExceptionHandler(ExceptionHandler)
            .UseHelp()
            //.UseHelpBuilder(context => DotnetHelpBuilder.Instance.Value)
            //.UseLocalizationResources(new CommandLineValidationMessages())
            .UseParseDirective()
            .UseSuggestDirective()
            .DisablePosixBinding()
            .Build();
        }

        private static CommandLineBuilder DisablePosixBinding(this CommandLineBuilder builder)
        {
            builder.EnablePosixBundling = false;
            return builder;
        }
    }
}
