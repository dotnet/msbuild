// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Xml;
using System.Runtime.InteropServices;

namespace Microsoft.Build.Tasks.Deployment.Bootstrapper
{
    internal enum CopyAllFilesType
    {
        CopyAllFilesFalse, CopyAllFilesTrue, CopyAllFilesIfNotHomeSite
    };

    /// <summary>
    /// This class represents a product in the found by the BootstrapperBuilder in the Path property.
    /// </summary>
    [ComVisible(true), Guid("532BF563-A85D-4088-8048-41F51AC5239F"), ClassInterface(ClassInterfaceType.None)]
    public class Product : IProduct
    {
        private readonly Dictionary<string, Package> _cultures = new Dictionary<string, Package>(StringComparer.OrdinalIgnoreCase);

        public Product()
        {
            Debug.Fail("Products are not to be created in this fashion.  Please use IBootstrapperBuilder.Products instead.");
            throw new InvalidOperationException();
        }

        internal Product(XmlNode node, string code, ProductValidationResults validationResults, string copyAll)
        {
            Node = node;
            Packages = new PackageCollection();
            Includes = new ProductCollection();
            Dependencies = new List<List<Product>>();
            MissingDependencies = new List<List<string>>();
            ProductCode = code;
            ValidationResults = validationResults;
            if (copyAll == "IfNotHomeSite")
            {
                CopyAllPackageFiles = CopyAllFilesType.CopyAllFilesIfNotHomeSite;
            }
            else if (copyAll == "false")
            {
                CopyAllPackageFiles = CopyAllFilesType.CopyAllFilesFalse;
            }
            else
            {
                CopyAllPackageFiles = CopyAllFilesType.CopyAllFilesTrue;
            }
        }

        internal XmlNode Node { get; }

        internal CopyAllFilesType CopyAllPackageFiles { get; }

        /// <summary>
        /// The ProductBuilder representation of this Product
        /// </summary>
        public ProductBuilder ProductBuilder => new ProductBuilder(this);

        /// <summary>
        /// A string specifying the unique identifier of this product
        /// </summary>
        public string ProductCode { get; }

        /// <summary>
        /// A human-readable name for this product
        /// </summary>
        public string Name
        {
            get
            {
                CultureInfo culture = Util.DefaultCultureInfo;
                Package p = Packages.Package(culture.Name);

                if (p != null)
                {
                    return p.Name;
                }

                while (culture != null && culture != CultureInfo.InvariantCulture)
                {
                    p = Packages.Package(culture.Parent.Name);

                    if (p != null)
                    {
                        return p.Name;
                    }

                    culture = culture.Parent;
                }

                if (Packages.Count > 0)
                {
                    return Packages.Item(0).Name;
                }

                return ProductCode;
            }
        }

        /// <summary>
        /// All products which this product also installs
        /// </summary>
        public ProductCollection Includes { get; }

        internal List<List<Product>> Dependencies { get; }

        internal bool ContainsCulture(string culture)
        {
            return _cultures.ContainsKey(culture);
        }

        internal bool ContainsDependencies(List<Product> dependenciesToCheck)
        {
            foreach (List<Product> d in Dependencies)
            {
                bool found = true;
                foreach (Product p in d)
                {
                    bool containedInDependencies = false;
                    foreach (Product pd in dependenciesToCheck)
                    {
                        if (p.ProductCode == pd.ProductCode)
                        {
                            containedInDependencies = true;
                            break;
                        }
                    }
                    if (!containedInDependencies)
                    {
                        found = false;
                        break;
                    }
                }

                if (found)
                {
                    return true;
                }
            }

            return false;
        }

        internal List<List<string>> MissingDependencies { get; }

        internal void AddPackage(Package package)
        {
            if (String.IsNullOrEmpty(package?.Culture))
            {
                throw new ArgumentNullException(nameof(package));
            }

            if (!_cultures.ContainsKey(package.Culture))
            {
                Packages.Add(package);
                _cultures.Add(package.Culture, package);
            }
            else
            {
                Debug.WriteLine(String.Format(CultureInfo.CurrentCulture, "A package with culture '{0}' has already been added to product '{1}'", package.Culture.ToLowerInvariant(), ProductCode));
            }
        }

        internal void AddIncludedProduct(Product product)
        {
            Includes.Add(product);
        }

        internal void AddDependentProduct(Product product)
        {
            var newDependency = new List<Product> { product };
            Dependencies.Add(newDependency);
        }

        internal void AddMissingDependency(List<string> productCodes)
        {
            bool found = false;
            foreach (List<string> md in MissingDependencies)
            {
                bool hasAll = true;
                foreach (string dep in md)
                {
                    if (!productCodes.Contains(dep))
                    {
                        hasAll = false;
                        break;
                    }
                }

                if (hasAll)
                {
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                MissingDependencies.Add(productCodes);
            }
        }

        internal PackageCollection Packages { get; }

        internal XmlValidationResults GetPackageValidationResults(string culture)
        {
            return ValidationResults?.PackageResults(culture);
        }

        internal bool ValidationPassed => ValidationResults == null || ValidationResults.ValidationPassed;

        internal ProductValidationResults ValidationResults { get; }
    }
}
