// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

namespace Microsoft.TemplateEngine.Cli.CommandParsing
{
    /// <summary>
    /// Defines supported dotnet new command filter option
    /// Filter options can be used along with other dotnet new command options to filter the required items for the action defined by other option, for example for --list option filters can limit the templates to be shown.
    /// </summary>
    internal class FilterOption
    {
        internal FilterOption(string name, Func<INewCommandInput, string> filterValue, Func<INewCommandInput, bool> isFilterSet)
        {
            Name = string.IsNullOrWhiteSpace(name) ? throw new ArgumentException("", nameof(filterValue)) : name;
            FilterValue = filterValue ?? throw new ArgumentNullException(nameof(filterValue));
            IsFilterSet = isFilterSet ?? throw new ArgumentNullException(nameof(isFilterSet));
        }

        /// <summary>
        /// Name of the option, should match option long name.
        /// </summary>
        internal string Name { get; }

        /// <summary>
        /// A predicate that gets option value from INewCommandInput type.
        /// </summary>
        internal Func<INewCommandInput, string> FilterValue { get; }

        /// <summary>
        /// A predicate that checks if option is set in INewCommandInput type.
        /// </summary>
        internal Func<INewCommandInput, bool> IsFilterSet { get; }
    }
}
