// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.NativeWrapper;

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

        protected bool? BundleIsMaintenance(INetBundleInfo bundle)
        {
            return _productCollection
                .FirstOrDefault(product => product.ProductVersion.Equals($"{bundle.Version.Major}.{bundle.Version.Minor}"))
                ?.SupportPhase.Equals(SupportPhase.Maintenance);
        }

        protected bool? BundleIsEndOfLife(INetBundleInfo bundle)
        {
            return _productCollection
                .FirstOrDefault(product => product.ProductVersion.Equals($"{bundle.Version.Major}.{bundle.Version.Minor}"))
                ?.IsOutOfSupport();
        }
    }
}
