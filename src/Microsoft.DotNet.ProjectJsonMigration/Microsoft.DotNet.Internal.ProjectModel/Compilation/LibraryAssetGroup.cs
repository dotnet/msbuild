// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq;
using System.Collections.Generic;

namespace Microsoft.DotNet.Internal.ProjectModel.Compilation
{
    internal class LibraryAssetGroup
    {
        public LibraryAssetGroup(string runtime, params LibraryAsset[] assets) : this(runtime, (IEnumerable<LibraryAsset>)assets) { }
        public LibraryAssetGroup(params LibraryAsset[] assets) : this(string.Empty, (IEnumerable<LibraryAsset>)assets) { }
        public LibraryAssetGroup(IEnumerable<LibraryAsset> assets) : this(string.Empty, assets) { }

        public LibraryAssetGroup(string runtime,
            IEnumerable<LibraryAsset> assets)
        {
            Runtime = runtime;
            Assets = assets.ToArray();
        }

        public string Runtime { get; }

        /// <summary>
        /// Gets a list of assets provided in this runtime group
        /// </summary>
        public IReadOnlyList<LibraryAsset> Assets { get; }
    }
}