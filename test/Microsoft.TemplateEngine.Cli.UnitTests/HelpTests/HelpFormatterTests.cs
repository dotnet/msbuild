using System;
using System.Collections.Generic;
using Xunit;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Mocks;
using Microsoft.TemplateEngine.TestHelper;

namespace Microsoft.TemplateEngine.Cli.UnitTests.HelpTests
{
    public class HelpFormatterTests
    {
        [Fact(DisplayName = nameof(CanShrinkOneColumn))]
        public void CanShrinkOneColumn()
        {
            ITemplateEngineHost host = new TestHost
            {
                HostIdentifier = "TestRunner",
                Version = "1.0.0.0",
                Locale = "en-US"
            };

            IEngineEnvironmentSettings environmentSettings = new MockEngineEnvironmentSettings()
            {
                Host = host,
                Environment = new MockEnvironment()
                {
                    ConsoleBufferWidth = 6 + 2 + 12 + 1
                }
            };

            IEnumerable<Tuple<string, string>> data = new List<Tuple<string, string>>()
            {
                new Tuple<string, string>("My test data", "My test data"),
                new Tuple<string, string>("My test data", "My test data")
            };

            string expectedOutput = $"Col...  Column 2    {Environment.NewLine}------  ------------{Environment.NewLine}My ...  My test data{Environment.NewLine}My ...  My test data{Environment.NewLine}";

            HelpFormatter<Tuple<string, string>> formatter =
             HelpFormatter
                 .For(
                     environmentSettings,
                     data,
                     columnPadding: 2,
                     headerSeparator: '-',
                     blankLineBetweenRows: false)
                 .DefineColumn(t => t.Item1, "Column 1", shrinkIfNeeded: true, minWidth: 2)
                 .DefineColumn(t => t.Item2, "Column 2");

            string result = formatter.Layout();
            Assert.Equal(expectedOutput, result);
        }

        [Fact(DisplayName = nameof(CanShrinkMultipleColumns))]
        public void CanShrinkMultipleColumns()
        {
            ITemplateEngineHost host = new TestHost
            {
                HostIdentifier = "TestRunner",
                Version = "1.0.0.0",
                Locale = "en-US"
            };

            IEngineEnvironmentSettings environmentSettings = new MockEngineEnvironmentSettings()
            {
                Host = host,
                Environment = new MockEnvironment()
                {
                    ConsoleBufferWidth = 5 + 2 + 7 + 1,
                }
            };

            IEnumerable<Tuple<string, string>> data = new List<Tuple<string, string>>()
            {
                new Tuple<string, string>("My test data", "My test data"),
                new Tuple<string, string>("My test data", "My test data")
            };

            string expectedOutput = $"Co...  Colu...{Environment.NewLine}-----  -------{Environment.NewLine}My...  My t...{Environment.NewLine}My...  My t...{Environment.NewLine}";

            HelpFormatter<Tuple<string, string>> formatter =
             HelpFormatter
                 .For(
                     environmentSettings,
                     data,
                     columnPadding: 2,
                     headerSeparator: '-',
                     blankLineBetweenRows: false)
                 .DefineColumn(t => t.Item1, "Column 1", shrinkIfNeeded: true, minWidth: 2)
                 .DefineColumn(t => t.Item2, "Column 2", shrinkIfNeeded: true, minWidth: 2);

            string result = formatter.Layout();
            Assert.Equal(expectedOutput, result);

        }

        [Fact(DisplayName = nameof(CannotShrinkOverMinimumWidth))]
        public void CannotShrinkOverMinimumWidth()
        {
            ITemplateEngineHost host = new TestHost
            {
                HostIdentifier = "TestRunner",
                Version = "1.0.0.0",
                Locale = "en-US"
            };

            IEngineEnvironmentSettings environmentSettings = new MockEngineEnvironmentSettings()
            {
                Host = host,
                Environment = new MockEnvironment()
                {
                    ConsoleBufferWidth = 10, //less than need for data below
                }
            };

            IEnumerable<Tuple<string, string>> data = new List<Tuple<string, string>>()
            {
                new Tuple<string, string>("My test data", "My test data"),
                new Tuple<string, string>("My test data", "My test data")
            };

            string expectedOutput = $"Column 1      Column 2   {Environment.NewLine}------------  -----------{Environment.NewLine}My test data  My test ...{Environment.NewLine}My test data  My test ...{Environment.NewLine}";

            HelpFormatter<Tuple<string, string>> formatter =
             HelpFormatter
                 .For(
                     environmentSettings,
                     data,
                     columnPadding: 2,
                     headerSeparator: '-',
                     blankLineBetweenRows: false)
                 .DefineColumn(t => t.Item1, "Column 1", shrinkIfNeeded: true, minWidth: 15)
                 .DefineColumn(t => t.Item2, "Column 2", shrinkIfNeeded: true, minWidth: 8);

            string result = formatter.Layout();
            Assert.Equal(expectedOutput, result);
        }
    }

}

