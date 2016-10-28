// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.DotNet.Internal.ProjectModel.Compilation
{
    internal class LibraryResourceAssembly
    {
        public LibraryResourceAssembly(LibraryAsset asset, string locale)
        {
            Asset = asset;
            Locale = locale;
        }

        public LibraryAsset Asset { get; }

        public string Locale { get; }
    }
}