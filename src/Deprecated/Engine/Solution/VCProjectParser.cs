// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Xml;

namespace Microsoft.Build.BuildEngine
{
	static internal class VCProjectParser
	{
        /// <summary>
        /// For a given VC project, retrieves the projects it references 
        /// </summary>
        /// <param name="projectPath"></param>
        /// <returns></returns>
        /// <owner>LukaszG</owner>
        static internal List<string> GetReferencedProjectGuids(XmlDocument project)
        {
            List<string> referencedProjectGuids = new List<string>();

            XmlNodeList referenceElements = project.DocumentElement.GetElementsByTagName("References");

            if (referenceElements.Count > 0)
            {
                foreach (XmlElement referenceElement in ((XmlElement)referenceElements[0]).GetElementsByTagName("ProjectReference"))
                {
                    string referencedProjectGuid = referenceElement.GetAttribute("ReferencedProjectIdentifier");

                    if (!string.IsNullOrEmpty(referencedProjectGuid))
                    {
                        referencedProjectGuids.Add(referencedProjectGuid);
                    }
                }
            }

            return referencedProjectGuids;
        }

        /// <summary>
        /// Is the project built as a static library for the given configuration?
        /// </summary>
        internal static bool IsStaticLibrary(XmlDocument project, string configurationName)
        {
            XmlNodeList configurationsElements = project.DocumentElement.GetElementsByTagName("Configurations");
            bool isStaticLibrary = false;

            // There should be only one configurations element
            if (configurationsElements.Count > 0)
            {
                foreach (XmlNode configurationNode in configurationsElements[0].ChildNodes)
                {
                    if (configurationNode.NodeType == XmlNodeType.Element)
                    {
                        XmlElement element = (XmlElement)configurationNode;

                        // Look for configuration that matches our name
                        if ((string.Equals(element.Name, "Configuration", StringComparison.OrdinalIgnoreCase)) &&
                            (string.Equals(element.GetAttribute("Name"), configurationName, StringComparison.OrdinalIgnoreCase)))
                        {
                            XmlElement configurationElement = element;
                            string configurationType = configurationElement.GetAttribute("ConfigurationType");
                            isStaticLibrary = (configurationType == "4");

                            // we found our configuration, nothing more to do here
                            break;
                        }
                    }
                }
            }

            return isStaticLibrary;
        }
    }
}
