// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

///--------------------------------------------------------------------------------------------
/// ParametersFile.cs
///
/// Implements using through MSDeploy's API
/// returned from the flavored client project.
///
/// Copyright(c) 2006 Microsoft Corporation
///--------------------------------------------------------------------------------------------
namespace Microsoft.NET.Sdk.Publish.Tasks.MsDeploy
{
    using System.IO;
    using Framework = Microsoft.Build.Framework;
    using Utilities = Microsoft.Build.Utilities;
    using Xml = System.Xml;

    public class CreateManifestFile : Utilities.Task
    {
        private Framework.ITaskItem[] m_manifests = null;
        private string m_manifestFile = null;
        private bool m_generateFileEvenIfEmpty = false;

        [Framework.Required]
        public Framework.ITaskItem[] Manifests
        {
            get { return m_manifests; }
            set { m_manifests = value; }
        }

        [Framework.Required]
        public string ManifestFile
        {
            get { return m_manifestFile; }
            set { m_manifestFile = value; }
        }


        public bool GenerateFileEvenIfEmpty
        {
            get { return m_generateFileEvenIfEmpty; }
            set { m_generateFileEvenIfEmpty = value; }
        }

        /// <summary>
        /// utility function to write the simple setParameter.xml file
        /// </summary>
        /// <param name="loggingHelper"></param>
        /// <param name="parameters"></param>
        /// <param name="outputFileName"></param>
        private static void WriteManifestsToFile(Utilities.TaskLoggingHelper loggingHelper, Framework.ITaskItem[] items, string outputFileName)
        {
            Xml.XmlDocument document = new();
            Xml.XmlElement manifestElement = document.CreateElement("sitemanifest");
            document.AppendChild(manifestElement);
            if (items != null)
            {
                foreach (Framework.ITaskItem item in items)
                {
                    string name = item.ItemSpec;
                    Xml.XmlElement providerElement = document.CreateElement(name);
                    string path = item.GetMetadata("Path");
                    providerElement.SetAttribute("path", path);

                    string additionProviderSetting = item.GetMetadata("AdditionalProviderSettings");
                    if (!string.IsNullOrEmpty(additionProviderSetting))
                    {
                        string[] providerSettings = additionProviderSetting.Split(';');
                        foreach (string ps in providerSettings)
                        {
                            string value = item.GetMetadata(ps);
                            if (!string.IsNullOrEmpty(value))
                            {
                                providerElement.SetAttribute(ps, value);
                            }
                        }
                    }
                    manifestElement.AppendChild(providerElement);
                }
            }

            // Save the UTF8 and Indented 
            Utility.SaveDocument(document, outputFileName, Encoding.UTF8);
        }

        /// <summary>
        /// The task execute function
        /// </summary>
        /// <returns></returns>
        public override bool Execute()
        {
            bool succeeded = true;

            bool fWriteFile = GenerateFileEvenIfEmpty;
            if (m_manifests != null && m_manifests.GetLength(0) > 0)
            {
                fWriteFile = true;
            }

            if (fWriteFile)
            {
                try
                {
                    if (!string.IsNullOrEmpty(ManifestFile))
                    {
                        if (!File.Exists(ManifestFile))
                        {
                            File.Create(ManifestFile);
                        }
                        WriteManifestsToFile(Log, m_manifests, ManifestFile);
                    }
                }
#if NET472
                catch (System.Xml.XmlException ex)
                {
                    System.Uri sourceUri = new(ex.SourceUri);
                    succeeded = false;
                }
#endif
                catch (System.Exception)
                {
                    succeeded = false;
                }
                finally
                {
                    //logger.EndSection(string.Format(System.Globalization.CultureInfo.CurrentCulture,succeeded ?
                    //    Resources.BUILDTASK_TransformXml_TransformationSucceeded :
                    //    Resources.BUILDTASK_TransformXml_TransformationFailed));
                }
            }
            return succeeded;
        }
    }



}
