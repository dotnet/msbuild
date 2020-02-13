// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Cli
{
    public class HelpFormatter
    {
        public static HelpFormatter<T> For<T>(IEngineEnvironmentSettings environmentSettings, IEnumerable<T> rows, int columnPadding, char? headerSeparator = null, bool blankLineBetweenRows = false)
        {
            return new HelpFormatter<T>(environmentSettings, rows, columnPadding, headerSeparator, blankLineBetweenRows);
        }
    }

    public class HelpFormatter<T>
    {
        private readonly bool _blankLineBetweenRows;
        private readonly int _columnPadding;
        private readonly List<ColumnDefinition> _columns = new List<ColumnDefinition>();
        private readonly char? _headerSeparator;
        private readonly IEnumerable<T> _rowDataItems;
        private readonly List<Tuple<int, bool, IComparer<string>>> _ordering = new List<Tuple<int, bool, IComparer<string>>>();
        private readonly IEngineEnvironmentSettings _environmentSettings;

        public HelpFormatter(IEngineEnvironmentSettings environmentSettings, IEnumerable<T> rows, int columnPadding, char? headerSeparator, bool blankLineBetweenRows)
        {
            _rowDataItems = rows ?? Enumerable.Empty<T>();
            _columnPadding = columnPadding;
            _headerSeparator = headerSeparator;
            _blankLineBetweenRows = blankLineBetweenRows;
            _environmentSettings = environmentSettings;
        }

        public HelpFormatter<T> DefineColumn(Func<T, string> binder, string header = null, bool shrinkIfNeeded = false)
        {
            _columns.Add(new ColumnDefinition(_environmentSettings, header, binder, shrinkIfNeeded: shrinkIfNeeded));
            return this;
        }

        public HelpFormatter<T> DefineColumn(Func<T, string> binder, out object column, string header = null, bool shrinkIfNeeded = false)
        {
            ColumnDefinition c = new ColumnDefinition(_environmentSettings, header, binder, shrinkIfNeeded: shrinkIfNeeded);
            _columns.Add(c);
            column = c;
            return this;
        }

        private static string ShrinkTextToLength(string text, int maxLength)
        {
            if (text.Length <= maxLength)
            {
                // The text is short enough, so return it
                return text;
            }
            // If the text is too long, shorten it enough to allow room for the ellipsis, then add the ellipsis
            return text.Substring(0, Math.Max(0, maxLength - 3)) + "...";
        }

        /// <summary>
        /// The minimum column width to render. All columns, including shrinkable columns, will
        /// render at least this size.
        /// </summary>
        private const int MinimumColumnWidth = 5;

        public string Layout()
        {
            Dictionary<int, int> columnWidthLookup = new Dictionary<int, int>();
            Dictionary<int, int> rowHeightForRow = new Dictionary<int, int>();
            List<TextWrapper[]> grid = new List<TextWrapper[]>();

            TextWrapper[] header = new TextWrapper[_columns.Count];
            int headerLines = 0;
            var shrinkableColumnIndex = -1;
            for (int i = 0; i < _columns.Count; ++i)
            {
                header[i] = new TextWrapper(_environmentSettings, _columns[i].Header, _columns[i].MaxWidth);
                headerLines = Math.Max(headerLines, header[i].LineCount);
                columnWidthLookup[i] = header[i].MaxWidth;
                if (_columns[i].ShrinkIfNeeded)
                {
                    if (shrinkableColumnIndex != -1)
                    {
                        throw new InvalidOperationException($"Cannot have more than one shrinkable column. These columns are shrinkable: Column #{shrinkableColumnIndex} ('{_columns[shrinkableColumnIndex].Header}') and Column #{i} ('{_columns[i].Header}').");
                    }
                    shrinkableColumnIndex = i;
                }
            }

            int lineNumber = 0;

            foreach (T rowDataItem in _rowDataItems)
            {
                TextWrapper[] row = new TextWrapper[_columns.Count];
                int rowHeight = 0;

                for (int i = 0; i < _columns.Count; ++i)
                {
                    row[i] = _columns[i].GetCell(rowDataItem);
                    columnWidthLookup[i] = Math.Max(columnWidthLookup[i], row[i].MaxWidth);
                    rowHeight = Math.Max(rowHeight, row[i].LineCount);
                }

                rowHeightForRow[lineNumber++] = rowHeight;
                grid.Add(row);
            }

            var amountForShrinkableColumnToGiveUp = 0; // If there's a shrinkable column, by how much should it shrink?

            if (shrinkableColumnIndex != -1)
            {
                // If there is a shrinkable column, figure out whether the grid fits as-is, or if it needs to be shrunken
                var maxAllowedGridWidth = _environmentSettings.Environment.ConsoleBufferWidth;

                var totalPaddingWidth = _columnPadding * (_columns.Count - 1);
                var maxRowWidth = columnWidthLookup.Sum(column => column.Value) + totalPaddingWidth;

                // We need the grid width to be at most 1 less than the buffer width. We don't want exactly
                // the buffer width because that will cause the caret to wrap on the last character, so we
                // stop 1 short of it.
                if (maxRowWidth >= maxAllowedGridWidth)
                {
                    amountForShrinkableColumnToGiveUp = maxRowWidth - maxAllowedGridWidth + 1;
                }
            }

            // Gets the column width, accounting for a possible shrunken column, and
            // minimum allowed column width.
            int GetColumnWidth(int column)
            {
                var maxColumnWidth = columnWidthLookup[column];
                if (column == shrinkableColumnIndex)
                {
                    maxColumnWidth -= amountForShrinkableColumnToGiveUp;
                }
                return Math.Max(maxColumnWidth, MinimumColumnWidth);
            }

            StringBuilder b = new StringBuilder();

            // Render column headers, if any exist
            if (_columns.Any(x => !string.IsNullOrEmpty(x.Header)))
            {
                for (int j = 0; j < headerLines; ++j)
                {
                    for (int i = 0; i < _columns.Count - 1; ++i)
                    {
                        b.Append(header[i].GetTextWithPadding(j, GetColumnWidth(i)));
                        b.Append("".PadRight(_columnPadding));
                    }

                    b.AppendLine(header[_columns.Count - 1].GetTextWithPadding(j, GetColumnWidth(_columns.Count - 1)));
                }
            }

            // Render header separator, if set
            if (_headerSeparator.HasValue)
            {
                for (int i = 0; i < _columns.Count; ++i)
                {
                    var columnWidth = Math.Max(header[i].MaxWidth, columnWidthLookup[i]);
                    if (i == shrinkableColumnIndex)
                    {
                        columnWidth -= amountForShrinkableColumnToGiveUp;
                    }
                    columnWidth = Math.Max(columnWidth, MinimumColumnWidth);
                    b.Append(new string(_headerSeparator.Value, columnWidth));

                    if (i < _columns.Count - 1)
                    {
                        b.Append(new string(' ', _columnPadding));
                    }
                }

                b.AppendLine();
            }

            IEnumerable<TextWrapper[]> rows = grid;

            // Apply ordering to list
            if (_ordering.Count > 0)
            {
                IOrderedEnumerable<TextWrapper[]> orderedRows;
                if (_ordering[0].Item2)
                {
                    orderedRows = rows.OrderByDescending(x => x[_ordering[0].Item1].RawText, _ordering[0].Item3);
                }
                else
                {
                    orderedRows = rows.OrderBy(x => x[_ordering[0].Item1].RawText, _ordering[0].Item3);
                }

                for (int i = 1; i < _ordering.Count; ++i)
                {
                    int localI = i;
                    if (_ordering[i].Item2)
                    {
                        orderedRows = orderedRows.ThenByDescending(x => x[_ordering[localI].Item1].RawText, _ordering[i].Item3);
                    }
                    else
                    {
                        orderedRows = orderedRows.ThenBy(x => x[_ordering[localI].Item1].RawText, _ordering[i].Item3);
                    }
                }

                rows = orderedRows;
            }

            // Render row contents (each row can have more than 1 line for multi-line content)
            int currentRowIndex = 0;
            foreach (TextWrapper[] rowToRender in rows)
            {
                for (int lineWithinRow = 0; lineWithinRow < rowHeightForRow[currentRowIndex]; ++lineWithinRow)
                {
                    // Render all columns except last column
                    for (int columnIndex = 0; columnIndex < _columns.Count - 1; ++columnIndex)
                    {
                        b.Append(rowToRender[columnIndex].GetTextWithPadding(lineWithinRow, GetColumnWidth(columnIndex)));
                        b.Append("".PadRight(_columnPadding));
                    }

                    // Render last column
                    b.AppendLine(rowToRender[_columns.Count - 1].GetTextWithPadding(lineWithinRow, GetColumnWidth(_columns.Count - 1)));
                }

                if (_blankLineBetweenRows)
                {
                    b.AppendLine();
                }

                ++currentRowIndex;
            }

            return b.ToString();
        }

        private class ColumnDefinition
        {
            private readonly int _maxWidth;
            private readonly string _header;
            private readonly Func<T, string> _binder;
            private readonly IEngineEnvironmentSettings _environmentSettings;

            public ColumnDefinition(IEngineEnvironmentSettings environmentSettings, string header, Func<T, string> binder, int maxWidth = -1, bool shrinkIfNeeded = false)
            {
                _header = header;
                _maxWidth = maxWidth > 0 ? maxWidth : int.MaxValue;
                _binder = binder;
                _environmentSettings = environmentSettings;
                ShrinkIfNeeded = shrinkIfNeeded;
            }

            public string Header => _header;

            public int MaxWidth => _maxWidth;

            /// <summary>
            /// Indicates that this column will be shrunk if there is not enough room to display the entire table.
            /// At most one column can have this set.
            /// </summary>
            public bool ShrinkIfNeeded { get; }

            public TextWrapper GetCell(T value)
            {
                return new TextWrapper(_environmentSettings, _binder(value), _maxWidth);
            }
        }

        private class TextWrapper
        {
            private readonly IReadOnlyList<string> _lines;

            public TextWrapper(IEngineEnvironmentSettings environmentSettings, string text, int maxWidth)
            {
                List<string> lines = new List<string>();
                int position = 0;
                int realMaxWidth = 0;

                while (position < text.Length)
                {
                    int newline = text.IndexOf(environmentSettings.Environment.NewLine, position, StringComparison.Ordinal);

                    if (newline > -1)
                    {
                        if (newline - position <= maxWidth)
                        {
                            lines.Add(text.Substring(position, newline - position).TrimEnd());
                            position = newline + environmentSettings.Environment.NewLine.Length;
                        }
                        else
                        {
                            GetLineText(text, lines, maxWidth, newline, ref position);
                        }
                    }
                    else
                    {
                        GetLineText(text, lines, maxWidth, text.Length - 1, ref position);
                    }

                    realMaxWidth = Math.Max(realMaxWidth, lines[lines.Count - 1].Length);
                }

                _lines = lines;
                MaxWidth = realMaxWidth;
                RawText = text;
            }

            public int LineCount => _lines.Count;

            public int MaxWidth { get; }

            public string GetTextWithPadding(int line, int maxColumnWidth)
            {
                var text = _lines.Count > line ? _lines[line] : string.Empty;
                var abbreviatedText = ShrinkTextToLength(text, maxColumnWidth);

                return
                    abbreviatedText
                    .PadRight(maxColumnWidth);
            }

            private static void GetLineText(string text, List<string> lines, int maxLength, int end, ref int position)
            {
                if (text.Length - position < maxLength)
                {
                    lines.Add(text.Substring(position));
                    position = text.Length;
                    return;
                }

                int lastBreak = text.LastIndexOfAny(new[] { ' ', '-' }, end, end - position);
                while (lastBreak > 0 && lastBreak - position > maxLength)
                {
                    --lastBreak;
                    lastBreak = text.LastIndexOfAny(new[] { ' ', '-' }, lastBreak, lastBreak - position);
                }

                if (lastBreak > 0)
                {
                    lines.Add(text.Substring(position, lastBreak - position + 1).TrimEnd());
                    position = lastBreak + 1;
                }
                else
                {
                    int properMax = Math.Min(maxLength - 1, text.Length - position);
                    lines.Add(text.Substring(position, properMax) + '-');
                    position += properMax;
                }
            }

            public string RawText { get; }
        }

        public HelpFormatter<T> OrderBy(object columnToken, IComparer<string> comparer = null)
        {
            comparer = comparer ?? StringComparer.Ordinal;
            int index = _columns.IndexOf(columnToken as ColumnDefinition);

            if (index < 0)
            {
                return this;
            }

            _ordering.Add(Tuple.Create(index, false, comparer));
            return this;
        }

        public HelpFormatter<T> OrderByDescending(object columnToken, IComparer<string> comparer = null)
        {
            comparer = comparer ?? StringComparer.Ordinal;
            int index = _columns.IndexOf(columnToken as ColumnDefinition);

            if (index < 0)
            {
                return this;
            }

            _ordering.Add(Tuple.Create(index, true, comparer));
            return this;
        }
    }
}
