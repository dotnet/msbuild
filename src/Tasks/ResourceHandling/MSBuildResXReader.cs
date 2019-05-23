// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

namespace Microsoft.Build.Tasks.ResourceHandling
{
    internal class MSBuildResXReader
    {
        public IReadOnlyList<IResource> Resources { get; }

        public static IReadOnlyList<IResource> ReadResources(Stream s, string filename)
        {
            // TODO: is it ok to hardcode the "shouldUseSourcePath" behavior?

            var resources = new List<IResource>();
            var aliases = new Dictionary<string, string>();

            using (var xmlReader = new XmlTextReader(s))
            {
                xmlReader.WhitespaceHandling = WhitespaceHandling.None;

                XDocument doc = XDocument.Load(xmlReader, LoadOptions.PreserveWhitespace);
                foreach (XElement elem in doc.Element("root").Elements())
                {
                    switch (elem.Name.LocalName)
                    {
                        case "schema":
                            // TODO: this
                            break;
                        case "assembly":
                            ParseAssemblyAlias(aliases, elem);
                            break;
                        case "resheader":
                            break;
                        case "data":
                            ParseData(filename, resources, aliases, elem);
                            break;
                        default:
                            throw new NotImplementedException();
                    }
                }
            }

            return resources;
        }

        private static void ParseAssemblyAlias(Dictionary<string,string> aliases, XElement elem)
        {
            string alias = elem.Attribute("alias").Value;
            string name = elem.Attribute("name").Value;

            aliases.Add(alias, name);
        }

        // Consts from https://github.com/dotnet/winforms/blob/16b192389b377c647ab3d280130781ab1a9d3385/src/System.Windows.Forms/src/System/Resources/ResXResourceWriter.cs#L46-L63
        private const string Beta2CompatSerializedObjectMimeType = "text/microsoft-urt/psuedoml-serialized/base64";
        private const string CompatBinSerializedObjectMimeType = "text/microsoft-urt/binary-serialized/base64";
        private const string CompatSoapSerializedObjectMimeType = "text/microsoft-urt/soap-serialized/base64";
        private const string BinSerializedObjectMimeType = "application/x-microsoft.net.object.binary.base64";
        private const string SoapSerializedObjectMimeType = "application/x-microsoft.net.object.soap.base64";
        private const string DefaultSerializedObjectMimeType = BinSerializedObjectMimeType;
        private const string ByteArraySerializedObjectMimeType = "application/x-microsoft.net.object.bytearray.base64";
        private const string ResMimeType = "text/microsoft-resx";

        private const string StringTypeName = "System.String, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089";

        private static string GetFullTypeNameFromAlias(string aliasedTypeName, Dictionary<string, string> aliases)
        {
            int indexStart = aliasedTypeName.IndexOf(',');
            if (aliases.TryGetValue(aliasedTypeName.Substring(indexStart + 2), out string fullAssemblyIdentity))
            {
                return aliasedTypeName.Substring(0, indexStart + 2) + fullAssemblyIdentity;
            }

            // Allow "System.String" bare or with no registered alias
            // TODO: is this overzealous? _Hopefully_ no one is putting a System.String type in their
            // own assembly.
            if (aliasedTypeName.StartsWith("System.String"))
            {
                return StringTypeName;
            }

            // No alias found. Hope it's sufficiently complete to be resolved at runtime
            return aliasedTypeName;
        }

        private static void ParseData(string resxFilename, List<IResource> resources, Dictionary<string,string> aliases, XElement elem)
        {
            string name = elem.Attribute("name").Value;
            string value = elem.Element("value").Value;

            string typename = elem.Attribute("type")?.Value;
            string mimetype = elem.Attribute("mimetype")?.Value;

            if (typename == null && mimetype == null)
            {
                // Simplest case: specify nothing, it's a string
                resources.Add(new StringResource(name, value, resxFilename));
                return;
            }

            if (typename != null)
            {
                typename = GetFullTypeNameFromAlias(typename, aliases);
            }

            if (typename == StringTypeName)
            {
                resources.Add(new StringResource(name, value, resxFilename));
                return;
            }

            if (typename.StartsWith("System.Resources.ResXFileRef", StringComparison.Ordinal)) // TODO: is this too general? Should it be OrdinalIgnoreCase?
            {
                AddLinkedResource(resxFilename, resources, name, value);
                return;
            }

            // TODO: validate typename at this point somehow to make sure it's vaguely right?

            if (mimetype != null)
            {
                switch (mimetype)
                {
                    case ByteArraySerializedObjectMimeType:
                        // TypeConverter from byte array
                        byte[] typeConverterBytes = Convert.FromBase64String(value);

                        resources.Add(new TypeConverterByteArrayResource(name, typename, typeConverterBytes, resxFilename));
                        return;
                    case BinSerializedObjectMimeType:
                    case Beta2CompatSerializedObjectMimeType:
                    case CompatBinSerializedObjectMimeType:
                        // TODO: binaryformatter
                    default:
                        throw new NotImplementedException();
                }
            }

            throw new NotImplementedException();
        }

        private static void AddLinkedResource(string resxFilename, List<IResource> resources, string name, string value)
        {
            string[] fileRefInfo = ParseResxFileRefString(value);

            string fileName = fileRefInfo[0];
            string fileRefType = fileRefInfo[1];
            string fileRefEncoding = fileRefInfo[2];

            if (fileRefType == StringTypeName)
            {
                // from https://github.com/dotnet/winforms/blob/a88c1a73fd7298b0a5c45251771f439262016826/src/System.Windows.Forms/src/System/Resources/ResXFileRef.cs#L231-L241
                Encoding textFileEncoding = fileRefEncoding != null
                    ? Encoding.GetEncoding(fileRefEncoding)
                    : Encoding.Default;
                using (StreamReader sr = new StreamReader(fileName, textFileEncoding))
                {
                    resources.Add(new StringResource(name, sr.ReadToEnd(), resxFilename));

                    return;
                }
            }

            throw new NotImplementedException();
        }

        /// <summary>
        /// Extract <see cref="IResource"/>s from a given file on disk.
        /// </summary>
        public static IReadOnlyList<IResource> GetResourcesFromFile(string filename)
        {
            using (var x = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                return ReadResources(x, filename);
            }
        }

        public static IReadOnlyList<IResource> GetResourcesFromString(string resxContent)
        {
            using (var x = new MemoryStream(Encoding.UTF8.GetBytes(resxContent)))
            {
                return ReadResources(x, null);
            }
        }

        // From https://github.com/dotnet/winforms/blob/a88c1a73fd7298b0a5c45251771f439262016826/src/System.Windows.Forms/src/System/Resources/ResXFileRef.cs#L187-L220
        internal static string[] ParseResxFileRefString(string stringValue)
        {
            string[] result = null;
            if (stringValue != null)
            {
                stringValue = stringValue.Trim();
                string fileName;
                string remainingString;
                if (stringValue.StartsWith("\""))
                {
                    int lastIndexOfQuote = stringValue.LastIndexOf("\"");
                    if (lastIndexOfQuote - 1 < 0)
                        throw new ArgumentException(nameof(stringValue));
                    fileName = stringValue.Substring(1, lastIndexOfQuote - 1); // remove the quotes in" ..... "
                    if (lastIndexOfQuote + 2 > stringValue.Length)
                        throw new ArgumentException(nameof(stringValue));
                    remainingString = stringValue.Substring(lastIndexOfQuote + 2);
                }
                else
                {
                    int nextSemiColumn = stringValue.IndexOf(";");
                    if (nextSemiColumn == -1)
                        throw new ArgumentException(nameof(stringValue));
                    fileName = stringValue.Substring(0, nextSemiColumn);
                    if (nextSemiColumn + 1 > stringValue.Length)
                        throw new ArgumentException(nameof(stringValue));
                    remainingString = stringValue.Substring(nextSemiColumn + 1);
                }
                string[] parts = remainingString.Split(';');
                if (parts.Length > 1)
                {
                    result = new string[] { fileName, parts[0], parts[1] };
                }
                else if (parts.Length > 0)
                {
                    result = new string[] { fileName, parts[0] };
                }
                else
                {
                    result = new string[] { fileName };
                }
            }
            return result;
        }

    }
}
