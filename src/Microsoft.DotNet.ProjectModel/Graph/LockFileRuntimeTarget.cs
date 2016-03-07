// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.DotNet.ProjectModel.Graph
{
    public class LockFileRuntimeTarget
    {
        public LockFileRuntimeTarget(string path, string runtime, string assetType)
        {
            Path = path;
            Runtime = runtime;
            AssetType = assetType;
        }

        public string Path { get; }

        public string Runtime { get; }

        public string AssetType { get; }
    }
}