// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.Build.Tasks.Deployment.Bootstrapper
{
    /// <summary>
    /// A buildable version of a Product.  Used for the BootstrapperBuilder's Build method.
    /// </summary>
    public class ProductBuilder : IProductBuilder
    {
        internal ProductBuilder(Product product)
        {
            Product = product;
        }

        /// <summary>
        /// The Product corresponding to this ProductBuilder
        /// </summary>
        public Product Product { get; }

        internal string Name => Product.Name;

        internal string ProductCode => Product.ProductCode;
    }
}
