// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Permissions;
using System.Xml;

namespace Microsoft.Build.Tasks.Deployment.ManifestUtilities
{
    /// <summary>
    /// Describes the application security trust information.
    /// </summary>
    [ComVisible(false)]
    public sealed class TrustInfo
    {
        private PermissionSet _inputPermissionSet;
        private XmlDocument _inputTrustInfoDocument;
        private bool _isFullTrust = true;
        private PermissionSet _outputPermissionSet;
        private string _sameSiteSetting = "site";
        private bool _sameSiteChanged;

        private void AddSameSiteAttribute(XmlElement permissionSetElement)
        {
            XmlAttribute sameSiteAttribute = (XmlAttribute)permissionSetElement.Attributes.GetNamedItem(XmlUtil.TrimPrefix(XPaths.sameSiteAttribute));
            if (sameSiteAttribute == null)
            {
                sameSiteAttribute = permissionSetElement.OwnerDocument.CreateAttribute(XmlUtil.TrimPrefix(XPaths.sameSiteAttribute));
                permissionSetElement.Attributes.Append(sameSiteAttribute);
            }

            sameSiteAttribute.Value = _sameSiteSetting;
        }

        /// <summary>
        /// Resets the object to its default state.
        /// </summary>
        public void Clear()
        {
            _inputPermissionSet = null;
            _inputTrustInfoDocument = null;
            _isFullTrust = true;
            _outputPermissionSet = null;
        }

        private void FixupPermissionSetElement(XmlElement permissionSetElement)
        {
            XmlDocument document = permissionSetElement.OwnerDocument;
            XmlNamespaceManager nsmgr = XmlNamespaces.GetNamespaceManager(document.NameTable);

            if (PreserveFullTrustPermissionSet)
            {
                var unrestrictedAttribute = (XmlAttribute)permissionSetElement.Attributes.GetNamedItem(XmlUtil.TrimPrefix(XPaths.unrestrictedAttribute));
                if (_isFullTrust)
                {
                    if (unrestrictedAttribute == null)
                    {
                        unrestrictedAttribute = document.CreateAttribute(XmlUtil.TrimPrefix(XPaths.unrestrictedAttribute));
                        permissionSetElement.Attributes.Append(unrestrictedAttribute);
                    }
                    unrestrictedAttribute.Value = "true";
                }
                else
                {
                    if (unrestrictedAttribute != null)
                    {
                        permissionSetElement.Attributes.RemoveNamedItem(XmlUtil.TrimPrefix(XPaths.unrestrictedAttribute));
                    }
                }
            }
            else
            {
                if (_isFullTrust)
                {
                    var unrestrictedAttribute = (XmlAttribute)permissionSetElement.Attributes.GetNamedItem(XmlUtil.TrimPrefix(XPaths.unrestrictedAttribute));
                    if (unrestrictedAttribute == null)
                    {
                        unrestrictedAttribute = document.CreateAttribute(XmlUtil.TrimPrefix(XPaths.unrestrictedAttribute));
                        permissionSetElement.Attributes.Append(unrestrictedAttribute);
                    }
                    unrestrictedAttribute.Value = "true";
                    while (permissionSetElement.FirstChild != null)
                    {
                        permissionSetElement.RemoveChild(permissionSetElement.FirstChild);
                    }
                }
            }

            // Add ID="Custom" attribute if there's not one already
            var idAttribute = (XmlAttribute)permissionSetElement.Attributes.GetNamedItem(XmlUtil.TrimPrefix(XPaths.idAttribute));
            if (idAttribute == null)
            {
                idAttribute = document.CreateAttribute(XmlUtil.TrimPrefix(XPaths.idAttribute));
                permissionSetElement.Attributes.Append(idAttribute);
            }

            if (String.IsNullOrEmpty(idAttribute.Value))
            {
                idAttribute.Value = "Custom";
            }

            AddSameSiteAttribute(permissionSetElement);

            if (permissionSetElement.ParentNode == null ||
                permissionSetElement.ParentNode.NodeType == XmlNodeType.Document)
            {
                return;
            }

            XmlAttribute idrefAttribute = null;
            XmlElement defaultAssemblyRequestElement = (XmlElement)permissionSetElement.ParentNode.SelectSingleNode(XPaths.defaultAssemblyRequestElement, nsmgr);
            if (defaultAssemblyRequestElement == null)
            {
                defaultAssemblyRequestElement = document.CreateElement(XmlUtil.TrimPrefix(XPaths.defaultAssemblyRequestElement), XmlNamespaces.asmv2);
                permissionSetElement.ParentNode.AppendChild(defaultAssemblyRequestElement);
            }
            idrefAttribute = (XmlAttribute)permissionSetElement.Attributes.GetNamedItem(XmlUtil.TrimPrefix(XPaths.permissionSetReferenceAttribute));
            if (idrefAttribute == null)
            {
                idrefAttribute = document.CreateAttribute(XmlUtil.TrimPrefix(XPaths.permissionSetReferenceAttribute));
                defaultAssemblyRequestElement.Attributes.Append(idrefAttribute);
            }

            if (String.Compare(idAttribute.Value, idrefAttribute.Value, StringComparison.Ordinal) != 0)
            {
                idrefAttribute.Value = idAttribute.Value;
            }
        }

        private PermissionSet GetInputPermissionSet()
        {
            if (_inputPermissionSet == null)
            {
                XmlElement psElement = GetInputPermissionSetElement();
                if (PreserveFullTrustPermissionSet)
                {
                    XmlAttribute unrestrictedAttribute = (XmlAttribute)psElement.Attributes.GetNamedItem(XmlUtil.TrimPrefix(XPaths.unrestrictedAttribute));
                    _isFullTrust = unrestrictedAttribute != null && Boolean.Parse(unrestrictedAttribute.Value);
                    if (_isFullTrust)
                    {
                        XmlDocument document = new XmlDocument();
                        document.AppendChild(document.ImportNode(psElement, true));
                        document.DocumentElement.Attributes.RemoveNamedItem(XmlUtil.TrimPrefix(XPaths.unrestrictedAttribute));
                        psElement = document.DocumentElement;
                    }
                    _inputPermissionSet = SecurityUtilities.XmlToPermissionSet(psElement);
                }
                else
                {
                    _inputPermissionSet = SecurityUtilities.XmlToPermissionSet(psElement);
                    _isFullTrust = _inputPermissionSet.IsUnrestricted();
                }
            }
            return _inputPermissionSet;
        }

        private XmlElement GetInputPermissionSetElement()
        {
            if (_inputTrustInfoDocument == null)
            {
                _inputTrustInfoDocument = new XmlDocument();
                XmlElement trustInfoElement = _inputTrustInfoDocument.CreateElement(XmlUtil.TrimPrefix(XPaths.trustInfoElement), XmlNamespaces.asmv2);
                _inputTrustInfoDocument.AppendChild(trustInfoElement);
            }
            return GetPermissionSetElement(_inputTrustInfoDocument);
        }

        private XmlElement GetInputRequestedPrivilegeElement()
        {
            if (_inputTrustInfoDocument == null)
                return null;
            XmlNamespaceManager nsmgr = XmlNamespaces.GetNamespaceManager(_inputTrustInfoDocument.NameTable);
            XmlElement trustInfoElement = _inputTrustInfoDocument.DocumentElement;
            XmlElement securityElement = (XmlElement) trustInfoElement?.SelectSingleNode(XPaths.securityElement, nsmgr);
            XmlElement requestedPrivilegeElement = (XmlElement) securityElement?.SelectSingleNode(XPaths.requestedPrivilegeElement, nsmgr);
            return requestedPrivilegeElement;
        }

        private static XmlElement GetRequestedPrivilegeElement(XmlElement inputRequestedPrivilegeElement, XmlDocument document)
        {
            //  <requestedPrivileges xmlns="urn:schemas-microsoft-com:asm.v3">
            //      <!--
            //          UAC Manifest Options
            //          If you want to change the Windows User Account Control level replace the 
            //          requestedExecutionLevel node with one of the following .
            //          <requestedExecutionLevel  level="asInvoker" />
            //          <requestedExecutionLevel  level="requireAdministrator" />
            //          <requestedExecutionLevel  level="highestAvailable" />
            //          If you want to utilize File and Registry Virtualization for backward compatibility
            //          delete the requestedExecutionLevel node.
            //      -->
            //      <requestedExecutionLevel level="asInvoker" />
            //  </requestedPrivileges>


            // we always create a requestedPrivilege node to put into the generated TrustInfo document
            //
            XmlElement requestedPrivilegeElement = document.CreateElement(XmlUtil.TrimPrefix(XPaths.requestedPrivilegeElement), XmlNamespaces.asmv3);
            document.AppendChild(requestedPrivilegeElement);

            // our three cases we need to handle are:
            //  (a) no requestedPrivilege node (and therefore no requestedExecutionLevel node as well) - use default values
            //  (b) requestedPrivilege node and no requestedExecutionLevel node - omit the requestedExecutionLevel node
            //  (c) requestedPrivilege node and requestedExecutionLevel node - use the incoming requestedExecutionLevel node values
            //
            // using null for both values is case (b) above -- do not output values
            //
            string executionLevelString = null;
            string executionUIAccessString = null;
            string commentString = null;

            // case (a) above -- load default values
            //
            if (inputRequestedPrivilegeElement == null)
            {
                // If UAC requestedPrivilege node is missing (possibly due to upgraded project) then automatically 
                //  add a default UAC requestedPrivilege node with a default requestedExecutionLevel node set to 
                //  the expected ClickOnce level (asInvoker) with uiAccess as false
                //
                executionLevelString = Constants.UACAsInvoker;
                executionUIAccessString = Constants.UACUIAccess;

                // load up a default comment string that we put in front of the requestedExecutionLevel node
                //  here so we can allow the passed-in node to override it if there is a comment present
                //
                System.Resources.ResourceManager resources = new System.Resources.ResourceManager("Microsoft.Build.Tasks.Core.Strings.ManifestUtilities", typeof(SecurityUtilities).Module.Assembly);
                commentString = resources.GetString("TrustInfo.RequestedExecutionLevelComment"); ;
            }
            else
            {
                // we need to see if the requestedExecutionLevel node is present to decide whether or not to create one.
                //
                XmlNamespaceManager nsmgr = XmlNamespaces.GetNamespaceManager(document.NameTable);
                XmlElement inputRequestedExecutionLevel = (XmlElement)inputRequestedPrivilegeElement.SelectSingleNode(XPaths.requestedExecutionLevelElement, nsmgr);

                // case (c) above -- use incoming values [note that we should do nothing for case (b) above
                //  because the default values will make us not emit the requestedExecutionLevel and comment]
                //
                if (inputRequestedExecutionLevel != null)
                {
                    XmlNode previousNode = inputRequestedExecutionLevel.PreviousSibling;

                    // fetch the current comment node if there is one (if there is not one, simply
                    //  keep the default null value which means we will not create one in the
                    //  output document)
                    //
                    if (previousNode != null && previousNode.NodeType == XmlNodeType.Comment)
                    {
                        commentString = ((XmlComment)previousNode).Data;
                    }

                    // fetch the current requestedExecutionLevel node's level attribute if there is one
                    //
                    if (inputRequestedExecutionLevel.HasAttribute("level"))
                    {
                        executionLevelString = inputRequestedExecutionLevel.GetAttribute("level");
                    }

                    // fetch the current requestedExecutionLevel node's uiAccess attribute if there is one
                    //
                    if (inputRequestedExecutionLevel.HasAttribute("uiAccess"))
                    {
                        executionUIAccessString = inputRequestedExecutionLevel.GetAttribute("uiAccess");
                    }
                }
            }

            if (commentString != null)
            {
                XmlComment requestedPrivilegeComment = document.CreateComment(commentString);
                requestedPrivilegeElement.AppendChild(requestedPrivilegeComment);
            }

            if (executionLevelString != null)
            {
                XmlElement requestedExecutionLevelElement = document.CreateElement(XmlUtil.TrimPrefix(XPaths.requestedExecutionLevelElement), XmlNamespaces.asmv3);
                requestedPrivilegeElement.AppendChild(requestedExecutionLevelElement);

                XmlAttribute levelAttribute = document.CreateAttribute("level");
                levelAttribute.Value = executionLevelString;
                requestedExecutionLevelElement.Attributes.Append(levelAttribute);

                if (executionUIAccessString != null)
                {
                    XmlAttribute uiAccessAttribute = document.CreateAttribute("uiAccess");
                    uiAccessAttribute.Value = executionUIAccessString;
                    requestedExecutionLevelElement.Attributes.Append(uiAccessAttribute);
                }
            }

            return requestedPrivilegeElement;
        }

        // Returns permission set sub-element, creating a full-trust permission-set if one doesn't exist
        private XmlElement GetPermissionSetElement(XmlDocument document)
        {
            Debug.Assert(document != null, "GetPermissionSetElement was passed a null document");
            XmlNamespaceManager nsmgr = XmlNamespaces.GetNamespaceManager(document.NameTable);
            XmlElement trustInfoElement = document.DocumentElement;
            var securityElement = (XmlElement)trustInfoElement.SelectSingleNode(XPaths.securityElement, nsmgr);
            if (securityElement == null)
            {
                securityElement = document.CreateElement(XmlUtil.TrimPrefix(XPaths.securityElement), XmlNamespaces.asmv2);
                trustInfoElement.AppendChild(securityElement);
            }
            XmlElement applicationRequestMinimumElement = (XmlElement)securityElement.SelectSingleNode(XPaths.applicationRequestMinimumElement, nsmgr);
            if (applicationRequestMinimumElement == null)
            {
                applicationRequestMinimumElement = document.CreateElement(XmlUtil.TrimPrefix(XPaths.applicationRequestMinimumElement), XmlNamespaces.asmv2);
                securityElement.AppendChild(applicationRequestMinimumElement);
            }
            XmlElement permissionSetElement = (XmlElement)applicationRequestMinimumElement.SelectSingleNode(XPaths.permissionSetElement, nsmgr);
            if (permissionSetElement == null)
            {
                permissionSetElement = document.CreateElement(XmlUtil.TrimPrefix(XPaths.permissionSetElement), XmlNamespaces.asmv2);
                applicationRequestMinimumElement.AppendChild(permissionSetElement);
                XmlAttribute unrestrictedAttribute = document.CreateAttribute(XmlUtil.TrimPrefix(XPaths.unrestrictedAttribute), XmlNamespaces.asmv2);
                unrestrictedAttribute.Value = _isFullTrust.ToString().ToLowerInvariant();
                permissionSetElement.Attributes.Append(unrestrictedAttribute);
            }
            return permissionSetElement;
        }

        private PermissionSet GetOutputPermissionSet()
        {
            if (_outputPermissionSet == null)
            {
                // NOTE: outputPermissionSet is now the same as the inputPermissionSet
                // we used to maintain a list of loadable vs. non-loadable permissions
                // this is now cut. If we get more time we should simplify this further
                // so there is only one permission set.
                _outputPermissionSet = GetInputPermissionSet();
            }

            return _outputPermissionSet;
        }

        // Computes permission set from _outputPermissionSet and _unknownPermissions and returns new document
        private XmlDocument GetOutputPermissionSetDocument()
        {
            PermissionSet outputPermissionSet = GetOutputPermissionSet();
            XmlDocument outputDocument = SecurityUtilities.PermissionSetToXml(outputPermissionSet);

            return outputDocument;
        }

        /// <summary>
        /// Determines whether the application has permission to call unmanaged code.
        /// </summary>
        public bool HasUnmanagedCodePermission
        {
            get
            {
                PermissionSet ps = GetOutputPermissionSet();
                if (ps == null)
                {
                    return false;
                }
                var ups = new PermissionSet(PermissionState.None);
                ups.AddPermission(new SecurityPermission(SecurityPermissionFlag.UnmanagedCode));
                return ps.Intersect(ups) != null;
            }
        }

        /// <summary>
        /// Determines whether the application is full trust or partial trust.
        /// </summary>
        public bool IsFullTrust
        {
            get
            {
                GetInputPermissionSet();
                return _isFullTrust;
            }
            set => _isFullTrust = value;
        }

        /// <summary>
        /// Gets or sets the permission set object for the application trust.
        /// </summary>
        public PermissionSet PermissionSet
        {
            get => GetOutputPermissionSet();
            set => _outputPermissionSet = value ?? throw new ArgumentNullException("PermissionSet cannot be set to null.");
        }

        /// <summary>
        /// Determines whether to preserve partial trust permission when the full trust flag is set.
        /// If this option is false with full trust specified, then any permissions defined in the permission set object will be dropped on save.
        /// </summary>
        public bool PreserveFullTrustPermissionSet { get; set; }

        /// <summary>
        /// Reads the application trust from an XML file.
        /// </summary>
        /// <param name="path">The name of the input file.</param>
        public void Read(string path)
        {
            using (Stream s = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                Read(s);
            }
        }

        /// <summary>
        /// Reads the application trust from an XML file.
        /// </summary>
        /// <param name="input">Specifies an input stream.</param>
        public void Read(Stream input)
        {
            Read(input, XPaths.trustInfoPath);
        }

        private void Read(Stream s, string xpath)
        {
            Clear();
            var document = new XmlDocument();
            var xrSettings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore };
            using (XmlReader xr = XmlReader.Create(s, xrSettings))
            {
                document.Load(xr);
            }
            XmlNamespaceManager nsmgr = XmlNamespaces.GetNamespaceManager(document.NameTable);
            var trustInfoElement = (XmlElement)document.SelectSingleNode(xpath, nsmgr);
            if (trustInfoElement == null)
            {
                return; // no trustInfo element is okay
            }
            ReadTrustInfo(trustInfoElement.OuterXml);
        }

        /// <summary>
        /// Reads the application trust from a ClickOnce application manifest.
        /// </summary>
        /// <param name="path">The name of the input file.</param>
        public void ReadManifest(string path)
        {
            using (Stream s = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                ReadManifest(s);
            }
        }

        /// <summary>
        /// Reads the application trust from a ClickOnce application manifest.
        /// </summary>
        /// <param name="input">Specifies an input stream.</param>
        public void ReadManifest(Stream input)
        {
            Read(input, XPaths.manifestTrustInfoPath);
        }

        [SuppressMessage("Microsoft.Security.Xml", "CA3057: DoNotUseLoadXml.")]
        private void ReadTrustInfo(string xml)
        {
            _inputTrustInfoDocument = new XmlDocument();
            // CA3057: DoNotUseLoadXml. Suppressed since the suggested fix is to use XmlReader.
            // XmlReader.Create(string) requires an URI. Whereas the input parameter 'xml' is file content and not a path.
            _inputTrustInfoDocument.LoadXml(xml);
            XmlElement psElement = GetInputPermissionSetElement();
            XmlAttribute unrestrictedAttribute = (XmlAttribute)psElement.Attributes.GetNamedItem(XmlUtil.TrimPrefix(XPaths.unrestrictedAttribute));
            _isFullTrust = unrestrictedAttribute != null && Boolean.Parse(unrestrictedAttribute.Value);
            XmlAttribute sameSiteAttribute = (XmlAttribute)psElement.Attributes.GetNamedItem(XmlUtil.TrimPrefix(XPaths.sameSiteAttribute));
            if (sameSiteAttribute != null)
                _sameSiteSetting = sameSiteAttribute.Value;
        }

        /// <summary>
        /// Describes the level of "same site" access permitted, specifying whether the application has permission to communicate with the server from which it was deployed.
        /// </summary>
        public string SameSiteAccess
        {
            get => _sameSiteSetting;
            set
            {
                _sameSiteSetting = value;
                _sameSiteChanged = true;
            }
        }

        public override string ToString()
        {
            var m = new MemoryStream();
            Write(m);
            m.Position = 0;
            var r = new StreamReader(m);
            return r.ReadToEnd();
        }

        /// <summary>
        /// Writes the application trust to an XML file.
        /// </summary>
        /// <param name="path">The name of the output file.</param>
        public void Write(string path)
        {
            using (Stream s = File.Open(path, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                Write(s);
                s.Flush();
            }
        }

        /// <summary>
        /// Writes the application trust to an XML file.
        /// </summary>
        /// <param name="output"></param>
        public void Write(Stream output)
        {
            var outputDocument = new XmlDocument();
            XmlElement inputPermissionSetElement = GetInputPermissionSetElement();

            //NOTE: XmlDocument.ImportNode munges "xmlns:asmv2" to "xmlns:d1p1" for some reason, use XmlUtil.CloneElementToDocument instead
            XmlElement outputPermissionSetElement = XmlUtil.CloneElementToDocument(inputPermissionSetElement, outputDocument, XmlNamespaces.asmv2);
            outputDocument.AppendChild(outputPermissionSetElement);

            string tempPrivilegeDocument = null;

            var privilegeDocument = new XmlDocument();
            XmlElement inputRequestedPrivilegeElement = GetInputRequestedPrivilegeElement();

            XmlElement requestedPrivilegeElement = GetRequestedPrivilegeElement(inputRequestedPrivilegeElement, privilegeDocument);

            if (requestedPrivilegeElement != null)
            {
                privilegeDocument.AppendChild(requestedPrivilegeElement);

                var p = new MemoryStream();
                privilegeDocument.Save(p);
                p.Position = 0;
                tempPrivilegeDocument = Util.WriteTempFile(p);
            }

            try
            {
                string trustInfoResource2 = "trustinfo2.xsl";

                // If permission set was not altered, just write out what was read in...
                MemoryStream m = new MemoryStream();
                if (_outputPermissionSet == null && !_sameSiteChanged)
                {
                    XmlElement permissionSetElement = outputDocument.DocumentElement;
                    FixupPermissionSetElement(permissionSetElement);

                    outputDocument.Save(m);
                    m.Position = 0;
                }
                else
                {
                    XmlDocument permissionSetDocument = GetOutputPermissionSetDocument();
                    XmlElement permissionSetElement = permissionSetDocument.DocumentElement;
                    FixupPermissionSetElement(permissionSetElement);

                    if (outputDocument.DocumentElement == null)
                    {
                        permissionSetDocument.Save(m);
                        m.Position = 0;
                    }
                    else
                    {
                        XmlElement oldPermissionSetElement = outputDocument.DocumentElement;
                        XmlElement newPermissionSetElement = (XmlElement)outputDocument.ImportNode(permissionSetElement, true);
                        oldPermissionSetElement.ParentNode.ReplaceChild(newPermissionSetElement, oldPermissionSetElement);

                        outputDocument.Save(m);
                        m.Position = 0;
                    }
                }

                // Wrap <PermissionSet> in a <TrustInfo> section
                Stream s = tempPrivilegeDocument != null ? XmlUtil.XslTransform(trustInfoResource2, m, new DictionaryEntry("defaultRequestedPrivileges", tempPrivilegeDocument)) : XmlUtil.XslTransform(trustInfoResource2, m);
                Util.CopyStream(s, output);
            }
            finally
            {
                if (tempPrivilegeDocument != null)
                {
                    File.Delete(tempPrivilegeDocument);
                }
            }
        }

        /// <summary>
        /// Writes the application trust to a ClickOnce application manifest.
        /// If the file exists, the trust section will be updated.
        /// If the file does not exist, a new template manifest with the specified trust will be created.
        /// </summary>
        /// <param name="path">The name of the output file.</param>
        public void WriteManifest(string path)
        {
            Stream s = null;
            try
            {
                if (File.Exists(path))
                {
                    s = File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                }
                else
                {
                    s = File.Open(path, FileMode.Create, FileAccess.Write, FileShare.None);
                }

                if (s.Length > 0)
                {
                    // If the file is not empty then we assume that it is already a
                    // valid manifest and that we are just modifying the trust info
                    WriteManifest(s, s);
                }
                else
                {
                    // If the file is empty we need to start with a manifest template.
                    WriteManifest(s);
                }
            }
            finally
            {
                if (s != null)
                {
                    s.Flush();
                    s.Close();
                }
            }
        }

        /// <summary>
        /// Writes the application trust to a new template ClickOnce application manifest.
        /// </summary>
        /// <param name="output">Specifies an output stream.</param>
        public void WriteManifest(Stream output)
        {
            string r = "manifest.xml";
            Stream input = Util.GetEmbeddedResourceStream(r);
            WriteManifest(input, output);
        }

        /// <summary>
        /// Updates an existing ClickOnce application manifest with the specified trust.
        /// </summary>
        /// <param name="input">Specifies an input stream.</param>
        /// <param name="output">Specifies an output stream.</param>
        public void WriteManifest(Stream input, Stream output)
        {
            int t1 = Environment.TickCount;
            var document = new XmlDocument();
            var xrSettings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore };
            using (XmlReader xr = XmlReader.Create(input, xrSettings))
            {
                document.Load(xr);
            }
            XmlNamespaceManager nsmgr = XmlNamespaces.GetNamespaceManager(document.NameTable);
            XmlElement assemblyElement = (XmlElement)document.SelectSingleNode(XPaths.assemblyElement, nsmgr);
            if (assemblyElement == null)
            {
                throw new BadImageFormatException();
            }

            var trustInfoElement = (XmlElement)assemblyElement.SelectSingleNode(XPaths.trustInfoElement, nsmgr);
            if (trustInfoElement == null)
            {
                trustInfoElement = document.CreateElement(XmlUtil.TrimPrefix(XPaths.trustInfoElement), XmlNamespaces.asmv2);
                assemblyElement.AppendChild(trustInfoElement);
            }

            // If we have an input trustinfo document and no output specified then just copy the input to the output
            if (_inputTrustInfoDocument != null && _outputPermissionSet == null && !_sameSiteChanged)
            {
                XmlElement newTrustInfoElement = (XmlElement)document.ImportNode(_inputTrustInfoDocument.DocumentElement, true);
                trustInfoElement.ParentNode.ReplaceChild(newTrustInfoElement, trustInfoElement);
            }
            else
            {
                var securityElement = (XmlElement)trustInfoElement.SelectSingleNode(XPaths.securityElement, nsmgr);
                if (securityElement == null)
                {
                    securityElement = document.CreateElement(XmlUtil.TrimPrefix(XPaths.securityElement), XmlNamespaces.asmv2);
                    trustInfoElement.AppendChild(securityElement);
                }
                var applicationRequestMinimumElement = (XmlElement)securityElement.SelectSingleNode(XPaths.applicationRequestMinimumElement, nsmgr);
                if (applicationRequestMinimumElement == null)
                {
                    applicationRequestMinimumElement = document.CreateElement(XmlUtil.TrimPrefix(XPaths.applicationRequestMinimumElement), XmlNamespaces.asmv2);
                    securityElement.AppendChild(applicationRequestMinimumElement);
                }

                XmlNodeList permissionSetNodes = applicationRequestMinimumElement.SelectNodes(XPaths.permissionSetElement, nsmgr);
                foreach (XmlNode permissionSetNode in permissionSetNodes)
                {
                    applicationRequestMinimumElement.RemoveChild(permissionSetNode);
                }

                XmlDocument permissionSetDocument = GetOutputPermissionSetDocument();
                var permissionSetElement = (XmlElement)document.ImportNode(permissionSetDocument.DocumentElement, true);
                applicationRequestMinimumElement.AppendChild(permissionSetElement);
                FixupPermissionSetElement(permissionSetElement);
            }

            // Truncate any contents that may be in the file
            if (output.Length > 0)
            {
                output.SetLength(0);
                output.Flush();
            }
            document.Save(output);
            Util.WriteLog(String.Format(CultureInfo.CurrentCulture, "ManifestWriter.WriteTrustInfo t={0}", Environment.TickCount - t1));
        }
    }
}
