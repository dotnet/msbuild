using Microsoft.TemplateSearch.Common;
using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.TemplateEngine.Cli.CommandParsing
{
    /// <summary>
    /// Defines supported dotnet new command filter option applicable to the package
    /// </summary>
    internal class PackageFilterOption : FilterOption
    {
        /// <summary>
        /// A predicate that returns the package match filter for the filter option
        /// Package match filter should if package is a match based on filter value
        /// </summary>
        internal Func<INewCommandInput, Func<PackInfo, bool>> PackageMatchFilter { get; set; }

    }
}
