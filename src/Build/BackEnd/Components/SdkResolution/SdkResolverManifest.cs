using Microsoft.Build.Shared;
using System;
using System.IO;
using System.Runtime.Serialization;
using System.Xml;

namespace Microsoft.Build.BackEnd.SdkResolution
{
    /// <summary>
    /// Serialization contract for an SDK Resolver manifest
    /// </summary>
    internal class SdkResolverManifest
    {
        internal string Path { get; set; }

        /// <summary>
        /// Deserialize the file into an SdkResolverManifest.
        /// </summary>
        /// <param name="filePath">Path to the manifest xml file.</param>
        /// <returns>New deserialized collection instance.</returns>
        internal static SdkResolverManifest Load(string filePath)
        {
            using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                try
                {
                    var doc = new XmlDocument();
                    doc.Load(fs);

                    if (doc.DocumentElement?.Name == "SdkResolver"
                        && doc.DocumentElement.NamespaceURI == "")
                    {
                        var pathElements = doc.DocumentElement.GetElementsByTagName("Path");
                        if (pathElements.Count > 0 && pathElements.Item(0) is XmlElement pathElement)
                        {
                            return new SdkResolverManifest() { Path = pathElement.InnerText };
                        }
                        else
                        {
                            ThrowInvalidDocumentNode("Path");
                        }
                    }
                    else
                    {
                        ThrowInvalidDocumentNode("SdkResolver");
                    }
                }
                catch(Exception e)
                {
                    throw new SerializationException(e.Message, e);
                }
            }

            return null;
        }

        private static void ThrowInvalidDocumentNode(string nodeName)
        {
            string message = ResourceUtilities.FormatResourceStringStripCodeAndKeyword(out var errorCode, out var helpKeyword, "InvalidSdkResolverDocumentNode", nodeName);

            throw new SerializationException(message);
        }
    }
}
