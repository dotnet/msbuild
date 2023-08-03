// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Xml;
using Microsoft.Build.Framework;

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
