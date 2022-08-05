// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Cli.Commands;

namespace Microsoft.TemplateEngine.Cli.TabularOutput
{
    internal class TabularOutputSettings
    {
        internal TabularOutputSettings(
            IEnvironment environment,
            IReadOnlyList<string>? columnsToDisplay = null,
            bool displayAllColumns = false,
            int columnPadding = 2,
            char? headerSeparator = '-',
            bool blankLineBetweenRows = false
            )
        {
            if (environment is null)
            {
                throw new ArgumentNullException(nameof(environment));
            }

            ColumnsToDisplay = columnsToDisplay ?? Array.Empty<string>();
            DisplayAllColumns = displayAllColumns;
            ColumnPadding = columnPadding;
            HeaderSeparator = headerSeparator;
            BlankLineBetweenRows = blankLineBetweenRows;
            ConsoleBufferWidth = environment.ConsoleBufferWidth;
            NewLine = environment.NewLine;
        }

        internal TabularOutputSettings(IEnvironment environment, ITabularOutputArgs args)
            : this(environment, columnsToDisplay: args.ColumnsToDisplay, displayAllColumns: args.DisplayAllColumns) { }

        public bool DisplayAllColumns { get; }

        public IReadOnlyList<string> ColumnsToDisplay { get; }

        public int ColumnPadding { get; }

        public char? HeaderSeparator { get; }

        public bool BlankLineBetweenRows { get; }

        public int ConsoleBufferWidth { get; }

        public string NewLine { get; }

        public string ShrinkReplacement => "...";

        internal static class ColumnNames
        {
            internal const string Author = "author";
            internal const string Language = "language";
            internal const string Tags = "tags";
            internal const string Type = "type";
        }
    }
}
