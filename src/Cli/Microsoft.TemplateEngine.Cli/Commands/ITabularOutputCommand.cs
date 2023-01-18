// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.CommandLine;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal interface ITabularOutputCommand
    {
        internal Option<bool> ColumnsAllOption { get; }

        internal Option<string[]> ColumnsOption { get; }
    }

    internal interface ITabularOutputArgs
    {
        internal bool DisplayAllColumns { get; }

        internal IReadOnlyList<string>? ColumnsToDisplay { get; }
    }
}
