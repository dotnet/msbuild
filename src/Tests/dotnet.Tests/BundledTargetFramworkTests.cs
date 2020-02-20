// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Tools.Test.Utilities;
using Xunit;
using FluentAssertions;
using NuGet.Frameworks;
using Microsoft.NET.TestFramework;
using Xunit.Abstractions;

namespace Microsoft.DotNet.Tests
{
    public class BundledTargetFrameworkTests : SdkTest
    {
        public BundledTargetFrameworkTests(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void VersionCommandDisplaysCorrectVersion()
        {
            var filePath = Path.Combine(
                AppContext.BaseDirectory,
                "ExpectedTargetFrameworkMoniker.txt");
            var targetFrameworkMoniker = GetTargetFrameworkMonikerFromFile(filePath);
            var shortFolderName = NuGetFramework
                .Parse(targetFrameworkMoniker)
                .GetShortFolderName();
            BundledTargetFramework
                .GetTargetFrameworkMoniker()
                .Should().Be(shortFolderName);
        }

        private static string GetTargetFrameworkMonikerFromFile(string versionFilePath)
        {
            using (var reader = new StreamReader(File.OpenRead(versionFilePath)))
            {
                return reader.ReadLine();
            }
        }
    }
}
