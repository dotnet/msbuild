// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Parsing;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal class BaseFilterableArgs : GlobalArgs
    {
        private IReadOnlyDictionary<FilterOptionDefinition, OptionResult> _filters;

        internal BaseFilterableArgs(BaseCommand command, ParseResult parseResult) : base(command, parseResult)
        {
            if (command is not IFilterableCommand filterableCommand)
            {
                throw new ArgumentException($"{nameof(command)} should be {nameof(IFilterableCommand)}", nameof(command));
            }
            _filters = ParseFilters(filterableCommand, parseResult);
        }

        /// <summary>
        /// Gets list of <see cref="FilterOptionDefinition"/> parsed from command.
        /// </summary>
        internal IEnumerable<FilterOptionDefinition> AppliedFilters => _filters.Keys;

        /// <summary>
        /// Gets value for filter <paramref name="filter"/>.
        /// </summary>
        /// <param name="filter"></param>
        /// <returns>value of the filter.</returns>
        /// <exception cref="ArgumentException">when <paramref name="filter"/> is not among <see cref="AppliedFilters"/>.</exception>
        internal string GetFilterValue(FilterOptionDefinition filter)
        {
            if (!_filters.ContainsKey(filter))
            {
                throw new ArgumentException($"{nameof(filter)} is not available in parse result.", nameof(filter));
            }

            return _filters[filter].GetValueOrDefault<string>() ?? string.Empty;
        }

        /// <summary>
        /// Gets token name used for filter <paramref name="filter"/>.
        /// </summary>
        /// <param name="filter"></param>
        /// <returns>Token or null when token cannot be evaluated.</returns>
        internal string? GetFilterToken(FilterOptionDefinition filter)
        {
            return _filters[filter].IdentifierToken?.Value;
        }

        private static IReadOnlyDictionary<FilterOptionDefinition, OptionResult> ParseFilters(IFilterableCommand filterableCommand, ParseResult parseResult)
        {
            Dictionary<FilterOptionDefinition, OptionResult> filterValues = new();
            foreach (var filter in filterableCommand.Filters)
            {
                OptionResult? value = parseResult.GetResult(filter.Value);
                if (value != null)
                {
                    filterValues[filter.Key] = value;
                }
            }
            return filterValues;
        }
    }
}
