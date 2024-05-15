// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Runtime.Versioning;
using System.Text;
using System.Xml;

using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Tasks.Deployment.ManifestUtilities;
using Microsoft.Build.Utilities;

#nullable enable

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Generates an application manifest or adds an entry to the existing one when PreferNativeArm64 property is true.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public sealed class PopulateSupportedArchitectures : TaskExtension
    {
        private const string supportedArchitectures = "supportedArchitectures";
        private const string windowsSettings = "windowsSettings";
        private const string SupportedArchitecturesValue = "amd64 arm64";
        private const string asmv3Prefix = "asmv3";
        private const string DefaultManifestName = "default.win32manifest";
        private const string WindowsSettingsNamespace = "http://schemas.microsoft.com/SMI/2024/WindowsSettings";

        private string _outputPath = string.Empty;
        private string _generatedManifestFullPath = string.Empty;

        /// <summary>
        /// Path to the existing application manifest.
        /// </summary>
        public string? ApplicationManifestPath { get; set; }

        /// <summary>
        /// Intermediate output path.
        /// </summary>
        [Required]
        public string OutputPath
        {
            get => _outputPath;
            set => _outputPath = value ?? throw new ArgumentNullException(nameof(OutputPath));
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

        public override bool Execute()
        {
            if (!string.IsNullOrEmpty(PathToManifest))
            {
                XmlDocument document = LoadManifest(PathToManifest);
                XmlNamespaceManager xmlNamespaceManager = XmlNamespaces.GetNamespaceManager(document.NameTable);

                ManifestValidationResult validationResult = ValidateManifest(document, xmlNamespaceManager);

                switch (validationResult)
                {
                    case ManifestValidationResult.Success:
                        PopulateSupportedArchitecturesElement(document, xmlNamespaceManager);
                        SaveManifest(document);
                        return true;
                    case ManifestValidationResult.SupportedArchitecturesExists:
                        return true;
                    default:
                        return false;
                }
            }

            return false;
        }

        private XmlDocument LoadManifest(string path)
        {
            XmlDocument document = new XmlDocument();
            using (FileStream fs = File.OpenRead(path))
            using (XmlReader xr = XmlReader.Create(fs, new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, CloseInput = true }))
            {
                document.Load(xr);
            }

            return document;
        }

        private void SaveManifest(XmlDocument document)
        {
            ManifestPath = Path.Combine(OutputPath, Path.GetFileName(PathToManifest));

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

            XmlNode? assemblyNode = document.SelectSingleNode(XPaths.assemblyElement, xmlNamespaceManager);
            if (assemblyNode != null)
            {
                XmlNode? supportedArchitecturesNode = assemblyNode.SelectSingleNode($"//*[local-name()='{supportedArchitectures}']", xmlNamespaceManager);
                if (supportedArchitecturesNode != null)
                {
                    if (!string.Equals(supportedArchitecturesNode.InnerText.Trim(), SupportedArchitecturesValue, StringComparison.OrdinalIgnoreCase))
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

        private string PathToManifest => string.IsNullOrEmpty(ApplicationManifestPath) || !File.Exists(ApplicationManifestPath)
                ? ToolLocationHelper.GetPathToDotNetFrameworkFile(DefaultManifestName, TargetDotNetFrameworkVersion.Latest)
                : ApplicationManifestPath ?? string.Empty;

        private void PopulateSupportedArchitecturesElement(XmlDocument document, XmlNamespaceManager xmlNamespaceManager)
        {
            XmlNode assemblyNode = document.SelectSingleNode(XPaths.assemblyElement, xmlNamespaceManager)
                ?? throw new InvalidOperationException(ResourceUtilities.GetResourceString("PopulateSupportedArchitectures.AssemblyNodeIsMissed"));

            XmlElement appNode = GetOrCreateXmlElement(document , xmlNamespaceManager, "application", asmv3Prefix, XmlNamespaces.asmv3);
            XmlElement winSettingsNode = GetOrCreateXmlElement(document, xmlNamespaceManager, windowsSettings, asmv3Prefix, XmlNamespaces.asmv3);
            if (string.IsNullOrEmpty(winSettingsNode.GetAttribute(XMakeAttributes.xmlns)))
            {
                winSettingsNode.SetAttribute(XMakeAttributes.xmlns, WindowsSettingsNamespace);
            }

            XmlElement supportedArchitecturesNode = GetOrCreateXmlElement(document, xmlNamespaceManager, supportedArchitectures, namespaceURI: WindowsSettingsNamespace);
            supportedArchitecturesNode.InnerText = SupportedArchitecturesValue;
            winSettingsNode.AppendChild(supportedArchitecturesNode);
            appNode.AppendChild(winSettingsNode);
            assemblyNode.AppendChild(appNode);
        }

        private XmlElement GetOrCreateXmlElement(XmlDocument document, XmlNamespaceManager xmlNamespaceManager, string localName, string prefix = "", string namespaceURI = "")
        {
            bool isPrefixed = !string.IsNullOrEmpty(prefix);

            XmlNode? existingNode = isPrefixed
                ? document.SelectSingleNode($"//{prefix}:{localName}", xmlNamespaceManager)
                : document.SelectSingleNode($"//{localName}", xmlNamespaceManager);

            if (existingNode is not null and XmlElement element)
            {
                return element;
            }

            return isPrefixed
                ? document.CreateElement(prefix, localName, namespaceURI)
                : document.CreateElement(localName, namespaceURI);
        }

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
    }
}
