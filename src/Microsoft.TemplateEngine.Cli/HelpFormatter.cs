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

        public HelpFormatter<T> DefineColumn(Func<T, string> binder, string header = null)
        {
            _columns.Add(new ColumnDefinition(_environmentSettings, header, binder));
            return this;
        }

        public HelpFormatter<T> DefineColumn(Func<T, string> binder, out object column, string header = null)
        {
            ColumnDefinition c = new ColumnDefinition(_environmentSettings, header, binder);
            _columns.Add(c);
            column = c;
            return this;
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

            StringBuilder b = new StringBuilder();

            // Render column headers, if any exist
            if (_columns.Any(x => !string.IsNullOrEmpty(x.Header)))
            {
                for (int j = 0; j < headerLines; ++j)
                {
                    for (int i = 0; i < _columns.Count - 1; ++i)
                    {
                        b.Append(header[i][j, padTo: columnWidthLookup[i]]);
                        b.Append("".PadRight(_columnPadding));
                    }

                    b.AppendLine(header[_columns.Count - 1][j, padTo: columnWidthLookup[_columns.Count - 1]]);
                }
            }

            // Render header separator, if set
            if (_headerSeparator.HasValue)
            {
                for (int i = 0; i < _columns.Count; ++i)
                {
                    var columnWidth = Math.Max(header[i].MaxWidth, columnWidthLookup[i]);
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
                        b.Append(rowToRender[columnIndex][lineWithinRow, padTo: columnWidthLookup[columnIndex]]);
                        b.Append("".PadRight(_columnPadding));
                    }

                    // Render last column
                    b.AppendLine(rowToRender[_columns.Count - 1][lineWithinRow, padTo: columnWidthLookup[_columns.Count - 1]]);
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

            public ColumnDefinition(IEngineEnvironmentSettings environmentSettings, string header, Func<T, string> binder, int maxWidth = -1)
            {
                _header = header;
                _maxWidth = maxWidth > 0 ? maxWidth : int.MaxValue;
                _binder = binder;
                _environmentSettings = environmentSettings;
            }

            public string Header => _header;

            public int MaxWidth => _maxWidth;

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

            public string this[int index, int padTo = 0]
            {
                get { return (_lines.Count > index ? _lines[index] : string.Empty).PadRight(MaxWidth).PadRight(padTo > MaxWidth ? padTo : MaxWidth); }
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
