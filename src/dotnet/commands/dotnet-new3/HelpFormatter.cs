using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Microsoft.DotNet.Tools.New3
{
    public class HelpFormatter
    {
        public static HelpFormatter<T> For<T>(IEnumerable<T> items, int columnPadding, char? headerSeparator = null, bool blankLineBetweenRows = false)
        {
            return new HelpFormatter<T>(items, columnPadding, headerSeparator, blankLineBetweenRows);
        }
    }

    public class HelpFormatter<T>
    {
        private readonly bool _blankLineBetweenRows;
        private readonly int _columnPadding;
        private readonly List<ColumnDefinition> _columns = new List<ColumnDefinition>();
        private readonly char? _headerSeparator;
        private readonly IEnumerable<T> _items;

        public HelpFormatter(IEnumerable<T> items, int columnPadding, char? headerSeparator, bool blankLineBetweenRows)
        {
            _items = items;
            _columnPadding = columnPadding;
            _headerSeparator = headerSeparator;
            _blankLineBetweenRows = blankLineBetweenRows;
        }

        public HelpFormatter<T> DefineColumn(Func<T, string> binder, string header = null, int maxWidth = 0, bool alwaysMaximizeWidth = false)
        {
            _columns.Add(new ColumnDefinition(header, binder, maxWidth, alwaysMaximizeWidth));
            return this;
        }

        public string Layout()
        {
            Dictionary<int, int> widthLookup = new Dictionary<int, int>();
            Dictionary<int, int> lineCountLookup = new Dictionary<int, int>();
            List<TextWrapper[]> textByRow = new List<TextWrapper[]>();

            TextWrapper[] header = new TextWrapper[_columns.Count];
            int headerLines = 0;
            for (int i = 0; i < _columns.Count; ++i)
            {
                header[i] = new TextWrapper(_columns[i].Header, _columns[i].MaxWidth, _columns[i].AlwaysMaximizeWidth);
                headerLines = Math.Max(headerLines, header[i].LineCount);
                widthLookup[i] = header[i].MaxWidth;
            }

            int lineNumber = 0;

            foreach (T item in _items)
            {
                TextWrapper[] line = new TextWrapper[_columns.Count];
                int maxLineCount = 0;

                for (int i = 0; i < _columns.Count; ++i)
                {
                    line[i] = _columns[i].GetCell(item);
                    widthLookup[i] = Math.Max(widthLookup[i], line[i].MaxWidth);
                    maxLineCount = Math.Max(maxLineCount, line[i].LineCount);
                }

                lineCountLookup[lineNumber++] = maxLineCount;
                textByRow.Add(line);
            }

            StringBuilder b = new StringBuilder();

            if (_columns.Any(x => !string.IsNullOrEmpty(x.Header)))
            {
                for (int j = 0; j < headerLines; ++j)
                {
                    for (int i = 0; i < _columns.Count - 1; ++i)
                    {
                        b.Append(header[i][j, widthLookup[i]]);
                        b.Append("".PadRight(_columnPadding));
                    }

                    b.AppendLine(header[_columns.Count - 1][j, widthLookup[_columns.Count - 1]]);
                }
            }

            if (_headerSeparator.HasValue)
            {
                int totalWidth = _columnPadding * (_columns.Count - 1);

                for (int i = 0; i < _columns.Count; ++i)
                {
                    totalWidth += Math.Max(header[i].MaxWidth, widthLookup[i]);
                }

                b.AppendLine("".PadRight(totalWidth, _headerSeparator.Value));
            }

            int currentLine = 0;
            foreach (TextWrapper[] line in textByRow)
            {
                for (int j = 0; j < lineCountLookup[currentLine]; ++j)
                {
                    for (int i = 0; i < _columns.Count - 1; ++i)
                    {
                        b.Append(line[i][j, widthLookup[i]]);
                        b.Append("".PadRight(_columnPadding));
                    }

                    b.AppendLine(line[_columns.Count - 1][j, widthLookup[_columns.Count - 1]]);
                }

                if (_blankLineBetweenRows)
                {
                    b.AppendLine();
                }

                ++currentLine;
            }

            return b.ToString();
        }

        private class ColumnDefinition
        {
            private readonly int _maxWidth;
            private readonly string _header;
            private readonly Func<T, string> _binder;
            private readonly bool _alwaysMaximizeWidth;

            public ColumnDefinition(string header, Func<T, string> binder, int maxWidth = -1, bool alwaysMaximizeWidth = false)
            {
                _header = header;
                _maxWidth = maxWidth > 0 ? maxWidth : int.MaxValue;
                _binder = binder;
                _alwaysMaximizeWidth = alwaysMaximizeWidth && maxWidth > 0;
            }

            public string Header => _header;

            public bool AlwaysMaximizeWidth => _alwaysMaximizeWidth;

            public int MaxWidth => _maxWidth;

            public TextWrapper GetCell(T value)
            {
                return new TextWrapper(_binder(value), _maxWidth, _alwaysMaximizeWidth);
            }
        }

        private class TextWrapper
        {
            private readonly IReadOnlyList<string> _lines;

            public TextWrapper(string text, int maxWidth, bool alwaysMax)
            {
                List<string> lines = new List<string>();
                int position = 0;
                int realMaxWidth = alwaysMax ? maxWidth : 0;

                while (position < text.Length)
                {
                    int newline = text.IndexOf(Environment.NewLine, position, StringComparison.Ordinal);

                    if (newline > -1)
                    {
                        if (newline - position <= maxWidth)
                        {
                            lines.Add(text.Substring(position, newline - position).TrimEnd());
                            position = newline + Environment.NewLine.Length;
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
                    position += maxLength - 1;
                }
            }
        }
    }
}
