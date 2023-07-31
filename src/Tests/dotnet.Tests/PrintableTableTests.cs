// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli;

namespace Microsoft.DotNet.Tests
{
    public class PrintableTableTests : SdkTest
    {
        public PrintableTableTests(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void GivenNoColumnsItPrintsNoLines()
        {
            var table = new PrintableTable<string[]>();

            var lines = new List<string>();
            table.PrintRows(new string[][] {}, l => lines.Add(l));

            lines.Should().BeEmpty();
        }

        [Fact]
        public void GivenAnEmptyRowsCollectionItPrintsColumnHeaders()
        {
            RunTest(new TestData() {
                Columns = new[] {
                    "First Column",
                    "2nd Column",
                    "Another Column"
                },
                Rows = new string[][] {
                },
                ExpectedLines = new[] {
                    "First Column      2nd Column      Another Column",
                    "------------------------------------------------"
                },
                ExpectedTableWidth = 48
            });
        }

        [Fact]
        public void GivenASingleRowItPrintsCorrectly()
        {
            RunTest(new TestData() {
                Columns = new[] {
                    "1st",
                    "2nd",
                    "3rd"
                },
                Rows = new string[][] {
                    new[] {
                        "first",
                        "second",
                        "third"
                    }
                },
                ExpectedLines = new[] {
                    "1st        2nd         3rd  ",
                    "----------------------------",
                    "first      second      third"
                },
                ExpectedTableWidth = 28
            });
        }

        [Fact]
        public void GivenMultipleRowsItPrintsCorrectly()
        {
            RunTest(new TestData() {
                Columns = new[] {
                    "First",
                    "Second",
                    "Third",
                    "Fourth",
                    "Fifth"
                },
                Rows = new string[][] {
                    new[] {
                        "1st",
                        "2nd",
                        "3rd",
                        "4th",
                        "5th"
                    },
                    new [] {
                        "a",
                        "b",
                        "c",
                        "d",
                        "e"
                    },
                    new [] {
                        "much longer string 1",
                        "much longer string 2",
                        "much longer string 3",
                        "much longer string 4",
                        "much longer string 5",
                    }
                },
                ExpectedLines = new[] {
                    "First                     Second                    Third                     Fourth                    Fifth               ",
                    "----------------------------------------------------------------------------------------------------------------------------",
                    "1st                       2nd                       3rd                       4th                       5th                 ",
                    "a                         b                         c                         d                         e                   ",
                    "much longer string 1      much longer string 2      much longer string 3      much longer string 4      much longer string 5"
                },
                ExpectedTableWidth = 124
            });
        }

        [Fact]
        public void GivenARowWithEmptyStringsItPrintsCorrectly()
        {
            RunTest(new TestData() {
                Columns = new[] {
                    "First",
                    "Second",
                    "Third",
                    "Fourth",
                    "Fifth"
                },
                Rows = new string[][] {
                    new[] {
                        "1st",
                        "2nd",
                        "3rd",
                        "4th",
                        "5th"
                    },
                    new [] {
                        "",
                        "",
                        "",
                        "",
                        ""
                    },
                    new [] {
                        "much longer string 1",
                        "much longer string 2",
                        "much longer string 3",
                        "much longer string 4",
                        "much longer string 5",
                    }
                },
                ExpectedLines = new[] {
                    "First                     Second                    Third                     Fourth                    Fifth               ",
                    "----------------------------------------------------------------------------------------------------------------------------",
                    "1st                       2nd                       3rd                       4th                       5th                 ",
                    "                                                                                                                            ",
                    "much longer string 1      much longer string 2      much longer string 3      much longer string 4      much longer string 5"
                },
                ExpectedTableWidth = 124
            });
        }

        [Fact]
        public void GivenColumnsWithMaximumWidthsItPrintsCorrectly()
        {
            RunTest(new TestData() {
                Columns = new[] {
                    "First",
                    "Second",
                    "Third",
                },
                ColumnWidths = new[] {
                    3,
                    int.MaxValue,
                    4
                },
                Rows = new string[][] {
                    new[] {
                        "123",
                        "1234567890",
                        "1234"
                    },
                    new [] {
                        "1",
                        "1",
                        "1",
                    },
                    new [] {
                        "12345",
                        "a much longer string",
                        "1234567890"
                    },
                    new [] {
                        "123456",
                        "hello world",
                        "12345678"
                    }
                },
                ExpectedLines = new[] {
                    "Fir      Second                    Thir",
                    "st                                 d   ",
                    "---------------------------------------",
                    "123      1234567890                1234",
                    "1        1                         1   ",
                    "123      a much longer string      1234",
                    "45                                 5678",
                    "                                   90  ",
                    "123      hello world               1234",
                    "456                                5678"
                },
                ExpectedTableWidth = 39
            });
        }

        [Fact]
        public void GivenARowContainingUnicodeCharactersItPrintsCorrectly()
        {
            RunTest(new TestData() {
                Columns = new[] {
                    "Poem"
                },
                Rows = new string[][] {
                    new [] {
                        "\u3044\u308D\u306F\u306B\u307B\u3078\u3068\u3061\u308A\u306C\u308B\u3092"
                    }
                },
                ExpectedLines = new[] {
                    "Poem        ",
                    "------------",
                    "\u3044\u308D\u306F\u306B\u307B\u3078\u3068\u3061\u308A\u306C\u308B\u3092"
                },
                ExpectedTableWidth = 12
            });
        }

        [Fact]
        public void GivenARowContainingUnicodeCharactersItWrapsCorrectly()
        {
            RunTest(new TestData() {
                Columns = new[] {
                    "Poem"
                },
                ColumnWidths = new [] {
                    5
                },
                Rows = new string[][] {
                    new [] {
                        "\u3044\u308D\u306F\u306B\u307B\u3078\u3068\u3061\u308A\u306C\u308B\u3092"
                    }
                },
                ExpectedLines = new[] {
                    "Poem ",
                    "-----",
                    "\u3044\u308D\u306F\u306B\u307B",
                    "\u3078\u3068\u3061\u308A\u306C",
                    "\u308B\u3092   "
                },
                ExpectedTableWidth = 5
            });
        }

        [Fact]
        public void GivenARowContainingUnicodeCombiningCharactersItPrintsCorrectly()
        {
            // The unicode string is "test" with "enclosing circle backslash" around each character
            // Given 0x20E0 is a combining character, the string should be four graphemes in length,
            // despite having eight codepoints. Thus there should be 10 spaces following the characters.
            RunTest(new TestData() {
                Columns = new[] {
                    "Unicode String"
                },
                Rows = new string[][] {
                    new [] {
                        "\u0074\u20E0\u0065\u20E0\u0073\u20E0\u0074\u20E0"
                    }
                },
                ExpectedLines = new[] {
                    "Unicode String",
                    "--------------",
                    "\u0074\u20E0\u0065\u20E0\u0073\u20E0\u0074\u20E0          "
                },
                ExpectedTableWidth = 14
            });
        }

        [Fact]
        public void GivenARowContainingUnicodeCombiningCharactersItWrapsCorrectly()
        {
            // See comment for GivenARowContainingUnicodeCombiningCharactersItPrintsCorrectly regarding string content
            // This should wrap after the second grapheme rather than the second code point (constituting the first grapheme)
            RunTest(new TestData() {
                Columns = new[] {
                    "01"
                },
                ColumnWidths = new[] {
                    2
                },
                Rows = new string[][] {
                    new [] {
                        "\u0074\u20E0\u0065\u20E0\u0073\u20E0\u0074\u20E0"
                    }
                },
                ExpectedLines = new[] {
                    "01",
                    "--",
                    "\u0074\u20E0\u0065\u20E0",
                    "\u0073\u20E0\u0074\u20E0"
                },
                ExpectedTableWidth = 2
            });
        }

        [Fact]
        public void GivenAnEmptyColumnHeaderItPrintsTheColumnHeaderAsEmpty()
        {
            RunTest(new TestData() {
                Columns = new[] {
                    "First",
                    "",
                    "Third",
                },
                Rows = new string[][] {
                    new[] {
                        "1st",
                        "2nd",
                        "3rd"
                    }
                },
                ExpectedLines = new[] {
                    "First               Third",
                    "-------------------------",
                    "1st        2nd      3rd  "
                },
                ExpectedTableWidth = 25
            });
        }

        [Fact]
        public void GivenAllEmptyColumnHeadersItPrintsTheEntireHeaderAsEmpty()
        {
            RunTest(new TestData() {
                Columns = new[] {
                    null,
                    "",
                    null,
                },
                Rows = new string[][] {
                    new[] {
                        "1st",
                        "2nd",
                        "3rd"
                    }
                },
                ExpectedLines = new[] {
                    "                     ",
                    "---------------------",
                    "1st      2nd      3rd"
                },
                ExpectedTableWidth = 21
            });
        }

        [Fact]
        public void GivenZeroWidthColumnsItSkipsTheColumns()
        {
            RunTest(new TestData() {
                Columns = new[] {
                    "",
                    "First",
                    null,
                    "Second",
                    ""
                },
                Rows = new string[][] {
                    new[] {
                        "",
                        "1st",
                        null,
                        "2nd",
                        ""
                    }
                },
                ExpectedLines = new[] {
                    "First      Second",
                    "-----------------",
                    "1st        2nd   "
                },
                ExpectedTableWidth = 17
            });
        }

        public class TestData
        {
            public IEnumerable<string> Columns { get; set; }
            public int[] ColumnWidths { get; set; }
            public IEnumerable<string[]> Rows { get; set; }
            public IEnumerable<string> ExpectedLines { get; set; }
            public int ExpectedTableWidth { get; set; }
        }

        private void RunTest(TestData data)
        {
            var table = new PrintableTable<string[]>();

            int index = 0;
            foreach (var column in data.Columns)
            {
                var i = index;
                table.AddColumn(
                    column,
                    r => r[i],
                    data.ColumnWidths?[i] ?? int.MaxValue);
                ++index;
            }

            var lines = new List<string>();
            table.PrintRows(data.Rows, l => lines.Add(l));

            lines.Should().Equal(data.ExpectedLines);
            table.CalculateWidth(data.Rows).Should().Be(data.ExpectedTableWidth);
        }
    }
}
