// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.DotNet.Cli.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.NET.TestFramework.Assertions
{
    public class DirectoryInfoAssertions
    {
        private DirectoryInfo _dirInfo;

        public DirectoryInfoAssertions(DirectoryInfo dir)
        {
            _dirInfo = dir;
        }

        public DirectoryInfo DirectoryInfo => _dirInfo;

        public AndConstraint<DirectoryInfoAssertions> Exist()
        {
            Execute.Assertion.ForCondition(_dirInfo.Exists)
                .FailWith("Expected directory {0} does not exist.", _dirInfo.FullName);
            return new AndConstraint<DirectoryInfoAssertions>(this);
        }

        public AndConstraint<DirectoryInfoAssertions> NotExist()
        {
            Execute.Assertion.ForCondition(!_dirInfo.Exists)
                .FailWith("Expected directory {0} not to exist.", _dirInfo.FullName);
            return new AndConstraint<DirectoryInfoAssertions>(this);
        }

        public AndConstraint<DirectoryInfoAssertions> HaveFile(string expectedFile)
        {
            var file = _dirInfo.EnumerateFiles(expectedFile, SearchOption.TopDirectoryOnly).SingleOrDefault();
            Execute.Assertion.ForCondition(file != null)
                .FailWith("Expected File {0} cannot be found in directory {1}.", expectedFile, _dirInfo.FullName);
            return new AndConstraint<DirectoryInfoAssertions>(this);
        }

        public AndConstraint<DirectoryInfoAssertions> NotHaveFile(string expectedFile)
        {
            var file = _dirInfo.EnumerateFiles(expectedFile, SearchOption.TopDirectoryOnly).SingleOrDefault();
            Execute.Assertion.ForCondition(file == null)
                .FailWith("File {0} should not be found in directory {1}.", expectedFile, _dirInfo.FullName);
            return new AndConstraint<DirectoryInfoAssertions>(this);
        }

        public AndConstraint<DirectoryInfoAssertions> HaveFiles(IEnumerable<string> expectedFiles)
        {
            foreach (var expectedFile in expectedFiles)
            {
                HaveFile(expectedFile);
            }

            return new AndConstraint<DirectoryInfoAssertions>(this);
        }

        public AndConstraint<DirectoryInfoAssertions> NotHaveFiles(IEnumerable<string> expectedFiles)
        {
            foreach (var expectedFile in expectedFiles)
            {
                NotHaveFile(expectedFile);
            }

            return new AndConstraint<DirectoryInfoAssertions>(this);
        }

        public AndConstraint<DirectoryInfoAssertions> HaveDirectory(string expectedDir)
        {
            var dir = _dirInfo.EnumerateDirectories(expectedDir, SearchOption.TopDirectoryOnly).SingleOrDefault();
            Execute.Assertion.ForCondition(dir != null)
                .FailWith("Expected directory {0} cannot be found inside directory {1}.", expectedDir, _dirInfo.FullName);

            return new AndConstraint<DirectoryInfoAssertions>(new DirectoryInfoAssertions(dir));
        }

        public AndConstraint<DirectoryInfoAssertions> OnlyHaveFiles(IEnumerable<string> expectedFiles, SearchOption searchOption = SearchOption.AllDirectories)
        {
            var actualFiles = _dirInfo.EnumerateFiles("*", searchOption)
                              .Select(f => f.FullName.Substring(_dirInfo.FullName.Length + 1) // make relative to _dirInfo
                              .Replace("\\", "/")); // normalize separator

            var missingFiles = Enumerable.Except(expectedFiles, actualFiles);
            var extraFiles = Enumerable.Except(actualFiles, expectedFiles);
            var nl = Environment.NewLine;

            Execute.Assertion.ForCondition(!missingFiles.Any())
                .FailWith($"Following files cannot be found inside directory {_dirInfo.FullName} {nl} {string.Join(nl, missingFiles)}");

            Execute.Assertion.ForCondition(!extraFiles.Any())
                .FailWith($"Following extra files are found inside directory {_dirInfo.FullName} {nl} {string.Join(nl, extraFiles)}");

            return new AndConstraint<DirectoryInfoAssertions>(this);
        }

        public AndConstraint<DirectoryInfoAssertions> NotHaveSubDirectories(params string[] notExpectedSubdirectories)
        {
            notExpectedSubdirectories = notExpectedSubdirectories ?? Array.Empty<string>();

            var subDirectories = _dirInfo.EnumerateDirectories();

            
            if (!notExpectedSubdirectories.Any())
            {
                //  If no subdirectories were passed in, it means there should be no subdirectories at all
                Execute.Assertion.ForCondition(!subDirectories.Any())
                    .FailWith("Directory {0} should not have any sub directories.", _dirInfo.FullName);
            }
            else
            {
                var actualSubDirectories = subDirectories
                                            .Select(f => f.FullName.Substring(_dirInfo.FullName.Length + 1) // make relative to _dirInfo
                                            .Replace("\\", "/")); // normalize separator

                var errorSubDirectories = notExpectedSubdirectories.Intersect(actualSubDirectories);

                var nl = Environment.NewLine;
                Execute.Assertion.ForCondition(!errorSubDirectories.Any())
                    .FailWith($"The following subdirectories should not be found inside directory {_dirInfo.FullName} {nl} {string.Join(nl, errorSubDirectories)}");
            }

            return new AndConstraint<DirectoryInfoAssertions>(this);
        }
    }
}
