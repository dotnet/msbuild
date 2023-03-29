// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Razor.Language;

namespace Microsoft.NET.Sdk.Razor.Tool
{
    internal class CompositeRazorProjectFileSystem : RazorProjectFileSystem
    {
        public CompositeRazorProjectFileSystem(IReadOnlyList<RazorProjectFileSystem> fileSystems)
        {
            FileSystems = fileSystems ?? throw new ArgumentNullException(nameof(fileSystems));
        }

        public IReadOnlyList<RazorProjectFileSystem> FileSystems { get; }

        public override IEnumerable<RazorProjectItem> EnumerateItems(string basePath)
        {
            foreach (var fileSystem in FileSystems)
            {
                foreach (var result in fileSystem.EnumerateItems(basePath))
                {
                    yield return result;
                }
            }
        }

        public override RazorProjectItem GetItem(string path)
        {
            return GetItem(path, fileKind: null);
        }

        public override RazorProjectItem GetItem(string path, string fileKind)
        {
            RazorProjectItem razorProjectItem = null;
            foreach (var fileSystem in FileSystems)
            {
                razorProjectItem = fileSystem.GetItem(path, fileKind);
                if (razorProjectItem != null && razorProjectItem.Exists)
                {
                    return razorProjectItem;
                }
            }

            return razorProjectItem;
        }
    }
}
