// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Completions;
using System.CommandLine.Parsing;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Cli.Commands;
using Microsoft.TemplateEngine.TestHelper;
using Xunit;

namespace Microsoft.TemplateEngine.Cli.UnitTests.ParserTests
{
    public class MiscTests
    {
        /// <summary>
        /// This test checks if help aliases are in sync with System.CommandLine.
        /// </summary>
        [Fact]
        public void KnownHelpAliasesAreCorrect()
        {
            var result = new CommandLineBuilder()
                .UseDefaults()
                .Build()
                .Parse(Constants.KnownHelpAliases[0]);

            var aliases = result.CommandResult
                .Children
                .OfType<OptionResult>()
                .Select(r => r.Option)
                .Where(o => o.HasAlias(Constants.KnownHelpAliases[0]))
                .Single()
                .Aliases;

            Assert.Equal(aliases.OrderBy(a => a), TemplateCommand.KnownHelpAliases.OrderBy(a => a));
        }

        /// <summary>
        /// This test check if default completion item comparer compares the instances using labels only.
        /// </summary>
        [Fact]
        public void CompletionItemCompareIsAsExpected()
        {
            Assert.Equal(
                new CompletionItem("my-label", kind: "Value", sortText: "sort-text", insertText: "insert-text", documentation: "doc", detail: "det"),
                new CompletionItem("my-label", kind: "Value", sortText: "sort-text", insertText: "insert-text", documentation: "doc", detail: "det"));

            Assert.Equal(
              new CompletionItem("my-label", kind: "Value", sortText: "sort-text1", insertText: "insert-text1", documentation: "doc1", detail: "det1"),
              new CompletionItem("my-label", kind: "Value", sortText: "sort-text2", insertText: "insert-text2", documentation: "doc2", detail: "det2"));

            Assert.NotEqual(
                 new CompletionItem("my-label", kind: "Value", sortText: "sort-text1", insertText: "insert-text1", documentation: "doc1", detail: "det1"),
                 new CompletionItem("my-label", kind: "Argument", sortText: "sort-text2", insertText: "insert-text2", documentation: "doc2", detail: "det2"));

        }

        [Theory]
        [InlineData("new --debug:attach", "--debug:attach")]
        [InlineData("new --debug:attach console", "--debug:attach")]
        [InlineData("new --debug:reinit", "--debug:reinit")]
        [InlineData("new --debug:ephemeral-hive", "--debug:ephemeral-hive")]
        [InlineData("new --debug:virtual-hive", "--debug:ephemeral-hive")]
        [InlineData("new --debug:rebuildcache", "--debug:rebuild-cache")]
        [InlineData("new --debug:rebuild-cache", "--debug:rebuild-cache")]
        [InlineData("new --debug:show-config", "--debug:show-config")]
        [InlineData("new --debug:showconfig", "--debug:show-config")]

        public void DebugFlagCanBeParsedOnNewLevel(string command, string option)
        {
             Dictionary<string, Func<GlobalArgs, bool>> optionsMap = new Dictionary<string, Func<GlobalArgs, bool>>()
            {
                { "--debug:attach", args => args.DebugAttach },
                { "--debug:ephemeral-hive", args => args.DebugVirtualizeSettings },
                { "--debug:reinit", args => args.DebugReinit },
                { "--debug:rebuild-cache", args => args.DebugRebuildCache },
                { "--debug:show-config", args => args.DebugShowConfig }
            };

            ITemplateEngineHost host = TestHost.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(includeTestTemplates: false));
            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host, _ => new TelemetryLogger(null, false), new NewCommandCallbacks());
            var parseResult = ParserFactory.CreateParser(myCommand).Parse(command);
            NewCommandArgs args = new NewCommandArgs(myCommand, parseResult);

            Assert.True(optionsMap[option](args));
        }

        [Theory]
        [InlineData("new install source --debug:attach", "--debug:attach")]
        [InlineData("new install source --debug:attach --add-source s1", "--debug:attach")]
        [InlineData("new install --debug:reinit source", "--debug:reinit")]
        [InlineData("new install source --debug:ephemeral-hive", "--debug:ephemeral-hive")]
        [InlineData("new install source --debug:virtual-hive", "--debug:ephemeral-hive")]
        [InlineData("new install source --debug:rebuildcache", "--debug:rebuild-cache")]
        [InlineData("new install source --debug:rebuild-cache", "--debug:rebuild-cache")]
        [InlineData("new install source --debug:show-config", "--debug:show-config")]
        [InlineData("new install source --debug:showconfig", "--debug:show-config")]

        public void DebugFlagCanBeParsedOnSubcommandLevel(string command, string option)
        {
            Dictionary<string, Func<GlobalArgs, bool>> optionsMap = new Dictionary<string, Func<GlobalArgs, bool>>()
            {
                { "--debug:attach", args => args.DebugAttach },
                { "--debug:ephemeral-hive", args => args.DebugVirtualizeSettings },
                { "--debug:reinit", args => args.DebugReinit },
                { "--debug:rebuild-cache", args => args.DebugRebuildCache },
                { "--debug:show-config", args => args.DebugShowConfig }
            };

            ITemplateEngineHost host = TestHost.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(includeTestTemplates: false));
            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host, _ => new TelemetryLogger(null, false), new NewCommandCallbacks());
            var parseResult = ParserFactory.CreateParser(myCommand).Parse(command);
            InstallCommandArgs args = new InstallCommandArgs((InstallCommand)parseResult.CommandResult.Command, parseResult);

            Assert.True(optionsMap[option](args));
        }

        [Theory]
        [InlineData("new console  --framework net5.0 --flag --debug:attach", "--debug:attach")]
        [InlineData("new console --framework net5.0 --debug:attach --flag", "--debug:attach")]
        [InlineData("new console --debug:reinit --framework net5.0 --flag", "--debug:reinit")]
        [InlineData("new console --framework net5.0 --debug:ephemeral-hive --flag", "--debug:ephemeral-hive")]
        [InlineData("new console --framework net5.0 --debug:virtual-hive --flag", "--debug:ephemeral-hive")]
        [InlineData("new console --framework net5.0 --debug:rebuildcache --flag", "--debug:rebuild-cache")]
        [InlineData("new console --framework net5.0 --debug:rebuild-cache --flag", "--debug:rebuild-cache")]
        [InlineData("new console --framework net5.0 --debug:show-config --flag", "--debug:show-config")]
        [InlineData("new console --framework net5.0 --debug:showconfig --flag", "--debug:show-config")]

        public void DebugFlagCanBeParsedOnTemplateSubcommandLevel(string command, string option)
        {
            Dictionary<string, Func<GlobalArgs, bool>> optionsMap = new Dictionary<string, Func<GlobalArgs, bool>>()
            {
                { "--debug:attach", args => args.DebugAttach },
                { "--debug:ephemeral-hive", args => args.DebugVirtualizeSettings },
                { "--debug:reinit", args => args.DebugReinit },
                { "--debug:rebuild-cache", args => args.DebugRebuildCache },
                { "--debug:show-config", args => args.DebugShowConfig }
            };

            ITemplateEngineHost host = TestHost.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(includeTestTemplates: false));
            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host, _ => new TelemetryLogger(null, false), new NewCommandCallbacks());
            var parseResult = myCommand.Parse(command);
            InstantiateCommandArgs args = InstantiateCommandArgs.FromNewCommandArgs(new NewCommandArgs(myCommand, parseResult));

            Assert.True(optionsMap[option](args));
            Assert.Equal("console", args.ShortName);
            Assert.Equal(new[] { "--framework", "net5.0", "--flag" }, args.RemainingArguments);
        }

        [Fact]
        public void ManuallyAddedOptionIsPreservedOnTemplateSubcommandLevel()
        {
            ITemplateEngineHost host = TestHost.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(includeTestTemplates: false));
            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host, _ => new TelemetryLogger(null, false), new NewCommandCallbacks());

            var customOption = new Option<string>("--newOption");
            myCommand.AddGlobalOption(customOption);

            var parseResult = myCommand.Parse("new console --newOption val");
            InstantiateCommandArgs args = InstantiateCommandArgs.FromNewCommandArgs(new NewCommandArgs(myCommand, parseResult));

            Assert.NotNull(args.ParseResult);
            Assert.Equal("console", args.ShortName);
            Assert.Empty(args.RemainingArguments);
            Assert.Equal("val", args.ParseResult.GetValueForOption(customOption));
        }
    }
}
