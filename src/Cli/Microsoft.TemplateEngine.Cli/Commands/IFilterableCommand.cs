// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.CommandLine;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal interface IFilterableCommand
    {
        IReadOnlyDictionary<FilterOptionDefinition, Option> Filters { get; }
    }
}
