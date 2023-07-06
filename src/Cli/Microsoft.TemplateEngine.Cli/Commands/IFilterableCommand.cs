// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal interface IFilterableCommand
    {
        IReadOnlyDictionary<FilterOptionDefinition, CliOption> Filters { get; }
    }
}
