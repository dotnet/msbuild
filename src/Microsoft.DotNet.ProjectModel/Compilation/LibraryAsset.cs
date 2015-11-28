// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.DotNet.ProjectModel.Compilation
{
    public struct LibraryAsset
    {
        public string Name { get; }
        public string RelativePath { get; }
        public string ResolvedPath { get; }

        public LibraryAsset(string name, string relativePath, string resolvedPath)
        {
            Name = name;
            RelativePath = relativePath;
            ResolvedPath = resolvedPath;
        }
    }
}
