// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.CommandLine;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal class TemplateOption
    {
        private readonly Option _option;

        internal TemplateOption(
            CliTemplateParameter parameter,
            IReadOnlyList<string> aliases)
        {
            TemplateParameter = parameter;
            Aliases = aliases;
            _option = parameter.GetOption(aliases);
        }

        internal CliTemplateParameter TemplateParameter { get; private set; }

        internal IReadOnlyList<string> Aliases { get; private set; }

        internal Option Option => _option;

    }
}
