// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Cli.CommandParsing;

namespace Microsoft.TemplateEngine.Cli
{
    public class HelpFormatter
    {
        public static HelpFormatter<T> For<T>(IEngineEnvironmentSettings environmentSettings, INewCommandInput commandInput, IEnumerable<T> rows, int columnPadding, char? headerSeparator = null, bool blankLineBetweenRows = false)
        {
            return new HelpFormatter<T>(environmentSettings, commandInput, rows, columnPadding, headerSeparator, blankLineBetweenRows);
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
        private const string ShrinkReplacement = "...";
        private readonly INewCommandInput _commandInput;

        public HelpFormatter(IEngineEnvironmentSettings environmentSettings, INewCommandInput commandInput, IEnumerable<T> rows, int columnPadding, char? headerSeparator, bool blankLineBetweenRows)
        {
            _rowDataItems = rows ?? Enumerable.Empty<T>();
            _columnPadding = columnPadding;
            _headerSeparator = headerSeparator;
            _blankLineBetweenRows = blankLineBetweenRows;
            _environmentSettings = environmentSettings;
            _commandInput = commandInput;
        }

        public HelpFormatter<T> DefineColumn(Func<T, string> binder,  string header = null, string columnName = null, bool shrinkIfNeeded = false, int minWidth = 2, bool showAlways = false, bool defaultColumn = true, bool rightAlign = false)
        {
            return DefineColumn(binder, out object c,  header, columnName, shrinkIfNeeded, minWidth, showAlways, defaultColumn, rightAlign);
        }

        public HelpFormatter<T> DefineColumn(Func<T, string> binder, out object column, string header = null, string columnName = null, bool shrinkIfNeeded = false, int minWidth = 2, bool showAlways = false, bool defaultColumn = true, bool rightAlign = false)
        {
            column = null;
            if ((_commandInput.Columns.Count == 0  && defaultColumn) || showAlways || (!string.IsNullOrWhiteSpace(columnName) && _commandInput.Columns.Contains(columnName)) || _commandInput.ShowAllColumns)
            {
                ColumnDefinition c = new ColumnDefinition(_environmentSettings, header, binder, shrinkIfNeeded: shrinkIfNeeded, minWidth: minWidth, rightAlign: rightAlign);
                _columns.Add(c);
                column = c;
            }
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
            return text.Substring(0, Math.Max(0, maxLength - ShrinkReplacement.Length)) + ShrinkReplacement;
        }

        public string Layout()
        {
            Dictionary<int, int> columnWidthLookup = new Dictionary<int, int>();
            Dictionary<int, int> rowHeightForRow = new Dictionary<int, int>();
            List<TextWrapper[]> grid = new List<TextWrapper[]>();

            TextWrapper[] header = new TextWrapper[_columns.Count];
            int headerLines = 0;
            for (int i = 0; i < _columns.Count; ++i)
            {
                header[i] = new TextWrapper(_environmentSettings, _columns[i].Header, _columns[i].MaxWidth);
                headerLines = Math.Max(headerLines, header[i].LineCount);
                columnWidthLookup[i] = header[i].MaxWidth;
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

            CalculateColumnWidth(columnWidthLookup);

            StringBuilder b = new StringBuilder();

            // Render column headers, if any exist
            if (_columns.Any(x => !string.IsNullOrEmpty(x.Header)))
            {
                for (int j = 0; j < headerLines; ++j)
                {
                    for (int i = 0; i < _columns.Count - 1; ++i)
                    {
                        b.Append(header[i].GetTextWithPadding(j, _columns[i].CalculatedWidth, _columns[i].RightAlign));
                        b.Append("".PadRight(_columnPadding));
                    }

                    b.AppendLine(header[_columns.Count - 1].GetTextWithPadding(j, _columns[_columns.Count - 1].CalculatedWidth, _columns[_columns.Count - 1].RightAlign));
                }
            }

            // Render header separator, if set
            if (_headerSeparator.HasValue)
            {
                for (int i = 0; i < _columns.Count; ++i)
                {
                    b.Append(new string(_headerSeparator.Value, _columns[i].CalculatedWidth));

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
                        b.Append(rowToRender[columnIndex].GetTextWithPadding(lineWithinRow, _columns[columnIndex].CalculatedWidth, _columns[columnIndex].RightAlign));
                        b.Append("".PadRight(_columnPadding));
                    }

                    // Render last column
                    b.AppendLine(rowToRender[_columns.Count - 1].GetTextWithPadding(lineWithinRow, _columns[_columns.Count - 1].CalculatedWidth, _columns[_columns.Count - 1].RightAlign));
                }

                if (_blankLineBetweenRows)
                {
                    b.AppendLine();
                }

                ++currentRowIndex;
            }

            return b.ToString();
        }

        private void CalculateColumnWidth(IReadOnlyDictionary<int, int> columnWidthLookup)
        {
            int maxAllowedGridWidth = _environmentSettings.Environment.ConsoleBufferWidth;
            int totalPaddingWidth = _columnPadding * (_columns.Count - 1);
            int maxRowWidth = columnWidthLookup.Sum(column => column.Value) + totalPaddingWidth;

            //set identified needed width as the starting point
            for (int i = 0; i < _columns.Count; ++i)
            {
                _columns[i].CalculatedWidth = columnWidthLookup[i];
            }

            // If there is no columns to shrink or it fits, use identified width
            if (!_columns.Any(col => col.ShrinkIfNeeded) || maxRowWidth < maxAllowedGridWidth)
            {
                return;
            }

            //calculate the minimum length we could have after shrinking
            int minimumLengthNeeded = 0;
            for (int i = 0; i < _columns.Count; ++i)
            {
                if (_columns[i].ShrinkIfNeeded)
                {
                    minimumLengthNeeded += Math.Min(_columns[i].MinWidth, columnWidthLookup[i]);
                }
                else
                {
                    minimumLengthNeeded += columnWidthLookup[i];
                }
            }

            //there is not enough space anyway - set all shrinkable columns to minimum width or use actual width if it is less than mininum
            if (minimumLengthNeeded >= maxAllowedGridWidth)
            {
                for (int i = 0; i < _columns.Count; ++i)
                {
                    if (_columns[i].ShrinkIfNeeded)
                    {
                        _columns[i].CalculatedWidth = Math.Min(_columns[i].MinWidth, columnWidthLookup[i]);
                    }
                }
                return;
            }

            // If there's a shrinkable column, by how much should it shrink?
            // We need the grid width to be at most 1 less than the buffer width. We don't want exactly
            // the buffer width because that will cause the caret to wrap on the last character, so we
            // stop 1 short of it.
            int amountForShrinkableColumnToGiveUp = maxRowWidth - maxAllowedGridWidth + 1;
            while (amountForShrinkableColumnToGiveUp > 0)
            {
                //picks up the widest column to shrink first
                ColumnDefinition columnToShrink = _columns.Aggregate<ColumnDefinition, ColumnDefinition>(null, (selectedColumn, currentColumn) =>
                {
                    if (currentColumn.ShrinkIfNeeded && currentColumn.CalculatedWidth > currentColumn.MinWidth)
                    {
                        if (selectedColumn == null || currentColumn.CalculatedWidth >= selectedColumn.CalculatedWidth)
                        {
                            selectedColumn = currentColumn;
                        }
                    }
                    return selectedColumn;
                });
                if (columnToShrink == null)
                {
                    //there is no column that can be shrinked
                    //this should not happen as we already checked if there is enough space to fit the columns with shrinking
                    break;
                }
                else
                {
                    columnToShrink.CalculatedWidth--;
                    amountForShrinkableColumnToGiveUp--;
                }    
            }

        }
    
        private class ColumnDefinition
        {
            private readonly Func<T, string> _binder;
            private readonly IEngineEnvironmentSettings _environmentSettings;

            public ColumnDefinition(IEngineEnvironmentSettings environmentSettings, string header, Func<T, string> binder, int minWidth = 2, int maxWidth = -1, bool shrinkIfNeeded = false, bool rightAlign = false)
            {
                Header = header;
                MaxWidth = maxWidth > 0 ? maxWidth : int.MaxValue;
                _binder = binder;
                _environmentSettings = environmentSettings;
                ShrinkIfNeeded = shrinkIfNeeded;
                MinWidth = minWidth + ShrinkReplacement.Length; //we need to add required width for shrink replacement
                RightAlign = rightAlign;
            }

            public string Header { get; }

            public int CalculatedWidth { get; set; }

            public int MinWidth { get; }

            public int MaxWidth { get; }

            public bool ShrinkIfNeeded { get; }

            public bool RightAlign { get; }

            public TextWrapper GetCell(T value)
            {
                return new TextWrapper(_environmentSettings, _binder(value), MaxWidth);
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

                if (!string.IsNullOrWhiteSpace(text))
                {
                    while (position < text.Length)
                    {
                        int newlineIndex = text.IndexOf(environmentSettings.Environment.NewLine, position, StringComparison.Ordinal);

                        if (newlineIndex > -1)
                        {
                            if (newlineIndex - position <= maxWidth)
                            {
                                lines.Add(text.Substring(position, newlineIndex - position).TrimEnd());
                                position = newlineIndex + environmentSettings.Environment.NewLine.Length;
                            }
                            else
                            {
                                GetLineText(text, lines, maxWidth, newlineIndex, ref position);
                            }
                        }
                        else
                        {
                            GetLineText(text, lines, maxWidth, text.Length - 1, ref position);
                        }

                        realMaxWidth = Math.Max(realMaxWidth, lines[lines.Count - 1].Length);
                    }
                }

                _lines = lines;
                MaxWidth = realMaxWidth;
                RawText = text;
            }

            public int LineCount => _lines.Count;

            public int MaxWidth { get; }

            public string GetTextWithPadding(int line, int maxColumnWidth, bool rightAlign = false)
            {
                var text = _lines.Count > line ? _lines[line] : string.Empty;
                var abbreviatedText = ShrinkTextToLength(text, maxColumnWidth);

                if (rightAlign)
                {
                    return abbreviatedText.PadLeft(maxColumnWidth);
                }
                else
                {
                    return abbreviatedText.PadRight(maxColumnWidth);
                }
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
