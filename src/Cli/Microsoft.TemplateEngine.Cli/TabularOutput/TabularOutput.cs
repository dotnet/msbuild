// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.TemplateEngine.Cli.TabularOutput
{
    internal class TabularOutput
    {
        internal static TabularOutput<T> For<T>(TabularOutputSettings settings, IEnumerable<T> rows)
        {
            return new TabularOutput<T>(settings, rows);
        }
    }

    internal class TabularOutput<T>
    {
        private readonly List<ColumnDefinition> _columns = new List<ColumnDefinition>();
        private readonly IEnumerable<T> _rowDataItems;
        private readonly List<Tuple<int, bool, IComparer<string>>> _ordering = new List<Tuple<int, bool, IComparer<string>>>();
        private readonly TabularOutputSettings _settings;

        internal TabularOutput(TabularOutputSettings settings, IEnumerable<T> rows)
        {
            _rowDataItems = rows ?? Enumerable.Empty<T>();
            _settings = settings;
        }

        internal TabularOutput<T> DefineColumn(
            Func<T, string> binder,
            string? header = null,
            string? columnName = null,
            bool shrinkIfNeeded = false,
            int minWidth = 2,
            bool showAlways = false,
            bool defaultColumn = true,
            TextAlign textAlign = TextAlign.Left)
        {
            return DefineColumn(
                binder,
                out object? c,
                header,
                columnName,
                shrinkIfNeeded,
                minWidth,
                showAlways,
                defaultColumn,
                textAlign);
        }

        internal TabularOutput<T> DefineColumn(
            Func<T, string> binder,
            out object? column,
            string? header = null,
            string? columnName = null,
            bool shrinkIfNeeded = false,
            int minWidth = 2,
            bool showAlways = false,
            bool defaultColumn = true,
            TextAlign textAlign = TextAlign.Left)
        {
            column = null;
            if ((_settings.ColumnsToDisplay.Count == 0 && defaultColumn) || showAlways || (!string.IsNullOrWhiteSpace(columnName) && _settings.ColumnsToDisplay.Contains(columnName)) || _settings.DisplayAllColumns)
            {
                ColumnDefinition c = new ColumnDefinition(
                    _settings,
                    header,
                    binder,
                    minWidth: minWidth,
                    shrinkIfNeeded: shrinkIfNeeded,
                    textAlign: textAlign);
                _columns.Add(c);
                column = c;
            }
            return this;
        }

        internal string Layout(int indent = 0)
        {
            Dictionary<int, int> columnWidthLookup = new Dictionary<int, int>();
            List<TextWrapper[]> grid = new List<TextWrapper[]>();

            TextWrapper[] header = new TextWrapper[_columns.Count];
            int headerLines = 0;
            for (int i = 0; i < _columns.Count; ++i)
            {
                header[i] = new TextWrapper(_columns[i].Header ?? string.Empty, _columns[i].MaxWidth, _settings.NewLine, _settings.ShrinkReplacement);
                headerLines = Math.Max(headerLines, header[i].LineCount);
                columnWidthLookup[i] = header[i].MaxWidth;
            }

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

                grid.Add(row);
            }

            CalculateColumnWidth(columnWidthLookup);

            StringBuilder b = new StringBuilder();

            // Render column headers, if any exist
            if (_columns.Any(x => !string.IsNullOrEmpty(x.Header)))
            {
                for (int j = 0; j < headerLines; ++j)
                {
                    for (int i = 0; i < _columns.Count; ++i)
                    {
                        header[i].AppendTextWithPadding(b, j, _columns[i].CalculatedWidth, _columns[i].TextAlign);
                        if (i != _columns.Count - 1)
                        {
                            b.Append(' ', _settings.ColumnPadding);
                        }
                    }
                    b.AppendLine().Indent(indent);
                }
            }

            // Render header separator, if set
            if (_settings.HeaderSeparator.HasValue)
            {
                for (int i = 0; i < _columns.Count; ++i)
                {
                    b.Append(new string(_settings.HeaderSeparator.Value, _columns[i].CalculatedWidth));

                    if (i < _columns.Count - 1)
                    {
                        b.Append(new string(' ', _settings.ColumnPadding));
                    }
                }
                b.AppendLine().Indent(indent);
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
                for (int lineWithinRow = 0; lineWithinRow < rowToRender.Max(row => row.LineCount); ++lineWithinRow)
                {
                    // Render all columns
                    for (int columnIndex = 0; columnIndex < _columns.Count; ++columnIndex)
                    {
                        rowToRender[columnIndex].AppendTextWithPadding(b, lineWithinRow, _columns[columnIndex].CalculatedWidth, _columns[columnIndex].TextAlign);
                        if (columnIndex != _columns.Count - 1)
                        {
                            b.Append(' ', _settings.ColumnPadding);
                        }
                    }
                    b.AppendLine().Indent(indent);
                }

                if (_settings.BlankLineBetweenRows)
                {
                    b.AppendLine();
                }

                ++currentRowIndex;
            }

            return b.ToString().Indent(indent);
        }

        internal TabularOutput<T> OrderBy(object? columnToken, IComparer<string>? comparer = null)
        {
            if (columnToken is not ColumnDefinition columnDefinition)
            {
                throw new ArgumentException($"{nameof(columnToken)} is not of type {nameof(ColumnDefinition)}", nameof(columnToken));
            }
            comparer = comparer ?? StringComparer.Ordinal;
            int index = _columns.IndexOf(columnDefinition);

            if (index < 0)
            {
                return this;
            }

            _ordering.Add(Tuple.Create(index, false, comparer));
            return this;
        }

        internal TabularOutput<T> OrderByDescending(object? columnToken, IComparer<string>? comparer = null)
        {
            if (columnToken is not ColumnDefinition columnDefinition)
            {
                throw new ArgumentException($"{nameof(columnToken)} is not of type {nameof(ColumnDefinition)}", nameof(columnToken));
            }
            comparer = comparer ?? StringComparer.Ordinal;
            int index = _columns.IndexOf(columnDefinition);

            if (index < 0)
            {
                return this;
            }

            _ordering.Add(Tuple.Create(index, true, comparer));
            return this;
        }

        private void CalculateColumnWidth(IReadOnlyDictionary<int, int> columnWidthLookup)
        {
            int maxAllowedGridWidth = _settings.ConsoleBufferWidth;
            int totalPaddingWidth = _settings.ColumnPadding * (_columns.Count - 1);
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
                ColumnDefinition? columnToShrink = _columns.Aggregate<ColumnDefinition, ColumnDefinition?>(null, (selectedColumn, currentColumn) =>
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
            private readonly TabularOutputSettings _settings;

            internal ColumnDefinition(
                TabularOutputSettings settings,
                string? header,
                Func<T, string> binder,
                int minWidth = 2,
                int maxWidth = -1,
                bool shrinkIfNeeded = false,
                TextAlign textAlign = TextAlign.Left)
            {
                Header = header;
                MaxWidth = maxWidth > 0 ? maxWidth : int.MaxValue;
                _binder = binder;
                ShrinkIfNeeded = shrinkIfNeeded;
                _settings = settings;
                MinWidth = minWidth + _settings.ShrinkReplacement.Length; //we need to add required width for shrink replacement
                TextAlign = textAlign;
            }

            internal string? Header { get; }

            internal int CalculatedWidth { get; set; }

            internal int MinWidth { get; }

            internal int MaxWidth { get; }

            internal bool ShrinkIfNeeded { get; }

            internal TextAlign TextAlign { get; }

            internal bool LeftAlign { get; }

            internal bool RightAlign { get; }

            internal TextWrapper GetCell(T value)
            {
                return new TextWrapper(_binder(value), MaxWidth, _settings.NewLine, _settings.ShrinkReplacement);
            }
        }

        private class TextWrapper
        {
            private readonly IReadOnlyList<string> _lines;
            private readonly string _shrinkReplacement;
            private readonly IDictionary<TextAlign, Func<string, int, StringBuilder, StringBuilder>> _alignRules = new Dictionary<TextAlign, Func<string, int, StringBuilder, StringBuilder>>()
            {
                { TextAlign.Left, (string abbreviatedText, int maxColumnWidth, StringBuilder b) => b.Append(abbreviatedText).Append(' ', maxColumnWidth - abbreviatedText.GetUnicodeLength()) },
                {
                    TextAlign.Center, (string abbreviatedText, int maxColumnWidth, StringBuilder b) =>
                    {
                        var centralPoint = (maxColumnWidth + abbreviatedText.GetUnicodeLength()) / 2;
                        var leftOffset = maxColumnWidth - centralPoint;
                        var rightOffset = maxColumnWidth - (leftOffset + abbreviatedText.GetUnicodeLength());
                        return b.Append(' ', leftOffset).Append(abbreviatedText).Append(' ', rightOffset);
                    }
                },
                { TextAlign.Right, (string abbreviatedText, int maxColumnWidth, StringBuilder b) => b.Append(' ', maxColumnWidth - abbreviatedText.GetUnicodeLength()).Append(abbreviatedText) }
            };

            internal TextWrapper(string text, int maxWidth, string newLine, string shrinkReplacement)
            {
                List<string> lines = new List<string>();
                int position = 0;
                int realMaxWidth = 0;

                if (!string.IsNullOrWhiteSpace(text))
                {
                    while (position < text.Length)
                    {
                        int newlineIndex = text.IndexOf(newLine, position, StringComparison.Ordinal);

                        if (newlineIndex > -1)
                        {
                            if (text.Substring(position, newlineIndex - position).GetUnicodeLength() <= maxWidth)
                            {
                                lines.Add(text.Substring(position, newlineIndex - position).TrimEnd());
                                position = newlineIndex + newLine.Length;
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

                        realMaxWidth = Math.Max(realMaxWidth, lines[lines.Count - 1].GetUnicodeLength());
                    }
                }

                _lines = lines;
                MaxWidth = realMaxWidth;
                RawText = text;
                _shrinkReplacement = shrinkReplacement;
            }

            internal int LineCount => _lines.Count;

            internal int MaxWidth { get; }

            internal string RawText { get; }

            internal void AppendTextWithPadding(StringBuilder b, int line, int maxColumnWidth, TextAlign textAlign = TextAlign.Left)
            {
                var text = _lines.Count > line ? _lines[line] : string.Empty;
                var abbreviatedText = ShrinkTextToLength(text, maxColumnWidth);

                AlignText(textAlign, abbreviatedText, maxColumnWidth, b);
            }

            private static void GetLineText(string text, List<string> lines, int maxLength, int end, ref int position)
            {
                if (text.Substring(position, text.Length - position).GetUnicodeLength() < maxLength)
                {
                    lines.Add(text.Substring(position));
                    position = text.Length;
                    return;
                }

                int lastBreak = text.LastIndexOfAny(new[] { ' ', '-' }, end, end - position);
                while (lastBreak > 0 && text.Substring(position, lastBreak - position).GetUnicodeLength() > maxLength)
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

            private StringBuilder AlignText(TextAlign textAlign, string text, int maxColumnWidth, StringBuilder b) => _alignRules[textAlign](text, maxColumnWidth, b);

            private string ShrinkTextToLength(string text, int maxLength)
            {
                if (text.GetUnicodeLength() <= maxLength)
                {
                    // The text is short enough, so return it
                    return text;
                }
                // If the text is too long, shorten it enough to allow room for the ellipsis, then add the ellipsis

                int desiredLength = maxLength - _shrinkReplacement.Length;
                int possibleLength = 1;
                while (text.Substring(0, possibleLength).GetUnicodeLength() <= desiredLength)
                {
                    possibleLength++;
                }
                return text.Substring(0, possibleLength - 1) + _shrinkReplacement;
            }
        }
    }
}
