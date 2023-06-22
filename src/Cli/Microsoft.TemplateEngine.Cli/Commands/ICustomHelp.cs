// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
        IEnumerable<Action<HelpContext>> CustomHelpLayout();
    }
}
