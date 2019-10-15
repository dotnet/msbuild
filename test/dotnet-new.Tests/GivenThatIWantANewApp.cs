// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using FluentAssertions;
using Microsoft.DotNet.Tools.Test.Utilities;
using Microsoft.Extensions.DependencyModel;
using Xunit;

namespace Microsoft.DotNet.New.Tests
{
    public class GivenThatIWantANewApp : TestBase
    {
        [Fact]
        public void When_dotnet_new_is_invoked_multiple_times_it_should_fail()
        {
            var rootPath = TestAssets.CreateTestDirectory().FullName;

            new NewCommand()
                .WithWorkingDirectory(rootPath)
                .Execute($"console --debug:ephemeral-hive --no-restore");

            DateTime expectedState = Directory.GetLastWriteTime(rootPath);

            var result = new NewCommand()
                .WithWorkingDirectory(rootPath)
                .ExecuteWithCapturedOutput($"console --debug:ephemeral-hive --no-restore");

            DateTime actualState = Directory.GetLastWriteTime(rootPath);

            Assert.Equal(expectedState, actualState);

            result.Should().Fail();
        }
    }
}
