// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text;
using Microsoft.Build.Framework;

namespace Microsoft.NET.Build.Tasks
{
    public class GenerateRegFreeComManifest : TaskBase
    {
        [Required]
        public string IntermediateAssembly { get; set; }

        [Required]
        public string ComHostName { get; set; }

        [Required]
        public string ClsidMapPath { get; set; }

        [Required]
        public string ComManifestPath { get; set; }

        protected override void ExecuteCore()
        {
            RegFreeComManifest.CreateManifestFromClsidmap(
                Path.GetFileNameWithoutExtension(IntermediateAssembly),
                ComHostName,
                FileUtilities.TryGetAssemblyVersion(IntermediateAssembly).ToString(),
                ClsidMapPath,
                ComManifestPath);
        }
    }
}
