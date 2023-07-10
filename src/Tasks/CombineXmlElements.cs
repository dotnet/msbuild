// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Xml.Linq;
using Microsoft.Build.Framework;

#nullable disable

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Combines multiple XML elements
    /// </summary>
    public class CombineXmlElements : TaskExtension
    {
        /// <summary>
        /// The root element name to use for the generated XML string
        /// </summary>
        public string RootElementName { get; set; }

        /// <summary>
        /// The XML elements to include as children of the root element
        /// </summary>
        public ITaskItem[] XmlElements { get; set; }

        /// <summary>
        /// The generated XML
        /// </summary>
        [Output]
        public string Result { get; set; }

        public override bool Execute()
        {
            if (XmlElements != null)
            {
                XElement root = new XElement(RootElementName);

                foreach (var item in XmlElements)
                {
                    root.Add(XElement.Parse(item.ItemSpec));
                }

                Result = root.ToString();
            }

            return !Log.HasLoggedErrors;
        }
    }
}
