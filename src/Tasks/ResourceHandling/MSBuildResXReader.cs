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

        public MSBuildResXReader(Stream s, string filename)
        {
            // TODO: is it ok to hardcode the "shouldUseSourcePath" behavior?

            var resources = new List<IResource>();

            using (var xmlReader = new XmlTextReader(s))
            {
                xmlReader.WhitespaceHandling = WhitespaceHandling.None;

                XDocument doc = XDocument.Load(xmlReader, LoadOptions.PreserveWhitespace);
                foreach (XElement elem in doc.Element("root").Elements())
                {
                    switch (elem.Name.LocalName)
                    {
                        case "schema":
                        case "assembly":
                        case "resheader":
                            break;
                        case "data":
                            ParseData(filename, resources, elem);
                            break;
                        default:
                            throw new NotImplementedException();
                    }
                }
            }

            Resources = resources;
        }

        private static void ParseData(string resxFilename, List<IResource> resources, XElement elem)
        {
            string name = elem.Attribute("name").Value;
            string value = elem.Element("value").Value;

            string typename = elem.Attribute("type")?.Value;
            string mimetype = elem.Attribute("mimetype")?.Value;

            if (typename == null && mimetype == null)
            {
                resources.Add(new StringResource(name, value, resxFilename));
            }
            else if (typename == "System.Resources.ResXFileRef, System.Windows.Forms")
            {
                string[] fileRefInfo = ParseResxFileRefString(value);

                string filename = fileRefInfo[0];
                string fileReftype = fileRefInfo[1];
                string fileRefEncoding = fileRefInfo[2];

                throw new NotImplementedException();
            }
        }

        public MSBuildResXReader(Stream s) : this(s, null)
        { }

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
