// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.TemplateFiltering;
using Microsoft.TemplateEngine.Cli.TemplateResolution;

namespace Microsoft.TemplateEngine.Cli.CommandParsing
{
    /// <summary>
    /// Defines supported dotnet new command filter option applicable to the template.
    /// </summary>
    internal class TemplateFilterOption : FilterOption
    {
        internal TemplateFilterOption(
            string name,
            Func<INewCommandInput, string> filterValue,
            Func<INewCommandInput, bool> isFilterSet,
            Func<INewCommandInput, Func<ITemplateInfo, MatchInfo?>> matchFilter,
            Func<TemplateListResolutionResult, bool> mismatchCriteria) : base(name, filterValue, isFilterSet)
        {
            TemplateMatchFilter = matchFilter ?? throw new ArgumentNullException(nameof(matchFilter));
            MismatchCriteria = mismatchCriteria ?? throw new ArgumentNullException(nameof(mismatchCriteria));
        }

        /// <summary>
        /// A predicate that returns the template match filter for the filter option.
        /// Template match filter should return the MatchInfo for the given template based on filter value.
        /// </summary>
        /// <remarks>
        /// Common template match filters are defined in Microsoft.TemplateEngine.Utils.WellKnonwnSearchFilter class.
        /// </remarks>
        internal Func<INewCommandInput, Func<ITemplateInfo, MatchInfo?>> TemplateMatchFilter { get; set; }

        /// <summary>
        /// A predicate that returns if the filter option caused a mismatch in ListOrHelpTemplateListResolutionResult in case of partial match.
        /// </summary>
        internal Func<TemplateListResolutionResult, bool> MismatchCriteria { get; set; }
    }
}
