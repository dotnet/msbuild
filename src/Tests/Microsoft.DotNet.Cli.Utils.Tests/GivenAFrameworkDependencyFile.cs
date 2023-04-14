// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using FluentAssertions;
using Microsoft.DotNet.Tools.Test.Utilities;
using Microsoft.Extensions.DependencyModel;
using Microsoft.NET.TestFramework;
using Xunit;

namespace Microsoft.DotNet.Cli.Utils.Tests
{
    public class GivenAFrameworkDependencyFile
    {
        private readonly IReadOnlyList<RuntimeFallbacks> _testRuntimeGraph;

        public GivenAFrameworkDependencyFile()
        {
            _testRuntimeGraph = new List<RuntimeFallbacks>
            {
                new RuntimeFallbacks("win-x64", new [] { "win", "any", "base" }),
                new RuntimeFallbacks("win8", new [] { "win7", "win", "any", "base" }),
                new RuntimeFallbacks($"{ToolsetInfo.LatestWinRuntimeIdentifier}", new [] { "win", "any", "base" }),
                new RuntimeFallbacks("win", new [] { "any", "base" }),
            };
        }

        [Fact]
        public void WhenPassSeveralCompatibleRuntimeIdentifiersItOutMostFitRid()
        {
            FrameworkDependencyFile.TryGetMostFitRuntimeIdentifier(
                    currentRuntimeIdentifier: ToolsetInfo.LatestWinRuntimeIdentifier,
                    alternativeCurrentRuntimeIdentifier : "win",
                    runtimeGraph : _testRuntimeGraph,
                    candidateRuntimeIdentifiers : new [] { "win", "any" },
                    mostFitRuntimeIdentifier : out string mostFitRid)
                .Should().BeTrue();

            mostFitRid.Should().Be("win");
        }

        [Fact]
        public void WhenPassSeveralCompatibleRuntimeIdentifiersItOutMostFitRid2()
        {
            FrameworkDependencyFile.TryGetMostFitRuntimeIdentifier(
                    currentRuntimeIdentifier: "win",
                    alternativeCurrentRuntimeIdentifier: null,
                    runtimeGraph: _testRuntimeGraph,
                    candidateRuntimeIdentifiers: new[] { "win", "any" },
                    mostFitRuntimeIdentifier: out string mostFitRid)
                .Should().BeTrue();

            mostFitRid.Should().Be("win");
        }

        [Fact]
        public void WhenPassSeveralCompatibleRuntimeIdentifiersAndCurrentRuntimeIdentifierIsNullReturnsFalse()
        {
            FrameworkDependencyFile.TryGetMostFitRuntimeIdentifier(
                    currentRuntimeIdentifier: null,
                    alternativeCurrentRuntimeIdentifier: null,
                    runtimeGraph: _testRuntimeGraph,
                    candidateRuntimeIdentifiers: new[] { "win", "any" },
                    mostFitRuntimeIdentifier: out string mostFitRid)
                .Should().BeFalse();
        }

        [Fact]
        public void WhenPassSeveralCompatibleRuntimeIdentifiersItOutMostFitRidWithCasingPreserved()
        {
            FrameworkDependencyFile.TryGetMostFitRuntimeIdentifier(
                    currentRuntimeIdentifier: ToolsetInfo.LatestWinRuntimeIdentifier,
                    alternativeCurrentRuntimeIdentifier : null,
                    runtimeGraph : _testRuntimeGraph,
                    candidateRuntimeIdentifiers : new [] { "Win", "any" },
                    mostFitRuntimeIdentifier : out string mostFitRid)
                .Should().BeTrue();

            mostFitRid.Should().Be("Win");
        }

        [Fact]
        public void WhenPassSeveralCompatibleRuntimeIdentifiersWithDuplicationItOutMostFitRid()
        {
            FrameworkDependencyFile.TryGetMostFitRuntimeIdentifier(
                    currentRuntimeIdentifier: ToolsetInfo.LatestWinRuntimeIdentifier,
                    alternativeCurrentRuntimeIdentifier : null,
                    runtimeGraph : _testRuntimeGraph,
                    candidateRuntimeIdentifiers : new [] { "win", "win", "any" },
                    mostFitRuntimeIdentifier : out string mostFitRid)
                .Should().BeTrue();

            mostFitRid.Should().Be("win");
        }

        [Fact]
        public void WhenPassSeveralCompatibleRuntimeIdentifiersAndDuplicationItOutMostFitRidWithCasingPreservedTheFirstIsFavorited()
        {
            FrameworkDependencyFile.TryGetMostFitRuntimeIdentifier(
                    currentRuntimeIdentifier: ToolsetInfo.LatestWinRuntimeIdentifier,
                    alternativeCurrentRuntimeIdentifier: null,
                    runtimeGraph: _testRuntimeGraph,
                    candidateRuntimeIdentifiers: new[] { "Win", "win", "win", "any" },
                    mostFitRuntimeIdentifier: out string mostFitRid)
                .Should().BeTrue();

            mostFitRid.Should().Be("Win");
        }

        [Fact]
        public void WhenPassSeveralNonCompatibleRuntimeIdentifiersItReturnsFalse()
        {
            FrameworkDependencyFile.TryGetMostFitRuntimeIdentifier(
                    currentRuntimeIdentifier: ToolsetInfo.LatestWinRuntimeIdentifier,
                    alternativeCurrentRuntimeIdentifier : null,
                    runtimeGraph : _testRuntimeGraph,
                    candidateRuntimeIdentifiers : new [] { "centos", "debian" },
                    mostFitRuntimeIdentifier : out string mostFitRid)
                .Should().BeFalse();
        }

        [Fact]
        public void WhenCurrentRuntimeIdentifierIsNotSupportedItUsesAlternative()
        {
            FrameworkDependencyFile.TryGetMostFitRuntimeIdentifier(
                    currentRuntimeIdentifier: "win-vnext",
                    alternativeCurrentRuntimeIdentifier: ToolsetInfo.LatestWinRuntimeIdentifier,
                    runtimeGraph: _testRuntimeGraph,
                    candidateRuntimeIdentifiers: new[] { "win", "any" },
                    mostFitRuntimeIdentifier: out string mostFitRid)
                .Should().BeTrue();

            mostFitRid.Should().Be("win");
        }

        [Fact]
        public void WhenCurrentRuntimeIdentifierIsNotSupportedSoIsTheAlternativeItReturnsFalse()
        {
            FrameworkDependencyFile.TryGetMostFitRuntimeIdentifier(
                    currentRuntimeIdentifier: $"{ToolsetInfo.LatestMacRuntimeIdentifier}-x64",
                    alternativeCurrentRuntimeIdentifier: "osx-x64",
                    runtimeGraph: _testRuntimeGraph,
                    candidateRuntimeIdentifiers: new[] { "win", "any" },
                    mostFitRuntimeIdentifier: out string mostFitRid)
                .Should().BeFalse();
        }
    }
}
