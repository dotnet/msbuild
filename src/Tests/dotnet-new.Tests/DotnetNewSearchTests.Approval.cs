// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.DotNet.Cli.Utils;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;

namespace Microsoft.DotNet.Cli.New.IntegrationTests
{
    [UsesVerify]
    [Collection("Verify Tests")]
    public partial class DotnetNewSearchTests
    {
        [Theory]
        [InlineData("--search")]
        [InlineData("search")]
        public Task CannotExecuteEmptyCriteria(string testCase)
        {
            CommandResult commandResult = new DotnetNewCommand(_log, testCase)
                .WithCustomHive(_sharedHome.HomeDirectory)
                .Execute();

            commandResult.Should().Fail();

            return Verify(commandResult.StdErr)
                .UseTextForParameters("common")
                .DisableRequireUniquePrefix();
        }

        [Fact]
        public Task CanShowMessageInCaseShortNameConflict()
        {
            string customHivePath = CreateTemporaryFolder(folderName: "Home");
            InstallTestTemplate("TemplateWithConflictShortName", _log, customHivePath);

            CommandResult commandResult = new DotnetNewCommand(_log, "search", "do-not-exist")
                  .WithCustomHive(customHivePath)
                  .WithoutBuiltInTemplates()
                  .Execute();

            commandResult
                .Should()
                .Fail();

            return Verify(commandResult.StdOut);
        }
    }
}
