// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Text;
using System.Xml;

using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Tasks.Deployment.ManifestUtilities;
using Microsoft.Build.Utilities;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Generates an application manifest or adds an entry to the existing one when PreferNativeArm64 property is true.
    /// </summary>
    public sealed class AddToWin32Manifest : TaskExtension
    {
        private const string supportedArchitectures = "supportedArchitectures";
        private const string windowsSettings = "windowsSettings";
        private const string application = "application";
        private const string asmv3Prefix = "asmv3";
        private const string DefaultManifestName = "default.win32manifest";
        private const string WindowsSettingsNamespace = "http://schemas.microsoft.com/SMI/2024/WindowsSettings";

        private string _outputDirectory = string.Empty;
        private string _supportedArchitectures = string.Empty;
        private string _generatedManifestFullPath = string.Empty;

        /// <summary>
        /// Represents the result of validating an application manifest.
        /// </summary>
        private enum ManifestValidationResult
        {
            /// <summary>
            /// The manifest validation was successful.
            /// </summary>
            Success = 1,

            /// <summary>
            /// The manifest validation failed.
            /// </summary>
            Failure,

            /// <summary>
            /// The supported architectures exist in the manifest with the expected value.
            /// </summary>
            SupportedArchitecturesExists,
        }

        /// <summary>
        /// Existing application manifest.
        /// </summary>
        public ITaskItem? ApplicationManifest { get; set; }

        /// <summary>
        /// Intermediate output directory.
        /// </summary>
        [Required]
        public string OutputDirectory
        {
            get => _outputDirectory;
            set => _outputDirectory = value ?? throw new ArgumentNullException(nameof(OutputDirectory));
        }

        /// <summary>
        /// Value for supportedArchitectures node.
        /// </summary>
        [Required]
        public string SupportedArchitectures
        {
            get => _supportedArchitectures;
            set => _supportedArchitectures = value ?? throw new ArgumentNullException(nameof(SupportedArchitectures));
        }

        /// <summary>
        /// Returns path to the generated manifest.
        /// </summary>
        [Output]
        public string ManifestPath
        {
            get => _generatedManifestFullPath;
            private set => _generatedManifestFullPath = value;
        }

        private string? GetManifestPath()
        {
            if (ApplicationManifest != null)
            {
                if (string.IsNullOrEmpty(ApplicationManifest.ItemSpec) || !File.Exists(ApplicationManifest?.ItemSpec))
                {
                    Log.LogErrorFromResources("AddToWin32Manifest.SpecifiedApplicationManifestCanNotBeFound", ApplicationManifest?.ItemSpec);
                    return null;
                }

                return ApplicationManifest!.ItemSpec;
            }

            string? defaultManifestPath = ToolLocationHelper.GetPathToDotNetFrameworkFile(DefaultManifestName, TargetDotNetFrameworkVersion.Version46);

            return defaultManifestPath;
        }

        private Stream? GetManifestStream(string? path)
        {
            // The logic for getting default manifest is similar to the one from Roslyn:
            // If Roslyn logic returns null, we fall back to reading embedded manifest.
            return path is null
                    ? typeof(AddToWin32Manifest).Assembly.GetManifestResourceStream($"Microsoft.Build.Tasks.Resources.{DefaultManifestName}")
                    : File.OpenRead(path);
        }

        public override bool Execute()
        {
            try
            {
                string? manifestPath = GetManifestPath();
                using Stream? stream = GetManifestStream(manifestPath);

                if (stream is null)
                {
                    Log.LogErrorFromResources("AddToWin32Manifest.ManifestCanNotBeOpened");

                    return !Log.HasLoggedErrors;
                }

                XmlDocument document = LoadManifest(stream);
                XmlNamespaceManager xmlNamespaceManager = XmlNamespaces.GetNamespaceManager(document.NameTable);

                ManifestValidationResult validationResult = ValidateManifest(manifestPath, document, xmlNamespaceManager);

                switch (validationResult)
                {
                    case ManifestValidationResult.Success:
                        AddSupportedArchitecturesElement(document, xmlNamespaceManager);
                        SaveManifest(document, Path.GetFileName(ApplicationManifest?.ItemSpec) ?? DefaultManifestName);
                        return !Log.HasLoggedErrors;
                    case ManifestValidationResult.SupportedArchitecturesExists:
                        return !Log.HasLoggedErrors;
                    case ManifestValidationResult.Failure:
                        return !Log.HasLoggedErrors;
                    default:
                        return false;
                }
            }
            catch (Exception ex)
            {
                Log.LogErrorFromResources("AddToWin32Manifest.ManifestCanNotBeOpenedWithException", ex.Message);

                return !Log.HasLoggedErrors;
            }
        }

        private XmlDocument LoadManifest(Stream stream)
        {
            XmlDocument document = new XmlDocument();

            using (XmlReader xr = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, CloseInput = true }))
            {
                document.Load(xr);
            }

            return document;
        }

        private void SaveManifest(XmlDocument document, string manifestName)
        {
            ManifestPath = Path.Combine(OutputDirectory, manifestName);
            using (var xmlWriter = new XmlTextWriter(ManifestPath, Encoding.UTF8))
            {
                xmlWriter.Formatting = Formatting.Indented;
                xmlWriter.Indentation = 4;
                document.Save(xmlWriter);
            }
        }

        private ManifestValidationResult ValidateManifest(string? manifestPath, XmlDocument document, XmlNamespaceManager xmlNamespaceManager)
        {
            if (ApplicationManifest == null)
            {
                return ManifestValidationResult.Success;
            }

            XmlNode? assemblyNode = document.SelectSingleNode(XPaths.assemblyElement, xmlNamespaceManager);

            if (assemblyNode is null)
            {
                Log.LogErrorFromResources("AddToWin32Manifest.AssemblyNodeIsMissed", manifestPath);
                return ManifestValidationResult.Failure;
            }

            XmlNode? supportedArchitecturesNode = GetNode(assemblyNode, supportedArchitectures, xmlNamespaceManager);
            if (supportedArchitecturesNode != null)
            {
                if (!string.Equals(supportedArchitecturesNode.InnerText.Trim(), SupportedArchitectures, StringComparison.OrdinalIgnoreCase))
                {
                    Log.LogErrorWithCodeFromResources("AddToWin32Manifest.InvalidValueInSupportedArchitectures", supportedArchitecturesNode.InnerText, manifestPath);

                    return ManifestValidationResult.Failure;
                }

                return ManifestValidationResult.SupportedArchitecturesExists;
            }

            return ManifestValidationResult.Success;
        }

        private void AddSupportedArchitecturesElement(XmlDocument document, XmlNamespaceManager xmlNamespaceManager)
        {
            XmlNode? assemblyNode = document.SelectSingleNode(XPaths.assemblyElement, xmlNamespaceManager);
            XmlElement appNode = GetOrCreateXmlElement(document, xmlNamespaceManager, application, asmv3Prefix, XmlNamespaces.asmv3);
            XmlElement winSettingsNode = GetOrCreateXmlElement(document, xmlNamespaceManager, windowsSettings, asmv3Prefix, XmlNamespaces.asmv3);
            if (string.IsNullOrEmpty(winSettingsNode.GetAttribute(XMakeAttributes.xmlns)))
            {
                winSettingsNode.SetAttribute(XMakeAttributes.xmlns, WindowsSettingsNamespace);
            }

            XmlElement supportedArchitecturesNode = GetOrCreateXmlElement(document, xmlNamespaceManager, supportedArchitectures, namespaceURI: WindowsSettingsNamespace);
            supportedArchitecturesNode.InnerText = SupportedArchitectures;
            winSettingsNode.AppendChild(supportedArchitecturesNode);

            // If ParentNode is null, this indicates that winSettingsNode was not a part of the manifest.
            if (winSettingsNode.ParentNode == null)
            {
                appNode.AppendChild(winSettingsNode);
            }

            if (appNode.ParentNode == null)
            {
                assemblyNode!.AppendChild(appNode);
            }
        }

        private XmlElement GetOrCreateXmlElement(XmlDocument document, XmlNamespaceManager xmlNamespaceManager, string localName, string prefix = "", string namespaceURI = "")
        {
            XmlNode? existingNode = GetNode(document, localName, xmlNamespaceManager);

            if (existingNode is XmlElement element)
            {
                return element;
            }

            return !string.IsNullOrEmpty(prefix)
                ? document.CreateElement(prefix, localName, namespaceURI)
                : document.CreateElement(localName, namespaceURI);
        }

        private XmlNode? GetNode(XmlNode node, string localName, XmlNamespaceManager xmlNamespaceManager) => node.SelectSingleNode($"//*[local-name()='{localName}']", xmlNamespaceManager);
    }
}
