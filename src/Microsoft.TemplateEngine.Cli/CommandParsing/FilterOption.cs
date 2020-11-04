using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.TemplateEngine.Cli.CommandParsing
{
    /// <summary>
    /// Defines supported dotnet new command filter option
    /// Filter options can be used along with other dotnet new command options to filter the required items for the action defined by other opition, for example for --list option filters can limit the templates to be shown
    /// </summary>
    internal class FilterOption
    {
        /// <summary>
        /// Name of the option, should match option long name. 
        /// </summary>
        internal string Name { get; set; }

        /// <summary>
        /// A predicate that gets option value from INewCommandInput type
        /// </summary>
        internal Func<INewCommandInput, string> FilterValue { get; set; }

        /// <summary>
        /// A predicate that checks if option is set in INewCommandInput type
        /// </summary>
        internal Func<INewCommandInput, bool> IsFilterSet { get; set; }
    }
}
