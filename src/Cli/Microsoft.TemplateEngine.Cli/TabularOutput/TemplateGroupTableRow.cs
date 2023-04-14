// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.TemplateEngine.Cli.TabularOutput
{
    /// <summary>
    /// Represents a table row for template group display.
    /// </summary>
    internal struct TemplateGroupTableRow
    {
        internal string Author { get; set; }

        internal string Classifications { get; set; }

        internal string Languages { get; set; }

        internal string Name { get; set; }

        internal string ShortNames { get; set; }

        internal string Type { get; set; }
    }
}
