// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using Microsoft.DotNet.Tools;

namespace Microsoft.DotNet.Cli
{
    // Represents a table (with rows of type T) that can be printed to a terminal.
    internal class PrintableTable<T>
    {
        public const string ColumnDelimiter = "      ";
        private List<Column> _columns = new List<Column>();

        private class Column
        {
            public string Header { get; set; }
            public Func<T, string> GetContent { get; set; }
            public int MaxWidth { get; set; }
            public override string ToString() { return Header; }
        }

        public void AddColumn(string header, Func<T, string> getContent, int maxWidth = int.MaxValue)
        {
            if (getContent == null)
            {
                throw new ArgumentNullException(nameof(getContent));
            }

            if (maxWidth <= 0)
            {
                throw new ArgumentException(
                    CommonLocalizableStrings.ColumnMaxWidthMustBeGreaterThanZero,
                    nameof(maxWidth));
            }

            _columns.Add(
                new Column() {
                    Header = header,
                    GetContent = getContent,
                    MaxWidth = maxWidth
                });
        }

        public void PrintRows(IEnumerable<T> rows, Action<string> writeLine)
        {
            if (rows == null)
            {
                throw new ArgumentNullException(nameof(rows));
            }

            if (writeLine == null)
            {
                throw new ArgumentNullException(nameof(writeLine));
            }

            var widths = CalculateColumnWidths(rows);
            var totalWidth = CalculateTotalWidth(widths);
            if (totalWidth == 0)
            {
                return;
            }

            foreach (var line in EnumerateHeaderLines(widths))
            {
                writeLine(line);
            }

            writeLine(new string('-', totalWidth));

            foreach (var row in rows)
            {
                foreach (var line in EnumerateRowLines(row, widths))
                {
                    writeLine(line);
                }
            }
        }

        public int CalculateWidth(IEnumerable<T> rows)
        {
            if (rows == null)
            {
                throw new ArgumentNullException(nameof(rows));
            }

            return CalculateTotalWidth(CalculateColumnWidths(rows));
        }

        private IEnumerable<string> EnumerateHeaderLines(int[] widths)
        {
            if (_columns.Count != widths.Length)
            {
                throw new InvalidOperationException();
            }

            return EnumerateLines(
                widths,
                _columns.Select(c => new StringInfo(c.Header ?? "")).ToArray());
        }

        private IEnumerable<string> EnumerateRowLines(T row, int[] widths)
        {
            if (_columns.Count != widths.Length)
            {
                throw new InvalidOperationException();
            }

            return EnumerateLines(
                widths,
                _columns.Select(c => new StringInfo(c.GetContent(row) ?? "")).ToArray());
        }

        private static IEnumerable<string> EnumerateLines(int[] widths, StringInfo[] contents)
        {
            if (widths.Length != contents.Length)
            {
                throw new InvalidOperationException();
            }

            if (contents.Length == 0)
            {
                yield break;
            }

            var builder = new StringBuilder();
            for (int line = 0; true; ++line)
            {
                builder.Clear();

                bool emptyLine = true;
                bool appendDelimiter = false;
                for (int i = 0; i < contents.Length; ++i)
                {
                    // Skip zero-width columns entirely
                    if (widths[i] == 0)
                    {
                        continue;
                    }

                    if (appendDelimiter)
                    {
                        builder.Append(ColumnDelimiter);
                    }

                    var startIndex = line * widths[i];
                    var length = contents[i].LengthInTextElements;
                    if (startIndex < length)
                    {
                        var endIndex = (line + 1) * widths[i];
                        length = endIndex >= length ? length - startIndex : widths[i];
                        builder.Append(contents[i].SubstringByTextElements(startIndex, length));
                        builder.Append(' ', widths[i] - length);
                        emptyLine = false;
                    }
                    else
                    {
                        // No more content for this column; append whitespace to fill remaining space
                        builder.Append(' ', widths[i]);
                    }

                    appendDelimiter = true;
                }

                if (emptyLine)
                {
                    // Yield an "empty" line on the first line only
                    if (line == 0)
                    {
                        yield return builder.ToString();
                    }
                    yield break;
                }

                yield return builder.ToString();
            }
        }

        private int[] CalculateColumnWidths(IEnumerable<T> rows)
        {
            return _columns
                .Select(c => {
                    var width = new StringInfo(c.Header ?? "").LengthInTextElements;

                    foreach (var row in rows)
                    {
                        width = Math.Max(
                            width,
                            new StringInfo(c.GetContent(row) ?? "").LengthInTextElements);
                    }

                    return Math.Min(width, c.MaxWidth);
                })
                .ToArray();
        }

        private static int CalculateTotalWidth(int[] widths)
        {
            int sum = 0;
            int count = 0;

            foreach (var width in widths)
            {
                if (width == 0)
                {
                    // Skip zero-width columns
                    continue;
                }

                sum += width;
                ++count;
            }

            if (count == 0)
            {
                return 0;
            }

            return sum + (ColumnDelimiter.Length * (count - 1));
        }
    }
}
