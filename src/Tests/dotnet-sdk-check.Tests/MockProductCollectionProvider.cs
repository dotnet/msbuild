// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.IO;
using Microsoft.Deployment.DotNet.Releases;

namespace Microsoft.DotNet.Tools.Sdk.Check
{
    public class MockProductCollectionProvider : IProductCollectionProvider
    {
        private readonly string _path;

        public MockProductCollectionProvider(string path)
        {
            _path = path;
        }

        public ProductCollection GetProductCollection()
        {
            return ProductCollection.GetFromFileAsync(Path.Combine(_path, "releases-index.json"), false).Result;
        }

        public IEnumerable<ProductRelease> GetProductReleases(Product product)
        {
            return product.GetReleasesAsync(Path.Combine(_path, product.ProductVersion, "releases.json"), false).Result;
        }
    }
}
