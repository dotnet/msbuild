// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Collections.Generic;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Tools.Test.Utilities;
using Xunit;
using FluentAssertions;

namespace Microsoft.DotNet.Tests
{
    public class GivenDotnetSdk : TestBase
    {
        [Fact]
        public void VersionCommandDisplaysCorrectVersion()
        {
            var versionFilePath = Path.Combine(AppContext.BaseDirectory, "ExpectedSdkVersion.txt");
            var version = GetVersionFromFile(versionFilePath);

            CommandResult result = new DotnetCommand()
                    .ExecuteWithCapturedOutput("--version");

            result.Should().Pass();
            result.StdOut.Trim().Should().Be(version);
        }

        private string GetVersionFromFile(string versionFilePath)
        {
            using (var reader = new StreamReader(File.OpenRead(versionFilePath)))
            {
                return reader.ReadLine();
            }
        }
    }
}
