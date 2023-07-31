// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ToolPackage;
using Microsoft.DotNet.Tools.Tool.List;
using Microsoft.Extensions.EnvironmentAbstractions;
using Moq;
using NuGet.Versioning;
using LocalizableStrings = Microsoft.DotNet.Tools.Tool.List.LocalizableStrings;
using Parser = Microsoft.DotNet.Cli.Parser;

namespace Microsoft.DotNet.Tests.Commands.Tool
{
    public class ToolListGlobalOrToolPathCommandTests
    {
        private readonly BufferedReporter _reporter;

        public ToolListGlobalOrToolPathCommandTests()
        {
            _reporter = new BufferedReporter();
        }

        [Fact]
        public void GivenNoInstalledPackagesItPrintsEmptyTable()
        {
            var store = new Mock<IToolPackageStoreQuery>(MockBehavior.Strict);
            store
                .Setup(s => s.EnumeratePackages())
                .Returns(new IToolPackage[0]);

            var command = CreateCommand(store.Object, "-g");

            command.Execute().Should().Be(0);

            _reporter.Lines.Should().Equal(EnumerateExpectedTableLines(store.Object));
        }

        [Fact]
        public void GivenAnInvalidToolPathItThrowsException()
        {
            var store = new Mock<IToolPackageStoreQuery>(MockBehavior.Strict);
            store
                .Setup(s => s.EnumeratePackages())
                .Returns(new IToolPackage[0]);

            var toolPath = "tool-path-does-not-exist";
            var command = CreateCommand(store.Object, $"--tool-path {toolPath}", toolPath);

            Action a = () => command.Execute();

            a.Should().Throw<GracefulException>()
             .And
             .Message
             .Should()
             .Be(string.Format(LocalizableStrings.InvalidToolPathOption, toolPath));
        }

        [Fact]
        public void GivenAToolPathItPassesToolPathToStoreFactory()
        {
            var store = new Mock<IToolPackageStoreQuery>(MockBehavior.Strict);
            store
                .Setup(s => s.EnumeratePackages())
                .Returns(new IToolPackage[0]);

            var toolPath = Path.GetTempPath();
            var command = CreateCommand(store.Object, $"--tool-path {toolPath}", toolPath);

            command.Execute().Should().Be(0);

            _reporter.Lines.Should().Equal(EnumerateExpectedTableLines(store.Object));
        }

        [Fact]
        public void GivenAToolPathItPassesToolPathToStoreFactoryFromRedirectCommand()
        {
            var store = new Mock<IToolPackageStoreQuery>(MockBehavior.Strict);
            store
                .Setup(s => s.EnumeratePackages())
                .Returns(new IToolPackage[0]);

            var toolPath = Path.GetTempPath();
            var result = Parser.Instance.Parse("dotnet tool list " + $"--tool-path {toolPath}");
            var toolListGlobalOrToolPathCommand = new ToolListGlobalOrToolPathCommand(
                result,
                toolPath1 =>
                {
                    AssertExpectedToolPath(toolPath1, toolPath);
                    return store.Object;
                },
                _reporter);

            var toolListCommand = new ToolListCommand(
                result,
                toolListGlobalOrToolPathCommand);

            toolListCommand.Execute().Should().Be(0);

            _reporter.Lines.Should().Equal(EnumerateExpectedTableLines(store.Object));
        }

        [Fact]
        public void GivenASingleInstalledPackageItPrintsThePackage()
        {
            var store = new Mock<IToolPackageStoreQuery>(MockBehavior.Strict);
            store
                .Setup(s => s.EnumeratePackages())
                .Returns(new[] {
                    CreateMockToolPackage(
                        "test.tool",
                        "1.3.5-preview",
                        new[] {
                            new RestoredCommand(new ToolCommandName("foo"), "dotnet", new FilePath("tool"))
                        }
                    )
                });

            var command = CreateCommand(store.Object, "-g");

            command.Execute().Should().Be(0);

            _reporter.Lines.Should().Equal(EnumerateExpectedTableLines(store.Object));
        }

        [Fact]
        public void GivenMultipleInstalledPackagesItPrintsThePackages()
        {
            var store = new Mock<IToolPackageStoreQuery>(MockBehavior.Strict);
            store
                .Setup(s => s.EnumeratePackages())
                .Returns(new[] {
                    CreateMockToolPackage(
                        "test.tool",
                        "1.3.5-preview",
                        new[] {
                            new RestoredCommand(new ToolCommandName("foo"), "dotnet", new FilePath("tool"))
                        }
                    ),
                    CreateMockToolPackage(
                        "another.tool",
                        "2.7.3",
                        new[] {
                            new RestoredCommand(new ToolCommandName("bar"), "dotnet", new FilePath("tool"))
                        }
                    ),
                    CreateMockToolPackage(
                        "some.tool",
                        "1.0.0",
                        new[] {
                            new RestoredCommand(new ToolCommandName("fancy-foo"), "dotnet", new FilePath("tool"))
                        }
                    )
                });

            var command = CreateCommand(store.Object, "-g");

            command.Execute().Should().Be(0);

            _reporter.Lines.Should().Equal(EnumerateExpectedTableLines(store.Object));
        }

        [Fact]
        public void GivenAPackageWithMultipleCommandsItListsThem()
        {
            var store = new Mock<IToolPackageStoreQuery>(MockBehavior.Strict);
            store
                .Setup(s => s.EnumeratePackages())
                .Returns(new[] {
                    CreateMockToolPackage(
                        "test.tool",
                        "1.3.5-preview",
                        new[] {
                            new RestoredCommand(new ToolCommandName("foo"), "dotnet", new FilePath("tool")),
                            new RestoredCommand(new ToolCommandName("bar"), "dotnet", new FilePath("tool")),
                            new RestoredCommand(new ToolCommandName("baz"), "dotnet", new FilePath("tool"))
                        }
                    )
                });

            var command = CreateCommand(store.Object, "-g");

            command.Execute().Should().Be(0);

            _reporter.Lines.Should().Equal(EnumerateExpectedTableLines(store.Object));
        }

        [Fact]
        public void GivenABrokenPackageItPrintsWarning()
        {
            var store = new Mock<IToolPackageStoreQuery>(MockBehavior.Strict);
            store
                .Setup(s => s.EnumeratePackages())
                .Returns(new[] {
                    CreateMockToolPackage(
                        "test.tool",
                        "1.3.5-preview",
                        new[] {
                            new RestoredCommand(new ToolCommandName("foo"), "dotnet", new FilePath("tool"))
                        }
                    ),
                    CreateMockBrokenPackage("another.tool", "2.7.3"),
                    CreateMockToolPackage(
                        "some.tool",
                        "1.0.0",
                        new[] {
                            new RestoredCommand(new ToolCommandName("fancy-foo"), "dotnet", new FilePath("tool"))
                        }
                    )
                });

            var command = CreateCommand(store.Object, "-g");

            command.Execute().Should().Be(0);

            _reporter.Lines.Should().Equal(
                EnumerateExpectedTableLines(store.Object).Prepend(
                    string.Format(
                        LocalizableStrings.InvalidPackageWarning,
                        "another.tool",
                        "broken").Yellow()));
        }

        private IToolPackage CreateMockToolPackage(string id, string version, IReadOnlyList<RestoredCommand> commands)
        {
            var package = new Mock<IToolPackage>(MockBehavior.Strict);

            package.SetupGet(p => p.Id).Returns(new PackageId(id));
            package.SetupGet(p => p.Version).Returns(NuGetVersion.Parse(version));
            package.SetupGet(p => p.Commands).Returns(commands);
            return package.Object;
        }

        [Fact]
        public void GivenPackageIdArgItPrintsThatPackage()
        {
            var store = new Mock<IToolPackageStoreQuery>(MockBehavior.Strict);
            store
                .Setup(s => s.EnumeratePackages())
                .Returns(new[] {
                     CreateMockToolPackage(
                        "test.tool",
                        "1.3.5-preview",
                        new[] {
                            new RestoredCommand(new ToolCommandName("foo"), "dotnet", new FilePath("tool"))
                        }
                    ),
                    CreateMockToolPackage(
                        "another.tool",
                        "2.7.3",
                        new[] {
                            new RestoredCommand(new ToolCommandName("bar"), "dotnet", new FilePath("tool"))
                        }
                    ),
                    CreateMockToolPackage(
                        "some.tool",
                        "1.0.0",
                        new[] {
                            new RestoredCommand(new ToolCommandName("fancy-foo"), "dotnet", new FilePath("tool"))
                        }
                    )
                });

            var command = CreateCommand(store.Object, "test.tool -g");

            command.Execute().Should().Be(0);

            _reporter.Lines.Should().Equal(EnumerateExpectedTableLines(store.Object, new PackageId("test.tool")));
        }

        [Fact]
        public void GivenNotInstalledPackageItPrintsEmpty()
        {
            var store = new Mock<IToolPackageStoreQuery>(MockBehavior.Strict);
            store
                .Setup(s => s.EnumeratePackages())
                .Returns(new[] {
                    CreateMockToolPackage(
                        "test.tool",
                        "1.3.5-preview",
                        new[] {
                            new RestoredCommand(new ToolCommandName("foo"), "dotnet", new FilePath("tool"))
                        }
                    )
                });

            var command = CreateCommand(store.Object, "not-installed-package -g");

            command.Execute().Should().Be(1);

            _reporter.Lines.Should().Equal(EnumerateExpectedTableLines(store.Object, new PackageId("not-installed-package")));
        }

        private IToolPackage CreateMockBrokenPackage(string id, string version)
        {
            var package = new Mock<IToolPackage>(MockBehavior.Strict);

            package.SetupGet(p => p.Id).Returns(new PackageId(id));
            package.SetupGet(p => p.Version).Returns(NuGetVersion.Parse(version));
            package.SetupGet(p => p.Commands).Throws(new ToolConfigurationException("broken"));
            return package.Object;
        }

        private ToolListGlobalOrToolPathCommand CreateCommand(IToolPackageStoreQuery store, string options = "", string expectedToolPath = null)
        {
            var result = Parser.Instance.Parse("dotnet tool list " + options);
            return new ToolListGlobalOrToolPathCommand(
                result,
                toolPath => { AssertExpectedToolPath(toolPath, expectedToolPath); return store; },
                _reporter);
        }

        private void AssertExpectedToolPath(DirectoryPath? toolPath, string expectedToolPath)
        {
            if (expectedToolPath != null)
            {
                toolPath.Should().NotBeNull();
                toolPath.Value.Value.Should().Be(expectedToolPath);
            }
            else
            {
                toolPath.Should().BeNull();
            }
        }

        private IEnumerable<string> EnumerateExpectedTableLines(IToolPackageStoreQuery store, PackageId? targetPackageId = null)
        {
            static string GetCommandsString(IToolPackage package)
            {
                return string.Join(ToolListGlobalOrToolPathCommand.CommandDelimiter, package.Commands.Select(c => c.Name));
            }

            var packages = store.EnumeratePackages().Where(
                (p) => PackageHasCommands(p) && ToolListGlobalOrToolPathCommand.PackageIdMatches(p, targetPackageId)
                ).OrderBy(package => package.Id);
            var columnDelimiter = PrintableTable<IToolPackageStoreQuery>.ColumnDelimiter;

            int packageIdColumnWidth = LocalizableStrings.PackageIdColumn.Length;
            int versionColumnWidth = LocalizableStrings.VersionColumn.Length;
            int commandsColumnWidth = LocalizableStrings.CommandsColumn.Length;
            foreach (var package in packages)
            {
                packageIdColumnWidth = Math.Max(packageIdColumnWidth, package.Id.ToString().Length);
                versionColumnWidth = Math.Max(versionColumnWidth, package.Version.ToNormalizedString().Length);
                commandsColumnWidth = Math.Max(commandsColumnWidth, GetCommandsString(package).Length);
            }

            yield return string.Format(
                "{0}{1}{2}{3}{4}",
                LocalizableStrings.PackageIdColumn.PadRight(packageIdColumnWidth),
                columnDelimiter,
                LocalizableStrings.VersionColumn.PadRight(versionColumnWidth),
                columnDelimiter,
                LocalizableStrings.CommandsColumn.PadRight(commandsColumnWidth));

            yield return new string(
                '-',
                packageIdColumnWidth + versionColumnWidth + commandsColumnWidth + (columnDelimiter.Length * 2));

            foreach (var package in packages)
            {
                yield return string.Format(
                    "{0}{1}{2}{3}{4}",
                    package.Id.ToString().PadRight(packageIdColumnWidth),
                    columnDelimiter,
                    package.Version.ToNormalizedString().PadRight(versionColumnWidth),
                    columnDelimiter,
                    GetCommandsString(package).PadRight(commandsColumnWidth));
            }
        }

        private static bool PackageHasCommands(IToolPackage package)
        {
            try
            {
                return package.Commands.Count >= 0;
            }
            catch (Exception ex) when (ex is ToolConfigurationException)
            {
                return false;
            }
        }
    }
}
