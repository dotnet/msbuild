// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Tools.Sdk.Check
{
    public class ProductCollectionProvider : IProductCollectionProvider
    {
        public ProductCollection GetProductCollection()
        {
            try
            {
                return ProductCollection.GetAsync().Result;
            }
            catch (Exception e)
            {
                throw new GracefulException(string.Format(LocalizableStrings.ReleasesLibraryFailed, e.Message));
            }
        }

        public IEnumerable<ProductRelease> GetProductReleases(Deployment.DotNet.Releases.Product product)
        {
            try
            {
                return product.GetReleasesAsync().Result;
            }
            catch (Exception e)
            {
                throw new GracefulException(string.Format(LocalizableStrings.ReleasesLibraryFailed, e.Message));
            }
        }
    }
}
