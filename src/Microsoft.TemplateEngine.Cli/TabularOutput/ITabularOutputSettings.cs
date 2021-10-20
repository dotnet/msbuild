// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

namespace Microsoft.TemplateEngine.Cli.TabularOutput
{
    internal interface ITabularOutputSettings
    {
        bool DisplayAllColumns { get; }

        IReadOnlyList<string> ColumnsToDisplay { get; }

        int ColumnPadding { get; }

        char? HeaderSeparator { get; }

        bool BlankLineBetweenRows { get; }

        int ConsoleBufferWidth { get; }

        string NewLine { get; }

        string ShrinkReplacement { get; }
    }
}
