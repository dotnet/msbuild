// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Deployment.DotNet.Releases;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.NativeWrapper;
using System.Linq;

namespace Microsoft.DotNet.Tools.Sdk.Check
{
    internal class BundleOutputWriter
    {
        protected ProductCollection _productCollection;

        protected readonly IProductCollectionProvider _productCollectionProvider;

        protected readonly IReporter _reporter;

        public BundleOutputWriter(
            ProductCollection productCollection,
            IProductCollectionProvider productCollectionProvider,
            IReporter reporter)
        {
            _productCollection = productCollection;
            _productCollectionProvider = productCollectionProvider;
            _reporter = reporter;
        }

        protected bool BundleIsMaintenance(INetBundleInfo bundle)
        {
            return _productCollection
                .First(product => product.ProductVersion.Equals($"{bundle.Version.Major}.{bundle.Version.Minor}"))
                .SupportPhase.Equals(SupportPhase.Maintenance);
        }

        protected bool BundleIsEndOfLife(INetBundleInfo bundle)
        {
            return _productCollection
                .First(product => product.ProductVersion.Equals($"{bundle.Version.Major}.{bundle.Version.Minor}"))
                .IsOutOfSupport();
        }
    }
}
