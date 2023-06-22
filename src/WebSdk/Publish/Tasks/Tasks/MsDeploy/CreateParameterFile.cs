// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Sdk.Publish.Tasks.MsDeploy
{
    using Microsoft.Build.Utilities;
    using System.IO;
    using Framework = Microsoft.Build.Framework;
    using Utilities = Microsoft.Build.Utilities;
    using Xml = System.Xml;
    using System.Text.RegularExpressions;

    public class CreateParameterFile : Task
    {
        private Framework.ITaskItem[] m_parameters = null;
        private string m_declareParametersFile = null;
        private string m_declareSetParametersFile = null;
        private string m_setParametersFile = null;
        private bool m_generateFileEvenIfEmpty = false;
        private bool m_includeDefaultValue = false;

        [Framework.Required]
        public Framework.ITaskItem[] Parameters
        {
            get { return m_parameters; }
            set { m_parameters = value; }
        }

        public string DeclareParameterFile
        {
            get { return m_declareParametersFile; }
            set { m_declareParametersFile = value; }
        }

        public string DeclareSetParameterFile
        {
            get { return m_declareSetParametersFile; }
            set { m_declareSetParametersFile = value; }
        }

        public string SetParameterFile
        {
            get { return m_setParametersFile; }
            set { m_setParametersFile = value; }
        }

        public bool OptimisticParameterDefaultValue { get; set; }


        public bool GenerateFileEvenIfEmpty
        {
            get { return m_generateFileEvenIfEmpty; }
            set { m_generateFileEvenIfEmpty = value; }
        }

        public bool IncludeDefaultValue
        {
            get { return m_includeDefaultValue; }
            set { m_includeDefaultValue = value; }
        }

        // MSDeploy is very case sensitive -- Do not change the case on the following string
        private static readonly string[] s_parameterAttributes = {DeclareParameterMetadata.Description.ToString().ToLowerInvariant(),
                                                                     "defaultValue",
                                                                     DeclareParameterMetadata.Tags.ToString().ToLowerInvariant(), };
        private static readonly string[] s_setParameterAttributes = {   SyncParameterMetadata.Description.ToString().ToLowerInvariant(),
                                                                        SyncParameterMetadata.Value.ToString().ToLowerInvariant(),
                                                                        SyncParameterMetadata.Tags.ToString().ToLowerInvariant(),};
        private static readonly string[] s_parameterEntryIdentities = { ExistingParameterValiationMetadata.Element.ToString().ToLowerInvariant(),
                                                                          ExistingDeclareParameterMetadata.Kind.ToString().ToLowerInvariant(),
                                                                          ExistingDeclareParameterMetadata.Scope.ToString().ToLowerInvariant(),
                                                                          ExistingDeclareParameterMetadata.Match.ToString().ToLowerInvariant(),};

        private static readonly string[] s_parameterValidationIdentities = {  ExistingParameterValiationMetadata.Element.ToString().ToLowerInvariant(),
                                                                           ExistingParameterValiationMetadata.Kind.ToString().ToLowerInvariant(),
                                                                          "validationString",};

        /// <summary>
        /// utility class to write the declare parameter.xml file
        /// </summary>
        /// <param name="loggingHelper"></param>
        /// <param name="parameters"></param>
        /// <param name="outputFileName"></param>
        private static void WriteDeclareParametersToFile(Utilities.TaskLoggingHelper loggingHelper, Framework.ITaskItem[] parameters, string outputFileName, bool foptimisticParameterDefaultValue)
        {
            WriteDeclareParametersToFile(loggingHelper, parameters, s_parameterAttributes, outputFileName, foptimisticParameterDefaultValue, DeclareParameterMetadata.DefaultValue.ToString());
        }

        private static void WriteDeclareSetParametersToFile(Utilities.TaskLoggingHelper loggingHelper, Framework.ITaskItem[] parameters, string outputFileName, bool foptimisticParameterDefaultValue)
        {
            WriteDeclareParametersToFile(loggingHelper, parameters, s_setParameterAttributes, outputFileName, foptimisticParameterDefaultValue, SyncParameterMetadata.Value.ToString());
        }


        private static void WriteDeclareParametersToFile(Utilities.TaskLoggingHelper loggingHelper,
                                                         Framework.ITaskItem[] parameters,
                                                         string[] parameterAttributes,
                                                         string outputFileName,
                                                         bool foptimisticParameterDefaultValue,
                                                         string optimisticParameterMetadata)
        {
            Xml.XmlDocument document = new System.Xml.XmlDocument();
            Xml.XmlElement parametersElement = document.CreateElement("parameters");
            document.AppendChild(parametersElement);

            if (parameters != null)
            {
                System.Collections.Generic.Dictionary<string, Xml.XmlElement> dictionaryLookup
                    = new System.Collections.Generic.Dictionary<string, Xml.XmlElement>(parameters.GetLength(0), System.StringComparer.OrdinalIgnoreCase);

                // we are on purpose to keep the order without optimistic change the Value/Default base on the non-null optimistic
                System.Collections.Generic.IList<Framework.ITaskItem> items
                    = Utility.SortParametersTaskItems(parameters, foptimisticParameterDefaultValue, optimisticParameterMetadata);

                foreach (Framework.ITaskItem item in items)
                {
                    string name = item.ItemSpec;
                    Xml.XmlElement parameterElement = null;
                    bool fCreateNew = false;
                    if (!dictionaryLookup.TryGetValue(name, out parameterElement))
                    {
                        fCreateNew = true;
                        parameterElement = document.CreateElement("parameter");
                        parameterElement.SetAttribute("name", name);
                        foreach (string attributeName in parameterAttributes)
                        {
                            string value = item.GetMetadata(attributeName);
                            parameterElement.SetAttribute(attributeName, value);
                        }
                        dictionaryLookup.Add(name, parameterElement);
                        parametersElement.AppendChild(parameterElement);
                    }
                    if (parameterElement != null)
                    {
                        string elementValue = item.GetMetadata(ExistingParameterValiationMetadata.Element.ToString());
                        if (string.IsNullOrEmpty(elementValue))
                            elementValue = "parameterEntry";

                        string[] parameterIdentities = s_parameterEntryIdentities;

                        if (string.Compare(elementValue, "parameterEntry", System.StringComparison.OrdinalIgnoreCase) == 0)
                        {
                            parameterIdentities = s_parameterEntryIdentities;
                        }
                        else if (string.Compare(elementValue, "parameterValidation", System.StringComparison.OrdinalIgnoreCase) == 0)
                        {
                            parameterIdentities = s_parameterValidationIdentities;
                        }

                        // from all existing node, if the parameter Entry is identical, we should not create a new one
                        int parameterIdentitiesCount = parameterIdentities.GetLength(0);
                        string[] identityValues = new string[parameterIdentitiesCount];
                        identityValues[0] = elementValue;

                        for (int i = 1; i < parameterIdentitiesCount; i++)
                        {
                            identityValues[i] = item.GetMetadata(parameterIdentities[i]);
                            if (string.Equals(parameterIdentities[i], ExistingDeclareParameterMetadata.Match.ToString().ToLowerInvariant()))
                            {
                                string metadataValue = item.GetMetadata(parameterIdentities[i]);

                                if (!string.IsNullOrEmpty(metadataValue)
                                    && (Directory.Exists(metadataValue)
                                    || File.Exists(metadataValue)))
                                {
                                    metadataValue = $"^{Regex.Escape(metadataValue)}$";
                                }

                                identityValues[i] = metadataValue;
                            }
                        }

                        if (!fCreateNew)
                        {
                            bool fIdentical = false;
                            foreach (Xml.XmlNode childNode in parameterElement.ChildNodes)
                            {
                                Xml.XmlElement childElement = childNode as Xml.XmlElement;
                                if (childElement != null)
                                {
                                    if (string.Compare(childElement.Name, identityValues[0], System.StringComparison.OrdinalIgnoreCase) == 0)
                                    {
                                        fIdentical = true;
                                        for (int i = 1; i < parameterIdentitiesCount; i++)
                                        {
                                            // case sensitive comparesion  should be O.K.
                                            if (string.CompareOrdinal(identityValues[i], childElement.GetAttribute(parameterIdentities[i])) != 0)
                                            {
                                                fIdentical = false;
                                                break;
                                            }
                                        }
                                        if (fIdentical)
                                            break;
                                    }
                                }
                            }
                            if (fIdentical)
                            {
                                // same ParameterEntry, skip this item 
                                continue;
                            }
                        }

                        bool fAddEntry = false;
                        for (int i = 1; i < parameterIdentitiesCount; i++)
                        {
                            fAddEntry |= !string.IsNullOrEmpty(identityValues[i]);
                        }
                        if (fAddEntry)
                        {
                            Xml.XmlElement parameterEntry = document.CreateElement(identityValues[0]);
                            for (int i = 1; i < parameterIdentitiesCount; i++)
                            {
                                string attributeName = parameterIdentities[i];
                                string value = identityValues[i];
                                if (!string.IsNullOrEmpty(value))
                                    parameterEntry.SetAttribute(attributeName, value);
                            }
                            parameterElement.AppendChild(parameterEntry);
                        }
                    }
                }
            }

            // Save the UTF8 and Indented 
            Utility.SaveDocument(document, outputFileName, System.Text.Encoding.UTF8);
        }

        /// <summary>
        /// utility function to write the simple setParameter.xml file
        /// </summary>
        /// <param name="loggingHelper"></param>
        /// <param name="parameters"></param>
        /// <param name="outputFileName"></param>
        private static void WriteSetParametersToFile(Utilities.TaskLoggingHelper loggingHelper, Framework.ITaskItem[] parameters, string outputFileName, bool foptimisticParameterDefaultValue)
        {
            Xml.XmlDocument document = new System.Xml.XmlDocument();
            Xml.XmlElement parametersElement = document.CreateElement("parameters");
            document.AppendChild(parametersElement);
            if (parameters != null)
            {
                System.Collections.Generic.IList<Framework.ITaskItem> items
                    = Utility.SortParametersTaskItems(parameters, foptimisticParameterDefaultValue, SimpleSyncParameterMetadata.Value.ToString());

                // only the first value win
                System.Collections.Generic.Dictionary<string, Xml.XmlElement> dictionaryLookup
                    = new System.Collections.Generic.Dictionary<string, Xml.XmlElement>(parameters.GetLength(0));

                foreach (Framework.ITaskItem item in items)
                {
                    string name = item.ItemSpec;
                    if (!dictionaryLookup.ContainsKey(name))
                    {
                        Xml.XmlElement parameterElement = document.CreateElement("setParameter");
                        parameterElement.SetAttribute("name", name);
                        string value = item.GetMetadata("value");
                        parameterElement.SetAttribute("value", value);
                        dictionaryLookup.Add(name, parameterElement);
                        parametersElement.AppendChild(parameterElement);
                    }
                }
            }

            // Save the UTF8 and Indented 
            Utility.SaveDocument(document, outputFileName, System.Text.Encoding.UTF8);
        }

        /// <summary>
        /// The task execute function
        /// </summary>
        /// <returns></returns>
        public override bool Execute()
        {
            bool succeeded = true;

            bool fWriteFile = GenerateFileEvenIfEmpty;
            if (m_parameters != null && m_parameters.GetLength(0) > 0)
            {
                fWriteFile = true;
            }

            if (fWriteFile)
            {
                try
                {
                    if (!File.Exists(DeclareSetParameterFile))
                    {
                        File.Create(DeclareSetParameterFile);
                    }

                    if (!string.IsNullOrEmpty(DeclareParameterFile))
                    {
                        WriteDeclareParametersToFile(Log, m_parameters, DeclareParameterFile, OptimisticParameterDefaultValue);
                    }
                    if (!string.IsNullOrEmpty(SetParameterFile))
                    {
                        WriteSetParametersToFile(Log, m_parameters, SetParameterFile, OptimisticParameterDefaultValue);
                    }

                    if (!string.IsNullOrEmpty(DeclareSetParameterFile))
                    {
                        if (IncludeDefaultValue)
                        {
                            WriteDeclareSetParametersToFile(Log, m_parameters, DeclareSetParameterFile, true /*OptimisticParameterDefaultValue */);
                        }
                        else
                        {
                            WriteDeclareSetParametersToFile(Log, m_parameters, DeclareSetParameterFile, OptimisticParameterDefaultValue);
                        }
                    }
                }
#if NET472
                catch (System.Xml.XmlException ex)
                {
                    System.Uri sourceUri = new System.Uri(ex.SourceUri);
                    succeeded = false;
                }
#endif
                catch (System.Exception)
                {
                    succeeded = false;
                }
            }
            return succeeded;
        }
    }
}
