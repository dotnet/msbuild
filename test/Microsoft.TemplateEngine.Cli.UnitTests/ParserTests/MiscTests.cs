// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.CommandLine.Builder;
using System.CommandLine.Parsing;
using Microsoft.TemplateEngine.Cli.Commands;
using Xunit;

namespace Microsoft.TemplateEngine.Cli.UnitTests.ParserTests
{
    public class MiscTests
    {
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
    }
}
