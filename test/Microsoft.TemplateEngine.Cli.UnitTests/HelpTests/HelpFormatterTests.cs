// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Cli.CommandParsing;
using Microsoft.TemplateEngine.Cli.TableOutput;
using Microsoft.TemplateEngine.Cli.UnitTests.CliMocks;
using Microsoft.TemplateEngine.Mocks;
using Xunit;

namespace Microsoft.TemplateEngine.Cli.UnitTests.HelpTests
{
    public class HelpFormatterTests
    {
        [Fact]
        public void CanShrinkOneColumn()
        {
            IEngineEnvironmentSettings environmentSettings = new MockEngineEnvironmentSettings()
            {
                Environment = new MockEnvironment()
                {
                    ConsoleBufferWidth = 6 + 2 + 12 + 1
                }
            };

            INewCommandInput command = new MockNewCommandInput();

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
                     command,
                     data,
                     columnPadding: 2,
                     headerSeparator: '-',
                     blankLineBetweenRows: false)
                 .DefineColumn(t => t.Item1, "Column 1", shrinkIfNeeded: true, minWidth: 2)
                 .DefineColumn(t => t.Item2, "Column 2");

            string result = formatter.Layout();
            Assert.Equal(expectedOutput, result);
        }

        [Fact]
        public void CanShrinkMultipleColumnsAndBalanceShrinking()
        {
            IEngineEnvironmentSettings environmentSettings = new MockEngineEnvironmentSettings()
            {
                Environment = new MockEnvironment()
                {
                    ConsoleBufferWidth = 6 + 2 + 6 + 1,
                }
            };

            INewCommandInput command = new MockNewCommandInput();

            IEnumerable<Tuple<string, string>> data = new List<Tuple<string, string>>()
            {
                new Tuple<string, string>("My test data", "My test data"),
                new Tuple<string, string>("My test data", "My test data")
            };

            string expectedOutput = $"Col...  Col...{Environment.NewLine}------  ------{Environment.NewLine}My ...  My ...{Environment.NewLine}My ...  My ...{Environment.NewLine}";

            HelpFormatter<Tuple<string, string>> formatter =
             HelpFormatter
                 .For(
                     environmentSettings,
                     command,
                     data,
                     columnPadding: 2,
                     headerSeparator: '-',
                     blankLineBetweenRows: false)
                 .DefineColumn(t => t.Item1, "Column 1", shrinkIfNeeded: true, minWidth: 2)
                 .DefineColumn(t => t.Item2, "Column 2", shrinkIfNeeded: true, minWidth: 2);

            string result = formatter.Layout();
            Assert.Equal(expectedOutput, result);
        }

        [Fact]
        public void CannotShrinkOverMinimumWidth()
        {
            IEngineEnvironmentSettings environmentSettings = new MockEngineEnvironmentSettings()
            {
                Environment = new MockEnvironment()
                {
                    ConsoleBufferWidth = 10, //less than need for data below
                }
            };

            INewCommandInput command = new MockNewCommandInput();

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
                     command,
                     data,
                     columnPadding: 2,
                     headerSeparator: '-',
                     blankLineBetweenRows: false)
                 .DefineColumn(t => t.Item1, "Column 1", shrinkIfNeeded: true, minWidth: 15)
                 .DefineColumn(t => t.Item2, "Column 2", shrinkIfNeeded: true, minWidth: 8);

            string result = formatter.Layout();
            Assert.Equal(expectedOutput, result);
        }

        [Fact]
        public void CanShowDefaultColumns()
        {
            IEngineEnvironmentSettings environmentSettings = new MockEngineEnvironmentSettings()
            {
                Environment = new MockEnvironment()
                {
                    ConsoleBufferWidth = 100
                }
            };

            INewCommandInput command = new MockNewCommandInput();

            IEnumerable<Tuple<string, string, string>> data = new List<Tuple<string, string, string>>()
            {
                new Tuple<string, string, string>("My test data", "My test data", "Column 3 data"),
                new Tuple<string, string, string>("My test data", "My test data", "Column 3 data")
            };

            string expectedOutput = $"Column 1      Column 2    {Environment.NewLine}------------  ------------{Environment.NewLine}My test data  My test data{Environment.NewLine}My test data  My test data{Environment.NewLine}";

            HelpFormatter<Tuple<string, string, string>> formatter =
             HelpFormatter
                 .For(
                     environmentSettings,
                     command,
                     data,
                     columnPadding: 2,
                     headerSeparator: '-',
                     blankLineBetweenRows: false)
                 .DefineColumn(t => t.Item1, "Column 1", showAlways: true)
                 .DefineColumn(t => t.Item2, "Column 2", columnName: "column2") //defaultColumn: true by default
                 .DefineColumn(t => t.Item3, "Column 3", columnName: "column3", defaultColumn: false);

            string result = formatter.Layout();
            Assert.Equal(expectedOutput, result);
        }

        [Fact]
        public void CanShowUserSelectedColumns()
        {
            IEngineEnvironmentSettings environmentSettings = new MockEngineEnvironmentSettings()
            {
                Environment = new MockEnvironment()
                {
                    ConsoleBufferWidth = 100
                }
            };

            INewCommandInput command = new MockNewCommandInput().WithCommandOption("--columns", "column3");

            IEnumerable<Tuple<string, string, string>> data = new List<Tuple<string, string, string>>()
            {
                new Tuple<string, string, string>("My test data", "My test data", "Column 3 data"),
                new Tuple<string, string, string>("My test data", "My test data", "Column 3 data")
            };

            string expectedOutput = $"Column 1      Column 3     {Environment.NewLine}------------  -------------{Environment.NewLine}My test data  Column 3 data{Environment.NewLine}My test data  Column 3 data{Environment.NewLine}";

            HelpFormatter<Tuple<string, string, string>> formatter =
             HelpFormatter
                 .For(
                     environmentSettings,
                     command,
                     data,
                     columnPadding: 2,
                     headerSeparator: '-',
                     blankLineBetweenRows: false)
                 .DefineColumn(t => t.Item1, "Column 1", showAlways: true)
                 .DefineColumn(t => t.Item2, "Column 2", columnName: "column2") //defaultColumn: true by default
                 .DefineColumn(t => t.Item3, "Column 3", columnName: "column3", defaultColumn: false);

            string result = formatter.Layout();
            Assert.Equal(expectedOutput, result);
        }

        [Fact]
        public void CanShowAllColumns()
        {
            IEngineEnvironmentSettings environmentSettings = new MockEngineEnvironmentSettings()
            {
                Environment = new MockEnvironment()
                {
                    ConsoleBufferWidth = 100
                }
            };

            INewCommandInput command = new MockNewCommandInput().WithCommandOption("--columns-all");

            IEnumerable<Tuple<string, string, string>> data = new List<Tuple<string, string, string>>()
            {
                new Tuple<string, string, string>("Column 1 data", "Column 2 data", "Column 3 data"),
                new Tuple<string, string, string>("Column 1 data", "Column 2 data", "Column 3 data")
            };

            string expectedOutput = $"Column 1       Column 2       Column 3     {Environment.NewLine}-------------  -------------  -------------{Environment.NewLine}Column 1 data  Column 2 data  Column 3 data{Environment.NewLine}Column 1 data  Column 2 data  Column 3 data{Environment.NewLine}";

            HelpFormatter<Tuple<string, string, string>> formatter =
             HelpFormatter
                 .For(
                     environmentSettings,
                     command,
                     data,
                     columnPadding: 2,
                     headerSeparator: '-',
                     blankLineBetweenRows: false)
                 .DefineColumn(t => t.Item1, "Column 1", showAlways: true)
                 .DefineColumn(t => t.Item2, "Column 2", columnName: "column2") //defaultColumn: true by default
                 .DefineColumn(t => t.Item3, "Column 3", columnName: "column3", defaultColumn: false);

            string result = formatter.Layout();
            Assert.Equal(expectedOutput, result);
        }

        [Fact]
        public void CanRightAlign()
        {
            IEngineEnvironmentSettings environmentSettings = new MockEngineEnvironmentSettings()
            {
                Environment = new MockEnvironment()
                {
                    ConsoleBufferWidth = 10, //less than need for data below
                }
            };

            INewCommandInput command = new MockNewCommandInput();

            IEnumerable<Tuple<string, string>> data = new List<Tuple<string, string>>()
            {
                new Tuple<string, string>("Monday", "Wednesday"),
                new Tuple<string, string>("Tuesday", "Sunday")
            };

            string expectedOutput = $"Column 1   Column 2{Environment.NewLine}--------  ---------{Environment.NewLine}Monday    Wednesday{Environment.NewLine}Tuesday      Sunday{Environment.NewLine}";

            HelpFormatter<Tuple<string, string>> formatter =
             HelpFormatter
                 .For(
                     environmentSettings,
                     command,
                     data,
                     columnPadding: 2,
                     headerSeparator: '-',
                     blankLineBetweenRows: false)
                 .DefineColumn(t => t.Item1, "Column 1")
                 .DefineColumn(t => t.Item2, "Column 2", rightAlign: true);

            string result = formatter.Layout();
            Assert.Equal(expectedOutput, result);
        }

        [Fact]
        public void CanCalculateWidthCorrectly()
        {
            IEngineEnvironmentSettings environmentSettings = new MockEngineEnvironmentSettings()
            {
                Environment = new MockEnvironment()
                {
                    ConsoleBufferWidth = 100, 
                }
            };

            INewCommandInput command = new MockNewCommandInput();

            IEnumerable<Tuple<string, string>> data = new List<Tuple<string, string>>()
            {
                new Tuple<string, string>("dotnet gitignore 文件", "gitignore"),
                new Tuple<string, string>("Dotnet 本地工具清单文件", "tool-manifest"),
                new Tuple<string, string>("控制台应用程序", "console"),
                new Tuple<string, string>("类库", "classlib"),
            };

            string expectedOutput =
@"模板名                   短名称       
-----------------------  -------------
dotnet gitignore 文件    gitignore    
Dotnet 本地工具清单文件  tool-manifest
控制台应用程序           console      
类库                     classlib     
";

            HelpFormatter<Tuple<string, string>> formatter =
             HelpFormatter
                 .For(
                     environmentSettings,
                     command,
                     data,
                     columnPadding: 2,
                     headerSeparator: '-',
                     blankLineBetweenRows: false)
                 .DefineColumn(t => t.Item1, "模板名")
                 .DefineColumn(t => t.Item2, "短名称");

            string result = formatter.Layout();
            Assert.Equal(expectedOutput, result);
        }

        [Fact]
        public void CanShrinkWideCharsCorrectly()
        {
            IEngineEnvironmentSettings environmentSettings = new MockEngineEnvironmentSettings()
            {
                Environment = new MockEnvironment()
                {
                    ConsoleBufferWidth = 30,
                }
            };

            INewCommandInput command = new MockNewCommandInput();

            IEnumerable<Tuple<string, string>> data = new List<Tuple<string, string>>()
            {
                new Tuple<string, string>("dotnet gitignore 文件", "gitignore"),
                new Tuple<string, string>("Dotnet 本地工具清单文件", "tool-manifest"),
                new Tuple<string, string>("控制台应用程序", "console"),
                new Tuple<string, string>("类库", "classlib"),
            };

            string expectedOutput =
@"模板名          短名称       
--------------  -------------
dotnet giti...  gitignore    
Dotnet 本地...  tool-manifest
控制台应用程序  console      
类库            classlib     
";

            HelpFormatter<Tuple<string, string>> formatter =
             HelpFormatter
                 .For(
                     environmentSettings,
                     command,
                     data,
                     columnPadding: 2,
                     headerSeparator: '-',
                     blankLineBetweenRows: false)
                 .DefineColumn(t => t.Item1, "模板名", shrinkIfNeeded: true)
                 .DefineColumn(t => t.Item2, "短名称");

            string result = formatter.Layout();
            Assert.Equal(expectedOutput, result);
        }
    }
}

