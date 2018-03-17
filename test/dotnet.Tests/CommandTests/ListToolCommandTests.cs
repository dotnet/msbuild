// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ToolPackage;
using Microsoft.DotNet.Tools;
using Microsoft.DotNet.Tools.List.Tool;
using Microsoft.DotNet.Tools.Test.Utilities;
using Microsoft.Extensions.DependencyModel.Tests;
using Microsoft.Extensions.EnvironmentAbstractions;
using Moq;
using NuGet.Versioning;
using Xunit;
using Parser = Microsoft.DotNet.Cli.Parser;
using LocalizableStrings = Microsoft.DotNet.Tools.List.Tool.LocalizableStrings;

namespace Microsoft.DotNet.Tests.Commands
{
    public class ListToolCommandTests
    {
        private readonly BufferedReporter _reporter;

        public ListToolCommandTests()
        {
            _reporter = new BufferedReporter();
        }

        [Fact]
        public void GivenAMissingGlobalOptionItErrors()
        {
            var store = new Mock<IToolPackageStore>(MockBehavior.Strict);

            var command = CreateCommand(store.Object);

            Action a = () => {
                command.Execute();
            };

            a.ShouldThrow<GracefulException>()
             .And
             .Message
             .Should()
             .Be(LocalizableStrings.ListToolCommandOnlySupportsGlobal);
        }

        [Fact]
        public void GivenNoInstalledPackagesItPrintsEmptyTable()
        {
            var store = new Mock<IToolPackageStore>(MockBehavior.Strict);
            store
                .Setup(s => s.EnumeratePackages())
                .Returns(new IToolPackage[0]);

            var command = CreateCommand(store.Object, "-g");

            command.Execute().Should().Be(0);

            _reporter.Lines.Should().Equal(
                string.Format(
                    "{0}      {1}      {2}",
                    LocalizableStrings.PackageIdColumn,
                    LocalizableStrings.VersionColumn,
                    LocalizableStrings.CommandsColumn
                ),
                "-------------------------------------");
        }

        [Fact]
        public void GivenASingleInstalledPackageItPrintsThePackage()
        {
            var store = new Mock<IToolPackageStore>(MockBehavior.Strict);
            store
                .Setup(s => s.EnumeratePackages())
                .Returns(new[] {
                    CreateMockToolPackage(
                        "test.tool",
                        "1.3.5-preview",
                        new[] {
                            new CommandSettings("foo", "dotnet", new FilePath("tool"))
                        }
                    )
                });

            var command = CreateCommand(store.Object, "-g");

            command.Execute().Should().Be(0);

            _reporter.Lines.Should().Equal(
                string.Format(
                    "{0}      {1}            {2}",
                    LocalizableStrings.PackageIdColumn,
                    LocalizableStrings.VersionColumn,
                    LocalizableStrings.CommandsColumn
                ),
                "-------------------------------------------",
                "test.tool       1.3.5-preview      foo     ");
        }

        [Fact]
        public void GivenMultipleInstalledPackagesItPrintsThePackages()
        {
            var store = new Mock<IToolPackageStore>(MockBehavior.Strict);
            store
                .Setup(s => s.EnumeratePackages())
                .Returns(new[] {
                    CreateMockToolPackage(
                        "test.tool",
                        "1.3.5-preview",
                        new[] {
                            new CommandSettings("foo", "dotnet", new FilePath("tool"))
                        }
                    ),
                    CreateMockToolPackage(
                        "another.tool",
                        "2.7.3",
                        new[] {
                            new CommandSettings("bar", "dotnet", new FilePath("tool"))
                        }
                    ),
                    CreateMockToolPackage(
                        "some.tool",
                        "1.0.0",
                        new[] {
                            new CommandSettings("fancy-foo", "dotnet", new FilePath("tool"))
                        }
                    )
                });

            var command = CreateCommand(store.Object, "-g");

            command.Execute().Should().Be(0);

            _reporter.Lines.Should().Equal(
                string.Format(
                    "{0}        {1}            {2} ",
                    LocalizableStrings.PackageIdColumn,
                    LocalizableStrings.VersionColumn,
                    LocalizableStrings.CommandsColumn
                ),
                "----------------------------------------------",
                "another.tool      2.7.3              bar      ",
                "some.tool         1.0.0              fancy-foo",
                "test.tool         1.3.5-preview      foo      ");
        }

        [Fact]
        public void GivenAPackageWithMultipleCommandsItListsThem()
        {
            var store = new Mock<IToolPackageStore>(MockBehavior.Strict);
            store
                .Setup(s => s.EnumeratePackages())
                .Returns(new[] {
                    CreateMockToolPackage(
                        "test.tool",
                        "1.3.5-preview",
                        new[] {
                            new CommandSettings("foo", "dotnet", new FilePath("tool")),
                            new CommandSettings("bar", "dotnet", new FilePath("tool")),
                            new CommandSettings("baz", "dotnet", new FilePath("tool"))
                        }
                    )
                });

            var command = CreateCommand(store.Object, "-g");

            command.Execute().Should().Be(0);

            _reporter.Lines.Should().Equal(
                string.Format(
                    "{0}      {1}            {2}     ",
                    LocalizableStrings.PackageIdColumn,
                    LocalizableStrings.VersionColumn,
                    LocalizableStrings.CommandsColumn
                ),
                "------------------------------------------------",
                "test.tool       1.3.5-preview      foo, bar, baz");
        }

        [Fact]
        public void GivenABrokenPackageItPrintsWarning()
        {
            var store = new Mock<IToolPackageStore>(MockBehavior.Strict);
            store
                .Setup(s => s.EnumeratePackages())
                .Returns(new[] {
                    CreateMockToolPackage(
                        "test.tool",
                        "1.3.5-preview",
                        new[] {
                            new CommandSettings("foo", "dotnet", new FilePath("tool"))
                        }
                    ),
                    CreateMockBrokenPackage("another.tool", "2.7.3"),
                    CreateMockToolPackage(
                        "some.tool",
                        "1.0.0",
                        new[] {
                            new CommandSettings("fancy-foo", "dotnet", new FilePath("tool"))
                        }
                    )
                });

            var command = CreateCommand(store.Object, "-g");

            command.Execute().Should().Be(0);

            _reporter.Lines.Should().Equal(
                string.Format(
                    LocalizableStrings.InvalidPackageWarning,
                    "another.tool",
                    "broken"
                ).Yellow(),
                string.Format(
                    "{0}      {1}            {2} ",
                    LocalizableStrings.PackageIdColumn,
                    LocalizableStrings.VersionColumn,
                    LocalizableStrings.CommandsColumn
                ),
                "--------------------------------------------",
                "some.tool       1.0.0              fancy-foo",
                "test.tool       1.3.5-preview      foo      ");
        }

        private IToolPackage CreateMockToolPackage(string id, string version, IReadOnlyList<CommandSettings> commands)
        {
            var package = new Mock<IToolPackage>(MockBehavior.Strict);

            package.SetupGet(p => p.Id).Returns(new PackageId(id));
            package.SetupGet(p => p.Version).Returns(NuGetVersion.Parse(version));
            package.SetupGet(p => p.Commands).Returns(commands);
            return package.Object;
        }

        private IToolPackage CreateMockBrokenPackage(string id, string version)
        {
            var package = new Mock<IToolPackage>(MockBehavior.Strict);

            package.SetupGet(p => p.Id).Returns(new PackageId(id));
            package.SetupGet(p => p.Version).Returns(NuGetVersion.Parse(version));
            package.SetupGet(p => p.Commands).Throws(new ToolConfigurationException("broken"));
            return package.Object;
        }

        private ListToolCommand CreateCommand(IToolPackageStore store, string options = "")
        {
            ParseResult result = Parser.Instance.Parse("dotnet list tool " + options);
            return new ListToolCommand(
                result["dotnet"]["list"]["tool"],
                result,
                store,
                _reporter);
        }
    }
}
