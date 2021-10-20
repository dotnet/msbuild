// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Cli.TabularOutput
{
    internal class CliTabularOutputSettings : ITabularOutputSettings
    {
        internal CliTabularOutputSettings(
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

        public bool DisplayAllColumns { get; }

        public IReadOnlyList<string> ColumnsToDisplay { get; }

        public int ColumnPadding { get; }

        public char? HeaderSeparator { get; }

        public bool BlankLineBetweenRows { get; }

        public int ConsoleBufferWidth { get; }

        public string NewLine { get; }

        public string ShrinkReplacement => "...";
    }
}
