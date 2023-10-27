// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Build.Tasks.AssemblyDependency
{
    /// <summary>
    /// Collection of assembly attributes.
    /// </summary>
    internal sealed class AssemblyAttributes
    {
        public string AssemblyFullPath { get; set; } = string.Empty;

        public string AssemblyName { get; set; } = string.Empty;

        public string DefaultAlias { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public string Culture { get; set; } = string.Empty;

        public string RuntimeVersion { get; set; } = string.Empty;

        public ushort MajorVersion { get; set; }

        public ushort MinorVersion { get; set; }

        public ushort BuildNumber { get; set; }

        public ushort RevisionNumber { get; set; }

        // it is a byte[] converted to string
        public string PublicHexKey { get; set; } = string.Empty;

        public bool IsAssembly { get; set; }

        public uint PeKind { get; set; }

        public bool IsImportedFromTypeLib { get; set; }

        public string TargetFrameworkMoniker { get; set; } = string.Empty;
    }
}
