// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.TestFramework
{
    public class TestAssets
    {
        private DirectoryInfo _root;

        public TestAssets(DirectoryInfo assetsRoot)
        {
            if (!assetsRoot.Exists)
            {
                throw new DirectoryNotFoundException($"Directory not found at '{assetsRoot}'");
            }

            _root = assetsRoot;
        }

        public TestAssetInfo Get(string name)
        {
            return Get(TestAssetKinds.TestProjects, name);
        }

        public TestAssetInfo Get(string kind, string name)
        {
            var assetDirectory = new DirectoryInfo(Path.Combine(_root.FullName, kind, name));

            return new TestAssetInfo(assetDirectory, name);
        }
    }
}
