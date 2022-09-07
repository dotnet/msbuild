// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.CommandLine;
using System.CommandLine.Help;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    /// <summary>
    /// If <see cref="Command"/> implements this interface, it can create custom help
    /// that should be used when building the parser.
    /// </summary>
    public interface ICustomHelp
    {
        /// <summary>
        /// Returns custom help layout for the command.
        /// </summary>
        IEnumerable<HelpSectionDelegate> CustomHelpLayout();
    }
}
