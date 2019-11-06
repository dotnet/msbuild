// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Tools.Test.Utilities
{
    public class DotnetCommand : TestCommand
    {
        public DotnetCommand()
            : this(RepoDirectoriesProvider.DotnetUnderTest)
        {
        }

        public DotnetCommand(string dotnetUnderTest)
            : base(dotnetUnderTest)
        {
        }
    }
}
