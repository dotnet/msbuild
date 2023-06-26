// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.DotNet.Watcher
{
    internal readonly struct FileItem
    {
        public string FilePath { get; init; }

        public string ProjectPath { get; init; }

        public bool IsStaticFile { get; init; }

        public string StaticWebAssetPath { get; init; }

        public bool IsNewFile { get; init; }
    }
}
