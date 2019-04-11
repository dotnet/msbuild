// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.NET.Build.Tasks
{
    internal static class RegFreeComManifest
    {
        /// <summary>
        /// Generates a side-by-side application manifest to enable reg-free COM.
        /// </summary>
        /// <param name="assemblyName">The name of the assembly.</param>
        /// <param name="comHostName">The name of the comhost library.</param>
        /// <param name="assemblyVersion">The version of the assembly.</param>
        /// <param name="clsidMapPath">The path to the clsidmap file.</param>
        /// <param name="comManifestPath">The path to which to write the manifest.</param>
        public static void CreateManifestFromClsidmap(string assemblyName, string comHostName, string assemblyVersion, string clsidMapPath, string comManifestPath)
        {
            XNamespace ns = "urn:schemas-microsoft-com:asm.v1";

            XElement manifest = new XElement(ns + "assembly", new XAttribute("manifestVersion", "1.0"));
            manifest.Add(new XElement(ns + "assemblyIdentity",
                new XAttribute("type", "win32"),
                new XAttribute("name", $"{assemblyName}.X"),
                new XAttribute("version", assemblyVersion)));

            XElement fileElement = new XElement(ns + "file", new XAttribute("name", comHostName));

            JObject clsidMap;
            string clsidMapText = File.ReadAllText(clsidMapPath);
            using (StreamReader clsidMapReader = File.OpenText(clsidMapPath))
            using (JsonTextReader jsonReader = new JsonTextReader(clsidMapReader))
            {
                clsidMap = JObject.Load(jsonReader);
            }

            foreach (JProperty property in clsidMap.Properties())
            {
                string guidMaybe = property.Name;
                Guid guid = Guid.Parse(guidMaybe);
                fileElement.Add(new XElement(ns + "comClass", new XAttribute("clsid", guid.ToString("B")), new XAttribute("threadingModel", "Both")));
            }

            manifest.Add(fileElement);

            XDocument manifestDocument = new XDocument(new XDeclaration("1.0", "UTF-8", "yes"), manifest);
            XmlWriterSettings settings = new XmlWriterSettings()
            {
                Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)
            };
            using (XmlWriter manifestWriter = XmlWriter.Create(comManifestPath, settings))
            {
                manifestDocument.WriteTo(manifestWriter);
            }
        }
    }
}
