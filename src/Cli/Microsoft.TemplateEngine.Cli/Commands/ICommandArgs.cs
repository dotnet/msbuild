// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal interface ICommandArgs
    {
        /// <summary>
        /// Gets the topmost parent <see cref="NewCommand"/>.
        /// It might not be the root command in command defintion, however it is the topmost parent command known by this assembly.
        /// </summary>
        internal NewCommand RootCommand { get; }

        /// <summary>
        /// Gets the executing <see cref="System.CommandLine.Command"/>.
        /// </summary>
        internal Command Command { get; }

        /// <summary>
        /// Gets the <see cref="System.CommandLine.ParseResult"/> for the command to be executed.
        /// </summary>
        internal ParseResult ParseResult { get; }
    }
}
