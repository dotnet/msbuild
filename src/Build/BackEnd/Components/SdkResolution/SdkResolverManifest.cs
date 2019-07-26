using Microsoft.Build.Shared;
using System.IO;
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
            XmlReaderSettings readerSettings = new XmlReaderSettings()
            {
                IgnoreComments = true,
                IgnoreWhitespace = true,
                DtdProcessing = DtdProcessing.Ignore,
                XmlResolver = null
            };

            using (FileStream stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (XmlReader reader = XmlReader.Create(stream, readerSettings))
            {
                while (reader.Read())
                {
                    switch (reader.NodeType)
                    {
                        case XmlNodeType.Element:
                            switch (reader.Name)
                            {
                                case "SdkResolver":
                                    return ParseSdkResolverElement(reader);

                                default:
                                    throw new XmlException(ResourceUtilities.FormatResourceStringStripCodeAndKeyword("UnrecognizedElement", reader.Name));
                            }

                        default:
                            throw new XmlException(ResourceUtilities.FormatResourceStringStripCodeAndKeyword("UnrecognizedElement", reader.Name));
                    }
                }
            }

            return null;
        }

        private static SdkResolverManifest ParseSdkResolverElement(XmlReader reader)
        {
            SdkResolverManifest manifest = new SdkResolverManifest();

            while (reader.Read())
            {
                switch (reader.NodeType)
                {
                    case XmlNodeType.Element:
                        {
                            switch (reader.Name)
                            {
                                case "Path":
                                    manifest.Path = reader.ReadElementContentAsString();
                                    break;

                                default:
                                    throw new XmlException(ResourceUtilities.FormatResourceStringStripCodeAndKeyword("UnrecognizedElement", reader.Name));
                            }
                        }
                        break;

                    default:
                        throw new XmlException(ResourceUtilities.FormatResourceStringStripCodeAndKeyword("UnrecognizedElement", reader.Name));
                }
            }

            return manifest;
        }
    }
}
