// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using NuGet.Versioning;

namespace Microsoft.DotNet.ProjectModel.Graph
{
    public class LockFileProjectLibrary
    {
        public string Name { get; set; }

        public NuGetVersion Version { get; set; }

        public string Path { get; set; }
    }
}