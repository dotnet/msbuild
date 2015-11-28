// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using NuGet.Versioning;

namespace Microsoft.DotNet.ProjectModel.Graph
{
    public class LockFilePackageLibrary
    {
        public string Name { get; set; }

        public NuGetVersion Version { get; set; }

        public bool IsServiceable { get; set; }

        public string Sha512 { get; set; }

        public IList<string> Files { get; set; } = new List<string>();
    }
}
