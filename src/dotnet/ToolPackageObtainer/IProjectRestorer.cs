// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.EnvironmentAbstractions;

namespace Microsoft.DotNet.ToolPackageObtainer
{
    internal interface IProjectRestorer
    {
        void Restore(
            FilePath projectPath,
            DirectoryPath assetJsonOutput, 
            FilePath? nugetconfig);
    }
}
