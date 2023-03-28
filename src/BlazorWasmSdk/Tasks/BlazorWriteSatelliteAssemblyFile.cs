// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.IO;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.NET.Sdk.BlazorWebAssembly
{
    public class BlazorWriteSatelliteAssemblyFile : Task
    {
        [Required]
        public ITaskItem[] SatelliteAssembly { get; set; }

        [Required]
        public ITaskItem WriteFile { get; set; }

        public override bool Execute()
        {
            using var fileStream = File.Create(WriteFile.ItemSpec);
            WriteSatelliteAssemblyFile(fileStream);
            return true;
        }

        internal void WriteSatelliteAssemblyFile(Stream stream)
        {
            var root = new XElement("SatelliteAssembly");

            foreach (var item in SatelliteAssembly)
            {
                // <Assembly Name="..." Culture="..." DestinationSubDirectory="..." />

                root.Add(new XElement("Assembly",
                    new XAttribute("Name", item.ItemSpec),
                    new XAttribute("Culture", item.GetMetadata("Culture")),
                    new XAttribute("DestinationSubDirectory", item.GetMetadata("DestinationSubDirectory"))));
            }

            var xmlWriterSettings = new XmlWriterSettings
            {
                Indent = true,
                OmitXmlDeclaration = true
            };

            using var writer = XmlWriter.Create(stream, xmlWriterSettings);
            var xDocument = new XDocument(root);

            xDocument.Save(writer);
        }
    }
}
