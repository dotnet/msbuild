// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml;

namespace Microsoft.Build.Tasks.Deployment.ManifestUtilities
{
    internal static class ManifestFormatter
    {
        public static Stream Format(Stream input)
        {
            int t1 = Environment.TickCount;

            var r = new XmlTextReader(input)
            {
                DtdProcessing = DtdProcessing.Ignore,
                WhitespaceHandling = WhitespaceHandling.None
            };
            XmlNamespaceManager nsmgr = XmlNamespaces.GetNamespaceManager(r.NameTable);

            var m = new MemoryStream();
            var w = new XmlTextWriter(m, Encoding.UTF8)
            {
                Formatting = Formatting.Indented,
                Indentation = 2
            };
            w.WriteStartDocument();

            while (r.Read())
            {
                switch (r.NodeType)
                {
                    case XmlNodeType.Element:
                        w.WriteStartElement(r.Prefix, r.LocalName, r.NamespaceURI);
                        if (r.HasAttributes)
                        {
                            string elementQName = XmlUtil.GetQName(r, nsmgr);
                            for (int i = 0; i < r.AttributeCount; ++i)
                            {
                                r.MoveToAttribute(i);
                                string attributeQName = XmlUtil.GetQName(r, nsmgr);
                                string xpath = elementQName + "/@" + attributeQName;
                                // Filter out language="*"
                                if ((xpath.Equals(XPaths.languageAttribute1, StringComparison.Ordinal) || xpath.Equals(
                                         XPaths.languageAttribute2,
                                         StringComparison.Ordinal)) && String.Equals(
                                        r.Value,
                                        "*",
                                        StringComparison.Ordinal))
                                {
                                    continue;
                                }
                                // Filter out attributes with empty values if attribute is on the list...
                                if (String.IsNullOrEmpty(r.Value) &&
                                    Array.BinarySearch(XPaths.emptyAttributeList, xpath) >= 0)
                                {
                                    continue;
                                }
                                w.WriteAttributeString(r.Prefix, r.LocalName, r.NamespaceURI, r.Value);
                            }

                            r.MoveToElement(); //Moves the reader back to the element node.
                        }

                        if (r.IsEmptyElement)
                        {
                            w.WriteEndElement();
                        }

                        break;

                    case XmlNodeType.EndElement:
                        w.WriteEndElement();
                        break;

                    case XmlNodeType.Comment:
                        w.WriteComment(r.Value);
                        break;

                    case XmlNodeType.CDATA:
                        w.WriteCData(r.Value);
                        break;

                    case XmlNodeType.Text:
                        w.WriteString(r.Value);
                        break;
                }
            }

            w.WriteEndDocument();
            w.Flush();
            m.Position = 0;
            Util.WriteLog(String.Format(CultureInfo.CurrentCulture, "ManifestWriter.Format t={0}", Environment.TickCount - t1));
            return m;
        }
    }
}
