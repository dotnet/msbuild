// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.DotNet.Internal.ProjectModel.FileSystemGlobbing.Internal
{
    internal interface IPattern
    {
        IPatternContext CreatePatternContextForInclude();

        IPatternContext CreatePatternContextForExclude();
    }
}