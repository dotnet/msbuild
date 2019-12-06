// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using FluentAssertions;
using Microsoft.DotNet.Tools.Test.Utilities;
using Microsoft.Extensions.DependencyModel;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.New.Tests
{
    public class GivenThatIWantANewApp : SdkTest
    {
        public GivenThatIWantANewApp(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void When_dotnet_new_is_invoked_multiple_times_it_should_fail()
        {
            var rootPath = _testAssetsManager.CreateTestDirectory().Path;

            new DotnetCommand(Log, "new")
                .WithWorkingDirectory(rootPath)
                .Execute($"console", "--debug:ephemeral-hive", "--no-restore");

            DateTime expectedState = Directory.GetLastWriteTime(rootPath);

            var result = new DotnetCommand(Log, "new")
                .WithWorkingDirectory(rootPath)
                .Execute($"console", "--debug:ephemeral-hive", "--no-restore");

            DateTime actualState = Directory.GetLastWriteTime(rootPath);

            Assert.Equal(expectedState, actualState);

            result.Should().Fail();
        }
    }
}
