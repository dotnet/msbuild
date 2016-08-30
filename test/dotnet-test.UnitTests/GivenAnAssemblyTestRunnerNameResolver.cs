// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.DotNet.Tools.Test;
using Microsoft.Extensions.EnvironmentAbstractions;
using Xunit;

namespace Microsoft.Dotnet.Tools.Test.Tests
{
    public class GivenAnAssemblyTestRunnerNameResolver
    {
        private readonly string _directoryOfAssemblyUnderTest = Path.Combine("c:", "some", "path");

        private const string TestRunnerName = "dotnet-test-someRunner";

        private static readonly string TestRunnerFileName = $"{TestRunnerName}.dll";

        [Fact]
        public void It_finds_the_runner_in_the_same_folder_as_the_assembly_when_the_path_passed_is_to_the_assembly()
        {
            var directoryMock = new DirectoryMock();

            directoryMock.AddFile(_directoryOfAssemblyUnderTest, TestRunnerFileName);

            var pathToAssemblyUnderTest = Path.Combine(_directoryOfAssemblyUnderTest, TestRunnerFileName);
            var assemblyTestRunnerResolver =
                new AssemblyTestRunnerNameResolver(pathToAssemblyUnderTest, directoryMock);

            var testRunner = assemblyTestRunnerResolver.ResolveTestRunner();

            testRunner.Should().Be(TestRunnerName);
        }

        [Fact]
        public void It_returns_a_test_runner_even_when_multiple_test_runners_are_present()
        {
            var directoryMock = new DirectoryMock();

            directoryMock.AddFile(_directoryOfAssemblyUnderTest, TestRunnerFileName);
            directoryMock.AddFile(_directoryOfAssemblyUnderTest, "dotnet-test-someOtherTestRunner.dll");
            directoryMock.AddFile(_directoryOfAssemblyUnderTest, "dotnet-test-AndYetAnotherTestRunner.dll");

            var assemblyTestRunnerResolver =
                new AssemblyTestRunnerNameResolver(_directoryOfAssemblyUnderTest, directoryMock);

            var bestEffortTestRunner = assemblyTestRunnerResolver.ResolveTestRunner();

            bestEffortTestRunner.Should().NotBeNull();
        }

        [Fact]
        public void It_returns_null_when_no_test_runner_is_found()
        {
            var directoryMock = new DirectoryMock();

            var assemblyTestRunnerResolver =
                new AssemblyTestRunnerNameResolver(_directoryOfAssemblyUnderTest, directoryMock);

            var testRunner = assemblyTestRunnerResolver.ResolveTestRunner();

            testRunner.Should().BeNull();
        }

        private class DirectoryMock : IDirectory
        {
            private readonly IList<string> _files = new List<string>();

            public bool Exists(string path)
            {
                throw new System.NotImplementedException();
            }

            public ITemporaryDirectory CreateTemporaryDirectory()
            {
                throw new System.NotImplementedException();
            }

            public IEnumerable<string> GetFiles(string path, string searchPattern)
            {
                var searchPatternRegex = new Regex(searchPattern);
                return _files.Where(f => f.StartsWith(path) && searchPatternRegex.IsMatch(f));
            }

            public string GetDirectoryFullName(string path)
            {
                return Path.GetDirectoryName(path);
            }

            public void AddFile(string path, string fileName)
            {
                _files.Add(Path.Combine(path, fileName));
            }
        }
    }
}
