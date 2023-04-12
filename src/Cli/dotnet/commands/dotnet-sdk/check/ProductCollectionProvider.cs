// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Tools.Sdk.Check
{
    public class ProductCollectionProvider : IProductCollectionProvider
    {
        public ProductCollection GetProductCollection(Uri uri = null, string filePath = null)
        {
            try
            {
                return uri != null ? Task.Run(() => ProductCollection.GetAsync(uri.ToString())).Result :
                    filePath != null ? Task.Run(() => ProductCollection.GetFromFileAsync(filePath, false)).Result :
                    Task.Run(() => ProductCollection.GetAsync()).Result;
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
