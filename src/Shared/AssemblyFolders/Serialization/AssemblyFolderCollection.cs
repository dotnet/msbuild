// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.IO;
using System.Xml;

#nullable disable

namespace Microsoft.Build.Shared.AssemblyFoldersFromConfig
{
    internal class AssemblyFolderCollection
    {
        internal AssemblyFolderCollection(List<AssemblyFolderItem> assemblyFolders) => AssemblyFolders = assemblyFolders;

        internal List<AssemblyFolderItem> AssemblyFolders { get; }

        /// <summary>
        /// Reads the AssemblyFolders config file into an <see cref="AssemblyFolderCollection"/>.
        /// </summary>
        /// <param name="filePath">Path to the AssemblyFolders config file.</param>
        /// <returns>A collection populated from the file.</returns>
        /// <exception cref="XmlException">The file is not well-formed XML.</exception>
        internal static AssemblyFolderCollection Load(string filePath)
        {
            // The expected document shape is:
            //
            //   <AssemblyFoldersConfig>
            //     <AssemblyFolders>
            //       <AssemblyFolder>
            //         <Name>...</Name>                          (optional)
            //         <FrameworkVersion>v4.5</FrameworkVersion>
            //         <Path>...</Path>
            //         <Platform>x86</Platform>                  (optional)
            //       </AssemblyFolder>
            //     </AssemblyFolders>
            //   </AssemblyFoldersConfig>
            //
            // The file is parsed with XmlDocument (not a reflection-based serializer), so this code is
            // safe under trimming and Native AOT. Child element order is not significant and
            // unrecognized elements are ignored.

            // Harden against XML external entity (XXE) attacks: no DTD processing, no external resolution.
            XmlReaderSettings settings = new()
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
            };

            XmlDocument document = new();
            using (FileStream stream = new(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (XmlReader reader = XmlReader.Create(stream, settings))
            {
                document.Load(reader);
            }

            List<AssemblyFolderItem> assemblyFolders = [];
            foreach (XmlNode folder in document.GetElementsByTagName("AssemblyFolder"))
            {
                AssemblyFolderItem item = new();
                foreach (XmlNode child in folder.ChildNodes)
                {
                    switch (child.LocalName)
                    {
                        case "Name":
                            item.Name = child.InnerText;
                            break;
                        case "FrameworkVersion":
                            item.FrameworkVersion = child.InnerText;
                            break;
                        case "Path":
                            item.Path = child.InnerText;
                            break;
                        case "Platform":
                            item.Platform = child.InnerText;
                            break;
                    }
                }

                assemblyFolders.Add(item);
            }

            return new AssemblyFolderCollection(assemblyFolders);
        }
    }
}
