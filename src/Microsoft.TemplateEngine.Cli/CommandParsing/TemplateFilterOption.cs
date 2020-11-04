using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Cli.TemplateResolution;
using Microsoft.TemplateEngine.Edge.Template;
using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.TemplateEngine.Cli.CommandParsing
{
    /// <summary>
    /// Defines supported dotnet new command filter option applicable to the template
    /// </summary>
    class TemplateFilterOption : FilterOption
    {
        /// <summary>
        /// A predicate that returns the template match filter for the filter option
        /// Template match filter should return the MatchInfo for the given template based on filter value.
        /// </summary>
        /// <remarks>
        /// Common template match filters are defined in Microsoft.TemplateEngine.Edge.Template.WellKnonwnSearchFilter class.
        /// </remarks>
        internal Func<INewCommandInput, Func<ITemplateInfo, MatchInfo?>> TemplateMatchFilter { get; set; }

        /// <summary>
        /// A predicate that returns if the filter option caused a mismatch in ListOrHelpTemplateListResolutionResult in case of partial match
        /// </summary>
        internal Func<ListOrHelpTemplateListResolutionResult, bool> MismatchCriteria { get; set; }
    }
}
