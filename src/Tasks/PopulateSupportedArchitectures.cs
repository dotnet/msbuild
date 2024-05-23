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
    public sealed class PopulateSupportedArchitectures : TaskExtension
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
        /// Path to the existing application manifest.
        /// </summary>
        public string? ApplicationManifestPath { get; set; }

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

        private Stream? GetManifestStream()
        {
            if (!string.IsNullOrEmpty(ApplicationManifestPath))
            {
                if (!File.Exists(ApplicationManifestPath))
                {
                    Log.LogErrorWithCodeFromResources("PopulateSupportedArchitectures.SpecifiedApplicationManifestCanNotBeFound", ApplicationManifestPath);
                    return null;
                }

                return File.OpenRead(ApplicationManifestPath);
            }

            string? defaultManifestPath = ToolLocationHelper.GetPathToDotNetFrameworkFile(DefaultManifestName, TargetDotNetFrameworkVersion.Version46);

            // The logic for getting default manifest is similar to the one from Roslyn:
            // If Roslyn logic returns null, we fall back to reading embedded manifest.
            return defaultManifestPath is null
                    ? typeof(PopulateSupportedArchitectures).Assembly.GetManifestResourceStream($"Microsoft.Build.Tasks.Resources.{DefaultManifestName}")
                    : File.OpenRead(defaultManifestPath);
        }

        public override bool Execute()
        {
            using Stream? stream = GetManifestStream();

            if (stream is not null)
            {
                XmlDocument document = LoadManifest(stream);
                XmlNamespaceManager xmlNamespaceManager = XmlNamespaces.GetNamespaceManager(document.NameTable);

                ManifestValidationResult validationResult = ValidateManifest(document, xmlNamespaceManager);

                switch (validationResult)
                {
                    case ManifestValidationResult.Success:
                        PopulateSupportedArchitecturesElement(document, xmlNamespaceManager);
                        SaveManifest(document, Path.GetFileName(ApplicationManifestPath) ?? DefaultManifestName);
                        return true;
                    case ManifestValidationResult.SupportedArchitecturesExists:
                        return true;
                    default:
                        return false;
                }
            }

            return false;
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
            using (XmlWriter xmlWriter = XmlWriter.Create(ManifestPath, new XmlWriterSettings { Indent = true, Encoding = Encoding.UTF8 }))
            {
                document.Save(xmlWriter);
            }
        }

        private ManifestValidationResult ValidateManifest(XmlDocument document, XmlNamespaceManager xmlNamespaceManager)
        {
            if (string.IsNullOrEmpty(ApplicationManifestPath))
            {
                return ManifestValidationResult.Success;
            }

            XmlNode assemblyNode = document.SelectSingleNode(XPaths.assemblyElement, xmlNamespaceManager)
                ?? throw new InvalidOperationException(ResourceUtilities.GetResourceString("PopulateSupportedArchitectures.AssemblyNodeIsMissed"));

            if (assemblyNode != null)
            {
                XmlNode? supportedArchitecturesNode = assemblyNode.SelectSingleNode($"//*[local-name()='{supportedArchitectures}']", xmlNamespaceManager);
                if (supportedArchitecturesNode != null)
                {
                    if (!string.Equals(supportedArchitecturesNode.InnerText.Trim(), SupportedArchitectures, StringComparison.OrdinalIgnoreCase))
                    {
                        Log.LogErrorWithCodeFromResources("PopulateSupportedArchitectures.InvalidValueInSupportedArchitectures", supportedArchitecturesNode.InnerText);

                        return ManifestValidationResult.Failure;
                    }

                    return ManifestValidationResult.SupportedArchitecturesExists;
                }

                return ManifestValidationResult.Success;
            }

            return ManifestValidationResult.Failure;
        }

        private void PopulateSupportedArchitecturesElement(XmlDocument document, XmlNamespaceManager xmlNamespaceManager)
        {
            XmlNode? assemblyNode = document.SelectSingleNode(XPaths.assemblyElement, xmlNamespaceManager);
            (XmlElement appNode, bool appNodeExisted) = GetOrCreateXmlElement(document, xmlNamespaceManager, application, asmv3Prefix, XmlNamespaces.asmv3);
            (XmlElement winSettingsNode, bool winSettingsNodeExisted) = GetOrCreateXmlElement(document, xmlNamespaceManager, windowsSettings, asmv3Prefix, XmlNamespaces.asmv3);
            if (string.IsNullOrEmpty(winSettingsNode.GetAttribute(XMakeAttributes.xmlns)))
            {
                winSettingsNode.SetAttribute(XMakeAttributes.xmlns, WindowsSettingsNamespace);
            }

            (XmlElement supportedArchitecturesNode, _) = GetOrCreateXmlElement(document, xmlNamespaceManager, supportedArchitectures, namespaceURI: WindowsSettingsNamespace);
            supportedArchitecturesNode.InnerText = SupportedArchitectures;
            winSettingsNode.AppendChild(supportedArchitecturesNode);

            // the null check prevents nodemoving it if already present in manifest. 
            if (!winSettingsNodeExisted)
            {
                appNode.AppendChild(winSettingsNode);
            }

            if (!appNodeExisted)
            {
                assemblyNode!.AppendChild(appNode);
            }
        }

        private (XmlElement Element, bool IsExist) GetOrCreateXmlElement(XmlDocument document, XmlNamespaceManager xmlNamespaceManager, string localName, string prefix = "", string namespaceURI = "")
        {
            bool isPrefixed = !string.IsNullOrEmpty(prefix);

            XmlNode? existingNode = isPrefixed
                ? document.SelectSingleNode($"//{prefix}:{localName}", xmlNamespaceManager)
                : document.SelectSingleNode($"//{localName}", xmlNamespaceManager);

            if (existingNode is XmlElement element)
            {
                return (element, true);
            }

            return isPrefixed
                ? (document.CreateElement(prefix, localName, namespaceURI), false)
                : (document.CreateElement(localName, namespaceURI), false);
        }
    }
}
