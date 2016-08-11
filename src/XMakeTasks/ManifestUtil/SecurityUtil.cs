// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.Win32;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Deployment.Internal.CodeSigning;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Permissions;
using System.Security.Policy;
using System.Text;
using System.Xml;
using FrameworkNameVersioning = System.Runtime.Versioning.FrameworkName;

namespace Microsoft.Build.Tasks.Deployment.ManifestUtilities
{
    /// <summary>
    /// Provides a set of utility functions for manipulating security permision sets and signing.
    /// </summary>
    [ComVisible(false)]
    public static class SecurityUtilities
    {
        private const string PermissionSetsFolder = "PermissionSets";
        private const string Everything = "Everything";
        private const string LocalIntranet = "LocalIntranet";
        private const string Internet = "Internet";
        private const string Custom = "Custom";
        private const string ToolName = "signtool.exe";
        private const int Fx2MajorVersion = 2;
        private const int Fx3MajorVersion = 3;
        private static readonly Version s_dotNet40Version = new Version("4.0");
        private static readonly Version s_dotNet45Version = new Version("4.5");

        private const string InternetPermissionSetXml = "<PermissionSet class=\"System.Security.PermissionSet\" version=\"1\" ID=\"Custom\" SameSite=\"site\">\n" +
                                                          "<IPermission class=\"System.Security.Permissions.FileDialogPermission, mscorlib, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089\" version=\"1\" Access=\"Open\" />\n" +
                                                          "<IPermission class=\"System.Security.Permissions.IsolatedStorageFilePermission, mscorlib, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089\" version=\"1\" Allowed=\"ApplicationIsolationByUser\" UserQuota=\"512000\" />\n" +
                                                          "<IPermission class=\"System.Security.Permissions.SecurityPermission, mscorlib, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089\" version=\"1\" Flags=\"Execution\" />\n" +
                                                          "<IPermission class=\"System.Security.Permissions.UIPermission, mscorlib, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089\" version=\"1\" Window=\"SafeTopLevelWindows\" Clipboard=\"OwnClipboard\" />\n" +
                                                          "<IPermission class=\"System.Drawing.Printing.PrintingPermission, System.Drawing, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a\" version=\"1\" Level=\"SafePrinting\" />\n" +
                                                        "</PermissionSet>";

        private const string LocalIntranetPermissionSetXml = "<PermissionSet class=\"System.Security.PermissionSet\" version=\"1\" ID=\"Custom\" SameSite=\"site\">\n" +
                                                                  "<IPermission class=\"System.Security.Permissions.EnvironmentPermission, mscorlib, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089\" version=\"1\" Read=\"USERNAME\" />\n" +
                                                                  "<IPermission class=\"System.Security.Permissions.FileDialogPermission, mscorlib, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089\" version=\"1\" Unrestricted=\"true\" />\n" +
                                                                  "<IPermission class=\"System.Security.Permissions.IsolatedStorageFilePermission, mscorlib, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089\" version=\"1\" Allowed=\"AssemblyIsolationByUser\" UserQuota=\"9223372036854775807\" Expiry=\"9223372036854775807\" Permanent=\"True\" />\n" +
                                                                  "<IPermission class=\"System.Security.Permissions.ReflectionPermission, mscorlib, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089\" version=\"1\" Flags=\"ReflectionEmit\" />\n" +
                                                                  "<IPermission class=\"System.Security.Permissions.SecurityPermission, mscorlib, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089\" version=\"1\" Flags=\"Assertion, Execution, BindingRedirects\" />\n" +
                                                                  "<IPermission class=\"System.Security.Permissions.UIPermission, mscorlib, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089\" version=\"1\" Unrestricted=\"true\" />\n" +
                                                                  "<IPermission class=\"System.Net.DnsPermission, System, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089\" version=\"1\" Unrestricted=\"true\" />\n" +
                                                                  "<IPermission class=\"System.Drawing.Printing.PrintingPermission, System.Drawing, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a\" version=\"1\" Level=\"DefaultPrinting\" />\n" +
                                                                "</PermissionSet>";

        private const string InternetPermissionSetWithWPFXml = "<PermissionSet class=\"System.Security.PermissionSet\" version=\"1\" ID=\"Custom\" SameSite=\"site\">\n" +
                                                                  "<IPermission class=\"System.Security.Permissions.FileDialogPermission, mscorlib, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089\" version=\"1\" Access=\"Open\" />\n" +
                                                                  "<IPermission class=\"System.Security.Permissions.IsolatedStorageFilePermission, mscorlib, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089\" version=\"1\" Allowed=\"ApplicationIsolationByUser\" UserQuota=\"512000\" />\n" +
                                                                  "<IPermission class=\"System.Security.Permissions.SecurityPermission, mscorlib, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089\" version=\"1\" Flags=\"Execution\" />\n" +
                                                                  "<IPermission class=\"System.Security.Permissions.UIPermission, mscorlib, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089\" version=\"1\" Window=\"SafeTopLevelWindows\" Clipboard=\"OwnClipboard\" />\n" +
                                                                  "<IPermission class=\"System.Drawing.Printing.PrintingPermission, System.Drawing, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a\" version=\"1\" Level=\"SafePrinting\" />\n" +
                                                                  "<IPermission class=\"System.Security.Permissions.MediaPermission, WindowsBase, Version=3.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35\" version=\"1\" Audio=\"SafeAudio\" Video=\"SafeVideo\" Image=\"SafeImage\" />\n" +
                                                                  "<IPermission class=\"System.Security.Permissions.WebBrowserPermission, WindowsBase, Version=3.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35\" version=\"1\" Level=\"Safe\" />\n" +
                                                                "</PermissionSet>";

        private const string LocalIntranetPermissionSetWithWPFXml = "<PermissionSet class=\"System.Security.PermissionSet\" version=\"1\" ID=\"Custom\" SameSite=\"site\">\n" +
                                                                          "<IPermission class=\"System.Security.Permissions.EnvironmentPermission, mscorlib, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089\" version=\"1\" Read=\"USERNAME\" />\n" +
                                                                          "<IPermission class=\"System.Security.Permissions.FileDialogPermission, mscorlib, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089\" version=\"1\" Unrestricted=\"true\" />\n" +
                                                                          "<IPermission class=\"System.Security.Permissions.IsolatedStorageFilePermission, mscorlib, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089\" version=\"1\" Allowed=\"AssemblyIsolationByUser\" UserQuota=\"9223372036854775807\" Expiry=\"9223372036854775807\" Permanent=\"True\" />\n" +
                                                                          "<IPermission class=\"System.Security.Permissions.ReflectionPermission, mscorlib, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089\" version=\"1\" Flags=\"ReflectionEmit\" />\n" +
                                                                          "<IPermission class=\"System.Security.Permissions.SecurityPermission, mscorlib, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089\" version=\"1\" Flags=\"Assertion, Execution, BindingRedirects\" />\n" +
                                                                          "<IPermission class=\"System.Security.Permissions.UIPermission, mscorlib, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089\" version=\"1\" Unrestricted=\"true\" />\n" +
                                                                          "<IPermission class=\"System.Net.DnsPermission, System, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089\" version=\"1\" Unrestricted=\"true\" />\n" +
                                                                          "<IPermission class=\"System.Drawing.Printing.PrintingPermission, System.Drawing, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a\" version=\"1\" Level=\"DefaultPrinting\" />\n" +
                                                                          "<IPermission class=\"System.Security.Permissions.MediaPermission, WindowsBase, Version=3.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35\" version=\"1\" Audio=\"SafeAudio\" Video=\"SafeVideo\" Image=\"SafeImage\" />\n" +
                                                                          "<IPermission class=\"System.Security.Permissions.WebBrowserPermission, WindowsBase, Version=3.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35\" version=\"1\" Level=\"Safe\" />\n" +
                                                                        "</PermissionSet>";

        /// <summary>
        /// Generates a permission set by computed the zone default permission set and adding any included permissions.
        /// </summary>
        /// <param name="targetZone">Specifies a zone default permission set, which is obtained from machine policy. Valid values are "Internet", "LocalIntranet", or "Custom". If "Custom" is specified, the generated permission set is based only on the includedPermissionSet parameter.</param>
        /// <param name="includedPermissionSet">A PermissionSet object containing the set of permissions to be explicitly included in the generated permission set. Permissions specified in this parameter will be included verbatim in the generated permission set, regardless of targetZone parameter.</param>
        /// <param name="excludedPermissions">This property is no longer used.</param>
        /// <returns>The generated permission set.</returns>
        public static PermissionSet ComputeZonePermissionSet(string targetZone, PermissionSet includedPermissionSet, string[] excludedPermissions)
        {
            return ComputeZonePermissionSetHelper(targetZone, includedPermissionSet, null, string.Empty);
        }

        internal static PermissionSet ComputeZonePermissionSetHelper(string targetZone, PermissionSet includedPermissionSet, ITaskItem[] dependencies, string targetFrameworkMoniker)
        {
            // Custom Set.
            if (String.IsNullOrEmpty(targetZone) || String.Equals(targetZone, Custom, StringComparison.OrdinalIgnoreCase))
            {
                // just return the included set, no magic
                return includedPermissionSet.Copy();
            }

            PermissionSet retSet = GetNamedPermissionSetFromZone(targetZone, dependencies, targetFrameworkMoniker);

            return retSet;
        }

        private static PermissionSet GetNamedPermissionSetFromZone(string targetZone, ITaskItem[] dependencies, string targetFrameworkMoniker)
        {
            switch (targetZone)
            {
                case LocalIntranet:
                    return GetNamedPermissionSet(LocalIntranet, dependencies, targetFrameworkMoniker);
                case Internet:
                    return GetNamedPermissionSet(Internet, dependencies, targetFrameworkMoniker);
                default:
                    throw new ArgumentException(String.Empty /* no message */, "targetZone");
            }
        }

        private static PermissionSet GetNamedPermissionSet(string targetZone, ITaskItem[] dependencies, string targetFrameworkMoniker)
        {
            FrameworkNameVersioning fn = null;

            if (!string.IsNullOrEmpty(targetFrameworkMoniker))
            {
                fn = new FrameworkNameVersioning(targetFrameworkMoniker);
            }
            else
            {
                fn = new FrameworkNameVersioning(".NETFramework", s_dotNet40Version);
            }

            int majorVersion = fn.Version.Major;

            if (majorVersion == Fx2MajorVersion)
            {
                return SecurityUtilities.XmlToPermissionSet((GetXmlElement(targetZone, majorVersion)));
            }
            else if (majorVersion == Fx3MajorVersion)
            {
                return SecurityUtilities.XmlToPermissionSet((GetXmlElement(targetZone, majorVersion)));
            }
            else
            {
                return SecurityUtilities.XmlToPermissionSet((GetXmlElement(targetZone, fn)));
            }
        }

        private static XmlElement GetXmlElement(string targetZone, FrameworkNameVersioning fn)
        {
            IList<string> paths = ToolLocationHelper.GetPathToReferenceAssemblies(fn);

            // Is the targeted CLR even installed?
            if (paths.Count > 0)
            {
                // first one is always framework requested.
                string path = Path.Combine(paths[0], PermissionSetsFolder);

                // PermissionSets folder doesn't exit
                if (Directory.Exists(path))
                {
                    string[] files = Directory.GetFiles(path, "*.xml");
                    FileInfo[] filesInfo = new FileInfo[files.Length];

                    int indexFound = -1;

                    // trim the extension.
                    for (int i = 0; i < files.Length; i++)
                    {
                        filesInfo[i] = new FileInfo(files[i]);

                        string fileInfoNoExt = Path.GetFileNameWithoutExtension(files[i]);

                        if (string.Equals(fileInfoNoExt, targetZone, StringComparison.OrdinalIgnoreCase))
                        {
                            indexFound = i;
                            break;
                        }
                    }

                    if (indexFound != -1)
                    {
                        string data = string.Empty;
                        FileInfo resultFile = filesInfo[indexFound];
                        using (FileStream fs = resultFile.OpenRead())
                        {
                            try
                            {
                                StreamReader sr = new StreamReader(fs);
                                data = sr.ReadToEnd(); // fs.Position value will be the length of the stream.
                                if (!string.IsNullOrEmpty(data))
                                {
                                    XmlDocument doc = new XmlDocument();
                                    XmlReaderSettings xrSettings = new XmlReaderSettings();
                                    xrSettings.DtdProcessing = DtdProcessing.Ignore;

                                    // http://msdn.microsoft.com/en-us/library/h2344bs2(v=vs.110).aspx
                                    // PermissionSets do not conform to document level, which is the default setting.
                                    xrSettings.ConformanceLevel = ConformanceLevel.Auto;
                                    try
                                    {
                                        fs.Position = 0; // Reset to 0 before using this stream in any other reader.
                                        using (XmlReader xr = XmlReader.Create(fs, xrSettings))
                                        {
                                            doc.Load(xr);
                                            return (XmlElement)doc.DocumentElement;
                                        }
                                    }
                                    catch (Exception)
                                    {
                                        //continue.
                                    }
                                }
                            }
                            catch (ArgumentException)
                            {
                                //continue.
                            }
                        }
                    }
                }
            }

            return GetCurrentCLRPermissions(targetZone);
        }

        [SuppressMessage("Microsoft.Security.Xml", "CA3057: DoNotUseLoadXml.")]
        private static XmlElement GetCurrentCLRPermissions(string targetZone)
        {
            string resultInString = string.Empty;
            SecurityZone zone = SecurityZone.NoZone;
            switch (targetZone)
            {
                case LocalIntranet:
                    zone = SecurityZone.Intranet;
                    break;
                case Internet:
                    zone = SecurityZone.Internet;
                    break;
                default:
                    throw new ArgumentException(String.Empty /* no message */, "targetZone");
            }

            Evidence evidence = new Evidence(new EvidenceBase[] { new Zone(zone), new System.Runtime.Hosting.ActivationArguments(new System.ApplicationIdentity("")) }, null);

            PermissionSet sandbox = SecurityManager.GetStandardSandbox(evidence);
            resultInString = sandbox.ToString();

            if (!string.IsNullOrEmpty(resultInString))
            {
                XmlDocument doc = new XmlDocument();
                // CA3057: DoNotUseLoadXml. Suppressed since the xml being loaded is a string representation of the PermissionSet.
                doc.LoadXml(resultInString);

                return (XmlElement)doc.DocumentElement;
            }

            return null;
        }

        private static XmlElement GetXmlElement(string targetZone, int majorVersion)
        {
            XmlDocument doc = null;

            switch (majorVersion)
            {
                case Fx2MajorVersion:
                    doc = CreateXmlDocV2(targetZone);
                    break;
                case Fx3MajorVersion:
                    doc = CreateXmlDocV3(targetZone);
                    break;
                default:
                    throw new ArgumentException(String.Empty /* no message */, "majorVersion");
            }

            XmlElement rootElement = (XmlElement)doc.DocumentElement;

            if (rootElement == null)
                return null;

            return rootElement;
        }

        [SuppressMessage("Microsoft.Security.Xml", "CA3057: DoNotUseLoadXml.")]
        private static XmlDocument CreateXmlDocV2(string targetZone)
        {
            XmlDocument doc = new XmlDocument();

            switch (targetZone)
            {
                case LocalIntranet:
                    // CA3057: DoNotUseLoadXml.  Suppressed since is LocalIntranetPermissionSetXml a constant string.
                    doc.LoadXml(LocalIntranetPermissionSetXml);
                    return doc;
                case Internet:
                    // CA3057: DoNotUseLoadXml.  Suppressed since is InternetPermissionSetXml a constant string.
                    doc.LoadXml(InternetPermissionSetXml);
                    return doc;
                default:
                    throw new ArgumentException(String.Empty /* no message */, "targetZone");
            }
        }

        [SuppressMessage("Microsoft.Security.Xml", "CA3057: DoNotUseLoadXml.")]
        private static XmlDocument CreateXmlDocV3(string targetZone)
        {
            XmlDocument doc = new XmlDocument();

            switch (targetZone)
            {
                case LocalIntranet:
                    // CA3057: DoNotUseLoadXml.  Suppressed since is LocalIntranetPermissionSetXml a constant string.
                    doc.LoadXml(LocalIntranetPermissionSetWithWPFXml);
                    return doc;
                case Internet:
                    // CA3057: DoNotUseLoadXml.  Suppressed since is InternetPermissionSetXml a constant string.
                    doc.LoadXml(InternetPermissionSetWithWPFXml);
                    return doc;
                default:
                    throw new ArgumentException(String.Empty /* no message */, "targetZone");
            }
        }

        private static string[] GetRegistryPermissionSetByName(string name)
        {
            string[] extensibleNamedPermissionSetRegistryInfo = null;
            RegistryKey localMachineKey = Registry.LocalMachine;

            using (RegistryKey versionIndependentFXKey = localMachineKey.OpenSubKey(@"Software\Microsoft\.NETFramework", false))
            {
                if (versionIndependentFXKey != null)
                {
                    using (RegistryKey namedPermissionSetsKey = versionIndependentFXKey.OpenSubKey(@"Security\Policy\Extensions\NamedPermissionSets", false))
                    {
                        if (namedPermissionSetsKey != null)
                        {
                            using (RegistryKey permissionSetKey = namedPermissionSetsKey.OpenSubKey(name, false))
                            {
                                if (permissionSetKey != null)
                                {
                                    string[] permissionKeys = permissionSetKey.GetSubKeyNames();
                                    extensibleNamedPermissionSetRegistryInfo = new string[permissionKeys.Length];
                                    for (int i = 0; i < permissionKeys.Length; i++)
                                    {
                                        using (RegistryKey permissionKey = permissionSetKey.OpenSubKey(permissionKeys[i], false))
                                        {
                                            string permissionXml = permissionKey.GetValue("Xml") as string;
                                            extensibleNamedPermissionSetRegistryInfo[i] = permissionXml;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            return extensibleNamedPermissionSetRegistryInfo;
        }

        private static PermissionSet RemoveNonReferencedPermissions(string[] setToFilter, ITaskItem[] dependencies)
        {
            PermissionSet retSet = new PermissionSet(PermissionState.None);
            if (dependencies == null || setToFilter == null || setToFilter.Length == 0)
                return retSet;

            List<string> assemblyNameList = new List<string>();
            foreach (ITaskItem dependency in dependencies)
            {
                AssemblyName dependentAssemblyName = AssemblyName.GetAssemblyName(dependency.ItemSpec);
                assemblyNameList.Add(dependentAssemblyName.Name + ", " + dependentAssemblyName.Version.ToString());
            }
            SecurityElement retSetElement = retSet.ToXml();
            foreach (string permissionXml in setToFilter)
            {
                if (!String.IsNullOrEmpty(permissionXml))
                {
                    string permissionAssemblyName;
                    string className;
                    string assemblyVersion;

                    SecurityElement permission = SecurityElement.FromString(permissionXml);

                    if (!ParseElementForAssemblyIdentification(permission, out className, out permissionAssemblyName, out assemblyVersion))
                        continue;
                    if (assemblyNameList.Contains(permissionAssemblyName + ", " + assemblyVersion))
                    {
                        retSetElement.AddChild(SecurityElement.FromString(permissionXml));
                    }
                }
            }
            retSet = new ReadOnlyPermissionSet(retSetElement);
            return retSet;
        }

        internal static bool ParseElementForAssemblyIdentification(SecurityElement el,
                                                                   out String className,
                                                                   out String assemblyName, // for example "WindowsBase"
                                                                   out String assemblyVersion)
        {
            className = null;
            assemblyName = null;
            assemblyVersion = null;

            String fullClassName = el.Attribute("class");

            if (fullClassName == null)
            {
                return false;
            }
            if (fullClassName.IndexOf('\'') >= 0)
            {
                fullClassName = fullClassName.Replace('\'', '\"');
            }

            int commaIndex = fullClassName.IndexOf(',');
            int namespaceClassNameLength;

            // If the classname is tagged with assembly information, find where
            // the assembly information begins.

            if (commaIndex == -1)
            {
                return false;
            }

            namespaceClassNameLength = commaIndex;
            className = fullClassName.Substring(0, namespaceClassNameLength);
            String assemblyFullName = fullClassName.Substring(commaIndex + 1);
            AssemblyName an = new AssemblyName(assemblyFullName);
            assemblyName = an.Name;
            assemblyVersion = an.Version.ToString();
            return true;
        }


        /// <summary>
        /// Converts an array of permission identity strings to a permission set object.
        /// </summary>
        /// <param name="ids">An array of permission identity strings.</param>
        /// <returns>The converted permission set.</returns>
        public static PermissionSet IdentityListToPermissionSet(string[] ids)
        {
            XmlDocument document = new XmlDocument();
            XmlElement permissionSetElement = document.CreateElement("PermissionSet");
            document.AppendChild(permissionSetElement);
            foreach (string id in ids)
            {
                XmlElement permissionElement = document.CreateElement("IPermission");
                XmlAttribute a = document.CreateAttribute("class");
                a.Value = id;
                permissionElement.Attributes.Append(a);
                permissionSetElement.AppendChild(permissionElement);
            }
            return XmlToPermissionSet(permissionSetElement);
        }

        /// <summary>
        /// Converts a permission set object to an array of permission identity strings.
        /// </summary>
        /// <param name="permissionSet">The input permission set to be converted.</param>
        /// <returns>An array of permission identity strings.</returns>
        [SuppressMessage("Microsoft.Security.Xml", "CA3057: DoNotUseLoadXml.")]
        public static string[] PermissionSetToIdentityList(PermissionSet permissionSet)
        {
            string psXml = permissionSet != null ? permissionSet.ToString() : "<PermissionSet/>";
            XmlDocument psDocument = new XmlDocument();
            // CA3057: DoNotUseLoadXml.  Suppressed since 'psXml' is a trusted or a constant string.
            psDocument.LoadXml(psXml);
            return XmlToIdentityList(psDocument.DocumentElement);
        }

        [SuppressMessage("Microsoft.Security.Xml", "CA3057: DoNotUseLoadXml.")]
        internal static XmlDocument PermissionSetToXml(PermissionSet ps)
        {
            XmlDocument inputDocument = new XmlDocument();
            string xml = (ps != null) ? ps.ToString() : "<PermissionSet/>";

            // CA3057: DoNotUseLoadXml.  Suppressed since 'xml' is a trusted or a constant string.
            inputDocument.LoadXml(xml);
            XmlDocument outputDocument = new XmlDocument();
            XmlElement psElement = XmlUtil.CloneElementToDocument(inputDocument.DocumentElement, outputDocument, XmlNamespaces.asmv2);
            outputDocument.AppendChild(psElement);
            return outputDocument;
        }

        private static SecurityElement XmlElementToSecurityElement(XmlElement xe)
        {
            SecurityElement se = new SecurityElement(xe.Name);
            foreach (XmlAttribute xa in xe.Attributes)
                se.AddAttribute(xa.Name, xa.Value);
            foreach (XmlNode xn in xe.ChildNodes)
                if (xn.NodeType == XmlNodeType.Element)
                    se.AddChild(XmlElementToSecurityElement((XmlElement)xn));
            return se;
        }

        private static string[] XmlToIdentityList(XmlElement psElement)
        {
            XmlNamespaceManager nsmgr = XmlNamespaces.GetNamespaceManager(psElement.OwnerDocument.NameTable);
            XmlNodeList nodes = psElement.SelectNodes(XPaths.permissionClassAttributeQuery, nsmgr);
            if (nodes == null || nodes.Count == 0)
                nodes = psElement.SelectNodes(XmlUtil.TrimPrefix(XPaths.permissionClassAttributeQuery));
            string[] a;
            if (nodes != null)
            {
                a = new string[nodes.Count];
                int i = 0;
                foreach (XmlNode node in nodes)
                    a[i++] = node.Value;
            }
            else
                a = new string[0];
            return a;
        }

        /// <summary>
        /// Converts an XmlElement into a PermissionSet object.
        /// </summary>
        /// <param name="element">An XML representation of the permission set.</param>
        /// <returns>The converted permission set.</returns>
        public static PermissionSet XmlToPermissionSet(XmlElement element)
        {
            if (element == null)
                return null;

            SecurityElement se = XmlElementToSecurityElement(element);
            if (se == null)
                return null;

            PermissionSet ps = new PermissionSet(PermissionState.None);
            try
            {
                ps = new ReadOnlyPermissionSet(se);
            }
            catch (ArgumentException ex)
            {
                //UNDONE: Need to log exception thrown from PermissionSet.FromXml
                Debug.Fail(String.Format(CultureInfo.CurrentCulture, "PermissionSet.FromXml failed: {0}\r\n\r\n{1}", ex.Message, element.OuterXml));
                return null;
            }
            return ps;
        }

        /// <summary>
        /// Signs a ClickOnce manifest or PE file.
        /// </summary>
        /// <param name="certThumbprint">Hexadecimal string that contains the SHA-1 hash of the certificate.</param>
        /// <param name="timestampUrl">URL that specifies an address of a time stamping server.</param>
        /// <param name="path">Path of the file to sign with the certificate.</param>
        public static void SignFile(string certThumbprint, Uri timestampUrl, string path)
        {
            SignFile(certThumbprint, timestampUrl, path, null);
        }

        /// <summary>
        /// Signs a ClickOnce manifest or PE file.
        /// </summary>
        /// <param name="certThumbprint">Hexadecimal string that contains the SHA-1 hash of the certificate.</param>
        /// <param name="timestampUrl">URL that specifies an address of a time stamping server.</param>
        /// <param name="path">Path of the file to sign with the certificate.</param>
        /// <param name="targetFrameworkVersion">Version of the .NET Framework for the target.</param>
        public static void SignFile(string certThumbprint, Uri timestampUrl, string path, string targetFrameworkVersion)
        {
            System.Resources.ResourceManager resources = new System.Resources.ResourceManager("Microsoft.Build.Tasks.Core.Strings.ManifestUtilities", typeof(SecurityUtilities).Module.Assembly);

            if (String.IsNullOrEmpty(certThumbprint))
                throw new ArgumentNullException("certThumbprint");

            X509Certificate2 cert = GetCert(certThumbprint);

            if (cert == null)
                throw new ArgumentException(resources.GetString("CertNotInStore"), "certThumbprint");

            if (!String.IsNullOrEmpty(targetFrameworkVersion))
            {
                Version targetVersion = Util.GetTargetFrameworkVersion(targetFrameworkVersion);

                if (targetVersion == null)
                    throw new ArgumentException("TargetFrameworkVersion");

                // SHA-256 digest can be parsed only with .NET 4.5 or higher.
                bool isTargetFrameworkSha256Supported = targetVersion.CompareTo(s_dotNet45Version) >= 0;
                SignFileInternal(cert, timestampUrl, path, isTargetFrameworkSha256Supported, resources);
            }
            else
            {
                SignFile(cert, timestampUrl, path);
            }
        }

        // We need to refactor these functions to handle real sign tool
        /// <summary>
        /// Signs a ClickOnce manifest.
        /// </summary>
        /// <param name="certPath">The certificate to be used to sign the file.</param>
        /// <param name="certPassword">The certificate password.</param>
        /// <param name="timestampUrl">URL that specifies an address of a time stamping server.</param>
        /// <param name="path">Path of the file to sign with the certificate.</param>
        /// <remarks>This function is only for signing a manifest, not a PE file.</remarks>
        public static void SignFile(string certPath, SecureString certPassword, Uri timestampUrl, string path)
        {
            X509Certificate2 cert = new X509Certificate2(certPath, certPassword, X509KeyStorageFlags.PersistKeySet);
            SignFile(cert, timestampUrl, path);
        }

        private static bool UseSha256Algorithm(X509Certificate2 cert)
        {
            Oid oid = cert.SignatureAlgorithm;
            return string.Equals(oid.FriendlyName, "sha256RSA", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Signs a ClickOnce manifest or PE file.
        /// </summary>
        /// <param name="cert">The certificate to be used to sign the file.</param>
        /// <param name="timestampUrl">URL that specifies an address of a time stamping server.</param>
        /// <param name="path">Path of the file to sign with the certificate.</param>
        /// <remarks>This function can only sign a PE file if the X509Certificate2 parameter represents a certificate in the 
        /// current user's personal certificate store.</remarks>
        public static void SignFile(X509Certificate2 cert, Uri timestampUrl, string path)
        {
            // setup resources
            System.Resources.ResourceManager resources = new System.Resources.ResourceManager("Microsoft.Build.Tasks.Core.Strings.ManifestUtilities", typeof(SecurityUtilities).Module.Assembly);
            SignFileInternal(cert, timestampUrl, path, true, resources);
        }

        private static void SignFileInternal(X509Certificate2 cert, Uri timestampUrl, string path, bool targetFrameworkSupportsSha256, System.Resources.ResourceManager resources)
        {
            if (cert == null)
                throw new ArgumentNullException("cert");

            if (String.IsNullOrEmpty(path))
                throw new ArgumentNullException("path");

            if (!File.Exists(path))
                throw new FileNotFoundException(String.Format(CultureInfo.InvariantCulture, resources.GetString("SecurityUtil.SignTargetNotFound"), path), path);

            bool useSha256 = UseSha256Algorithm(cert) && targetFrameworkSupportsSha256;

            if (PathUtil.IsPEFile(path))
            {
                if (IsCertInStore(cert))
                    SignPEFile(cert, timestampUrl, path, resources, useSha256);
                else
                    throw new InvalidOperationException(resources.GetString("SignFile.CertNotInStore"));
            }
            else
            {
                if (cert.PrivateKey == null)
                    throw new InvalidOperationException(resources.GetString("SignFile.CertMissingPrivateKey"));

                if (cert.PrivateKey.GetType() != typeof(RSACryptoServiceProvider))
                    throw new ApplicationException(resources.GetString("SecurityUtil.OnlyRSACertsAreAllowed"));
                try
                {
                    XmlDocument doc = new XmlDocument();
                    doc.PreserveWhitespace = true;
                    XmlReaderSettings xrSettings = new XmlReaderSettings();
                    xrSettings.DtdProcessing = DtdProcessing.Ignore;
                    using (XmlReader xr = XmlReader.Create(path, xrSettings))
                    {
                        doc.Load(xr);
                    }
                    SignedCmiManifest2 manifest = new SignedCmiManifest2(doc, useSha256);
                    RSACryptoServiceProvider csp;

                    if (useSha256)
                    {
                        csp = SignedCmiManifest2.GetFixedRSACryptoServiceProvider(cert.PrivateKey as RSACryptoServiceProvider, useSha256);
                    }
                    else
                    {
                        csp = cert.PrivateKey as RSACryptoServiceProvider;
                    }

                    CmiManifestSigner2 signer = new CmiManifestSigner2(csp, cert, useSha256);
                    if (timestampUrl == null)
                        manifest.Sign(signer);
                    else
                        manifest.Sign(signer, timestampUrl.ToString());
                    doc.Save(path);
                }
                catch (Exception ex)
                {
                    int exceptionHR = System.Runtime.InteropServices.Marshal.GetHRForException(ex);
                    if (exceptionHR == -2147012889 || exceptionHR == -2147012867)
                    {
                        throw new ApplicationException(resources.GetString("SecurityUtil.TimestampUrlNotFound"), ex);
                    }
                    throw new ApplicationException(ex.Message, ex);
                }
            }
        }


        private static void SignPEFile(X509Certificate2 cert, System.Uri timestampUrl, string path, System.Resources.ResourceManager resources, bool useSha256)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo(GetPathToTool(resources), GetCommandLineParameters(cert.Thumbprint, timestampUrl, path, useSha256));
            startInfo.CreateNoWindow = true;
            startInfo.UseShellExecute = false;
            startInfo.RedirectStandardError = true;
            startInfo.RedirectStandardOutput = true;

            Process signTool = null;

            try
            {
                signTool = Process.Start(startInfo);
                signTool.WaitForExit();

                while (!signTool.HasExited)
                {
                    System.Threading.Thread.Sleep(50);
                }
                switch (signTool.ExitCode)
                {
                    case 0:
                        // everything was fine
                        break;
                    case 1:
                        // error, report it
                        throw new ApplicationException(String.Format(CultureInfo.InvariantCulture, resources.GetString("SecurityUtil.SigntoolFail"), path, signTool.StandardError.ReadToEnd()));
                    case 2:
                        // warning, report it
                        throw new WarningException(String.Format(CultureInfo.InvariantCulture, resources.GetString("SecurityUtil.SigntoolWarning"), path, signTool.StandardError.ReadToEnd()));
                    default:
                        // treat as error
                        throw new ApplicationException(String.Format(CultureInfo.InvariantCulture, resources.GetString("SecurityUtil.SigntoolFail"), path, signTool.StandardError.ReadToEnd()));
                }
            }
            finally
            {
                if (signTool != null)
                    signTool.Close();
            }
        }

        internal static string GetCommandLineParameters(string certThumbprint, Uri timestampUrl, string path, bool useSha256)
        {
            StringBuilder commandLine = new StringBuilder();
            if (useSha256)
                commandLine.Append(String.Format(CultureInfo.InvariantCulture, "sign /fd sha256 /sha1 {0} ", certThumbprint));
            else
            {
                commandLine.Append(String.Format(CultureInfo.InvariantCulture, "sign /sha1 {0} ", certThumbprint));
            }
            if (timestampUrl != null)
                commandLine.Append(String.Format(CultureInfo.InvariantCulture, "/t {0} ", timestampUrl.ToString()));
            commandLine.Append(string.Format(CultureInfo.InvariantCulture, "\"{0}\"", path));
            return commandLine.ToString();
        }

        internal static string GetPathToTool(System.Resources.ResourceManager resources)
        {
#pragma warning disable 618 // Disabling warning on using internal ToolLocationHelper API. At some point we should migrate this.
            string toolPath = ToolLocationHelper.GetPathToWindowsSdkFile(ToolName, TargetDotNetFrameworkVersion.VersionLatest, VisualStudioVersion.VersionLatest);
            if (toolPath == null || !File.Exists(toolPath))
            {
                toolPath = ToolLocationHelper.GetPathToWindowsSdkFile(ToolName, TargetDotNetFrameworkVersion.Version45,
                    VisualStudioVersion.Version110);
            }
            if (toolPath == null || !File.Exists(toolPath))
            {
                var pathToDotNetFrameworkSdk = ToolLocationHelper.GetPathToDotNetFrameworkSdk(TargetDotNetFrameworkVersion.Version40, VisualStudioVersion.Version100);
                if (pathToDotNetFrameworkSdk != null)
                {
                    toolPath = Path.Combine(pathToDotNetFrameworkSdk, "bin", ToolName);
                }
            }
            if (toolPath == null || !File.Exists(toolPath))
            {
                toolPath = GetVersionIndependentToolPath(ToolName);
            }
            if (toolPath == null || !File.Exists(toolPath))
            {
                toolPath = Path.Combine(Directory.GetCurrentDirectory(), ToolName);
            }
            if (!File.Exists(toolPath))
            {
                throw new ApplicationException(String.Format(CultureInfo.CurrentCulture,
                    resources.GetString("SecurityUtil.SigntoolNotFound"), toolPath));
            }

            return toolPath;
#pragma warning restore 618
        }

        internal static X509Certificate2 GetCert(string thumbprint)
        {
            X509Store personalStore = new X509Store(StoreName.My, StoreLocation.CurrentUser);
            try
            {
                personalStore.Open(OpenFlags.ReadOnly);
                X509Certificate2Collection foundCerts = personalStore.Certificates.Find(X509FindType.FindByThumbprint, thumbprint, false);
                if (foundCerts.Count == 1)
                {
                    return foundCerts[0];
                }
            }
            finally
            {
                personalStore.Close();
            }
            return null;
        }

        private static bool IsCertInStore(X509Certificate2 cert)
        {
            X509Store personalStore = new X509Store(StoreName.My, StoreLocation.CurrentUser);
            try
            {
                personalStore.Open(OpenFlags.ReadOnly);
                X509Certificate2Collection foundCerts = personalStore.Certificates.Find(X509FindType.FindByThumbprint, cert.Thumbprint, false);
                if (foundCerts.Count == 1)
                    return true;
            }
            finally
            {
                personalStore.Close();
            }
            return false;
        }

        private static string GetVersionIndependentToolPath(string toolName)
        {
            RegistryKey localMachineKey = Registry.LocalMachine;
            const string versionIndependentToolKeyName = @"Software\Microsoft\ClickOnce\SignTool";

            using (RegistryKey versionIndependentToolKey = localMachineKey.OpenSubKey(versionIndependentToolKeyName, writable: false))
            {
                string versionIndependentToolPath = null;

                if (versionIndependentToolKey != null)
                {
                    versionIndependentToolPath = versionIndependentToolKey.GetValue("Path") as string;
                }

                return versionIndependentToolPath != null ? Path.Combine(versionIndependentToolPath, toolName) : null;
            }
        }
    }
}
