// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Watcher.Tools
{
    internal static class DotnetMuxer
    {
        public static string MuxerPath { get; } = new Muxer().MuxerPath;
    }
}
