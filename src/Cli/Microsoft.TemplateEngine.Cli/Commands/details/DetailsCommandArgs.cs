// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal class DetailsCommandArgs : GlobalArgs
    {
        internal DetailsCommandArgs(DetailsCommand detailsCommand, ParseResult parseResult) : base(detailsCommand, parseResult)
        {
            string nameCriteria = parseResult.GetValue(DetailsCommand.NameArgument)
                ?? throw new ArgumentException($"{nameof(parseResult)} should contain one argument for {nameof(DetailsCommand.NameArgument)}", nameof(parseResult));

            NameCriteria = nameCriteria;
            VersionCriteria = null;
            Interactive = parseResult.GetValue(detailsCommand.InteractiveOption);
            AdditionalSources = parseResult.GetValue(detailsCommand.AddSourceOption);
        }

        internal bool Interactive { get; }

        internal string NameCriteria { get; }

        internal string? VersionCriteria { get; }

        internal IReadOnlyList<string>? AdditionalSources { get; }
    }
}
