// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.MigrateCommand;
using Microsoft.DotNet.Tools.New;

namespace Microsoft.DotNet.Tools.Migrate
{
    public class DotnetNewRedirector : ICanCreateDotnetCoreTemplate
    {
        public void CreateWithWithEphemeralHiveAndNoRestore(
            string templateName,
            string outputDirectory,
            string workingDirectory)
        {
            var newCommandArgs = new[] {templateName, "-o", "outputDirectory"};
            var result = NewCommandShim.Run(newCommandArgs);

            if (result != 0)
            {
                throw new GracefulException(
                    $"{nameof(NewCommandShim)} " +
                    "failed to execuate given args " +
                    $"{string.Join(" ", newCommandArgs)}");
            }
        }
    }
}