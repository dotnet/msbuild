// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.CommandLine;
using System.CommandLine.IO;
using System.CommandLine.Parsing;
using System.Threading.Tasks;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.Watcher.Tools
{
    public class CommandLineOptionsTests
    {
        private readonly Extensions.Tools.Internal.TestReporter _testReporter;
        private readonly TestConsole _console;

        public CommandLineOptionsTests(ITestOutputHelper output)
        {
            _console = new();
            _testReporter = new(output);
        }

        [Theory]
        [InlineData(new object[] { new[] { "-h" } })]
        [InlineData(new object[] { new[] { "-?" } })]
        [InlineData(new object[] { new[] { "--help" } })]
        [InlineData(new object[] { new[] { "--help", "--bogus" } })]
        public async Task HelpArgs(string[] args)
        {
            var rootCommand = Program.CreateRootCommand(c => Task.FromResult(0), _testReporter);

            await rootCommand.InvokeAsync(args, _console);

            Assert.Contains("Usage:", _console.Out.ToString());
        }

        [Theory]
        [InlineData(new[] { "run" }, new[] { "run" })]
        [InlineData(new[] { "run", "--", "subarg" }, new[] { "run", "subarg" })]
        [InlineData(new[] { "--", "run", "--", "subarg" }, new[] { "run", "--", "subarg" })]
        [InlineData(new[] { "--unrecognized-arg" }, new[] { "--unrecognized-arg" })]
        public async Task ParsesRemainingArgs(string[] args, string[] expected)
        {
            CommandLineOptions options = null;

            var rootCommand = Program.CreateRootCommand(c =>
            {
                options = c;
                return Task.FromResult(0);
            }, _testReporter);

            await rootCommand.InvokeAsync(args, _console);

            Assert.NotNull(options);

            Assert.Equal(expected, options.RemainingArguments);
            Assert.Empty(_console.Out.ToString());
        }

        [Fact]
        public async Task CannotHaveQuietAndVerbose()
        {
            var rootCommand = Program.CreateRootCommand(c => Task.FromResult(0), _testReporter);

            await rootCommand.InvokeAsync(new[] { "--quiet", "--verbose" }, _console);

            Assert.Contains(Resources.Error_QuietAndVerboseSpecified, _console.Error.ToString());
        }

        [Fact]
        public async Task ShortFormForProjectArgumentPrintsWarning()
        {
            var reporter = new Mock<Extensions.Tools.Internal.IReporter>();
            reporter.Setup(r => r.Warn(Resources.Warning_ProjectAbbreviationDeprecated, It.IsAny<string>())).Verifiable();
            CommandLineOptions options = null;
            var rootCommand = Program.CreateRootCommand(c => { options = c; return Task.FromResult(0); }, reporter.Object);

            await rootCommand.InvokeAsync(new[] { "-p", "MyProject.csproj" }, _console);

            reporter.Verify();
            Assert.NotNull(options);
            Assert.Equal("MyProject.csproj", options.Project);
        }

        [Fact]
        public async Task LongFormForProjectArgumentWorks()
        {
            var reporter = new Mock<Extensions.Tools.Internal.IReporter>();
            CommandLineOptions options = null;
            var rootCommand = Program.CreateRootCommand(c => { options = c; return Task.FromResult(0); }, reporter.Object);

            await rootCommand.InvokeAsync(new[] { "--project", "MyProject.csproj" }, _console);

            reporter.Verify(r => r.Warn(It.IsAny<string>(), It.IsAny<string>()), Times.Never());
            Assert.NotNull(options);
            Assert.Equal("MyProject.csproj", options.Project);
        }

        [Fact]
        public async Task LongFormForLaunchProfileArgumentWorks()
        {
            var reporter = new Mock<Extensions.Tools.Internal.IReporter>();
            CommandLineOptions options = null;
            var rootCommand = Program.CreateRootCommand(c => { options = c; return Task.FromResult(0); }, reporter.Object);

            await rootCommand.InvokeAsync(new[] { "--launch-profile", "CustomLaunchProfile" }, _console);

            reporter.Verify(r => r.Warn(It.IsAny<string>(), It.IsAny<string>()), Times.Never());
            Assert.NotNull(options);
            Assert.Equal("CustomLaunchProfile", options.LaunchProfile);
        }

        [Fact]
        public async Task ShortFormForLaunchProfileArgumentWorks()
        {
            var reporter = new Mock<Extensions.Tools.Internal.IReporter>();
            CommandLineOptions options = null;
            var rootCommand = Program.CreateRootCommand(c => { options = c; return Task.FromResult(0); }, reporter.Object);

            await rootCommand.InvokeAsync(new[] { "-lp", "CustomLaunchProfile" }, _console);

            reporter.Verify(r => r.Warn(It.IsAny<string>(), It.IsAny<string>()), Times.Never());
            Assert.NotNull(options);
            Assert.Equal("CustomLaunchProfile", options.LaunchProfile);
        }
    }
}
