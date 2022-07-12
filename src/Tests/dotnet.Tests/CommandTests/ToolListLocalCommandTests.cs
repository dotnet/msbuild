// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.IO;
using FluentAssertions;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ToolManifest;
using Microsoft.DotNet.ToolPackage;
using Microsoft.DotNet.Tools.Tool.List;
using Microsoft.Extensions.EnvironmentAbstractions;
using NuGet.Versioning;
using Xunit;
using Microsoft.NET.TestFramework.Utilities;
using System.CommandLine;
using System.CommandLine.Parsing;
using Parser = Microsoft.DotNet.Cli.Parser;

namespace Microsoft.DotNet.Tests.Commands.Tool
{
    public class ToolListLocalCommandTests
    {
        private readonly ParseResult _parseResult;
        private readonly BufferedReporter _reporter;
        private readonly string _temporaryDirectory;
        private readonly FakeManifestInspector _toolManifestInspector;
        private readonly ToolListLocalCommand _defaultToolListLocalCommand;
        private readonly string _testManifestPath;

        public ToolListLocalCommandTests()
        {
            _reporter = new BufferedReporter();
            _temporaryDirectory = Path.GetTempPath();
            _testManifestPath = Path.Combine(Path.GetTempPath(), "filename");

            _toolManifestInspector = new FakeManifestInspector(
                new List<(ToolManifestPackage toolManifestPackage, FilePath SourceManifest)>()
                {
                    (new ToolManifestPackage(
                        new PackageId("package.id"),
                        NuGetVersion.Parse("2.1.4"),
                        new[] {new ToolCommandName("package-name")},
                        new DirectoryPath(_temporaryDirectory)), new FilePath(_testManifestPath)),
                }
            );
            _parseResult = Parser.Instance.Parse("dotnet tool list");
            _defaultToolListLocalCommand = new ToolListLocalCommand(
                _parseResult,
                _toolManifestInspector,
                _reporter);
        }

        [Fact]
        public void GivenManifestInspectorItPrintsTheTable()
        {
            _defaultToolListLocalCommand.Execute();
            _reporter.Lines.Should().Contain(l => l.Contains("package.id"));
            _reporter.Lines.Should().Contain(l => l.Contains("2.1.4"));
            _reporter.Lines.Should().Contain(l => l.Contains(_testManifestPath));
            _reporter.Lines.Should().Contain(l => l.Contains("package-name"));
        }

        [Fact]
        public void GivenManifestInspectorWhenCalledFromRedirectCommandItPrintsTheTable()
        {
            var command = new ToolListCommand(result: _parseResult,
                toolListLocalCommand: _defaultToolListLocalCommand);
            _defaultToolListLocalCommand.Execute();
            _reporter.Lines.Should().Contain(l => l.Contains("package.id"));
            _reporter.Lines.Should().Contain(l => l.Contains("2.1.4"));
            _reporter.Lines.Should().Contain(l => l.Contains(_testManifestPath));
            _reporter.Lines.Should().Contain(l => l.Contains("package-name"));
        }

        private class FakeManifestInspector : IToolManifestInspector
        {
            private readonly IReadOnlyCollection<(ToolManifestPackage toolManifestPackage, FilePath SourceManifest)>
                ToToReturn;

            public FakeManifestInspector(
                IReadOnlyCollection<(ToolManifestPackage toolManifestPackage, FilePath SourceManifest)> toReturn)
            {
                ToToReturn = toReturn;
            }

            public IReadOnlyCollection<(ToolManifestPackage toolManifestPackage, FilePath SourceManifest)> Inspect(
                FilePath? filePath = null)
            {
                return ToToReturn;
            }
        }
    }
}
