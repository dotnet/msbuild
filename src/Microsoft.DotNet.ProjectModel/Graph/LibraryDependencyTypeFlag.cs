// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.DotNet.ProjectModel.Graph
{
    [Flags]
    public enum LibraryDependencyTypeFlag
    {
        None = 0,
        MainReference = 1 << 0,
        MainSource = 1 << 1,
        MainExport = 1 << 2,
        PreprocessReference = 1 << 3,
        RuntimeComponent = 1 << 4,
        DevComponent = 1 << 5,
        PreprocessComponent = 1 << 6,
        BecomesNupkgDependency = 1 << 7,
    }
}
