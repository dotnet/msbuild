// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.New.IntegrationTests
{
    internal static class Extensions
    {
        internal static string FormatOutputStreams(this CommandResult commandResult)
        {
            return "StdErr:" + Environment.NewLine + commandResult.StdErr + Environment.NewLine + "StdOut:" + Environment.NewLine + commandResult.StdOut;
        }
    }
}
