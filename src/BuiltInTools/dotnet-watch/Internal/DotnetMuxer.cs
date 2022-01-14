// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.IO;

namespace Microsoft.DotNet.Watcher.Tools
{
    internal static class DotnetMuxer
    {
        static DotnetMuxer()
        {
            MuxerPath = Process.GetCurrentProcess().MainModule.FileName;
            Debug.Assert(Path.GetFileNameWithoutExtension(MuxerPath) == "dotnet", $"Invalid muxer path {MuxerPath}");
        }

        public static string MuxerPath { get; }
    }
}
