// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Xml;
using System.Xml.Serialization;

namespace Microsoft.Build.Tasks.Deployment.ManifestUtilities
{
    /// <summary>
    /// Reads an XML manifest file into an object representation.
    /// </summary>
    [ComVisible(false)]
    public static class ManifestReader
    {
        internal static ComInfo[] GetComInfo(string path)
        {
            XmlDocument document = GetXmlDocument(path);
            XmlNamespaceManager nsmgr = XmlNamespaces.GetNamespaceManager(document.NameTable);
            string manifestFileName = Path.GetFileName(path);

            List<ComInfo> comInfoList = new List<ComInfo>();
            XmlNodeList comNodes = document.SelectNodes(XPaths.comFilesPath, nsmgr);
            foreach (XmlNode comNode in comNodes)
            {
                XmlNode nameNode = comNode.SelectSingleNode(XPaths.fileNameAttribute, nsmgr);
                string componentFileName = nameNode != null ? nameNode.Value : null;

                XmlNodeList clsidNodes = comNode.SelectNodes(XPaths.clsidAttribute, nsmgr);
                foreach (XmlNode clsidNode in clsidNodes)
                    comInfoList.Add(new ComInfo(manifestFileName, componentFileName, clsidNode.Value, null));

                XmlNodeList tlbidNodes = comNode.SelectNodes(XPaths.tlbidAttribute, nsmgr);
                foreach (XmlNode tlbidNode in tlbidNodes)
                    comInfoList.Add(new ComInfo(manifestFileName, componentFileName, null, tlbidNode.Value));
            }

            return comInfoList.ToArray();
        }

        private static XmlDocument GetXmlDocument(string path)
        {
            using (Stream s = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                byte[] buffer = new byte[2];
                s.Read(buffer, 0, 2);
                s.Position = 0;
                XmlDocument document = new XmlDocument();
                XmlReaderSettings xrSettings = new XmlReaderSettings();
                xrSettings.DtdProcessing = DtdProcessing.Ignore;
                // if first two bytes are "MZ" then we're looking at an .exe or a .dll not a .manifest
                if ((buffer[0] == 0x4D) && (buffer[1] == 0x5A))
                {
                    Stream m = EmbeddedManifestReader.Read(path);
                    if (m == null)
                        throw new BadImageFormatException(null, path);

                    using (XmlReader xr = XmlReader.Create(m, xrSettings))
                    {
                        document.Load(xr);
                    }
                }
                else
                {
                    using (XmlReader xr = XmlReader.Create(s, xrSettings))
                    {
                        document.Load(xr);
                    }
                }

                return document;
            }
        }

        private static Manifest ReadEmbeddedManifest(string path)
        {
            Stream m = EmbeddedManifestReader.Read(path);
            if (m == null)
                return null;

            Util.WriteLogFile(Path.GetFileNameWithoutExtension(path) + ".embedded.xml", m);
            Manifest manifest = ReadManifest(m, false);
            manifest.SourcePath = path;
            return manifest;
        }

        /// <summary>
        /// Reads the specified manifest XML and returns an object representation.
        /// </summary>
        /// <param name="path">The name of the input file.</param>
        /// <param name="preserveStream">Specifies whether to preserve the input stream in the InputStream property of the resulting manifest object. Used by ManifestWriter to reconstitute input which is not represented in the object representation. This option is not honored if the specified input file is an embedded manfiest in a PE.</param>
        /// <returns>A base object representation of the manifest. Can be cast to AssemblyManifest, ApplicationManifest, or DeployManifest to access more specific functionality.</returns>
        public static Manifest ReadManifest(string path, bool preserveStream)
        {
            if (path == null) throw new ArgumentNullException("path");
            string manifestType = null;
            if (path.EndsWith(".application", StringComparison.Ordinal))
                manifestType = "DeployManifest";
            else if (path.EndsWith(".exe.manifest", StringComparison.Ordinal))
                manifestType = "ApplicationManifest";
            return ReadManifest(manifestType, path, preserveStream);
        }

        /// <summary>
        /// Reads the specified manifest XML and returns an object representation.
        /// </summary>
        /// <param name="manifestType">Specifies the expected type of the manifest. Valid values are "AssemblyManifest", "ApplicationManifest", or "DepoyManifest".</param>
        /// <param name="path">The name of the input file.</param>
        /// <param name="preserveStream">Specifies whether to preserve the input stream in the InputStream property of the resulting manifest object. Used by ManifestWriter to reconstitute input which is not represented in the object representation. This option is not honored if the specified input file is an embedded manfiest in a PE.</param>
        /// <returns>A base object representation of the manifest. Can be cast to AssemblyManifest, ApplicationManifest, or DeployManifest to access more specific functionality.</returns>
        public static Manifest ReadManifest(string manifestType, string path, bool preserveStream)
        {
            Manifest m = null;
            using (Stream s = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                byte[] buffer = new byte[2];
                s.Read(buffer, 0, 2);
                s.Position = 0;
                // if first two bytes are "MZ" then we're looking at an .exe or a .dll not a .manifest
                if ((buffer[0] == 0x4D) && (buffer[1] == 0x5A))
                {
                    m = ReadEmbeddedManifest(path);
                }
                else
                {
                    m = ReadManifest(manifestType, s, preserveStream);
                    m.SourcePath = path;
                }
            }
            return m;
        }

        /// <summary>
        /// Reads the specified manifest XML and returns an object representation.
        /// </summary>
        /// <param name="input">Specifies an input stream.</param>
        /// <param name="preserveStream">Specifies whether to preserve the input stream in the InputStream property of the resulting manifest object. Used by ManifestWriter to reconstitute input which is not represented in the object representation.</param>
        /// <returns>A base object representation of the manifest. Can be cast to AssemblyManifest, ApplicationManifest, or DeployManifest to access more specific functionality.</returns>
        public static Manifest ReadManifest(Stream input, bool preserveStream)
        {
            return ReadManifest(null, input, preserveStream);
        }

        /// <summary>
        /// Reads the specified manifest XML and returns an object representation.
        /// </summary>
        /// <param name="manifestType">Specifies the expected type of the manifest. Valid values are "AssemblyManifest", "ApplicationManifest", or "DepoyManifest".</param>
        /// <param name="input">Specifies an input stream.</param>
        /// <param name="preserveStream">Specifies whether to preserve the input stream in the InputStream property of the resulting manifest object. Used by ManifestWriter to reconstitute input which is not represented in the object representation.</param>
        /// <returns>A base object representation of the manifest. Can be cast to AssemblyManifest, ApplicationManifest, or DeployManifest to access more specific functionality.</returns>
        public static Manifest ReadManifest(string manifestType, Stream input, bool preserveStream)
        {
            int t1 = Environment.TickCount;
            string resource = "read2.xsl";
            Manifest m = null;
            Stream s;
            if (manifestType != null)
            {
                DictionaryEntry arg = new DictionaryEntry("manifest-type", manifestType);
                s = XmlUtil.XslTransform(resource, input, arg);
            }
            else
            {
                s = XmlUtil.XslTransform(resource, input);
            }

            try
            {
                s.Position = 0;
                m = Deserialize(s);
                if (m.GetType() == typeof(ApplicationManifest))
                {
                    ApplicationManifest am = (ApplicationManifest)m;
                    am.TrustInfo = new TrustInfo();
                    am.TrustInfo.ReadManifest(input);
                }
                if (preserveStream)
                {
                    input.Position = 0;
                    m.InputStream = new MemoryStream();
                    Util.CopyStream(input, m.InputStream);
                }
                s.Position = 0;
                string n = m.AssemblyIdentity.GetFullName(AssemblyIdentity.FullNameFlags.All);
                if (String.IsNullOrEmpty(n))
                    n = m.GetType().Name;
                Util.WriteLogFile(n + ".read.xml", s);
            }
            finally
            {
                s.Close();
            }
            Util.WriteLog(String.Format(CultureInfo.InvariantCulture, "ManifestReader.ReadManifest t={0}", Environment.TickCount - t1));
            m.OnAfterLoad();
            return m;
        }

        private static Manifest Deserialize(string path)
        {
            Manifest m = null;

            using (Stream s = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                m = Deserialize(s);
            }

            return m;
        }

        private static Manifest Deserialize(Stream s)
        {
            s.Position = 0;
            XmlTextReader r = new XmlTextReader(s);
            r.DtdProcessing = DtdProcessing.Ignore;

            do
                r.Read();
            while (r.NodeType != XmlNodeType.Element);
            string ns = typeof(Util).Namespace;
            string tn = String.Format(CultureInfo.InvariantCulture, "{0}.{1}", ns, r.Name);
            Type t = Type.GetType(tn);
            s.Position = 0;

            XmlSerializer xs = new XmlSerializer(t);

            int t1 = Environment.TickCount;
            XmlReaderSettings xrSettings = new XmlReaderSettings();
            xrSettings.DtdProcessing = DtdProcessing.Ignore;
            using (XmlReader xr = XmlReader.Create(s, xrSettings))
            {
                Manifest m = (Manifest)xs.Deserialize(xr);
                Util.WriteLog(String.Format(CultureInfo.CurrentCulture, "ManifestReader.Deserialize t={0}", Environment.TickCount - t1));
                return m;
            }
        }
    }

    internal class ComInfo
    {
        private readonly string _componentFileName;
        private readonly string _clsid;
        private readonly string _manifestFileName;
        private readonly string _tlbid;
        public ComInfo(string manifestFileName, string componentFileName, string clsid, string tlbid)
        {
            _componentFileName = componentFileName;
            _clsid = clsid;
            _manifestFileName = manifestFileName;
            _tlbid = tlbid;
        }
        public string ComponentFileName { get { return _componentFileName; } }
        public string ClsId { get { return _clsid; } }
        public string ManifestFileName { get { return _manifestFileName; } }
        public string TlbId { get { return _tlbid; } }
    }
}
