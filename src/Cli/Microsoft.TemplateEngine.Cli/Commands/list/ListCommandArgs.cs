// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal class ListCommandArgs : BaseFilterableArgs, ITabularOutputArgs
    {
        internal ListCommandArgs(BaseListCommand command, ParseResult parseResult) : base(command, parseResult)
        {
            string? nameCriteria = parseResult.GetValue(BaseListCommand.NameArgument);
            if (!string.IsNullOrWhiteSpace(nameCriteria))
            {
                ListNameCriteria = nameCriteria;
            }
            // for legacy case new command argument is also accepted
            else if (command is LegacyListCommand legacySearchCommand)
            {
                string? newCommandArgument = parseResult.GetValue(NewCommand.ShortNameArgument);
                if (!string.IsNullOrWhiteSpace(newCommandArgument))
                {
                    ListNameCriteria = newCommandArgument;
                }
            }
            (DisplayAllColumns, ColumnsToDisplay) = ParseTabularOutputSettings(command, parseResult);
            if (AppliedFilters.Contains(FilterOptionDefinition.LanguageFilter))
            {
                Language = GetFilterValue(FilterOptionDefinition.LanguageFilter);
            }
            IgnoreConstraints = parseResult.GetValue(BaseListCommand.IgnoreConstraintsOption);
        }

        public bool DisplayAllColumns { get; }

        public IReadOnlyList<string>? ColumnsToDisplay { get; }

        internal string? ListNameCriteria { get; }

        internal string? Language { get; }

        internal bool IgnoreConstraints { get; }
    }
}
