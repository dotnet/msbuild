// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace Microsoft.Extensions.ProjectModel.Graph
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
