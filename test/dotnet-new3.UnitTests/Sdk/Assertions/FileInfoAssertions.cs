// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.NET.TestFramework.Assertions
{
    public class FileInfoAssertions
    {
        private FileInfo _fileInfo;

        public FileInfoAssertions(FileInfo file)
        {
            _fileInfo = file;
        }

        public FileInfo FileInfo => _fileInfo;

        public AndConstraint<FileInfoAssertions> Exist(string because = "", params object[] reasonArgs)
        {
            Execute.Assertion
                .ForCondition(_fileInfo.Exists)
                .BecauseOf(because, reasonArgs)
                .FailWith($"Expected File {_fileInfo.FullName} to exist, but it does not.");
            return new AndConstraint<FileInfoAssertions>(this);
        }

        public AndConstraint<FileInfoAssertions> NotExist(string because = "", params object[] reasonArgs)
        {
            Execute.Assertion
                .ForCondition(!_fileInfo.Exists)
                .BecauseOf(because, reasonArgs)
                .FailWith($"Expected File {_fileInfo.FullName} to not exist, but it does.");
            return new AndConstraint<FileInfoAssertions>(this);
        }

        public AndWhichConstraint<FileInfoAssertions, DateTimeOffset> HaveLastWriteTimeUtc(string because = "", params object[] reasonArgs)
        {
            var lastWriteTimeUtc = _fileInfo.LastWriteTimeUtc;

            return new AndWhichConstraint<FileInfoAssertions, DateTimeOffset>(this, lastWriteTimeUtc);
        }
    }
}
