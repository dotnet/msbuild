// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Framework;

internal enum ItemSpecModifierKind
{
    FullPath,
    RootDir,
    Filename,
    Extension,
    RelativeDir,
    Directory,
    RecursiveDir,
    Identity,
    ModifiedTime,
    CreatedTime,
    AccessedTime,
    DefiningProjectFullPath,
    DefiningProjectDirectory,
    DefiningProjectName,
    DefiningProjectExtension
}
