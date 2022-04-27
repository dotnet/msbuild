using Microsoft.Build.Shared;
using System.IO;
using System.Xml;

#nullable disable

namespace Microsoft.Build.BackEnd.SdkResolution
{
    /// <summary>
    /// Serialization contract for an SDK Resolver manifest
    /// </summary>
    internal class SdkResolverManifest
    {
        public SdkResolverManifest()
        {
        }

        public SdkResolverManifest(string path, string namePattern)
        {
            Path = path;
            NamePattern = namePattern;
        }

        internal string Path { get; set; }

        internal string NamePattern { get; set; }

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
                    if (reader.NodeType == XmlNodeType.Element && reader.Name == "SdkResolver")
                    {
                        return ParseSdkResolverElement(reader);
                    }
                    else
                    {
                        throw new XmlException(ResourceUtilities.FormatResourceStringStripCodeAndKeyword("UnrecognizedElement", reader.Name));
                    }
                }
            }

            return null;
        }

        // This parsing code is very specific, but it should be all right as long as manifest has simple structure.
        private static SdkResolverManifest ParseSdkResolverElement(XmlReader reader)
        {
            SdkResolverManifest manifest = new SdkResolverManifest();

            reader.Read();
            while (!reader.EOF)
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
                                case "NamePattern":
                                    manifest.NamePattern = reader.ReadElementContentAsString();
                                    break;
                                default:
                                    throw new XmlException(ResourceUtilities.FormatResourceStringStripCodeAndKeyword("UnrecognizedElement", reader.Name));
                            }
                            break;
                        }

                    case XmlNodeType.EndElement:
                        reader.Read();
                        break;

                    default:
                        throw new XmlException(ResourceUtilities.FormatResourceStringStripCodeAndKeyword("UnrecognizedElement", reader.Name));
                }
            }

            return manifest;
        }
    }
}
