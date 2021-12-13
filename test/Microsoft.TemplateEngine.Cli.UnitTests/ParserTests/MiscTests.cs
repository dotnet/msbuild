// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.CommandLine.Builder;
using System.CommandLine.Completions;
using System.CommandLine.Parsing;
using Microsoft.TemplateEngine.Cli.Commands;
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
                .Parse("-h");

            var aliases = result.CommandResult
                .Children
                .OfType<OptionResult>()
                .Select(r => r.Option)
                .Where(o => o.HasAlias("-h"))
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
    }
}
