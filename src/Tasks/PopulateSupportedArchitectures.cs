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

        public string? ApplicationManifestPath { get; set; }

        [Required]
        public string OutputPath
        {
            get => _outputPath;
            set => _outputPath = value ?? throw new ArgumentNullException(nameof(OutputPath));
        }

        [Output]
        public string ManifestPath
        {
            get => _generatedManifestFullPath;
            private set => _generatedManifestFullPath = value;
        }

        public override bool Execute()
        {
            bool success = false;

            if (!string.IsNullOrEmpty(PathToManifest))
            {
                XmlDocument document = LoadManifest(PathToManifest);
                XmlNamespaceManager xmlNamespaceManager = XmlNamespaces.GetNamespaceManager(document.NameTable);

                if (!string.IsNullOrEmpty(ApplicationManifestPath) && !IsExistingManifestValid(document, xmlNamespaceManager))
                {
                    return false;
                }

                PopulateSupportedArchitecturesElement(document, xmlNamespaceManager);

                _generatedManifestFullPath = Path.Combine(OutputPath, Path.GetFileName(PathToManifest));
                SaveManifest(document, _generatedManifestFullPath);

                success = true;
            }

            return success;
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

        private void SaveManifest(XmlDocument document, string outputFilePath)
        {
            using (XmlWriter xmlWriter = XmlWriter.Create(outputFilePath, new XmlWriterSettings { Indent = true, Encoding = Encoding.UTF8 }))
            {
                document.Save(xmlWriter);
            }
        }

        private bool IsExistingManifestValid(XmlDocument document, XmlNamespaceManager xmlNamespaceManager)
        {
            bool isValid = false;

            XmlNode? assemblyNode = document.SelectSingleNode(XPaths.assemblyElement, xmlNamespaceManager);
            if (assemblyNode != null)
            {
                XmlNode? supportedArchitecturesNode = assemblyNode.SelectSingleNode($"//*[local-name()='{supportedArchitectures}']", xmlNamespaceManager);
                if (supportedArchitecturesNode != null && !String.Equals(supportedArchitecturesNode.InnerText.Trim(), SupportedArchitecturesValue, StringComparison.OrdinalIgnoreCase))
                {
                    Log.LogErrorWithCodeFromResources("PopulateSupportedArchitectures.InvalidValueInSupportedArchitectures", supportedArchitecturesNode.InnerText);

                    return isValid;
                }

                isValid = true;
            }

            return isValid;
        }

        private string PathToManifest => string.IsNullOrEmpty(ApplicationManifestPath) || !File.Exists(ApplicationManifestPath)
                ? ToolLocationHelper.GetPathToDotNetFrameworkFile(DefaultManifestName, TargetDotNetFrameworkVersion.Latest)
                : ApplicationManifestPath ?? string.Empty;

        private void PopulateSupportedArchitecturesElement(XmlDocument document, XmlNamespaceManager xmlNamespaceManager)
        {
            XmlNode? assemblyNode = document.SelectSingleNode(XPaths.assemblyElement, xmlNamespaceManager)
                ?? throw new InvalidOperationException(ResourceUtilities.GetResourceString("PopulateSupportedArchitectures.AssemblyNodeIsMissed"));

            XmlNode appNode = GetOrCreateXmlElement(document , xmlNamespaceManager, "application", asmv3Prefix, XmlNamespaces.asmv3);
            XmlElement winSettingsNode = GetOrCreateXmlElement(document, xmlNamespaceManager, windowsSettings, asmv3Prefix, XmlNamespaces.asmv3);
            if (string.IsNullOrEmpty(winSettingsNode.GetAttribute(XMakeAttributes.xmlns)))
            {
                winSettingsNode.SetAttribute(XMakeAttributes.xmlns, WindowsSettingsNamespace);
            }

            XmlNode supportedArchitecturesNode = GetOrCreateXmlElement(document, xmlNamespaceManager, supportedArchitectures, namespaceURI: WindowsSettingsNamespace);
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
    }
}
