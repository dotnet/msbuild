// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Xml;
using System.Xml.XPath;
using System.Xml.Xsl;

namespace Microsoft.Build.Tasks.Deployment.ManifestUtilities
{
    internal static class XmlUtil
    {
        private static readonly ResourceResolver s_resolver = new ResourceResolver();

        public static string GetQName(XmlTextReader r, XmlNamespaceManager nsmgr)
        {
            string prefix = !String.IsNullOrEmpty(r.Prefix) ? r.Prefix : nsmgr.LookupPrefix(r.NamespaceURI);
            if (!String.IsNullOrEmpty(prefix))
                return prefix + ":" + r.LocalName;
            else
                return r.LocalName;
        }

        //NOTE: XmlDocument.ImportNode munges "xmlns:asmv2" to "xmlns:d1p1" for some reason, use XmlUtil.CloneElementToDocument instead
        public static XmlElement CloneElementToDocument(XmlElement element, XmlDocument document, string namespaceURI)
        {
            XmlElement newElement = document.CreateElement(element.Name, namespaceURI);
            foreach (XmlAttribute attribute in element.Attributes)
            {
                XmlAttribute newAttribute = document.CreateAttribute(attribute.Name);
                newAttribute.Value = attribute.Value;
                newElement.Attributes.Append(newAttribute);
            }
            foreach (XmlNode node in element.ChildNodes)
            {
                if (node.NodeType == XmlNodeType.Element)
                {
                    XmlElement childElement = CloneElementToDocument((XmlElement)node, document, namespaceURI);
                    newElement.AppendChild(childElement);
                }
                else if (node.NodeType == XmlNodeType.Comment)
                {
                    XmlComment childComment = document.CreateComment(((XmlComment)node).Data);
                    newElement.AppendChild(childComment);
                }
            }
            return newElement;
        }

        public static string TrimPrefix(string s)
        {
            int i = s.IndexOf(':');
            if (i < 0)
                return s;
            return s.Substring(i + 1);
        }

        [SuppressMessage("Microsoft.Security.Xml", "CA3073: ReviewTrustedXsltUse.", Justification = "Input style sheet comes from our own assemblies. Hence it is a trusted source.")]
        [SuppressMessage("Microsoft.Security.Xml", "CA3053: UseSecureXmlResolver.", Justification = "Input style sheet comes from our own assemblies. Hence it is a trusted source.")]
        [SuppressMessage("Microsoft.Security.Xml", "CA3059: UseXmlReaderForXPathDocument.", Justification = "Input style sheet comes from our own assemblies. Hence it is a trusted source.")]
        public static Stream XslTransform(string resource, Stream input, params DictionaryEntry[] entries)
        {
            int t1 = Environment.TickCount;

            Stream s = Util.GetEmbeddedResourceStream(resource);

            int t2 = Environment.TickCount;
            XPathDocument d = new XPathDocument(s);
            Util.WriteLog(String.Format(CultureInfo.CurrentCulture, "new XPathDocument(1) t={0}", Environment.TickCount - t2));

            int t3 = Environment.TickCount;
            var xslc = new XslCompiledTransform();
            // Using the Trusted Xslt is fine as the style sheet comes from our own assemblies.
            // This is similar to the prior this.GetType().Assembly/Evidence method that was used in the now depricated XslTransform.
            xslc.Load(d, XsltSettings.TrustedXslt, s_resolver);
            Util.WriteLog(String.Format(CultureInfo.CurrentCulture, "XslCompiledTransform.Load t={0}", Environment.TickCount - t3));

            // Need to copy input stream because XmlReader will close it,
            // causing errors for later callers that access the same stream
            var clonedInput = new MemoryStream();
            Util.CopyStream(input, clonedInput);

            int t4 = Environment.TickCount;
            XmlReader xml = XmlReader.Create(clonedInput);
            Util.WriteLog(String.Format(CultureInfo.CurrentCulture, "new XmlReader(2) t={0}", Environment.TickCount - t4));

            XsltArgumentList args = null;
            if (entries.Length > 0)
            {
                args = new XsltArgumentList();
                foreach (DictionaryEntry entry in entries)
                {
                    string key = entry.Key.ToString();
                    object val = entry.Value.ToString();
                    args.AddParam(key, "", val);
                    Util.WriteLog(String.Format(CultureInfo.CurrentCulture, "arg: key='{0}' value='{1}'", key, val.ToString()));
                }
            }

            var m = new MemoryStream();
            var w = new XmlTextWriter(m, Encoding.UTF8);
            w.WriteStartDocument();

            int t5 = Environment.TickCount;
            xslc.Transform(xml, args, w, s_resolver);
            Util.WriteLog(String.Format(CultureInfo.CurrentCulture, "XslCompiledTransform.Transform t={0}", Environment.TickCount - t4));

            w.WriteEndDocument();
            w.Flush();
            m.Position = 0;

            Util.WriteLog(String.Format(CultureInfo.CurrentCulture, "XslCompiledTransform(\"{0}\") t={1}", resource, Environment.TickCount - t1));
            return m;
        }

        private class ResourceResolver : XmlUrlResolver
        {
            public override Object GetEntity(Uri uri, string role, Type t)
            {
                if (!uri.IsAbsoluteUri)
                {
                    // As this is not an absolute URI, the file operations below won't work anyways, so we return null.
                    // This method used to throw an exception on an absolute URI, but it was silently consumed by XslTransform.  XslCompiledTransform is no longer silent about these inner exceptions.
                    return null;
                }

                string filename = uri.Segments[uri.Segments.Length - 1];
                Stream s = null;

                // If path is in temp then we immediately know we can skip the first two checks...
                if (!uri.LocalPath.StartsWith(Path.GetTempPath(), StringComparison.Ordinal))
                {
                    // First look in assembly resources...
                    Assembly a = Assembly.GetExecutingAssembly();
                    s = a.GetManifestResourceStream(String.Format(CultureInfo.InvariantCulture, "{0}.{1}", typeof(Util).Namespace, filename));

                    if (s != null)
                        return s;

                    // Next look in current directory...
                    try
                    {
                        s = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read);
                    }
                    catch (FileNotFoundException)
                    {
                    }
                    if (s != null)
                        return s;
                }

                // Lastly, look at full specified uri path...
                try
                {
                    s = new FileStream(uri.LocalPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                }
                catch (DirectoryNotFoundException)
                {
                }
                catch (FileNotFoundException)
                {
                }
                if (s != null)
                    return s;

                // Didn't find the resource...
                Debug.Fail(String.Format(CultureInfo.CurrentCulture, "ResourceResolver could not find file '{0}'", filename));
                return null;
            }
        }
    }
}
