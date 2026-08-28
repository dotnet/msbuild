// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;

#nullable disable

namespace Microsoft.Build.Shared.AssemblyFoldersFromConfig
{
    [DebuggerDisplay("{Name}: FrameworkVersion = {FrameworkVersion}, Platform = {Platform}, Path= {Path}")]
    internal class AssemblyFolderItem
    {
        internal string Name { get; set; }

        internal string FrameworkVersion { get; set; }

        internal string Path { get; set; }

        internal string Platform { get; set; }
    }
}
