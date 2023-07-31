// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine.Completions;
using System.Reflection;
using Microsoft.TemplateEngine.Cli.Commands;
using Microsoft.TemplateEngine.Cli.TabularOutput;
using Microsoft.TemplateEngine.Mocks;

namespace Microsoft.TemplateEngine.Cli.UnitTests
{
    public class TabularOutputTests
    {
        [Fact]
        public void CanShrinkOneColumn()
        {
            TabularOutputSettings outputSettings = new(
                new MockEnvironment()
                {
                    ConsoleBufferWidth = 6 + 2 + 12 + 1
                });

            IEnumerable<Tuple<string, string>> data = new List<Tuple<string, string>>()
            {
                new Tuple<string, string>("My test data", "My test data"),
                new Tuple<string, string>("My test data", "My test data")
            };

            string expectedOutput = $"Col...  Column 2    {Environment.NewLine}------  ------------{Environment.NewLine}My ...  My test data{Environment.NewLine}My ...  My test data{Environment.NewLine}";

            TabularOutput<Tuple<string, string>> formatter =
             TabularOutput.TabularOutput
                 .For(outputSettings, data)
                 .DefineColumn(t => t.Item1, "Column 1", shrinkIfNeeded: true, minWidth: 2)
                 .DefineColumn(t => t.Item2, "Column 2");

            string result = formatter.Layout();
            Assert.Equal(expectedOutput, result);
        }

        [Fact]
        public void CanShrinkMultipleColumnsAndBalanceShrinking()
        {
            TabularOutputSettings outputSettings = new(
                new MockEnvironment()
                {
                    ConsoleBufferWidth = 6 + 2 + 6 + 1,
                });

            IEnumerable<Tuple<string, string>> data = new List<Tuple<string, string>>()
            {
                new Tuple<string, string>("My test data", "My test data"),
                new Tuple<string, string>("My test data", "My test data")
            };

            string expectedOutput = $"Col...  Col...{Environment.NewLine}------  ------{Environment.NewLine}My ...  My ...{Environment.NewLine}My ...  My ...{Environment.NewLine}";

            TabularOutput<Tuple<string, string>> formatter =
             TabularOutput.TabularOutput
                 .For(outputSettings, data)
                 .DefineColumn(t => t.Item1, "Column 1", shrinkIfNeeded: true, minWidth: 2)
                 .DefineColumn(t => t.Item2, "Column 2", shrinkIfNeeded: true, minWidth: 2);

            string result = formatter.Layout();
            Assert.Equal(expectedOutput, result);
        }

        [Fact]
        public void CannotShrinkOverMinimumWidth()
        {
            TabularOutputSettings outputSettings = new(
                 new MockEnvironment()
                 {
                     ConsoleBufferWidth = 10, //less than need for data below
                 });

            IEnumerable<Tuple<string, string>> data = new List<Tuple<string, string>>()
            {
                new Tuple<string, string>("My test data", "My test data"),
                new Tuple<string, string>("My test data", "My test data")
            };

            string expectedOutput = $"Column 1      Column 2   {Environment.NewLine}------------  -----------{Environment.NewLine}My test data  My test ...{Environment.NewLine}My test data  My test ...{Environment.NewLine}";

            TabularOutput<Tuple<string, string>> formatter =
             TabularOutput.TabularOutput
                 .For(outputSettings, data)
                 .DefineColumn(t => t.Item1, "Column 1", shrinkIfNeeded: true, minWidth: 15)
                 .DefineColumn(t => t.Item2, "Column 2", shrinkIfNeeded: true, minWidth: 8);

            string result = formatter.Layout();
            Assert.Equal(expectedOutput, result);
        }

        [Fact]
        public void CanShowDefaultColumns()
        {
            TabularOutputSettings outputSettings = new(
                       new MockEnvironment()
                       {
                           ConsoleBufferWidth = 100
                       });

            IEnumerable<Tuple<string, string, string>> data = new List<Tuple<string, string, string>>()
            {
                new Tuple<string, string, string>("My test data", "My test data", "Column 3 data"),
                new Tuple<string, string, string>("My test data", "My test data", "Column 3 data")
            };

            string expectedOutput = $"Column 1      Column 2    {Environment.NewLine}------------  ------------{Environment.NewLine}My test data  My test data{Environment.NewLine}My test data  My test data{Environment.NewLine}";

            TabularOutput<Tuple<string, string, string>> formatter =
             TabularOutput.TabularOutput
                 .For(outputSettings, data)
                 .DefineColumn(t => t.Item1, "Column 1", showAlways: true)
                 .DefineColumn(t => t.Item2, "Column 2", columnName: "column2") //defaultColumn: true by default
                 .DefineColumn(t => t.Item3, "Column 3", columnName: "column3", defaultColumn: false);

            string result = formatter.Layout();
            Assert.Equal(expectedOutput, result);
        }

        [Fact]
        public void CanShowUserSelectedColumns()
        {
            TabularOutputSettings outputSettings = new(
                        new MockEnvironment()
                        {
                            ConsoleBufferWidth = 100
                        },
                        columnsToDisplay: new[] { "column3" });

            IEnumerable<Tuple<string, string, string>> data = new List<Tuple<string, string, string>>()
            {
                new Tuple<string, string, string>("My test data", "My test data", "Column 3 data"),
                new Tuple<string, string, string>("My test data", "My test data", "Column 3 data")
            };

            string expectedOutput = $"Column 1      Column 3     {Environment.NewLine}------------  -------------{Environment.NewLine}My test data  Column 3 data{Environment.NewLine}My test data  Column 3 data{Environment.NewLine}";

            TabularOutput<Tuple<string, string, string>> formatter =
             TabularOutput.TabularOutput
                 .For(outputSettings, data)
                 .DefineColumn(t => t.Item1, "Column 1", showAlways: true)
                 .DefineColumn(t => t.Item2, "Column 2", columnName: "column2") //defaultColumn: true by default
                 .DefineColumn(t => t.Item3, "Column 3", columnName: "column3", defaultColumn: false);

            string result = formatter.Layout();
            Assert.Equal(expectedOutput, result);
        }

        [Fact]
        public void CanShowAllColumns()
        {
            TabularOutputSettings outputSettings = new(new MockEnvironment() { ConsoleBufferWidth = 10 }, displayAllColumns: true);

            IEnumerable<Tuple<string, string, string>> data = new List<Tuple<string, string, string>>()
            {
                new Tuple<string, string, string>("Column 1 data", "Column 2 data", "Column 3 data"),
                new Tuple<string, string, string>("Column 1 data", "Column 2 data", "Column 3 data")
            };

            string expectedOutput = $"Column 1       Column 2       Column 3     {Environment.NewLine}-------------  -------------  -------------{Environment.NewLine}Column 1 data  Column 2 data  Column 3 data{Environment.NewLine}Column 1 data  Column 2 data  Column 3 data{Environment.NewLine}";

            TabularOutput<Tuple<string, string, string>> formatter =
             TabularOutput.TabularOutput
                 .For(outputSettings, data)
                 .DefineColumn(t => t.Item1, "Column 1", showAlways: true)
                 .DefineColumn(t => t.Item2, "Column 2", columnName: "column2") //defaultColumn: true by default
                 .DefineColumn(t => t.Item3, "Column 3", columnName: "column3", defaultColumn: false);

            string result = formatter.Layout();
            Assert.Equal(expectedOutput, result);
        }

        [Fact]
        public void CanCenterAlign()
        {
            TabularOutputSettings outputSettings = new(new MockEnvironment() { ConsoleBufferWidth = 10 });

            IEnumerable<Tuple<string, string>> data = new List<Tuple<string, string>>()
            {
                new Tuple<string, string>("Monday", "Wednesday"),
                new Tuple<string, string>("Tuesday", "Sunday")
            };

            string expectedOutput = $"Column 1   Column 2{Environment.NewLine}--------  ---------{Environment.NewLine}Monday    Wednesday{Environment.NewLine}Tuesday     Sunday {Environment.NewLine}";

            TabularOutput<Tuple<string, string>> formatter =
             TabularOutput.TabularOutput
                 .For(outputSettings, data)
                 .DefineColumn(t => t.Item1, "Column 1")
                 .DefineColumn(t => t.Item2, "Column 2", textAlign: TextAlign.Center);

            string result = formatter.Layout();
            Assert.Equal(expectedOutput, result);
        }

        [Fact]
        public void CanRightAlign()
        {
            TabularOutputSettings outputSettings = new(new MockEnvironment() { ConsoleBufferWidth = 10 });

            IEnumerable<Tuple<string, string>> data = new List<Tuple<string, string>>()
            {
                new Tuple<string, string>("Monday", "Wednesday"),
                new Tuple<string, string>("Tuesday", "Sunday")
            };

            string expectedOutput = $"Column 1   Column 2{Environment.NewLine}--------  ---------{Environment.NewLine}Monday    Wednesday{Environment.NewLine}Tuesday      Sunday{Environment.NewLine}";

            TabularOutput<Tuple<string, string>> formatter =
            TabularOutput.TabularOutput
                .For(outputSettings, data)
                .DefineColumn(t => t.Item1, "Column 1")
                .DefineColumn(t => t.Item2, "Column 2", textAlign: TextAlign.Right);

            string result = formatter.Layout();
            Assert.Equal(expectedOutput, result);
        }

        [Fact]
        public void CanCalculateWidthCorrectly()
        {
            TabularOutputSettings outputSettings = new(
                            new MockEnvironment()
                            {
                                ConsoleBufferWidth = 100
                            });

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

            TabularOutput<Tuple<string, string>> formatter =
             TabularOutput.TabularOutput
                 .For(
                     outputSettings,
                     data)
                 .DefineColumn(t => t.Item1, "模板名")
                 .DefineColumn(t => t.Item2, "短名称");

            string result = formatter.Layout();
            Assert.Equal(expectedOutput, result);
        }

        [Fact]
        public void CanShrinkWideCharsCorrectly()
        {
            TabularOutputSettings outputSettings = new(
                            new MockEnvironment()
                            {
                                ConsoleBufferWidth = 30
                            });

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

            TabularOutput<Tuple<string, string>> formatter =
             TabularOutput.TabularOutput
                 .For(
                     outputSettings,
                     data)
                 .DefineColumn(t => t.Item1, "模板名", shrinkIfNeeded: true)
                 .DefineColumn(t => t.Item2, "短名称");

            string result = formatter.Layout();
            Assert.Equal(expectedOutput, result);
        }

        [Fact]
        public void CanIndentAllRows()
        {
            TabularOutputSettings outputSettings = new(new MockEnvironment() { ConsoleBufferWidth = 10 }, displayAllColumns: true);

            IEnumerable<Tuple<string, string, string>> data = new List<Tuple<string, string, string>>()
            {
                new Tuple<string, string, string>("Column 1 data", "Column 2 data", "Column 3 data"),
                new Tuple<string, string, string>("Column 1 data", "Column 2 data", "Column 3 data")
            };

            string expectedOutput = $"   Column 1       Column 2       Column 3     {Environment.NewLine}   -------------  -------------  -------------{Environment.NewLine}   Column 1 data  Column 2 data  Column 3 data{Environment.NewLine}   Column 1 data  Column 2 data  Column 3 data{Environment.NewLine}   ";

            TabularOutput<Tuple<string, string, string>> formatter =
             TabularOutput.TabularOutput
                 .For(outputSettings, data)
                 .DefineColumn(t => t.Item1, "Column 1", showAlways: true)
                 .DefineColumn(t => t.Item2, "Column 2", columnName: "column2") //defaultColumn: true by default
                 .DefineColumn(t => t.Item3, "Column 3", columnName: "column3", defaultColumn: false);

            string result = formatter.Layout(1);
            Assert.Equal(expectedOutput, result);
        }

        [Fact]
        public void VerifyColumnsOptionHasAllColumnNamesDefined()
        {
            var columnOption = SharedOptionsFactory.CreateColumnsOption();

            //Gets suggestions defined in column options
            List<string?> suggestedValues = columnOption.GetCompletions(CompletionContext.Empty).Select(c => c.Label).ToList<string?>();
            suggestedValues.Sort();

            //Gets constants defined in TabularOutputSettings.ColumnNams
            List<string?> columnNamesConstants = (typeof(TabularOutputSettings.ColumnNames))
                .GetFields(BindingFlags.NonPublic | BindingFlags.Static)
                .Where(fi => fi.IsLiteral && !fi.IsInitOnly)
                .Select(fi => (string?)fi.GetValue(null))
                .ToList();
            columnNamesConstants.Sort();

            Assert.Equal(suggestedValues, columnNamesConstants);
        }
    }
}

