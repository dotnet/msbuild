// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Globalization;
using System.Runtime.InteropServices;

namespace Microsoft.Build.Tasks.Deployment.Bootstrapper
{
    /// <summary>
    /// A buildable version of a Product.  Used for the BootstrapperBuilder's Build method.
    /// </summary>
    public class ProductBuilder : IProductBuilder
    {
        private Product _product;
        private string _culture;

        internal ProductBuilder(Product product)
        {
            _product = product;
            _culture = string.Empty;
        }

        internal ProductBuilder(Product product, string culture)
        {
            _product = product;
            _culture = culture;
        }

        /// <summary>
        /// The Product corresponding to this ProductBuilder
        /// </summary>
        public Product Product
        {
            get { return _product; }
        }

        internal string Name
        {
            get { return _product.Name; }
        }

        internal string ProductCode
        {
            get { return _product.ProductCode; }
        }
    }
}
