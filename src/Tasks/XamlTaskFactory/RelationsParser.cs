// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>
// <summary> Property description class for the XamlTaskFactory parser. </summary>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Xml;
using System.IO;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Tasks.Xaml
{
    /// <summary>
    /// Class describing the relationship between switches.
    /// </summary>
    internal class SwitchRelations
    {
        public SwitchRelations()
        {
            SwitchValue = String.Empty;
            Status = String.Empty;
            Conflicts = new List<string>();
            Overrides = new List<string>();
            Requires = new List<string>();
            IncludedPlatforms = new List<string>();
            ExcludedPlatforms = new List<string>();
            ExternalOverrides = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            ExternalConflicts = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            ExternalRequires = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        }

        public SwitchRelations Clone()
        {
            var cloned = new SwitchRelations
            {
                SwitchValue = SwitchValue,
                Status = Status,
                Conflicts = new List<string>(Conflicts),
                Overrides = new List<string>(Overrides),
                Requires = new List<string>(Requires),
                ExcludedPlatforms = new List<string>(ExcludedPlatforms),
                IncludedPlatforms = new List<string>(IncludedPlatforms),
                ExternalConflicts = new Dictionary<string, List<string>>(
                    ExternalConflicts,
                    StringComparer.OrdinalIgnoreCase),
                ExternalOverrides = new Dictionary<string, List<string>>(
                    ExternalOverrides,
                    StringComparer.OrdinalIgnoreCase),
                ExternalRequires = new Dictionary<string, List<string>>(
                    ExternalRequires,
                    StringComparer.OrdinalIgnoreCase)
            };

            return cloned;
        }

        public string SwitchValue { get; set; }

        public string Status { get; set; }

        public List<string> Conflicts { get; set; }

        public List<string> IncludedPlatforms { get; set; }

        public List<string> ExcludedPlatforms { get; set; }

        public List<string> Overrides { get; set; }

        public List<string> Requires { get; set; }

        public Dictionary<string, List<string>> ExternalOverrides { get; set; }

        public Dictionary<string, List<string>> ExternalConflicts { get; set; }

        public Dictionary<string, List<string>> ExternalRequires { get; set; }
    }

    /// <summary>
    /// The RelationsParser class takes an xml file and parses the parameters for a task.
    /// </summary>
    internal class RelationsParser
    {
        /// <summary>
        /// A boolean to see if the current file parsed is an import file.
        /// </summary>
        private bool _isImport;

        #region Private const strings
        private const string xmlNamespace = "http://schemas.microsoft.com/developer/msbuild/tasks/2005";
        private const string toolNameString = "TOOLNAME";
        private const string prefixString = "PREFIX";
        private const string baseClassAttribute = "BASECLASS";
        private const string namespaceAttribute = "NAMESPACE";
        private const string resourceNamespaceAttribute = "RESOURCENAMESPACE";
        private const string importType = "IMPORT";
        private const string tasksAttribute = "TASKS";
        private const string task = "TASK";
        private const string nameProperty = "NAME";
        private const string status = "STATUS";
        private const string switchName = "SWITCH";
        private const string argumentValueName = "ARGUMENTVALUE";
        private const string relations = "RELATIONS";
        private const string switchGroupType = "SWITCHGROUP";
        private const string switchType = "SWITCH";
        private const string includedPlatformType = "INCLUDEDPLATFORM";
        private const string excludedPlatformType = "EXCLUDEDPLATFORM";
        private const string overridesType = "OVERRIDES";
        private const string requiresType = "REQUIRES";
        private const string toolAttribute = "TOOL";
        private const string switchAttribute = "SWITCH";

        #endregion
        
        #region Properties

        /// <summary>
        /// The name of the task
        /// </summary>
        public string GeneratedTaskName { get; set; }

        /// <summary>
        /// The base type of the class
        /// </summary>
        public string BaseClass { get; private set; } = "DataDrivenToolTask";

        /// <summary>
        /// The namespace of the class
        /// </summary>
        public string Namespace { get; private set; } = "MyDataDrivenTasks";

        /// <summary>
        /// Namespace for the resources
        /// </summary>
        public string ResourceNamespace { get; private set; }

        /// <summary>
        /// The name of the executable
        /// </summary>
        public string ToolName { get; private set; }

        /// <summary>
        /// The default prefix for each switch
        /// </summary>
        public string DefaultPrefix { get; private set; } = "/";

        /// <summary>
        /// All of the parameters that were parsed
        /// </summary>
        public LinkedList<Property> Properties { get; } = new LinkedList<Property>();

        /// <summary>
        /// All of the parameters that have a default value
        /// </summary>
        public LinkedList<Property> DefaultSet { get; } = new LinkedList<Property>();

        /// <summary>
        /// All of the properties that serve as fallbacks for unset properties
        /// </summary>
        public Dictionary<string, string> FallbackSet { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Returns the number of errors encountered
        /// </summary>
        public int ErrorCount { get; private set; }

        /// <summary>
        /// Returns the log of errors
        /// </summary>
        public LinkedList<string> ErrorLog { get; } = new LinkedList<string>();

        public Dictionary<string, SwitchRelations> SwitchRelationsList { get; } = new Dictionary<string, SwitchRelations>(StringComparer.OrdinalIgnoreCase);

        #endregion

        /// <summary>
        /// The method that loads in an XML file
        /// </summary>
        /// <param name="fileName">the xml file containing switches and properties</param>
        private XmlDocument LoadFile(string fileName)
        {
            try
            {
                var xmlDocument = new XmlDocument();
                XmlReaderSettings settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore };
                XmlReader reader = XmlReader.Create(fileName, settings);
                xmlDocument.Load(reader);
                return xmlDocument;
            }
            catch (FileNotFoundException e)
            {
                LogError("LoadFailed", e.ToString());
                return null;
            }
            catch (XmlException e)
            {
                LogError("XmlError", e.ToString());
                return null;
            }
        }

        /// <summary>
        /// Overloaded method that reads from a stream to load.
        /// </summary>
        /// <param name="xml">the xml file containing switches and properties</param>
        internal XmlDocument LoadXml(string xml)
        {
            try
            {
                var xmlDocument = new XmlDocument();
                XmlReaderSettings settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore };
                XmlReader reader = XmlReader.Create(new StringReader(xml), settings);
                xmlDocument.Load(reader);
                return xmlDocument;
            }
            catch (XmlException e)
            {
                LogError("XmlError", e.ToString());
                return null;
            }
        }

        /// <summary>
        /// Parses the xml file
        /// </summary>
        public bool ParseXmlDocument(string fileName)
        {
            XmlDocument xmlDocument = LoadFile(fileName);
            if (xmlDocument != null)
            {
                return ParseXmlDocument(xmlDocument);
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Parses the loaded xml file, creates toolSwitches and adds them to the properties list
        /// </summary>
        internal bool ParseXmlDocument(XmlDocument xmlDocument)
        {
            ErrorUtilities.VerifyThrow(xmlDocument != null, nameof(xmlDocument));

            // find the root element
            XmlNode node = xmlDocument.FirstChild;
            while (!IsXmlRootElement(node))
            {
                node = node.NextSibling;
            }

            // now we know that we've found the root; verify it is the task element
            // verify the namespace
            if (String.IsNullOrEmpty(node.NamespaceURI) || !String.Equals(node.NamespaceURI, xmlNamespace, StringComparison.OrdinalIgnoreCase))
            {
                LogError("InvalidNamespace", xmlNamespace);
                return false;
            }

            // verify that the element name is "task"
            if (!VerifyNodeName(node))
            {
                LogError("MissingRootElement", relations);
                return false;
            }
            else if (!VerifyAttributeExists(node, nameProperty) && !_isImport)
            {
                // we must have the name attribute if it not an import
                LogError("MissingAttribute", task, nameProperty);
                return false;
            }
            // TODO verify resource namespace exists

            // we now know that that there is indeed a name attribute
            // assign prefix, toolname if they exist
            foreach (XmlAttribute attribute in node.Attributes)
            {
                if (String.Equals(attribute.Name, prefixString, StringComparison.OrdinalIgnoreCase))
                {
                    DefaultPrefix = attribute.InnerText;
                }
                else if (String.Equals(attribute.Name, toolNameString, StringComparison.OrdinalIgnoreCase))
                {
                    ToolName = attribute.InnerText;
                }
                else if (String.Equals(attribute.Name, nameProperty, StringComparison.OrdinalIgnoreCase))
                {
                    GeneratedTaskName = attribute.InnerText;
                }
                else if (String.Equals(attribute.Name, baseClassAttribute, StringComparison.OrdinalIgnoreCase))
                {
                    BaseClass = attribute.InnerText;
                }
                else if (String.Equals(attribute.Name, namespaceAttribute, StringComparison.OrdinalIgnoreCase))
                {
                    Namespace = attribute.InnerText;
                }
                else if (String.Equals(attribute.Name, resourceNamespaceAttribute, StringComparison.OrdinalIgnoreCase))
                {
                    ResourceNamespace = attribute.InnerText;
                }
            }
            // parse the child nodes if it has any
            if (node.HasChildNodes)
            {
                return ParseSwitchGroupOrSwitch(node.FirstChild, SwitchRelationsList, null);
            }
            else
            {
                LogError("NoChildren");
                return false;
            }
        }

        /// <summary>
        /// Checks to see if the "name" attribute exists
        /// </summary>
        private static bool VerifyAttributeExists(XmlNode node, string attributeName)
        {
            if (node.Attributes != null)
            {
                foreach (XmlAttribute attribute in node.Attributes)
                {
                    if (attribute.Name.Equals(attributeName, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Checks to see if the element's name is "task"
        /// </summary>
        private static bool VerifyNodeName(XmlNode node)
        {
            return String.Equals(node.Name, relations, StringComparison.OrdinalIgnoreCase);
        }

        private bool ParseSwitchGroupOrSwitch(XmlNode node, Dictionary<string, SwitchRelations> switchRelationsList, SwitchRelations switchRelations)
        {
            while (node != null)
            {
                if (node.NodeType == XmlNodeType.Element)
                {
                    // if the node's name is <ParameterGroup> get all the attributes
                    if (String.Equals(node.Name, switchGroupType, StringComparison.OrdinalIgnoreCase))
                    {
                        SwitchRelations newSwitchRelations = ObtainAttributes(node, switchRelations);
                        if (!ParseSwitchGroupOrSwitch(node.FirstChild, switchRelationsList, newSwitchRelations))
                        {
                            return false;
                        }
                    }
                    else if (String.Equals(node.Name, switchType, StringComparison.OrdinalIgnoreCase))
                    {
                        // node is a switchRelations
                        if (!ParseSwitch(node, switchRelationsList, switchRelations))
                        {
                            return false;
                        }
                    }
                    else if (String.Equals(node.Name, importType, StringComparison.OrdinalIgnoreCase))
                    {
                        // node is an import option
                        if (!ParseImportOption(node))
                        {
                            return false;
                        }
                    }
                }
                node = node.NextSibling;
            }
            return true;
        }

        private bool ParseImportOption(XmlNode node)
        {
            if (!VerifyAttributeExists(node, tasksAttribute))
            {
                LogError("MissingAttribute", importType, tasksAttribute);
                return false;
            }
            else
            {
                // we now know there is a tasks attribute
                string[] importTasks = null;
                foreach (XmlAttribute attribute in node.Attributes)
                {
                    if (String.Equals(attribute.Name, tasksAttribute, StringComparison.OrdinalIgnoreCase))
                    {
                        importTasks = attribute.InnerText.Split(';');
                    }
                }
                _isImport = true;
                foreach (string task in importTasks)
                {
                    if (!ParseXmlDocument(task))
                    {
                        return false;
                    }
                }
                _isImport = false;
            }
            return true;
        }

        private static bool ParseSwitch(XmlNode node, Dictionary<string, SwitchRelations> switchRelationsList, SwitchRelations switchRelations)
        {
            SwitchRelations switchRelationsToAdd = ObtainAttributes(node, switchRelations);

            // make sure that the switchRelationsList has a name, unless it is type always
            if (string.IsNullOrEmpty(switchRelationsToAdd.SwitchValue))
            {
                return false;
            }

            // generate the list of parameters in order
            if (!switchRelationsList.ContainsKey(switchRelationsToAdd.SwitchValue))
            {
                switchRelationsList.Remove(switchRelationsToAdd.SwitchValue);
            }

            // build the dependencies and the values for a parameter
            XmlNode child = node.FirstChild;
            while (child != null)
            {
                if (child.NodeType == XmlNodeType.Element)
                {
                    if (String.Equals(child.Name, requiresType, StringComparison.OrdinalIgnoreCase))
                    {
                        string tool = String.Empty;
                        string Switch = String.Empty;
                        bool isExternal = false;
                        foreach (XmlAttribute attrib in child.Attributes)
                        {
                            switch (attrib.Name.ToUpperInvariant())
                            {
                                case nameProperty:
                                    break;
                                case toolAttribute:
                                    isExternal = true;
                                    tool = attrib.InnerText;
                                    break;
                                case switchAttribute:
                                    Switch = attrib.InnerText;
                                    break;
                                default:
                                    return false;
                            }
                        }

                        if (!isExternal)
                        {
                            if (Switch != String.Empty)
                            {
                                switchRelationsToAdd.Requires.Add(Switch);
                            }
                            else
                            {
                                return false;
                            }
                        }
                        else
                        {
                            if (!switchRelationsToAdd.ExternalRequires.ContainsKey(tool))
                            {
                                var switches = new List<string> { Switch };
                                switchRelationsToAdd.ExternalRequires.Add(tool, switches);
                            }
                            else
                            {
                                switchRelationsToAdd.ExternalRequires[tool].Add(Switch);
                            }
                        }
                    }

                    else if (String.Equals(child.Name, includedPlatformType, StringComparison.OrdinalIgnoreCase))
                    {
                        foreach (XmlAttribute attrib in child.Attributes)
                        {
                            switch (attrib.Name.ToUpperInvariant())
                            {
                                case nameProperty:
                                    switchRelationsToAdd.IncludedPlatforms.Add(attrib.InnerText);
                                    break;
                                default:
                                    return false;
                            }
                        }
                    }
                    else if (String.Equals(child.Name, excludedPlatformType, StringComparison.OrdinalIgnoreCase))
                    {
                        foreach (XmlAttribute attrib in child.Attributes)
                        {
                            switch (attrib.Name.ToUpperInvariant())
                            {
                                case nameProperty:
                                    switchRelationsToAdd.ExcludedPlatforms.Add(attrib.InnerText);
                                    break;
                                default:
                                    return false;
                            }
                        }
                    }
                    else if (String.Equals(child.Name, overridesType, StringComparison.OrdinalIgnoreCase))
                    {
                        foreach (XmlAttribute attrib in child.Attributes)
                        {
                            switch (attrib.Name.ToUpperInvariant())
                            {
                                case switchName:
                                    switchRelationsToAdd.Overrides.Add(attrib.InnerText);
                                    break;
                                case argumentValueName:
                                    break;
                                default:
                                    return false;
                            }
                        }
                    }
                }
                child = child.NextSibling;
            }

            // We've read any enumerated values and any dependencies, so we just 
            // have to add the switchRelations
            switchRelationsList.Add(switchRelationsToAdd.SwitchValue, switchRelationsToAdd);
            return true;
        }

        /// <summary>
        /// Gets all the attributes assigned in the xml file for this parameter or all of the nested switches for 
        /// this parameter group
        /// </summary>
        private static SwitchRelations ObtainAttributes(XmlNode node, SwitchRelations switchGroup)
        {
            SwitchRelations switchRelations;
            if (switchGroup != null)
            {
                switchRelations = switchGroup.Clone();
            }
            else
            {
                switchRelations = new SwitchRelations();
            }
            foreach (XmlAttribute attribute in node.Attributes)
            {
                // do case-insensitive comparison
                switch (attribute.Name.ToUpperInvariant())
                {
                    case nameProperty:
                        switchRelations.SwitchValue = attribute.InnerText;
                        break;
                    case status:
                        switchRelations.Status = attribute.InnerText;
                        break;
                    default:
                        //LogError("InvalidAttribute", attribute.Name);
                        break;
                }
            }
            return switchRelations;
        }

        /// <summary>
        /// Increases the error count by 1, and logs the error message
        /// </summary>
        private void LogError(string messageResourceName, params object[] messageArgs)
        {
            ErrorLog.AddLast(ResourceUtilities.FormatResourceString(messageResourceName, messageArgs));
            ErrorCount++;
        }

        /// <summary>
        /// An XML document can have many root nodes, but usually we want the single root 
        /// element. Callers can test each root node in turn with this method, until it returns
        /// true.
        /// </summary>
        /// <param name="node">Candidate root node</param>
        /// <returns>true if node is the root element</returns>
        private static bool IsXmlRootElement(XmlNode node)
        {
            // "A Document node can have the following child node types: XmlDeclaration,
            // Element (maximum of one), ProcessingInstruction, Comment, and DocumentType."
            return (
                   (node.NodeType != XmlNodeType.Comment) &&
                   (node.NodeType != XmlNodeType.Whitespace) &&
                   (node.NodeType != XmlNodeType.XmlDeclaration) &&
                   (node.NodeType != XmlNodeType.ProcessingInstruction) &&
                   (node.NodeType != XmlNodeType.DocumentType)
                   );
        }
    }
}
