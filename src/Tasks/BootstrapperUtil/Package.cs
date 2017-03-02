// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Xml;
using System.Runtime.InteropServices;

namespace Microsoft.Build.Tasks.Deployment.Bootstrapper
{
    [ComVisible(false)]
    internal class Package
    {
        private string _name;
        private string _culture;
        private Product _product;
        private XmlNode _node;
        private XmlValidationResults _validationResults;

        public Package(Product product, XmlNode node, XmlValidationResults validationResults, string name, string culture)
        {
            _product = product;
            _node = node;
            _name = name;
            _culture = culture;
            _validationResults = validationResults;
        }

        internal XmlNode Node
        {
            get { return _node; }
        }

        public string Name
        {
            get { return _name; }
        }

        public string Culture
        {
            get { return _culture; }
        }

        public Product Product
        {
            get { return _product; }
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

        internal XmlValidationResults ValidationResults
        {
            get { return _validationResults; }
        }
    }
}
