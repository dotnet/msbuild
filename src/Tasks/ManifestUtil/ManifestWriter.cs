// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Xml.Serialization;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Tasks.Deployment.ManifestUtilities
{
    /// <summary>
    /// Writes object representation of a manifest to XML.
    /// </summary>
    [ComVisible(false)]
    public static class ManifestWriter
    {
        private static Stream Serialize(Manifest manifest)
        {
            manifest.OnBeforeSave();
            var m = new MemoryStream();
            var s = new XmlSerializer(manifest.GetType());
            var w = new StreamWriter(m);

            int t1 = Environment.TickCount;
            s.Serialize(w, manifest);
            Util.WriteLog(String.Format(CultureInfo.CurrentCulture, "ManifestWriter.Serialize t={0}", Environment.TickCount - t1));

            w.Flush();
            m.Position = 0;
            return m;
        }

        /// <summary>
        /// Writes the specified object representation of a manifest to XML.
        /// The name of the output file is inferred from the SourcePath property of the manifest.
        /// </summary>
        /// <param name="manifest">The object representation of the manifest.</param>
        public static void WriteManifest(Manifest manifest)
        {
            string path = manifest.SourcePath ?? "manifest.xml";
            WriteManifest(manifest, path);
        }

        /// <summary>
        /// Writes the specified object representation of a manifest to XML.
        /// </summary>
        /// <param name="manifest">The object representation of the manifest.</param>
        /// <param name="path">The name of the output file.</param>
        public static void WriteManifest(Manifest manifest, string path)
        {
            using (Stream s = File.Open(path, FileMode.Create, FileAccess.Write, FileShare.Write))
            {
                WriteManifest(manifest, s);
            }
        }

        /// <summary>
        /// Writes the specified object representation of a manifest to XML.
        /// </summary>
        /// <param name="manifest">The object representation of the manifest.</param>
        /// <param name="path">The name of the output file.</param>
        /// <param name="targetframeWorkVersion">The target framework version.</param>
        public static void WriteManifest(Manifest manifest, string path, string targetframeWorkVersion)
        {
            using (Stream s = File.Open(path, FileMode.Create, FileAccess.Write, FileShare.Write))
            {
                WriteManifest(manifest, s, targetframeWorkVersion);
            }
        }

        /// <summary>
        /// Writes the specified object representation of a manifest to XML.
        /// </summary>
        /// <param name="manifest">The object representation of the manifest.</param>
        /// <param name="output">Specifies an output stream.</param>
        public static void WriteManifest(Manifest manifest, Stream output)
        {
            WriteManifest(manifest, output, null);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="manifest"></param>
        /// <param name="output"></param>
        /// <param name="targetframeWorkVersion">it will always use sha256 as signature algorithm if TFV is null</param>
        private static void WriteManifest(Manifest manifest, Stream output, string targetframeWorkVersion)
        {
            int t1 = Environment.TickCount;
            Stream s1 = Serialize(manifest);
            string n = manifest.AssemblyIdentity.GetFullName(AssemblyIdentity.FullNameFlags.All);
            if (String.IsNullOrEmpty(n))
            {
                n = manifest.GetType().Name;
            }
            Util.WriteLogFile(n + ".write.0-serialized.xml", s1);

            string resource;

            if (string.IsNullOrEmpty(targetframeWorkVersion) || Util.CompareFrameworkVersions(targetframeWorkVersion, Constants.TargetFrameworkVersion40) <= 0)
            {
                resource = "write2.xsl";
            }
            else
            {
                resource = "write3.xsl";
            }

            Stream s2;

            if (manifest.GetType() == typeof(ApplicationManifest))
            {
                var am = (ApplicationManifest)manifest;
                if (am.TrustInfo == null)
                {
                    s2 = XmlUtil.XslTransform(resource, s1);
                }
                else
                {
                    // May throw IO-related exceptions
                    string temp = FileUtilities.GetTemporaryFile();

                    am.TrustInfo.Write(temp);
                    if (Util.logging)
                    {
                        try
                        {
                            File.Copy(temp, Path.Combine(Util.logPath, n + ".trust-file.xml"), true);
                        }
                        catch (IOException)
                        {
                        }
                        catch (ArgumentException)
                        {
                        }
                        catch (UnauthorizedAccessException)
                        {
                        }
                        catch (NotSupportedException)
                        {
                        }
                    }

                    var arg = new DictionaryEntry("trust-file", temp);
                    try
                    {
                        s2 = XmlUtil.XslTransform(resource, s1, arg);
                    }
                    finally
                    {
                        File.Delete(temp);
                    }
                }
            }
            else
            {
                s2 = XmlUtil.XslTransform(resource, s1);
            }
            Util.WriteLogFile(n + ".write.1-transformed.xml", s2);

            Stream s3;
            if (manifest.InputStream == null)
            {
                s3 = s2;
            }
            else
            {
                string temp = Util.WriteTempFile(manifest.InputStream);
                var arg = new DictionaryEntry("base-file", temp);
                try
                {
                    s3 = XmlUtil.XslTransform("merge.xsl", s2, arg);
                }
                finally
                {
                    File.Delete(temp);
                }
                Util.WriteLogFile(n + ".write.2-merged.xml", s3);
            }

            Stream s4 = ManifestFormatter.Format(s3);
            Util.WriteLogFile(n + ".write.3-formatted.xml", s4);

            Util.CopyStream(s4, output);
            Util.WriteLog(String.Format(CultureInfo.CurrentCulture, "ManifestWriter.WriteManifest t={0}", Environment.TickCount - t1));
        }
    }
}
