// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.Deployment.DotNet.Releases;

namespace Microsoft.DotNet.Tools.Sdk.Check
{
    public interface IProductCollectionProvider
    {
        ProductCollection GetProductCollection();

        IEnumerable<ProductRelease> GetProductReleases(Product product);
    }
}
