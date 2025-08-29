﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Runtime.Serialization;

#nullable disable

namespace Microsoft.Build.Shared.AssemblyFoldersFromConfig
{
    [DataContract(Name = "AssemblyFolder", Namespace = "")]
    [DebuggerDisplay("{Name}: FrameworkVersion = {FrameworkVersion}, Platform = {Platform}, Path= {Path}")]
    internal class AssemblyFolderItem
    {
        [DataMember(IsRequired = false, Order = 1)]
        internal string Name { get; set; }

        [DataMember(IsRequired = true, Order = 2)]
        internal string FrameworkVersion { get; set; }

        [DataMember(IsRequired = true, Order = 3)]
        internal string Path { get; set; }

        [DataMember(IsRequired = false, Order = 4)]
        internal string Platform { get; set; }
    }
}
