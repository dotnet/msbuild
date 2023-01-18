// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

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
