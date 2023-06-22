// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Parsing;
using FakeItEasy;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Cli.Commands;
using Microsoft.TemplateEngine.Cli.PostActionProcessors;
using Microsoft.TemplateEngine.Edge;
using Microsoft.TemplateEngine.Edge.Settings;
using Microsoft.TemplateEngine.Mocks;
using Microsoft.TemplateEngine.TestHelper;

namespace Microsoft.TemplateEngine.Cli.UnitTests.ParserTests
{
    public partial class InstantiateTests : BaseTest
    {
        [Fact]
        public void Instantiate_CanParseTemplateWithOptions()
        {
            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(RepoTemplatePackages));
            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host);
            ParseResult parseResult = myCommand.Parse("new console --framework net5.0");
            InstantiateCommandArgs args = InstantiateCommandArgs.FromNewCommandArgs(new NewCommandArgs(myCommand, parseResult));

            Assert.Equal("console", args.ShortName);
            Assert.Contains("--framework", args.RemainingArguments);
            Assert.Contains("net5.0", args.RemainingArguments);
        }

        private readonly IReadOnlyDictionary<string, IReadOnlyList<MockTemplateInfo>> _testSets = new Dictionary<string, IReadOnlyList<MockTemplateInfo>>()
        {
            {
                "BasicSet2Templates",
                new MockTemplateInfo[]
                {
                    new MockTemplateInfo("Template", identity: "Template1", groupIdentity: "Group", precedence: 100),
                    new MockTemplateInfo("Template", identity: "Template2", groupIdentity: "Group", precedence: 200)
                }
            },
            {
                "2TemplatesWithDifferentShortName",
                new MockTemplateInfo[]
                {
                    new MockTemplateInfo("ShortName1", identity: "Template1", groupIdentity: "Group", precedence: 100),
                    new MockTemplateInfo("ShortName2", identity: "Template2", groupIdentity: "Group", precedence: 200)
                }
            },
            {
                "2TemplatesWithDifferentShortNameSamePrecedence",
                new MockTemplateInfo[]
                {
                    new MockTemplateInfo("ShortName1", identity: "Template1", groupIdentity: "Group"),
                    new MockTemplateInfo("ShortName2", identity: "Template2", groupIdentity: "Group")
                }
            },
            {
                "MultipleTemplatesWithChoiceParameter",
                new MockTemplateInfo[]
                {
                    new MockTemplateInfo("foo", name: "Foo template", identity: "foo.test_1", groupIdentity: "foo.test.template", precedence: 100)
                                    .WithChoiceParameter("MyChoice", "value_1"),
                    new MockTemplateInfo("foo", name: "Foo template", identity: "foo.test_2", groupIdentity: "foo.test.template", precedence: 200)
                                    .WithChoiceParameter("MyChoice", "value_2", "value_3")
                }
            },
            {
                "MultipleTemplatesWithMultipleChoiceParameters",
                new MockTemplateInfo[]
                {
                    new MockTemplateInfo("foo", name: "Foo template", identity: "foo.test_1", groupIdentity: "foo.test.template", precedence: 100)
                                    .WithChoiceParameter("MyChoice", "value", "other_value")
                                    .WithChoiceParameter("OtherChoice", "foo"),
                    new MockTemplateInfo("foo", name: "Foo template", identity: "foo.test_2", groupIdentity: "foo.test.template", precedence: 200)
                                    .WithChoiceParameter("MyChoice", "value")
                                    .WithChoiceParameter("OtherChoice", "foo", "bar_1")
                }
            },
            {
                "SingleFSharpTemplate",
                new MockTemplateInfo[]
                {
                    new MockTemplateInfo("foo", name: "Foo template", identity: "foo.test_1", groupIdentity: "foo.test.template", precedence: 100)
                                    .WithTag("language", "F#")
                }
            },
            {
                "FSharpVBTemplatesWithDifferentPrecedence",
                new MockTemplateInfo[]
                {
                    new MockTemplateInfo("foo", name: "Foo template", identity: "foo.test_1.FSharp", groupIdentity: "foo.test.template", precedence: 100)
                                     .WithTag("language", "F#"),
                    new MockTemplateInfo("foo", name: "Foo template", identity: "foo.test_1.VB", groupIdentity: "foo.test.template", precedence: 200)
                                     .WithTag("language", "VB")
                }
            },
            {
                "FSharpVBTemplatesWithSamePrecedence",
                new MockTemplateInfo[]
                {
                    new MockTemplateInfo("foo", name: "Foo template", identity: "foo.test_1.FSharp", groupIdentity: "foo.test.template", precedence: 100)
                                     .WithTag("language", "F#"),
                    new MockTemplateInfo("foo", name: "Foo template", identity: "foo.test_1.VB", groupIdentity: "foo.test.template", precedence: 100)
                                     .WithTag("language", "VB")
                }
            },
            {
                "SinglePerlTemplate",
                new MockTemplateInfo[]
                {
                    new MockTemplateInfo("foo", identity: "foo.Perl", groupIdentity: "foo.group").WithTag("language", "Perl"),
                }
            },
            {
                "LispPerlTemplates",
                new MockTemplateInfo[]
                {
                    new MockTemplateInfo("foo", identity: "foo.Perl", groupIdentity: "foo.group").WithTag("language", "Perl"),
                    new MockTemplateInfo("foo", identity: "foo.Lisp", groupIdentity: "foo.group").WithTag("language", "LISP")
                }
            },
            {
                "2TemplatesWithDifferentParameters",
                new MockTemplateInfo[]
                {
                    new MockTemplateInfo("foo", identity: "foo.bar", groupIdentity: "foo.group").WithParameters("bar"),
                    new MockTemplateInfo("foo", identity: "foo.baz", groupIdentity: "foo.group").WithParameters("baz"),
                }
            },
            {
                "2TemplatesWithDifferentChoiceOptions",
                new MockTemplateInfo[]
                {
                    new MockTemplateInfo("foo", identity: "foo.1", groupIdentity: "foo.group").WithChoiceParameter("framework", "netcoreapp2.1", "netcoreapp3.1"),
                    new MockTemplateInfo("foo", identity: "foo.2", groupIdentity: "foo.group").WithChoiceParameter("framework", "net5.0"),
                }
            },
            {
                "MultiShortNameGroup",
                new MockTemplateInfo[]
                {
                     new MockTemplateInfo(new string[] { "aaa", "bbb" }, name: "High precedence C# in group", precedence: 2000, identity: "MultiName.Test.High.CSharp", groupIdentity: "MultiName.Test")
                            .WithTag("language", "C#")
                            .WithChoiceParameter("foo", "A", "W")
                            .WithParameters("HighC"),
                     new MockTemplateInfo(new string[] { "ccc", "ddd", "eee" }, name: "Low precedence C# in group", precedence: 100, identity: "MultiName.Test.Low.CSharp", groupIdentity: "MultiName.Test")
                            .WithTag("language", "C#")
                            .WithChoiceParameter("foo", "A", "X")
                            .WithParameters("LowC"),
                     new MockTemplateInfo(new string[] { "fff" }, name: "Only F# in group", precedence: 100, identity: "Multiname.Test.Only.FSharp", groupIdentity: "MultiName.Test")
                           .WithTag("language", "F#")
                           .WithChoiceParameter("foo", "A", "Y")
                           .WithParameters("OnlyF"),
                }
            }
        };

        //extracted to data to reuse in other tests
        public static IEnumerable<object?[]> CanEvaluateTemplateToRunData =>
            new object?[][]
            {
                //invalid choice parameter value
                new[] { "foo --MyChoice value", "MultipleTemplatesWithChoiceParameter", null, null },
                //higher precedence template is preferred
                new[] { "foo --MyChoice value --OtherChoice foo", "MultipleTemplatesWithMultipleChoiceParameters", null, "foo.test_2" },
               //in case there is only one template  in the group, language mismatch is ignored
                new[] { "foo", "SingleFSharpTemplate", null, "foo.test_1" },
                //in case there is multiple templates in the group of different languages, the higher precedence template is preferred
                new[] { "foo", "FSharpVBTemplatesWithDifferentPrecedence", null, "foo.test_1.VB" },
                //in case there is multiple templates in the group of different languages with same language, both are selected
                new[] { "foo", "FSharpVBTemplatesWithSamePrecedence", null, "foo.test_1.VB|foo.test_1.FSharp" },
                new[] { "foo", "FSharpVBTemplatesWithSamePrecedence", "C#", "foo.test_1.VB|foo.test_1.FSharp" },
                new[] { "foo", "FSharpVBTemplatesWithSamePrecedence", "VB", "foo.test_1.VB" },
                new[] { "foo", "FSharpVBTemplatesWithSamePrecedence", "F#", "foo.test_1.FSharp" },
                //in case there is multiple templates in the group, higher precedence is selected
                new[] { "Template", "BasicSet2Templates", null, "Template2" },
                //in case there is group has multiple shortnames, any name can be used, and still higher precedence is selected
                new[] { "ShortName1", "2TemplatesWithDifferentShortName", null, "Template2" },
                new[] { "ShortName2", "2TemplatesWithDifferentShortName", null, "Template2" },
                //in case there is group has multiple shortnames but same preference, any name can be used, and both templates are selected
                new[] { "ShortName1", "2TemplatesWithDifferentShortNameSamePrecedence", null, "Template1|Template2" },
                new[] { "ShortName2", "2TemplatesWithDifferentShortNameSamePrecedence", null, "Template1|Template2" },
                //cases for single template in group vs default language
                new[] { "foo", "SinglePerlTemplate", "Perl", "foo.Perl" },
                new[] { "foo", "SinglePerlTemplate", null, "foo.Perl" },
                new[] { "foo --language Perl", "SinglePerlTemplate", "Perl", "foo.Perl" },
                new[] { "foo --language Perl", "SinglePerlTemplate", null, "foo.Perl" },
                new[] { "foo", "SinglePerlTemplate", "C#", "foo.Perl" },
                new[] { "foo --language Perl", "SinglePerlTemplate", "C#", "foo.Perl" },
                new[] { "foo --language C#", "SinglePerlTemplate", "C#", null },
                //cases for multiple languages templates in group vs default language
                new[] { "foo", "LispPerlTemplates", "Perl", "foo.Perl" },
                new[] { "foo", "LispPerlTemplates", null, "foo.Perl|foo.Lisp" },
                new[] { "foo --language LISP", "LispPerlTemplates", "Perl", "foo.Lisp" },
                new[] { "foo --language lisp", "LispPerlTemplates", "Perl", "foo.Lisp" },      //argument case doesn't matter
                //cases for non-choice parameters: same precedence but different parameters templates
                new[] { "foo --baz whatever", "2TemplatesWithDifferentParameters", null, "foo.baz" },
                new[] { "foo --bar whatever", "2TemplatesWithDifferentParameters", null, "foo.bar" },
                new[] { "foo --bat whatever", "2TemplatesWithDifferentParameters", null, null },
                //cases for choice parameters: same precedence but different choice templates (same parameter)
                new[] { "foo --framework net5.0", "2TemplatesWithDifferentChoiceOptions", null, "foo.2" },
                new[] { "foo --framework NET5.0", "2TemplatesWithDifferentChoiceOptions", null, "foo.2" },     //argument case doesn't matter
                new[] { "foo --framework netcoreapp2.1", "2TemplatesWithDifferentChoiceOptions", null, "foo.1" },
                new[] { "foo --framework netcoreapp2.0", "2TemplatesWithDifferentChoiceOptions", null, null },
            };

        [Theory]
#pragma warning disable CA1825 // Avoid zero-length array allocations. https://github.com/dotnet/sdk/issues/28672
        [MemberData(nameof(CanEvaluateTemplateToRunData))]
#pragma warning restore CA1825 // Avoid zero-length array allocations.
        internal void CanEvaluateTemplateToRun(string command, string templateSet, string? defaultLanguage, string? expectedIdentitiesStr)
        {
            TemplateGroup templateGroup = TemplateGroup.FromTemplateList(
                CliTemplateInfo.FromTemplateInfo(_testSets[templateSet], A.Fake<IHostSpecificDataLoader>()))
                .Single();

            string[] expectedIdentities = expectedIdentitiesStr?.Split("|") ?? Array.Empty<string>();

            var defaultParams = new Dictionary<string, string>();
            if (defaultLanguage != null)
            {
                defaultParams["prefs:language"] = defaultLanguage;
            }

            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(defaultParameters: defaultParams);
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);

            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host);
            ParseResult parseResult = myCommand.Parse($" new {command}");
            InstantiateCommandArgs args = InstantiateCommandArgs.FromNewCommandArgs(new NewCommandArgs(myCommand, parseResult));
            HashSet<TemplateCommand> templateCommands = InstantiateCommand.GetTemplateCommand(args, settings, A.Fake<TemplatePackageManager>(), templateGroup);
            Assert.Equal(expectedIdentities.Length, templateCommands.Count);
            Assert.Equal(expectedIdentities.OrderBy(s => s), templateCommands.Select(templateCommand => templateCommand.Template.Identity).OrderBy(s => s));
        }

        [Theory]
        [InlineData("new foo --name name", "name")]
        [InlineData("new foo -n name", "name")]
        [InlineData("new foo", null)]
        [InlineData("new --name name foo ", "name")]
        [InlineData("new -n name foo", "name")]
        internal void CanParseNameOption(string command, string? expectedValue)
        {
            MockTemplateInfo template = new("foo", identity: "foo.1", groupIdentity: "foo.group");

            TemplateGroup templateGroup = TemplateGroup.FromTemplateList(
                CliTemplateInfo.FromTemplateInfo(new[] { template }, A.Fake<IHostSpecificDataLoader>()))
                .Single();

            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost();
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);
            TemplatePackageManager packageManager = A.Fake<TemplatePackageManager>();

            RootCommand rootCommand = new();
            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host);
            rootCommand.AddCommand(myCommand);
            ParseResult parseResult = rootCommand.Parse(command);
            InstantiateCommandArgs args = InstantiateCommandArgs.FromNewCommandArgs(new NewCommandArgs(myCommand, parseResult));
            TemplateCommand templateCommand = new(myCommand, settings, packageManager, templateGroup, templateGroup.Templates.Single());
            Parser parser = ParserFactory.CreateParser(templateCommand);
            ParseResult templateParseResult = parser.Parse(args.TokensToInvoke ?? Array.Empty<string>());
            var templateArgs = new TemplateCommandArgs(templateCommand, myCommand, templateParseResult);

            Assert.Equal(expectedValue, templateArgs.Name);
        }

        public static IEnumerable<object?[]> CanParseTemplateOptionsData =>
            new object?[][]
            {
                //bool
                new[] { "foo --bool", "bool", "bool", null, null, "True" },
                new[] { "foo -b", "bool", "bool", null, null, "True" },
                new[] { "foo --bool true", "bool", "bool", null, null, "True" },
                new[] { "foo -b true", "bool", "bool", null, null, "True" },
                new[] { "foo --bool false", "bool", "bool", null, null, "False" },
                new[] { "foo -b false", "bool", "bool", null, null, "False" },
                //the default values are ignored when creating args - they are processed by template engine core
                new[] { "foo", "bool", "bool", null, null, null },
                new[] { "foo", "bool", "bool", "true", null, null },
                new[] { "foo", "bool", "bool", "false", null, null },
                //text
                new[] { "foo --text val", "text", "string", null, null, "val" },
                new[] { "foo -t val", "text", "string", null, null, "val" },
                new[] { "foo --text val", "text", "text", null, null, "val" },
                new[] { "foo", "text", "text", "def", null, null },
                new[] { "foo --text", "text", "text", null, "defIfNoOpValue", "defIfNoOpValue" },
                //int
                new[] { "foo --int 30", "int", "int", null, null, "30" },
                new[] { "foo --int 30", "int", "integer", null, null, "30" },
                new[] { "foo -in 30", "int", "integer", null, null, "30" }, //-i is already defined for legacy install command
                new[] { "foo", "int", "integer", "50", null, null },
                new[] { "foo --int", "int", "int", null, "550", "550" },
                //float
                new[] { "foo --float 30.9", "float", "float", null, null, "30.9" },
                new[] { "foo -f 30.9", "float", "float", null, null, "30.9" },
                new[] { "foo", "float", "float", "50.9", null, null },
                new[] { "foo --float", "float", "float", null, "5.501", "5.501" },

                //hex
                new[] { "foo --hex 0xABCDEF", "hex", "hex", null, null, "0xABCDEF" },
                new[] { "foo -he 0xABCDEF", "hex", "hex", null, null, "0xABCDEF" }, //-h is already defined for help
                new[] { "foo", "hex", "hex", "0xABCDE", null, null },
                new[] { "foo --hex", "hex", "hex", null, "0xABCD", "0xABCD" },
            };

        [Theory]
#pragma warning disable CA1825 // Avoid zero-length array allocations. https://github.com/dotnet/sdk/issues/28672
        [MemberData(nameof(CanParseTemplateOptionsData))]
#pragma warning restore CA1825 // Avoid zero-length array allocations.
        internal void CanParseTemplateOptions(string command, string parameterName, string parameterType, string? defaultValue, string? defaultIfNoOptionValue, string? expectedValue)
        {
            MockTemplateInfo template = new MockTemplateInfo("foo", identity: "foo.1", groupIdentity: "foo.group")
                .WithParameter(parameterName, parameterType, defaultValue: defaultValue, defaultIfNoOptionValue: defaultIfNoOptionValue);

            TemplateGroup templateGroup = TemplateGroup.FromTemplateList(
                CliTemplateInfo.FromTemplateInfo(new[] { template }, A.Fake<IHostSpecificDataLoader>()))
                .Single();

            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost();
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);
            TemplatePackageManager packageManager = A.Fake<TemplatePackageManager>();

            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host);
            Parser parser = ParserFactory.CreateParser(myCommand);
            ParseResult parseResult = parser.Parse($" new {command}");
            InstantiateCommandArgs args = InstantiateCommandArgs.FromNewCommandArgs(new NewCommandArgs(myCommand, parseResult));
            TemplateCommand templateCommand = new(myCommand, settings, packageManager, templateGroup, templateGroup.Templates.Single());
            Parser templateCommandParser = ParserFactory.CreateParser(templateCommand);
            ParseResult templateParseResult = templateCommandParser.Parse(args.RemainingArguments ?? Array.Empty<string>());
            var templateArgs = new TemplateCommandArgs(templateCommand, myCommand, templateParseResult);

            if (string.IsNullOrWhiteSpace(expectedValue))
            {
                Assert.False(templateArgs.TemplateParameters.ContainsKey(parameterName));
            }
            else
            {
                Assert.True(templateArgs.TemplateParameters.ContainsKey(parameterName));
                Assert.Equal(expectedValue, templateArgs.TemplateParameters[parameterName]);
            }
        }

        public static IEnumerable<object?[]> CanParseChoiceTemplateOptionsData =>
            new object?[][]
            {
                new[] { "foo --framework net5.0", "framework", "net5.0|net6.0", null, "net5.0" },
                new[] { "foo -f net5.0", "framework", "net5.0|net6.0", null, "net5.0" },
                new[] { "foo --framework net6.0", "framework", "net5.0|net6.0", null, "net6.0" },
                new[] { "foo --framework ", "framework", "net5.0|net6.0", "net6.0", "net6.0" },
            };

        [Theory]
#pragma warning disable CA1825 // Avoid zero-length array allocations. https://github.com/dotnet/sdk/issues/28672
        [MemberData(nameof(CanParseChoiceTemplateOptionsData))]
#pragma warning restore CA1825 // Avoid zero-length array allocations.
        internal void CanParseChoiceTemplateOptions(string command, string parameterName, string parameterValues, string? defaultIfNoOptionValue, string? expectedValue)
        {
            MockTemplateInfo template = new MockTemplateInfo("foo", identity: "foo.1", groupIdentity: "foo.group")
                .WithChoiceParameter(parameterName, parameterValues.Split("|"), defaultIfNoOptionValue: defaultIfNoOptionValue);

            TemplateGroup templateGroup = TemplateGroup.FromTemplateList(
                CliTemplateInfo.FromTemplateInfo(new[] { template }, A.Fake<IHostSpecificDataLoader>()))
                .Single();

            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost();
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);
            TemplatePackageManager packageManager = A.Fake<TemplatePackageManager>();

            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host);
            ParseResult parseResult = myCommand.Parse($" new {command}");
            InstantiateCommandArgs args = InstantiateCommandArgs.FromNewCommandArgs(new NewCommandArgs(myCommand, parseResult));
            TemplateCommand templateCommand = new(myCommand, settings, packageManager, templateGroup, templateGroup.Templates.Single());
            Parser parser = ParserFactory.CreateParser(templateCommand);
            ParseResult templateParseResult = parser.Parse(args.RemainingArguments ?? Array.Empty<string>());
            var templateArgs = new TemplateCommandArgs(templateCommand, myCommand, templateParseResult);

            if (string.IsNullOrWhiteSpace(expectedValue))
            {
                Assert.False(templateArgs.TemplateParameters.ContainsKey(parameterName));
            }
            else
            {
                Assert.True(templateArgs.TemplateParameters.ContainsKey(parameterName));
                Assert.Equal(expectedValue, templateArgs.TemplateParameters[parameterName]);
            }
        }

        public static IEnumerable<object?[]> CanParseMultiChoiceTemplateOptionsData =>
            new object?[][]
            {
                new[] { "foo --framework net5.0 --framework net7.0", "framework", "net5.0|net6.0|net7.0", null, "net5.0|net7.0" },
                new[] { "foo -f net5.0", "framework", "net5.0|net6.0|net7.0", null, "net5.0" },
                new[] { "foo -f net5.0 -f net7.0 -f net6.0", "framework", "net5.0|net6.0|net7.0", null, "net5.0|net7.0|net6.0" },
                new[] { "foo --framework net6.0", "framework", "net5.0|net6.0|net7.0", null, "net6.0" },
                new[] { "foo --framework ", "framework", "net5.0|net6.0|net7.0", "net5.0|net6.0", "net5.0|net6.0" },
            };

        [Theory]
#pragma warning disable CA1825 // Avoid zero-length array allocations. https://github.com/dotnet/sdk/issues/28672
        [MemberData(nameof(CanParseMultiChoiceTemplateOptionsData))]
#pragma warning restore CA1825 // Avoid zero-length array allocations.
        internal void CanParseMultiChoiceTemplateOptions(string command, string parameterName, string parameterValues, string? defaultIfNoOptionValue, string? expectedValue)
        {
            MockTemplateInfo template = new MockTemplateInfo("foo", identity: "foo.1", groupIdentity: "foo.group")
                .WithChoiceParameter(parameterName, parameterValues.Split("|"), defaultIfNoOptionValue: defaultIfNoOptionValue, allowMultipleValues: true);

            TemplateGroup templateGroup = TemplateGroup.FromTemplateList(
                CliTemplateInfo.FromTemplateInfo(new[] { template }, A.Fake<IHostSpecificDataLoader>()))
                .Single();

            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost();
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);
            TemplatePackageManager packageManager = A.Fake<TemplatePackageManager>();

            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host);
            ParseResult parseResult = myCommand.Parse($" new {command}");
            InstantiateCommandArgs args = InstantiateCommandArgs.FromNewCommandArgs(new NewCommandArgs(myCommand, parseResult));
            TemplateCommand templateCommand = new(myCommand, settings, packageManager, templateGroup, templateGroup.Templates.Single());
            Parser parser = ParserFactory.CreateParser(templateCommand);
            ParseResult templateParseResult = parser.Parse(args.RemainingArguments ?? Array.Empty<string>());
            var templateArgs = new TemplateCommandArgs(templateCommand, myCommand, templateParseResult);

            if (string.IsNullOrWhiteSpace(expectedValue))
            {
                Assert.False(templateArgs.TemplateParameters.ContainsKey(parameterName));
            }
            else
            {
                Assert.True(templateArgs.TemplateParameters.ContainsKey(parameterName));
                Assert.Equal(expectedValue, templateArgs.TemplateParameters[parameterName]);
            }
        }

        public static IEnumerable<object?[]> CanDetectParseErrorsTemplateOptionsData =>
            new object?[][]
            {
                //bool
                //new object?[] { "foo", "bool", "bool", true, null, null, "Option '--bool' is required." },
                new object?[] { "foo -b text", "bool", "bool", true, null, null, "Unrecognized command or argument 'text'." },
                new object?[] { "foo --bool 0", "bool", "bool", true, null, null, "Unrecognized command or argument '0'." },
                //text
                new object?[] { "foo --text", "text", "string", false, null, null, "Required argument missing for option: '--text'." },
                //new object?[] { "foo", "text", "string", true, null, null, "Option '--text' is required." },
                //int
                new object?[] { "foo --int text", "int", "int", false, null, null, "Cannot parse argument 'text' for option '--int' as expected type 'Int64'." },
                new object?[] { "foo --int", "int", "int", false, null, null, "Required argument missing for option: '--int'." },
                //new object?[] { "foo", "int", "int", true, null, null, "Option '--int' is required." },
                new object?[] { "foo --int", "int", "int", true, null, "not-int", "Cannot parse default if option without value 'not-int' for option '--int' as expected type 'Int64'." },
                //float
                new object?[] { "foo --float text", "float", "float", false, null, null, "Cannot parse argument 'text' for option '--float' as expected type 'Double'." },
                new object?[] { "foo --float", "float", "float", false, null, null, "Required argument missing for option: '--float'." },
                //new object?[] { "foo", "float", "float", true, null, null, "Option '--float' is required." },
                new object?[] { "foo --float", "float", "float", true, null, "not-float", "Cannot parse default if option without value 'not-float' for option '--float' as expected type 'Double'." },

                //hex
                new object?[] { "foo --hex text", "hex", "hex", false, null, null, "Cannot parse argument 'text' for option '--hex' as expected type 'Int64'." },
                new object?[] { "foo --hex", "hex", "hex", false, null, null, "Required argument missing for option: '--hex'." },
               // new object?[] { "foo", "hex", "hex", true, null, null, "Option '--hex' is required." },
                new object?[] { "foo --hex", "hex", "hex", true, null, "not-hex", "Cannot parse default if option without value 'not-hex' for option '--hex' as expected type 'Int64'." },

            };

        [Theory]
#pragma warning disable CA1825 // Avoid zero-length array allocations. https://github.com/dotnet/sdk/issues/28672
        [MemberData(nameof(CanDetectParseErrorsTemplateOptionsData))]
#pragma warning restore CA1825 // Avoid zero-length array allocations.
        internal void CanDetectParseErrorsTemplateOptions(
            string command,
            string parameterName,
            string parameterType,
            bool isRequired,
            string? defaultValue,
            string? defaultIfNoOptionValue,
            string expectedError)
        {
            MockTemplateInfo template = new MockTemplateInfo("foo", identity: "foo.1", groupIdentity: "foo.group")
                .WithParameter(parameterName, parameterType, isRequired, defaultValue: defaultValue, defaultIfNoOptionValue: defaultIfNoOptionValue);

            TemplateGroup templateGroup = TemplateGroup.FromTemplateList(
                CliTemplateInfo.FromTemplateInfo(new[] { template }, A.Fake<IHostSpecificDataLoader>()))
                .Single();

            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost();
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);
            TemplatePackageManager packageManager = A.Fake<TemplatePackageManager>();

            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host);
            ParseResult parseResult = myCommand.Parse($" new {command}");
            InstantiateCommandArgs args = InstantiateCommandArgs.FromNewCommandArgs(new NewCommandArgs(myCommand, parseResult));

            TemplateCommand templateCommand = new(myCommand, settings, packageManager, templateGroup, templateGroup.Templates.Single());
            Parser parser = ParserFactory.CreateParser(templateCommand);
            ParseResult templateParseResult = parser.Parse(args.RemainingArguments ?? Array.Empty<string>());
            Assert.True(templateParseResult.Errors.Any());
            Assert.Equal(expectedError, templateParseResult.Errors.Single().Message);
        }

        public static IEnumerable<object?[]> CanDetectParseErrorsChoiceTemplateOptionsData =>
            new object?[][]
            {
                new object?[] { "foo --framework netcoreapp3.1", "framework", "net5.0|net6.0", false, null, null, "Argument(s) 'netcoreapp3.1' are not recognized. Must be one of: 'net5.0', 'net6.0'." },
                //https://github.com/dotnet/command-line-api/issues/1609
                //new object?[] { "foo --framework", "framework", "net5.0|net6.0", false, null, null, "Required argument missing for option: '--framework'." },
                //requireness is no longer set on parser level
                //new object?[] { "foo", "framework", "net5.0|net6.0", true, null, null, "Option '--framework' is required." },
                new object?[] { "foo --framework", "framework", "net5.0|net6.0", true, null, "netcoreapp2.1", $"Cannot parse default if option without value 'netcoreapp2.1' for option '--framework' as expected type 'choice': value 'netcoreapp2.1' is not allowed, allowed values are: 'net5.0','net6.0'. Did you mean one of the following?{Environment.NewLine}net5.0{Environment.NewLine}net6.0" }
            };

        [Theory]
#pragma warning disable CA1825 // Avoid zero-length array allocations. https://github.com/dotnet/sdk/issues/28672
        [MemberData(nameof(CanDetectParseErrorsChoiceTemplateOptionsData))]
#pragma warning restore CA1825 // Avoid zero-length array allocations.
        internal void CanDetectParseErrorsChoiceTemplateOptions(
              string command,
              string parameterName,
              string parameterValues,
              bool isRequired,
              string? defaultValue,
              string? defaultIfNoOptionValue,
              string expectedError)
        {
            MockTemplateInfo template = new MockTemplateInfo("foo", identity: "foo.1", groupIdentity: "foo.group")
                .WithChoiceParameter(parameterName, parameterValues.Split("|"), isRequired, defaultValue, defaultIfNoOptionValue);

            TemplateGroup templateGroup = TemplateGroup.FromTemplateList(
                CliTemplateInfo.FromTemplateInfo(new[] { template }, A.Fake<IHostSpecificDataLoader>()))
                .Single();

            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost();
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);
            TemplatePackageManager packageManager = A.Fake<TemplatePackageManager>();

            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host);
            Parser parser = ParserFactory.CreateParser(myCommand);
            ParseResult parseResult = parser.Parse($" new {command}");
            InstantiateCommandArgs args = InstantiateCommandArgs.FromNewCommandArgs(new NewCommandArgs(myCommand, parseResult));

            TemplateCommand templateCommand = new(myCommand, settings, packageManager, templateGroup, templateGroup.Templates.Single());
            Parser templateCommandParser = ParserFactory.CreateParser(templateCommand);
            ParseResult templateParseResult = templateCommandParser.Parse(args.RemainingArguments ?? Array.Empty<string>());
            Assert.True(templateParseResult.Errors.Any());
            Assert.Equal(expectedError, templateParseResult.Errors.Single().Message);
        }

        [Fact]
        internal void DoNotAddAllowScriptOptionForTemplate()
        {
            var template = new MockTemplateInfo("foo", identity: "foo.1", groupIdentity: "foo.group");

            TemplateGroup templateGroup = TemplateGroup.FromTemplateList(
                CliTemplateInfo.FromTemplateInfo(new[] { template }, A.Fake<IHostSpecificDataLoader>()))
                .Single();

            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost();
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);
            TemplatePackageManager packageManager = A.Fake<TemplatePackageManager>();

            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host);
            ParseResult parseResult = myCommand.Parse($" new foo");
            InstantiateCommandArgs args = InstantiateCommandArgs.FromNewCommandArgs(new NewCommandArgs(myCommand, parseResult));

            TemplateCommand templateCommand = new(myCommand, settings, packageManager, templateGroup, templateGroup.Templates.Single());
            Parser parser = ParserFactory.CreateParser(templateCommand);
            ParseResult templateParseResult = parser.Parse(args.RemainingArguments ?? Array.Empty<string>());

            TemplateCommandArgs templateArgs = new(templateCommand, myCommand, templateParseResult);
            Assert.Null(templateArgs.AllowScripts);
        }

        [Theory]
        [InlineData("foo", AllowRunScripts.Prompt)]
        [InlineData("foo --allow-scripts prompt", AllowRunScripts.Prompt)]
        [InlineData("foo --allow-scripts Prompt", AllowRunScripts.Prompt)]
        [InlineData("foo --allow-scripts yes", AllowRunScripts.Yes)]
        [InlineData("foo --allow-scripts Yes", AllowRunScripts.Yes)]
        [InlineData("foo --allow-scripts no", AllowRunScripts.No)]
        [InlineData("foo --allow-scripts NO", AllowRunScripts.No)]
        internal void CanParseAllowScriptsOption(string command, AllowRunScripts? result)
        {
            MockTemplateInfo template = new MockTemplateInfo("foo", identity: "foo.1", groupIdentity: "foo.group")
                .WithPostActions(ProcessStartPostActionProcessor.ActionProcessorId);

            TemplateGroup templateGroup = TemplateGroup.FromTemplateList(
                CliTemplateInfo.FromTemplateInfo(new[] { template }, A.Fake<IHostSpecificDataLoader>()))
                .Single();

            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost();
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);
            TemplatePackageManager packageManager = A.Fake<TemplatePackageManager>();

            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host);
            ParseResult parseResult = myCommand.Parse(command);
            InstantiateCommandArgs args = InstantiateCommandArgs.FromNewCommandArgs(new NewCommandArgs(myCommand, parseResult));

            TemplateCommand templateCommand = new(myCommand, settings, packageManager, templateGroup, templateGroup.Templates.Single());
            Parser parser = ParserFactory.CreateParser(templateCommand);
            ParseResult templateParseResult = parser.Parse(args.RemainingArguments ?? Array.Empty<string>());

            TemplateCommandArgs templateArgs = new(templateCommand, myCommand, templateParseResult);
            Assert.Equal(result, templateArgs.AllowScripts);
        }

        #region MultiShortNameResolutionTests
        [Theory]
        [InlineData("aaa")]
        [InlineData("bbb")]
        [InlineData("ccc")]
        [InlineData("ddd")]
        [InlineData("eee")]
        [InlineData("fff")]
        public void AllTemplatesInGroupUseAllShortNamesForResolution(string shortName)
        {
            TemplateGroup templateGroup = TemplateGroup.FromTemplateList(
               CliTemplateInfo.FromTemplateInfo(_testSets["MultiShortNameGroup"], A.Fake<IHostSpecificDataLoader>()))
               .Single();
            var defaultParams = new Dictionary<string, string>()
            {
                { "prefs:language", "C#" }
            };

            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(defaultParameters: defaultParams);
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);

            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host);
            ParseResult parseResult = myCommand.Parse($"new {shortName}");
            InstantiateCommandArgs args = InstantiateCommandArgs.FromNewCommandArgs(new NewCommandArgs(myCommand, parseResult));
            HashSet<TemplateCommand> templateCommands = InstantiateCommand.GetTemplateCommand(args, settings, A.Fake<TemplatePackageManager>(), templateGroup);
            Assert.Single(templateCommands);
            Assert.Equal("MultiName.Test.High.CSharp", templateCommands.Single().Template.Identity);
        }

        [Theory]
        [InlineData("aaa")]
        [InlineData("bbb")]
        [InlineData("ccc")]
        [InlineData("ddd")]
        [InlineData("eee")]
        [InlineData("fff")]
        public void ExplicitLanguageChoiceIsHonoredWithMultipleShortNames(string shortName)
        {
            TemplateGroup templateGroup = TemplateGroup.FromTemplateList(
               CliTemplateInfo.FromTemplateInfo(_testSets["MultiShortNameGroup"], A.Fake<IHostSpecificDataLoader>()))
               .Single();
            var defaultParams = new Dictionary<string, string>()
            {
                { "prefs:language", "C#" }
            };

            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(defaultParameters: defaultParams);
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);

            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host);
            string command = $"new {shortName} --language F#";
            ParseResult parseResult = myCommand.Parse(command);
            InstantiateCommandArgs args = InstantiateCommandArgs.FromNewCommandArgs(new NewCommandArgs(myCommand, parseResult));
            HashSet<TemplateCommand> templateCommands = InstantiateCommand.GetTemplateCommand(args, settings, A.Fake<TemplatePackageManager>(), templateGroup);
            Assert.Single(templateCommands);
            Assert.Equal("Multiname.Test.Only.FSharp", templateCommands.Single().Template.Identity);

        }

        [Theory]
        [InlineData("aaa", "W", "MultiName.Test.High.CSharp")] // uses a short name from the expected invokable template
        [InlineData("fff", "W", "MultiName.Test.High.CSharp")] // uses a short name from a different template in the group
        [InlineData("ccc", "X", "MultiName.Test.Low.CSharp")] // uses a short name from the expected invokable template
        [InlineData("fff", "X", "MultiName.Test.Low.CSharp")] // uses a short name from a different template in the group
        [InlineData("fff", "Y", "Multiname.Test.Only.FSharp")] // uses a short name from the expected invokable template
        [InlineData("eee", "Y", "Multiname.Test.Only.FSharp")] // uses a short name from a different template in the group
        public void ChoiceValueDisambiguatesMatchesWithMultipleShortNames(string name, string fooChoice, string expectedIdentity)
        {
            TemplateGroup templateGroup = TemplateGroup.FromTemplateList(
               CliTemplateInfo.FromTemplateInfo(_testSets["MultiShortNameGroup"], A.Fake<IHostSpecificDataLoader>()))
               .Single();
            var defaultParams = new Dictionary<string, string>()
            {
                { "prefs:language", "C#" }
            };

            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(defaultParameters: defaultParams);
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);

            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host);

            string command = $"new {name} --foo {fooChoice}";
            ParseResult parseResult = myCommand.Parse($" new {command}");
            InstantiateCommandArgs args = InstantiateCommandArgs.FromNewCommandArgs(new NewCommandArgs(myCommand, parseResult));
            HashSet<TemplateCommand> templateCommands = InstantiateCommand.GetTemplateCommand(args, settings, A.Fake<TemplatePackageManager>(), templateGroup);
            Assert.Single(templateCommands);
            Assert.Equal(expectedIdentity, templateCommands.Single().Template.Identity);
        }

        [Theory]
        [InlineData("aaa", "HighC", "someValue", "MultiName.Test.High.CSharp")] // uses a short name from the expected invokable template
        [InlineData("fff", "HighC", "someValue", "MultiName.Test.High.CSharp")] // uses a short name from a different template in the group
        [InlineData("ccc", "LowC", "someValue", "MultiName.Test.Low.CSharp")] // uses a short name from the expected invokable template
        [InlineData("fff", "LowC", "someValue", "MultiName.Test.Low.CSharp")] // uses a short name from a different template in the group
        [InlineData("fff", "OnlyF", "someValue", "Multiname.Test.Only.FSharp")] // uses a short name from the expected invokable template
        [InlineData("eee", "OnlyF", "someValue", "Multiname.Test.Only.FSharp")] // uses a short name from a different template in the group
        public void ParameterExistenceDisambiguatesMatchesWithMultipleShortNames(string name, string paramName, string paramValue, string expectedIdentity)
        {
            TemplateGroup templateGroup = TemplateGroup.FromTemplateList(
               CliTemplateInfo.FromTemplateInfo(_testSets["MultiShortNameGroup"], A.Fake<IHostSpecificDataLoader>()))
               .Single();
            var defaultParams = new Dictionary<string, string>()
            {
                { "prefs:language", "C#" }
            };

            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(defaultParameters: defaultParams);
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);

            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host);

            string command = $"new {name} --{paramName} {paramValue}";
            ParseResult parseResult = myCommand.Parse($" new {command}");
            InstantiateCommandArgs args = InstantiateCommandArgs.FromNewCommandArgs(new NewCommandArgs(myCommand, parseResult));
            HashSet<TemplateCommand> templateCommands = InstantiateCommand.GetTemplateCommand(args, settings, A.Fake<TemplatePackageManager>(), templateGroup);
            Assert.Single(templateCommands);
            Assert.Equal(expectedIdentity, templateCommands.Single().Template.Identity);
        }
        #endregion

        [Theory]
        [InlineData("new foo --dry-run", "dry-run")]
        [InlineData("new --dry-run foo", "dry-run")]
        [InlineData("new foo --force", "force")]
        [InlineData("new --force foo", "force")]
        [InlineData("new foo --no-update-check", "no-update-check")]
        [InlineData("new --no-update-check foo", "no-update-check")]
        internal void CanParseFlagsOption(string command, string action)
        {
            Func<TemplateCommandArgs, bool> expectedAction = action switch
            {
                "dry-run" => (TemplateCommandArgs ta) => ta.IsDryRun,
                "force" => (TemplateCommandArgs ta) => ta.IsForceFlagSpecified,
                "no-update-check" => (TemplateCommandArgs ta) => ta.NoUpdateCheck,
                _ => throw new Exception("Not expected value")
            };

            MockTemplateInfo template = new("foo", identity: "foo.1", groupIdentity: "foo.group");
            TemplateGroup templateGroup = TemplateGroup.FromTemplateList(
                CliTemplateInfo.FromTemplateInfo(new[] { template }, A.Fake<IHostSpecificDataLoader>()))
                .Single();

            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost();
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);
            TemplatePackageManager packageManager = A.Fake<TemplatePackageManager>();

            RootCommand rootCommand = new();
            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host);
            rootCommand.AddCommand(myCommand);
            ParseResult parseResult = rootCommand.Parse(command);
            InstantiateCommandArgs args = InstantiateCommandArgs.FromNewCommandArgs(new NewCommandArgs(myCommand, parseResult));
            TemplateCommand templateCommand = new(
                instantiateCommand: myCommand,
                environmentSettings: settings,
                templatePackageManager: packageManager,
                templateGroup: templateGroup,
                template: templateGroup.Templates.Single());
            Parser parser = ParserFactory.CreateParser(templateCommand);
            ParseResult templateParseResult = parser.Parse(args.TokensToInvoke ?? Array.Empty<string>());
            var templateArgs = new TemplateCommandArgs(templateCommand, myCommand, templateParseResult);

            Assert.True(expectedAction(templateArgs));
        }

        [Fact]
        internal void CanParse_WithoutRequiredParameter()
        {
            MockTemplateInfo template = new MockTemplateInfo("foo", identity: "foo.1", groupIdentity: "foo.group")
            .WithChoiceParameter("param", new[] { "val1", "val2" }, isRequired: true);
            TemplateGroup templateGroup = TemplateGroup.FromTemplateList(
                CliTemplateInfo.FromTemplateInfo(new[] { template }, A.Fake<IHostSpecificDataLoader>()))
                .Single();

            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost();
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);
            TemplatePackageManager packageManager = A.Fake<TemplatePackageManager>();

            RootCommand rootCommand = new();
            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host);
            rootCommand.AddCommand(myCommand);
            ParseResult parseResult = rootCommand.Parse("new foo");
            InstantiateCommandArgs args = InstantiateCommandArgs.FromNewCommandArgs(new NewCommandArgs(myCommand, parseResult));
            HashSet<TemplateCommand> templateCommands = InstantiateCommand.GetTemplateCommand(args, settings, A.Fake<TemplatePackageManager>(), templateGroup);
            Assert.Single(templateCommands);
        }
    }
}
