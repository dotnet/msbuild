// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Linq;
using System.Resources;
using System.Xml.Linq;

namespace Microsoft.DotNet.Tools.Resgen
{
    internal class ResourcesFileGenerator
    {
        public static void Generate(ResourceFile sourceFile, Stream outputStream)
        {
            if (outputStream == null) throw new ArgumentNullException(nameof(outputStream));
            using (var input = sourceFile.File.OpenRead())
            {
                var document = XDocument.Load(input);
                var data = document.Root.Elements("data").ToArray();
                if (data.Any())
                {
                    var rw = new ResourceWriter(outputStream);

                    foreach (var e in data)
                    {
                        var name = e.Attribute("name").Value;
                        var valueElement = e.Element("value");
                        var value = valueElement != null ? valueElement.Value : e.Value;
                        rw.AddResource(name, value);
                    }

                    rw.Generate();
                }
            }
        }
    }
}