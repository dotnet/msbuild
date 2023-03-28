// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;
using System.Xml;

#nullable disable

namespace Microsoft.Build.Tasks.Deployment.Bootstrapper
{
    [ComVisible(false)]
    internal class Package
    {
        public Package(Product product, XmlNode node, XmlValidationResults validationResults, string name, string culture)
        {
            Product = product;
            Node = node;
            Name = name;
            Culture = culture;
            ValidationResults = validationResults;
        }

        internal XmlNode Node { get; }

        public string Name { get; }

        public string Culture { get; }

        public Product Product { get; }

        internal bool ValidationPassed
        {
            get
            {
                if (ValidationResults == null)
                {
                    return true;
                }
                return ValidationResults.ValidationPassed;
            }
        }

        internal XmlValidationResults ValidationResults { get; }
    }
}
