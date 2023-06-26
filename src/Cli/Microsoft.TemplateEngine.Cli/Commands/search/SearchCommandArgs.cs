// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal class SearchCommandArgs : BaseFilterableArgs, ITabularOutputArgs
    {
        internal SearchCommandArgs(BaseSearchCommand command, ParseResult parseResult) : base(command, parseResult)
        {
            string? nameCriteria = parseResult.GetValue(BaseSearchCommand.NameArgument);
            if (!string.IsNullOrWhiteSpace(nameCriteria))
            {
                SearchNameCriteria = nameCriteria;
            }
            // for legacy case new command argument is also accepted
            else if (command is LegacySearchCommand legacySearchCommand)
            {
                string? newCommandArgument = parseResult.GetValue(NewCommand.ShortNameArgument);
                if (!string.IsNullOrWhiteSpace(newCommandArgument))
                {
                    SearchNameCriteria = newCommandArgument;
                }
            }
            (DisplayAllColumns, ColumnsToDisplay) = ParseTabularOutputSettings(command, parseResult);

            if (AppliedFilters.Contains(FilterOptionDefinition.LanguageFilter))
            {
                Language = GetFilterValue(FilterOptionDefinition.LanguageFilter);
            }
        }

        public bool DisplayAllColumns { get; }

        public IReadOnlyList<string>? ColumnsToDisplay { get; }

        internal string? SearchNameCriteria { get; }

        internal string? Language { get; }
    }
}
