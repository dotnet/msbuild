// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.DotNet.Core.Build.Tasks
{
    /// <summary>
    /// Values for File Group Metadata
    /// </summary>
    internal enum FileGroup
    {
        CompileTimeAssembly,
        RuntimeAssembly,
        ContentFile,
        NativeLibrary,
        ResourceAssembly,
        RuntimeTarget,
        FrameworkAssembly
    }
}
