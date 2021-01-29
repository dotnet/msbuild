// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Combines items that represent properties and values into an XML representation.
    /// </summary>
    public class CombineTargetFrameworkInfoProperties : TaskExtension
    {
        /// <summary>
        /// The root element name to use for the generated XML string
        /// </summary>
        public string RootElementName { get; set; }

        /// <summary>
        /// Items to include in the XML.  The ItemSpec should be the property name, and it should have Value metadata for its value.
        /// </summary>
        public ITaskItem[] PropertiesAndValues { get; set; }

        /// <summary>
        /// The generated XML representation of the properties and values.
        /// </summary>
        [Output]
        public string Result { get; set; }

        public override bool Execute()
        {
            if (PropertiesAndValues != null)
            {
                XElement root = new XElement(RootElementName);

                foreach (var item in PropertiesAndValues)
                {
                    root.Add(new XElement(item.ItemSpec, item.GetMetadata("Value")));
                }

                Result = root.ToString();
            }
            return !Log.HasLoggedErrors;
        }
    }
}
