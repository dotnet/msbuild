using System.IO;
using System.Runtime.Serialization;
using System.Xml;

namespace Microsoft.Build.BackEnd.SdkResolution
{
    /// <summary>
    /// Serialization contract for an SDK Resolver manifest
    /// </summary>
    [DataContract(Name = "SdkResolver", Namespace = "")]
    internal class SdkResolverManifest
    {
        [DataMember(IsRequired = false, Order = 1)]
        internal string Path { get; set; }

        /// <summary>
        /// Deserialize the file into an SdkResolverManifest.
        /// </summary>
        /// <param name="filePath">Path to the manifest xml file.</param>
        /// <returns>New deserialized collection instance.</returns>
        internal static SdkResolverManifest Load(string filePath)
        {
            using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (XmlDictionaryReader reader = XmlDictionaryReader.CreateTextReader(fs, new XmlDictionaryReaderQuotas()))
            {
                DataContractSerializer serializer = new DataContractSerializer(typeof(SdkResolverManifest));
                return (SdkResolverManifest)serializer.ReadObject(reader, true);
            }
        }
    }
}
