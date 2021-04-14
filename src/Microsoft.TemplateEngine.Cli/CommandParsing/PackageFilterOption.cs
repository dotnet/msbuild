// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using Microsoft.TemplateSearch.Common;

namespace Microsoft.TemplateEngine.Cli.CommandParsing
{
    /// <summary>
    /// Defines supported dotnet new command filter option applicable to the package
    /// </summary>
    internal class PackageFilterOption : FilterOption
    {
        internal PackageFilterOption(
            string name,
            Func<INewCommandInput, string> filterValue,
            Func<INewCommandInput, bool> isFilterSet,
            Func<INewCommandInput, Func<PackInfo, bool>> matchFilter) : base(name, filterValue, isFilterSet)
        {
            PackageMatchFilter = matchFilter ?? throw new ArgumentNullException(nameof(matchFilter));
        }

        /// <summary>
        /// A predicate that returns the package match filter for the filter option
        /// Package match filter should if package is a match based on filter value
        /// </summary>
        internal Func<INewCommandInput, Func<PackInfo, bool>> PackageMatchFilter { get; set; }

    }
}
