// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Microsoft.DotNet.Watcher
{
    public readonly struct FileItem
    {
        public string FilePath { get; init; }

        public string ProjectPath { get; init; }

        public bool IsStaticFile { get; init; }

        public string StaticWebAssetPath { get; init; }
    }
}
