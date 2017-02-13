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
    [ComVisible(true), GuidAttribute("532BF563-A85D-4088-8048-41F51AC5239F"), ClassInterface(ClassInterfaceType.None)]
    public class Product : IProduct
    {
        private XmlNode _node;
        private string _productCode;
        private PackageCollection _packages;
        private ProductCollection _includes;
        private List<List<Product>> _dependencies;
        private ArrayList _missingDependencies;
        private Hashtable _cultures;
        private CopyAllFilesType _copyAllPackageFiles;
        private ProductValidationResults _validationResults;

        public Product()
        {
            Debug.Fail("Products are not to be created in this fashion.  Please use IBootstrapperBuilder.Products instead.");
            throw new InvalidOperationException();
        }

        internal Product(XmlNode node, string code, ProductValidationResults validationResults, string copyAll)
        {
            _node = node;
            _packages = new PackageCollection();
            _includes = new ProductCollection();
            _dependencies = new List<List<Product>>();
            _missingDependencies = new ArrayList();
            _productCode = code;
            _validationResults = validationResults;
            _cultures = new Hashtable();
            if (copyAll == "IfNotHomeSite")
                _copyAllPackageFiles = CopyAllFilesType.CopyAllFilesIfNotHomeSite;
            else if (copyAll == "false")
                _copyAllPackageFiles = CopyAllFilesType.CopyAllFilesFalse;
            else
                _copyAllPackageFiles = CopyAllFilesType.CopyAllFilesTrue;
        }

        internal XmlNode Node
        {
            get { return _node; }
        }

        internal CopyAllFilesType CopyAllPackageFiles
        {
            get { return _copyAllPackageFiles; }
        }

        /// <summary>
        /// The ProductBuilder representation of this Product
        /// </summary>
        public ProductBuilder ProductBuilder
        {
            get { return new ProductBuilder(this); }
        }

        /// <summary>
        /// A string specifying the unique identifier of this product
        /// </summary>
        public string ProductCode
        {
            get { return _productCode; }
        }

        /// <summary>
        /// A human-readable name for this product
        /// </summary>
        public string Name
        {
            get
            {
                CultureInfo culture = Util.DefaultCultureInfo;
                Package p = _packages.Package(culture.Name);

                if (p != null)
                {
                    return p.Name;
                }

                while (culture != null && culture != CultureInfo.InvariantCulture)
                {
                    p = _packages.Package(culture.Parent.Name);

                    if (p != null)
                    {
                        return p.Name;
                    }

                    culture = culture.Parent;
                }

                if (_packages.Count > 0)
                {
                    return _packages.Item(0).Name;
                }

                return _productCode.ToString();
            }
        }

        /// <summary>
        /// All products which this product also installs
        /// </summary>
        public ProductCollection Includes
        {
            get { return _includes; }
        }

        internal List<List<Product>> Dependencies
        {
            get { return _dependencies; }
        }

        internal bool ContainsCulture(string culture)
        {
            return _cultures.Contains(culture.ToLowerInvariant());
        }

        internal bool ContainsDependencies(List<Product> dependenciesToCheck)
        {
            foreach (List<Product> d in _dependencies)
            {
                bool found = true;
                foreach (Product p in d)
                {
                    bool containedInDependencies = false;
                    foreach (Product pd in dependenciesToCheck)
                    {
                        if (p._productCode == pd._productCode)
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

        internal ArrayList MissingDependencies
        {
            get
            {
                return _missingDependencies;
            }
        }

        internal void AddPackage(Package package)
        {
            if (package == null || String.IsNullOrEmpty(package.Culture))
                throw new ArgumentNullException("package");

            if (!_cultures.Contains(package.Culture.ToLowerInvariant()))
            {
                _packages.Add(package);
                _cultures.Add(package.Culture.ToLowerInvariant(), package);
            }
            else
            {
                Debug.WriteLine(String.Format(CultureInfo.CurrentCulture, "A package with culture '{0}' has already been added to product '{1}'", package.Culture.ToLowerInvariant(), ProductCode));
            }
        }

        internal void AddIncludedProduct(Product product)
        {
            _includes.Add(product);
        }

        internal void AddDependentProduct(Product product)
        {
            List<Product> newDependency = new List<Product>();
            newDependency.Add(product);
            _dependencies.Add(newDependency);
        }

        internal void AddMissingDependency(ArrayList productCodes)
        {
            bool found = false;
            foreach (ArrayList md in _missingDependencies)
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
                _missingDependencies.Add(productCodes);
            }
        }

        internal PackageCollection Packages
        {
            get { return _packages; }
        }

        internal XmlValidationResults GetPackageValidationResults(string culture)
        {
            if (_validationResults == null)
                return null;
            return _validationResults.PackageResults(culture);
        }

        internal bool ValidationPassed
        {
            get
            {
                if (_validationResults == null)
                    return true;
                return _validationResults.ValidationPassed;
            }
        }

        internal ProductValidationResults ValidationResults
        {
            get { return _validationResults; }
        }
    }
}
