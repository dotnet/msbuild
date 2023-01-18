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
    public partial class DotnetNewUpdateTests
    {
        [Fact]
        public Task CanShowMessageInCaseShortNameConflict()
        {
            string customHivePath = CreateTemporaryFolder(folderName: "Home");
            InstallTestTemplate("TemplateWithConflictShortName", _log, customHivePath);

            CommandResult commandResult = new DotnetNewCommand(_log, "update")
                  .WithCustomHive(customHivePath)
                  .WithoutBuiltInTemplates()
                  .Execute();

            commandResult
                .Should()
                .Pass();

            return Verify(commandResult.StdOut);
        }
    }
}
