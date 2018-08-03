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

            var comInfoList = new List<ComInfo>();
            XmlNodeList comNodes = document.SelectNodes(XPaths.comFilesPath, nsmgr);
            foreach (XmlNode comNode in comNodes)
            {
                XmlNode nameNode = comNode.SelectSingleNode(XPaths.fileNameAttribute, nsmgr);
                string componentFileName = nameNode?.Value;

                XmlNodeList clsidNodes = comNode.SelectNodes(XPaths.clsidAttribute, nsmgr);
                foreach (XmlNode clsidNode in clsidNodes)
                {
                    comInfoList.Add(new ComInfo(manifestFileName, componentFileName, clsidNode.Value, null));
                }

                XmlNodeList tlbidNodes = comNode.SelectNodes(XPaths.tlbidAttribute, nsmgr);
                foreach (XmlNode tlbidNode in tlbidNodes)
                {
                    comInfoList.Add(new ComInfo(manifestFileName, componentFileName, null, tlbidNode.Value));
                }
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
                var document = new XmlDocument();
                var xrSettings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore };
                // if first two bytes are "MZ" then we're looking at an .exe or a .dll not a .manifest
                if ((buffer[0] == 0x4D) && (buffer[1] == 0x5A))
                {
                    Stream m = EmbeddedManifestReader.Read(path);
                    if (m == null)
                    {
                        throw new BadImageFormatException(null, path);
                    }

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
            {
                return null;
            }

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
            if (path == null) throw new ArgumentNullException(nameof(path));
            string manifestType = null;
            if (path.EndsWith(".application", StringComparison.Ordinal))
            {
                manifestType = "DeployManifest";
            }
            else if (path.EndsWith(".exe.manifest", StringComparison.Ordinal))
            {
                manifestType = "ApplicationManifest";
            }
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
            Manifest m;
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
            const string resource = "read2.xsl";
            Manifest m;
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
                    var am = (ApplicationManifest)m;
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
                {
                    n = m.GetType().Name;
                }
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

        private static Manifest Deserialize(Stream s)
        {
            s.Position = 0;
            var r = new XmlTextReader(s) { DtdProcessing = DtdProcessing.Ignore };

            do
            {
                r.Read();
            } while (r.NodeType != XmlNodeType.Element);
            string ns = typeof(Util).Namespace;
            string tn = String.Format(CultureInfo.InvariantCulture, "{0}.{1}", ns, r.Name);
            Type t = Type.GetType(tn);
            s.Position = 0;

            var xs = new XmlSerializer(t);

            int t1 = Environment.TickCount;
            var xrSettings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore };
            using (XmlReader xr = XmlReader.Create(s, xrSettings))
            {
                var m = (Manifest)xs.Deserialize(xr);
                Util.WriteLog(String.Format(CultureInfo.CurrentCulture, "ManifestReader.Deserialize t={0}", Environment.TickCount - t1));
                return m;
            }
        }
    }

    internal class ComInfo
    {
        public ComInfo(string manifestFileName, string componentFileName, string clsid, string tlbid)
        {
            ComponentFileName = componentFileName;
            ClsId = clsid;
            ManifestFileName = manifestFileName;
            TlbId = tlbid;
        }
        public string ComponentFileName { get; }

        public string ClsId { get; }

        public string ManifestFileName { get; }
        public string TlbId { get; }
    }
}
