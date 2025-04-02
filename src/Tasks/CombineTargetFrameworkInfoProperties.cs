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
        public string RootElementName { get; set; }
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
                if ((!UseAttributeForTargetFrameworkInfoPropertyNames && string.IsNullOrEmpty(RootElementName)) || (UseAttributeForTargetFrameworkInfoPropertyNames && RootElementName == null))
                {
                    string resource = UseAttributeForTargetFrameworkInfoPropertyNames ? "CombineTargetFrameworkInfoProperties.NotNullRootElementName" : "CombineTargetFrameworkInfoProperties.NotNullAndEmptyRootElementName";
                    Log.LogErrorWithCodeFromResources(resource, nameof(RootElementName), nameof(UseAttributeForTargetFrameworkInfoPropertyNames));
                }
                else
                {
                    XElement root = UseAttributeForTargetFrameworkInfoPropertyNames ?
                        new("TargetFramework", new XAttribute("Name", EscapingUtilities.Escape(RootElementName))) :
                        new(RootElementName);

                    foreach (ITaskItem item in PropertiesAndValues)
                    {
                        root.Add(new XElement(item.ItemSpec, item.GetMetadata("Value")));
                    }

                    Result = root.ToString();
                }
            }
            return !Log.HasLoggedErrors;
        }
    }
}
