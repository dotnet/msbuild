// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.Extensions.EnvironmentAbstractions;

namespace Microsoft.DotNet.ToolPackage
{
    internal interface IToolPackageStore
    {
        DirectoryPath Root { get; }

        IEnumerable<IToolPackage> GetInstalledPackages(string packageId);
    }
}
