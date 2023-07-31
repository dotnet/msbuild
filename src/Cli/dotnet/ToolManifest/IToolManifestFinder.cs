// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.ToolPackage;
using Microsoft.Extensions.EnvironmentAbstractions;

namespace Microsoft.DotNet.ToolManifest
{
    internal interface IToolManifestFinder
    {
        IReadOnlyCollection<ToolManifestPackage> Find(FilePath? filePath = null);
        FilePath FindFirst(bool createManifestFileOption = false);
        IReadOnlyList<FilePath> FindByPackageId(PackageId packageId);
    }
}
