// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>
// <summary> Property description class for the XamlTaskFactory parser. </summary>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using System.IO;
using Microsoft.Build.Shared;
using Microsoft.Build.Utilities;

namespace Microsoft.Build.Tasks.Xaml
{
    /// <summary>
    /// Class describing the relationship between switches.
    /// </summary>
    internal class SwitchRelations
    {
        private string _switchValue;
        private string _status;
        private List<string> _includedPlatforms;
        private List<string> _excludedPlatforms;
        private List<string> _conflicts;
        private List<string> _overrides;
        private List<string> _requires;
        private Dictionary<string, List<string>> _externalOverrides;
        private Dictionary<string, List<string>> _externalConflicts;
        private Dictionary<string, List<string>> _externalRequires;

        public SwitchRelations()
        {
            _switchValue = String.Empty;
            _status = String.Empty;
            _conflicts = new List<string>();
            _overrides = new List<string>();
            _requires = new List<string>();
            _includedPlatforms = new List<string>();
            _excludedPlatforms = new List<string>();
            _externalOverrides = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            _externalConflicts = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            _externalRequires = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        }

        public SwitchRelations Clone()
        {
            SwitchRelations cloned = new SwitchRelations();
            cloned._switchValue = _switchValue;
            cloned._status = _status;
            cloned._conflicts = new List<string>(_conflicts);
            cloned._overrides = new List<string>(_overrides);
            cloned._requires = new List<string>(_requires);
            cloned._excludedPlatforms = new List<string>(_excludedPlatforms);
            cloned._includedPlatforms = new List<string>(_includedPlatforms);
            cloned._externalConflicts = new Dictionary<string, List<string>>(_externalConflicts, StringComparer.OrdinalIgnoreCase);
            cloned._externalOverrides = new Dictionary<string, List<string>>(_externalOverrides, StringComparer.OrdinalIgnoreCase);
            cloned._externalRequires = new Dictionary<string, List<string>>(_externalRequires, StringComparer.OrdinalIgnoreCase);

            return cloned;
        }

        public string SwitchValue
        {
            get
            {
                return _switchValue;
            }

            set
            {
                _switchValue = value;
            }
        }

        public string Status
        {
            get
            {
                return _status;
            }

            set
            {
                _status = value;
            }
        }

        public List<string> Conflicts
        {
            get
            {
                return _conflicts;
            }

            set
            {
                _conflicts = value;
            }
        }

        public List<string> IncludedPlatforms
        {
            get
            {
                return _includedPlatforms;
            }

            set
            {
                _includedPlatforms = value;
            }
        }

        public List<string> ExcludedPlatforms
        {
            get
            {
                return _excludedPlatforms;
            }

            set
            {
                _excludedPlatforms = value;
            }
        }

        public List<string> Overrides
        {
            get
            {
                return _overrides;
            }

            set
            {
                _overrides = value;
            }
        }

        public List<string> Requires
        {
            get
            {
                return _requires;
            }

            set
            {
                _requires = value;
            }
        }

        public Dictionary<string, List<string>> ExternalOverrides
        {
            get
            {
                return _externalOverrides;
            }
            set
            {
                _externalOverrides = value;
            }
        }

        public Dictionary<string, List<string>> ExternalConflicts
        {
            get
            {
                return _externalConflicts;
            }

            set
            {
                _externalConflicts = value;
            }
        }

        public Dictionary<string, List<string>> ExternalRequires
        {
            get
            {
                return _externalRequires;
            }

            set
            {
                _externalRequires = value;
            }
        }
    }

    /// <summary>
    /// The RelationsParser class takes an xml file and parses the parameters for a task.
    /// </summary>
    internal class RelationsParser
    {
        /// <summary>
        /// The name of the task e.g., CL
        /// </summary>
        private string _name;

        /// <summary>
        /// The name of the executable e.g., cl.exe
        /// </summary>
        private string _toolName;

        /// <summary>
        /// The base class 
        /// </summary>
        private string _baseClass = "DataDrivenToolTask";

        /// <summary>
        /// The namespace to generate the class into
        /// </summary>
        private string _namespaceValue = "MyDataDrivenTasks";

        /// <summary>
        /// The resource namespace to pass to the base class, if any
        /// </summary>
        private string _resourceNamespaceValue = null;

        /// <summary>
        /// The prefix to append before a switch is emitted.
        /// Is typically a "/", but can also be a "-"
        /// </summary>
        private string _defaultPrefix = "/";

        /// <summary>
        /// The list that contains all of the properties that can be set on a task
        /// </summary>
        private LinkedList<Property> _properties = new LinkedList<Property>();

        /// <summary>
        /// The list that contains all of the properties that can be set on a task
        /// </summary>
        private Dictionary<string, SwitchRelations> _switchRelationsList = new Dictionary<string, SwitchRelations>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// The list that contains all of the properties that have a default value
        /// </summary>
        private LinkedList<Property> _defaultSet = new LinkedList<Property>();

        /// <summary>
        /// The list of properties that serve as fallbacks for other properties.
        /// That is, if a certain property is not set, but has a fallback, we need to check
        /// to see if that fallback is set.
        /// </summary>
        private Dictionary<string, string> _fallbackSet = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// A boolean to see if the current file parsed is an import file.
        /// </summary>
        private bool _isImport;

        /// <summary>
        /// The number of errors that occurred while parsing the xml file or generating the code
        /// </summary>
        private int _errorCount;

        /// <summary>
        /// The errors that occurred while parsing the xml file or generating the code
        /// </summary>
        private LinkedList<string> _errorLog = new LinkedList<string>();

        #region Private const strings
        private const string xmlNamespace = "http://schemas.microsoft.com/developer/msbuild/tasks/2005";
        private const string toolNameString = "TOOLNAME";
        private const string prefixString = "PREFIX";
        private const string baseClassAttribute = "BASECLASS";
        private const string namespaceAttribute = "NAMESPACE";
        private const string resourceNamespaceAttribute = "RESOURCENAMESPACE";
        private const string importType = "IMPORT";
        private const string tasksAttribute = "TASKS";
        private const string parameterType = "PARAMETER";
        private const string parameterGroupType = "PARAMETERGROUP";
        private const string enumType = "VALUE";
        private const string task = "TASK";
        private const string nameProperty = "NAME";
        private const string status = "STATUS";
        private const string switchName = "SWITCH";
        private const string reverseSwitchName = "REVERSESWITCH";
        private const string oldName = "OLDNAME";
        private const string argumentType = "ARGUMENT";
        private const string argumentValueName = "ARGUMENTVALUE";
        private const string relations = "RELATIONS";
        private const string switchGroupType = "SWITCHGROUP";
        private const string switchType = "SWITCH";
        private const string includedPlatformType = "INCLUDEDPLATFORM";
        private const string excludedPlatformType = "EXCLUDEDPLATFORM";
        private const string overridesType = "OVERRIDES";
        private const string conflictsType = "CONFLICTS";
        private const string requiresType = "REQUIRES";
        private const string externalOverridesType = "EXTERNALOVERRIDES";
        private const string externalConflictsType = "EXTERNALCONFLICTS";
        private const string externalRequiresType = "EXTERNALREQUIRES";
        private const string toolAttribute = "TOOL";
        private const string switchAttribute = "SWITCH";

        // properties
        private const string typeProperty = "TYPE";
        private const string typeAlways = "ALWAYS";
        private const string trueProperty = "TRUE";
        private const string falseProperty = "FALSE";
        private const string minProperty = "MIN";
        private const string maxProperty = "MAX";
        private const string separatorProperty = "SEPARATOR";
        private const string defaultProperty = "DEFAULT";
        private const string fallbackProperty = "FALLBACKARGUMENTPARAMETER";
        private const string outputProperty = "OUTPUT";
        private const string argumentProperty = "ARGUMENTPARAMETER";
        private const string argumentRequiredProperty = "REQUIRED";
        private const string propertyRequiredProperty = "REQUIRED";
        private const string reversibleProperty = "REVERSIBLE";
        private const string categoryProperty = "CATEGORY";
        private const string displayNameProperty = "DISPLAYNAME";
        private const string descriptionProperty = "DESCRIPTION";
        #endregion

        /// <summary>
        /// The constructor.
        /// </summary>
        public RelationsParser()
        {
            // do nothing
        }

        #region Properties

        /// <summary>
        /// The name of the task
        /// </summary>
        public string GeneratedTaskName
        {
            get
            {
                return _name;
            }
            set
            {
                _name = value;
            }
        }

        /// <summary>
        /// The base type of the class
        /// </summary>
        public string BaseClass
        {
            get
            {
                return _baseClass;
            }
        }

        /// <summary>
        /// The namespace of the class
        /// </summary>
        public string Namespace
        {
            get
            {
                return _namespaceValue;
            }
        }

        /// <summary>
        /// Namespace for the resources
        /// </summary>
        public string ResourceNamespace
        {
            get
            {
                return _resourceNamespaceValue;
            }
        }

        /// <summary>
        /// The name of the executable
        /// </summary>
        public string ToolName
        {
            get
            {
                return _toolName;
            }
        }

        /// <summary>
        /// The default prefix for each switch
        /// </summary>
        public string DefaultPrefix
        {
            get
            {
                return _defaultPrefix;
            }
        }

        /// <summary>
        /// All of the parameters that were parsed
        /// </summary>
        public LinkedList<Property> Properties
        {
            get
            {
                return _properties;
            }
        }

        /// <summary>
        /// All of the parameters that have a default value
        /// </summary>
        public LinkedList<Property> DefaultSet
        {
            get
            {
                return _defaultSet;
            }
        }

        /// <summary>
        /// All of the properties that serve as fallbacks for unset properties
        /// </summary>
        public Dictionary<string, string> FallbackSet
        {
            get
            {
                return _fallbackSet;
            }
        }

        /// <summary>
        /// Returns the number of errors encountered
        /// </summary>
        public int ErrorCount
        {
            get
            {
                return _errorCount;
            }
        }

        /// <summary>
        /// Returns the log of errors
        /// </summary>
        public LinkedList<string> ErrorLog
        {
            get
            {
                return _errorLog;
            }
        }

        public Dictionary<string, SwitchRelations> SwitchRelationsList
        {
            get
            {
                return _switchRelationsList;
            }
        }


        #endregion

        /// <summary>
        /// The method that loads in an XML file
        /// </summary>
        /// <param name="fileName">the xml file containing switches and properties</param>
        private XmlDocument LoadFile(string fileName)
        {
            try
            {
                XmlDocument xmlDocument = new XmlDocument();
                XmlReaderSettings settings = new XmlReaderSettings();
                settings.DtdProcessing = DtdProcessing.Ignore;
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
                XmlDocument xmlDocument = new XmlDocument();
                XmlReaderSettings settings = new XmlReaderSettings();
                settings.DtdProcessing = DtdProcessing.Ignore;
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
        /// <param name="fileName"></param>
        /// <returns></returns>
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
            ErrorUtilities.VerifyThrow(xmlDocument != null, "NoXml");

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
                    _defaultPrefix = attribute.InnerText;
                }
                else if (String.Equals(attribute.Name, toolNameString, StringComparison.OrdinalIgnoreCase))
                {
                    _toolName = attribute.InnerText;
                }
                else if (String.Equals(attribute.Name, nameProperty, StringComparison.OrdinalIgnoreCase))
                {
                    _name = attribute.InnerText;
                }
                else if (String.Equals(attribute.Name, baseClassAttribute, StringComparison.OrdinalIgnoreCase))
                {
                    _baseClass = attribute.InnerText;
                }
                else if (String.Equals(attribute.Name, namespaceAttribute, StringComparison.OrdinalIgnoreCase))
                {
                    _namespaceValue = attribute.InnerText;
                }
                else if (String.Equals(attribute.Name, resourceNamespaceAttribute, StringComparison.OrdinalIgnoreCase))
                {
                    _resourceNamespaceValue = attribute.InnerText;
                }
            }
            // parse the child nodes if it has any
            if (node.HasChildNodes)
            {
                return ParseSwitchGroupOrSwitch(node.FirstChild, _switchRelationsList, null);
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
        /// <param name="node"></param>
        /// <param name="attributeName"></param>
        /// <returns></returns>
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
        /// <param name="node"></param>
        /// <returns></returns>
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

        private bool ParseSwitch(XmlNode node, Dictionary<string, SwitchRelations> switchRelationsList, SwitchRelations switchRelations)
        {
            SwitchRelations switchRelationsToAdd = ObtainAttributes(node, switchRelations);

            // make sure that the switchRelationsList has a name, unless it is type always
            if (switchRelationsToAdd.SwitchValue == null || switchRelationsToAdd.SwitchValue == String.Empty)
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
                        string Name = String.Empty;
                        string Tool = String.Empty;
                        string Switch = String.Empty;
                        bool isExternal = false;
                        foreach (XmlAttribute attrib in child.Attributes)
                        {
                            switch (attrib.Name.ToUpperInvariant())
                            {
                                case nameProperty:
                                    Name = attrib.InnerText;
                                    break;
                                case toolAttribute:
                                    isExternal = true;
                                    Tool = attrib.InnerText;
                                    break;
                                case switchAttribute:
                                    Switch = attrib.InnerText;
                                    break;
                                default:
                                    return false;
                            }
                        }

                        if (!isExternal)
                            if (Switch != String.Empty)
                                switchRelationsToAdd.Requires.Add(Switch);
                            else
                                return false;
                        else
                        {
                            if (!switchRelationsToAdd.ExternalRequires.ContainsKey(Tool))
                            {
                                List<string> switches = new List<string>();
                                switches.Add(Switch);
                                switchRelationsToAdd.ExternalRequires.Add(Tool, switches);
                            }
                            else
                            {
                                switchRelationsToAdd.ExternalRequires[Tool].Add(Switch);
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
        /// <param name="node"></param>
        /// <param name="switchGroup"></param>
        /// <returns></returns>
        private SwitchRelations ObtainAttributes(XmlNode node, SwitchRelations switchGroup)
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
        /// <param name="messageResourceName"></param>
        /// <param name="messageArgs"></param>
        private void LogError(string messageResourceName, params object[] messageArgs)
        {
            _errorLog.AddLast(ResourceUtilities.FormatResourceString(messageResourceName, messageArgs));
            _errorCount++;
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
