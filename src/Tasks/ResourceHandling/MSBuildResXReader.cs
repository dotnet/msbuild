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

        private static void ParseData(string filename, List<IResource> resources, XElement elem)
        {
            string name = elem.Attribute("name").Value;
            string value = elem.Element("value").Value;

            string typename = elem.Attribute("type")?.Value;
            string mimetype = elem.Attribute("mimetype")?.Value;

            if (typename == null && mimetype == null)
            {
                resources.Add(new StringResource(name, value, filename));
            }
        }

        public MSBuildResXReader(Stream s) : this(s, null)
        { }
    }
}
