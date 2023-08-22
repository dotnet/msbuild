// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions.Execution;

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

        public AndConstraint<DirectoryInfoAssertions> HaveFilesMatching(
            string expectedFilesSearchPattern,
            SearchOption searchOption,
            string because = "",
            params object[] reasonArgs)
        {
            var matchingFileExists = _dirInfo.EnumerateFiles(expectedFilesSearchPattern, searchOption).Any();

            Execute.Assertion
                .ForCondition(matchingFileExists == true)
                .BecauseOf(because, reasonArgs)
                .FailWith("Expected directory {0} to contain files matching {1}, but no matching file exists.",
                    _dirInfo.FullName, expectedFilesSearchPattern);

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

        public AndConstraint<DirectoryInfoAssertions> NotHaveFilesMatching(string expectedFilesSearchPattern, SearchOption searchOption)
        {
            var matchingFileCount = _dirInfo.EnumerateFiles(expectedFilesSearchPattern, searchOption).Count();
            Execute.Assertion.ForCondition(matchingFileCount == 0)
                .FailWith("Found {0} files that should not exist in directory {1}. No file matching {2} should exist.",
                    matchingFileCount, _dirInfo.FullName, expectedFilesSearchPattern);
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

        public AndConstraint<DirectoryInfoAssertions> BeEmpty()
        {
            Execute.Assertion.ForCondition(!_dirInfo.EnumerateFileSystemInfos().Any())
                .FailWith($"The directory {_dirInfo.FullName} is not empty.");

            return new AndConstraint<DirectoryInfoAssertions>(this);
        }

        public AndConstraint<DirectoryInfoAssertions> NotBeEmpty()
        {
            Execute.Assertion.ForCondition(_dirInfo.EnumerateFileSystemInfos().Any())
                .FailWith($"The directory {_dirInfo.FullName} is empty.");

            return new AndConstraint<DirectoryInfoAssertions>(this);
        }

        public AndConstraint<DirectoryInfoAssertions> NotExist(string because = "", params object[] reasonArgs)
        {
            Execute.Assertion
                .ForCondition(_dirInfo.Exists == false)
                .BecauseOf(because, reasonArgs)
                .FailWith($"Expected directory {_dirInfo.FullName} to not exist, but it does.");

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
