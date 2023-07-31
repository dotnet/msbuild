// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Deployment.DotNet.Releases;

namespace Microsoft.DotNet.Tools.Sdk.Check
{
    public interface IProductCollectionProvider
    {
        ProductCollection GetProductCollection(Uri uri = null, string filePath = null);

        IEnumerable<ProductRelease> GetProductReleases(Product product);
    }
}
