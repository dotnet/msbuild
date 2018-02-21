// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.Extensions.EnvironmentAbstractions;

namespace Microsoft.DotNet.ToolPackage
{
    internal interface IProjectRestorer
    {
        void Restore(
            FilePath project,
            DirectoryPath assetJsonOutput,
            FilePath? nugetConfig = null,
            string source = null,
            string verbosity = null);
    }
}
