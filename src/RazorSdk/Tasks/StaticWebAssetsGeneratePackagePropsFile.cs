// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.AspNetCore.Razor.Tasks
{
    public class StaticWebAssetsGeneratePackagePropsFile : Task
    {
        [Required]
        public string PropsFileImport { get; set; }

        [Required]
        public string BuildTargetPath { get; set; }

        public override bool Execute()
        {
            var document = new XDocument(new XDeclaration("1.0", "utf-8", "yes"));
            var root = new XElement(
                "Project",
                new XElement("Import",
                    new XAttribute("Project", PropsFileImport)));

            document.Add(root);

            var settings = new XmlWriterSettings
            {
                Encoding = Encoding.UTF8,
                CloseOutput = true,
                OmitXmlDeclaration = true,
                Indent = true,
                NewLineOnAttributes = false,
                Async = true
            };

            using (var xmlWriter = GetXmlWriter(settings))
            {
                document.WriteTo(xmlWriter);
            }

            return !Log.HasLoggedErrors;
        }

        private XmlWriter GetXmlWriter(XmlWriterSettings settings)
        {
            var fileStream = new FileStream(BuildTargetPath, FileMode.Create);
            return XmlWriter.Create(fileStream, settings);
        }
    }
}
