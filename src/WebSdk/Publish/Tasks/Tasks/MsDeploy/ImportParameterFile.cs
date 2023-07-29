// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Sdk.Publish.Tasks.Tasks.MsDeploy
{
    using Microsoft.Build.Utilities;
    using Framework = Build.Framework;
    using Utilities = Build.Utilities;
    using Xml = System.Xml;
    using System.Diagnostics;
    using Microsoft.NET.Sdk.Publish.Tasks.MsDeploy;
    using Microsoft.Web.XmlTransform;
    using Microsoft.NET.Sdk.Publish.Tasks.Xdt;
    using System.IO;
    using Microsoft.NET.Sdk.Publish.Tasks.Properties;

    public class ImportParameterFile : Task
    {
        private Framework.ITaskItem[] m_sourceFiles = null;
        private List<Framework.ITaskItem> m_parametersList = new List<Framework.ITaskItem>(8);

        [Framework.Required]
        public Framework.ITaskItem[] Files
        {
            get { return m_sourceFiles; }
            set { m_sourceFiles = value; }
        }

        [Framework.Output]
        public Framework.ITaskItem[] Result
        {
            get { return this.m_parametersList.ToArray(); }
        }


        public bool DisableEscapeMSBuildVariable
        {
            get;
            set;
        }

        /// <summary>
        /// Utility function to pare the top level of the element
        /// </summary>
        /// <param name="element"></param>
        private void ReadParametersElement(Xml.XmlElement element)
        {
            Debug.Assert(element != null);
            if (string.Compare(element.Name, "parameters", System.StringComparison.OrdinalIgnoreCase) == 0)
            {
                foreach (Xml.XmlNode childNode in element.ChildNodes)
                {
                    Xml.XmlElement childElement = childNode as Xml.XmlElement;
                    if (childElement != null)
                    {
                        ReadParameterElement(childElement);
                    }
                }
            }
        }

        /// <summary>
        /// Parse the Parameter element
        /// </summary>
        /// <param name="element"></param>
        private void ReadParameterElement(Xml.XmlElement element)
        {
            Debug.Assert(element != null);
            if (string.Compare(element.Name, "parameter", System.StringComparison.OrdinalIgnoreCase) == 0)
            {
                Xml.XmlAttribute nameAttribute = element.Attributes.GetNamedItem("name") as Xml.XmlAttribute;
                if (nameAttribute != null)
                {
                    Utilities.TaskItem taskItem = new Microsoft.Build.Utilities.TaskItem(nameAttribute.Value);
                    foreach (Xml.XmlNode attribute in element.Attributes)
                    {
                        string attributeName = attribute.Name.ToLower(System.Globalization.CultureInfo.InvariantCulture);
                        if (string.CompareOrdinal(attributeName, "xmlns") == 0
                            || attribute.Name.StartsWith("xmlns:", System.StringComparison.Ordinal)
                            || string.CompareOrdinal(attributeName, "name") == 0
                            )
                        {
                            continue;
                        }
                        string value = DisableEscapeMSBuildVariable ? attribute.Value : Utility.EscapeTextForMSBuildVariable(attribute.Value);
                        taskItem.SetMetadata(attribute.Name, value);
                    }
                    // work around the MSDeploy.exe limition of the Parameter must have the ParameterEntry.
                    // m_parametersList.Add(taskItem);
                    bool fAddNoParameterEntryParameter = true;

                    foreach (Xml.XmlNode childNode in element.ChildNodes)
                    {
                        Xml.XmlElement childElement = childNode as Xml.XmlElement;
                        if (childElement != null)
                        {
                            Utilities.TaskItem childEntry = ReadParameterEntryElement(childElement, taskItem);
                            if (childEntry != null)
                            {
                                fAddNoParameterEntryParameter = false; // we have Parameter entry, supress adding the Parameter with no entry
                                m_parametersList.Add(childEntry);
                            }
                        }
                    }

                    if (fAddNoParameterEntryParameter)
                    {
                        // finally add a parameter without any entry
                        m_parametersList.Add(taskItem);
                    }
                }
            }
        }

        /// <summary>
        /// Helper function to parse the ParameterEntryElement
        /// </summary>
        /// <param name="element"></param>
        /// <param name="parentItem"></param>
        /// <returns></returns>
        private Utilities.TaskItem ReadParameterEntryElement(Xml.XmlElement element, Utilities.TaskItem parentItem)
        {
            Debug.Assert(element != null && parentItem != null);
            Utilities.TaskItem taskItem = null;
            if (string.Compare(element.Name, "parameterEntry", System.StringComparison.OrdinalIgnoreCase) == 0)
            {
                taskItem = new Microsoft.Build.Utilities.TaskItem(parentItem);
                taskItem.RemoveMetadata("OriginalItemSpec");
                foreach (Xml.XmlNode attribute in element.Attributes)
                {
                    if (attribute != null && attribute.Name != null && attribute.Value != null)
                    {
                        string value = DisableEscapeMSBuildVariable ? attribute.Value : Utility.EscapeTextForMSBuildVariable(attribute.Value);
                        taskItem.SetMetadata(attribute.Name, value);
                    }
                }
            }
            else if (string.Compare(element.Name, "parameterValidation", System.StringComparison.OrdinalIgnoreCase) == 0)
            {
                taskItem = new Microsoft.Build.Utilities.TaskItem(parentItem);
                taskItem.RemoveMetadata("OriginalItemSpec");
                taskItem.SetMetadata("Element", "parameterValidation");
                foreach (Xml.XmlNode attribute in element.Attributes)
                {
                    if (attribute != null && attribute.Name != null && attribute.Value != null)
                    {
                        string value = DisableEscapeMSBuildVariable ? attribute.Value : Utility.EscapeTextForMSBuildVariable(attribute.Value);
                        taskItem.SetMetadata(attribute.Name, value);
                    }
                }
            }
            return taskItem;
        }

        /// <summary>
        /// The task execute function
        /// </summary>
        /// <returns></returns>
        public override bool Execute()
        {
            bool succeeded = true;
            IXmlTransformationLogger logger = new TaskTransformationLogger(Log);

            if (m_sourceFiles != null)
            {
                try
                {
                    foreach (Framework.ITaskItem item in m_sourceFiles)
                    {
                        string filePath = item.GetMetadata("FullPath");
                        if (!File.Exists(filePath))
                        {
                            Log.LogError(Resources.BUILDTASK_TransformXml_SourceLoadFailed, new object[] { filePath });
                            succeeded = false;
                            break;
                        }
                        Xml.XmlDocument document = new System.Xml.XmlDocument();
                        document.Load(filePath);
                        foreach (Xml.XmlNode node in document.ChildNodes)
                        {
                            Xml.XmlElement element = node as Xml.XmlElement;
                            if (element != null)
                            {
                                ReadParametersElement(element);
                            }
                        }
                    }
                }
                catch (System.Xml.XmlException ex)
                {
                    System.Uri sourceUri = new System.Uri(ex.SourceUri);
                    logger.LogError(sourceUri.LocalPath, ex.LineNumber, ex.LinePosition, ex.Message);
                    succeeded = false;
                }
                catch (System.Exception ex)
                {
                    logger.LogErrorFromException(ex);
                    succeeded = false;
                }
            }
            return succeeded;
        }
    }
}
