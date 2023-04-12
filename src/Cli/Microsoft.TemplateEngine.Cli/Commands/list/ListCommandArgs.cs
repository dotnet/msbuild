// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.CommandLine;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal class ListCommandArgs : BaseFilterableArgs, ITabularOutputArgs
    {
        internal ListCommandArgs(BaseListCommand command, ParseResult parseResult) : base(command, parseResult)
        {
            string? nameCriteria = parseResult.GetValueForArgument(ListCommand.NameArgument);
            if (!string.IsNullOrWhiteSpace(nameCriteria))
            {
                ListNameCriteria = nameCriteria;
            }
            // for legacy case new command argument is also accepted
            else if (command is LegacyListCommand legacySearchCommand)
            {
                string? newCommandArgument = parseResult.GetValueForArgument(NewCommand.ShortNameArgument);
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
            IgnoreConstraints = parseResult.GetValueForOption(ListCommand.IgnoreConstraintsOption);
        }

        public bool DisplayAllColumns { get; }

        public IReadOnlyList<string>? ColumnsToDisplay { get; }

        internal string? ListNameCriteria { get; }

        internal string? Language { get; }

        internal bool IgnoreConstraints { get; }
    }
}
