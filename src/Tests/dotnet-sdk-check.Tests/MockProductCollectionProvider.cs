// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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

        public ProductCollection GetProductCollection(Uri uri = null, string filePath = null)
        {
            return ProductCollection.GetFromFileAsync(Path.Combine(_path, "releases-index.json"), false).Result;
        }

        public IEnumerable<ProductRelease> GetProductReleases(Product product)
        {
            return product.GetReleasesAsync(Path.Combine(_path, product.ProductVersion, "releases.json"), false).Result;
        }
    }
}
