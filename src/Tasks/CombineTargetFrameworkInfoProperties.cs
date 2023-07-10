// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Xml.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

#nullable disable

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
        private string _rootElementName;

        /// <summary>
        /// Gets or sets the root element name to use for the generated XML string
        /// </summary>
        public string RootElementName
        {
            get
            {
                if (!UseAttributeForTargetFrameworkInfoPropertyNames)
                {
                    ErrorUtilities.VerifyThrowArgumentLength(_rootElementName, nameof(RootElementName));
                }
                else
                {
                    ErrorUtilities.VerifyThrowArgumentNull(_rootElementName, nameof(RootElementName));
                }
                return _rootElementName;
            }

            set => _rootElementName = value;
        }

        /// <summary>
        /// Items to include in the XML.  The ItemSpec should be the property name, and it should have Value metadata for its value.
        /// </summary>
        public ITaskItem[] PropertiesAndValues { get; set; }

        /// <summary>
        /// Opts into or out of using the new schema with Property Name=... rather than just specifying the RootElementName.
        /// </summary>
        public bool UseAttributeForTargetFrameworkInfoPropertyNames { get; set; } = false;

        /// <summary>
        /// The generated XML representation of the properties and values.
        /// </summary>
        [Output]
        public string Result { get; set; }

        public override bool Execute()
        {
            if (PropertiesAndValues != null)
            {
                if (!UseAttributeForTargetFrameworkInfoPropertyNames)
                {
                    ErrorUtilities.VerifyThrowArgumentLength(_rootElementName, nameof(RootElementName));
                }
                else
                {
                    ErrorUtilities.VerifyThrowArgumentNull(_rootElementName, nameof(RootElementName));
                }
                XElement root = UseAttributeForTargetFrameworkInfoPropertyNames ?
                    new("TargetFramework", new XAttribute("Name", EscapingUtilities.Escape(_rootElementName))) :
                    new(_rootElementName);

                foreach (ITaskItem item in PropertiesAndValues)
                {
                    root.Add(new XElement(item.ItemSpec, item.GetMetadata("Value")));
                }

                Result = root.ToString();
            }
            return !Log.HasLoggedErrors;
        }
    }
}
