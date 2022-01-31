// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
