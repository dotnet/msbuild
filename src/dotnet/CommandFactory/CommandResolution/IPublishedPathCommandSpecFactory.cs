// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Microsoft.DotNet.CommandFactory
{
    public interface IPublishedPathCommandSpecFactory
    {
        CommandSpec CreateCommandSpecFromPublishFolder(
            string commandPath,
            IEnumerable<string> commandArguments,
            string depsFilePath,
            string runtimeConfigPath);
    }
}
